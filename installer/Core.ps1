Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-GameRoot([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    if (!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Choose a real game folder, not a link.' }
    $root = $item.FullName.TrimEnd('\','/')
    foreach ($file in @('Gunfire Reborn.exe','GameAssembly.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $root $file) -PathType Leaf)) { throw "Not a Gunfire Reborn installation: missing $file." }
    }
    return $root
}
function Get-SafePath([string]$Root,[string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw 'Absolute paths are not permitted in a manifest.' }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $target = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (!$target.StartsWith($base + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes the selected folder: $Relative" }
    $check = $target
    while ($check.Length -ge $base.Length) {
        if (Test-Path -LiteralPath $check) {
            if ((Get-Item -LiteralPath $check).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked paths are not supported: $check" }
        }
        if ($check -eq $base) { break }
        $check = [IO.Path]::GetDirectoryName($check)
    }
    return $target
}
function Assert-GameClosed {
    if (Get-Process -Name 'Gunfire Reborn','UnityCrashHandler64' -ErrorAction SilentlyContinue) { throw 'Close Gunfire Reborn and its crash handler before changing files.' }
}
function Get-InstallStatePath([string]$GameRoot,[string]$StateBase = "$env:LOCALAPPDATA\GunfireCharacterSheet\installs") {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $key = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($GameRoot.ToLowerInvariant())))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
    return Join-Path (Join-Path $StateBase $key) 'install.json'
}
function Get-PrerequisiteStatus([string]$GameRoot,$Compatibility) {
    foreach ($component in $Compatibility.components) {
        $missing = @(); $different = @()
        $required = @($component.required)
        if ($component.id -eq 'bepinex') { $required += @($component.files | Where-Object { $_.path -like 'BepInEx/core/*' -or $_.path -eq 'doorstop_config.ini' } | ForEach-Object path) }
        foreach ($relative in @($required | Select-Object -Unique)) {
            $file = Get-SafePath $GameRoot $relative
            $expected = @($component.files | Where-Object { $_.path -eq $relative })
            if (!(Test-Path -LiteralPath $file -PathType Leaf)) { $missing += $relative }
            elseif ($expected.Count -ne 1 -or (Get-Sha256 $file) -ne $expected[0].sha256) { $different += $relative }
        }
        # The runtime is part of BepInEx; incomplete core-only installations fail.
        if ($component.id -eq 'bepinex') {
            foreach ($entry in $component.files | Where-Object { $_.path -like 'dotnet/*' }) {
                $file = Get-SafePath $GameRoot $entry.path
                if (!(Test-Path -LiteralPath $file -PathType Leaf)) { $missing += $entry.path }
                elseif ((Get-Sha256 $file) -ne $entry.sha256) { $different += $entry.path }
            }
        }
        [pscustomobject]@{Id=$component.id;Name=$component.name;Version=$component.version;Status=$(if($missing.Count){'Missing / incomplete'}elseif($different.Count){'Different / unverified'}else{'Verified'});Url=$component.url;Instructions=$component.instructions;Missing=$missing;Different=$different}
    }
}
function Find-GameFolders {
    $roots = @()
    foreach ($registry in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $entry = Get-ItemProperty $registry -ErrorAction SilentlyContinue
        if ($entry) {
            foreach ($key in @('SteamPath','InstallPath')) { if ($entry.PSObject.Properties[$key]) { $roots += $entry.$key } }
        }
    }
    foreach ($root in @($roots | Select-Object -Unique)) {
        $libraries = @($root)
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw),'"path"\s+"([^"]+)"')) { $libraries += $match.Groups[1].Value.Replace('\\','\') }
        }
        foreach ($library in $libraries) {
            $acf = Join-Path $library 'steamapps\appmanifest_1217060.acf'
            if (!(Test-Path -LiteralPath $acf)) { continue }
            $match = [regex]::Match((Get-Content -LiteralPath $acf -Raw),'"installdir"\s+"([^"]+)"')
            if ($match.Success) {
                $candidate = Join-Path $library ('steamapps\common\'+$match.Groups[1].Value)
                if (Test-Path -LiteralPath (Join-Path $candidate 'Gunfire Reborn.exe')) { $candidate }
            }
        }
    }
}
function Install-CharacterSheet([string]$GameRoot,[string]$PackageRoot,[switch]$AllowUnverified,[string]$OriginalMetadata='', [string]$StateBase="$env:LOCALAPPDATA\GunfireCharacterSheet\installs") {
    $root = Get-GameRoot $GameRoot
    Assert-GameClosed
    $compatibility = Get-Content -LiteralPath (Join-Path $PackageRoot 'compatibility.json') -Raw | ConvertFrom-Json
    $release = Get-Content -LiteralPath (Join-Path $PackageRoot 'release.json') -Raw | ConvertFrom-Json
    $status = @(Get-PrerequisiteStatus $root $compatibility)
    if (@($status | Where-Object { $_.Status -eq 'Missing / incomplete' }).Count) { throw 'Install the missing prerequisites using the supplied instructions, then check again.' }
    $sameBuild = (Get-Sha256 (Join-Path $root 'GameAssembly.dll')) -eq $compatibility.game.gameAssemblySha256
    if ((!$sameBuild -or @($status | Where-Object { $_.Status -ne 'Verified' }).Count) -and !$AllowUnverified) { throw 'This game or prerequisite version is unverified. Use the explicit override only if you accept an untested combination.' }
    $source = Get-SafePath $PackageRoot $release.payload
    if ((Get-Sha256 $source) -ne $release.sha256) { throw 'The mod payload failed its checksum.' }
    $relative = 'BepInEx/plugins/GunfireStatsDiagnostic/GunfireStatsDiagnostic.dll'
    $target = Get-SafePath $root $relative
    $statePath = Get-InstallStatePath $root $StateBase
    $stateDir = Split-Path $statePath
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    $previous = $null
    if (Test-Path -LiteralPath $statePath) { $previous = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json }
    if ($previous -and $previous.gameRoot -ne $root) { throw 'Installation record belongs to a different game folder.' }
    $before = $null
    if (Test-Path -LiteralPath $target) { $before=Join-Path $stateDir ('mod-before-'+[guid]::NewGuid()+'.dll');Copy-Item -LiteralPath $target -Destination $before }
    $metadataBackup = if ($previous) { $previous.metadataBackup } else { $null }
    if ($OriginalMetadata) {
        $original = Get-Item -LiteralPath $OriginalMetadata
        if ($original.PSIsContainer -or (Get-Sha256 $original.FullName) -eq $compatibility.components[2].files[0].sha256) { throw 'Select an original vanilla metadata backup, not restored metadata.' }
        $backup = Join-Path $stateDir ('original-metadata-'+[guid]::NewGuid()+'.dat')
        Copy-Item -LiteralPath $original.FullName -Destination $backup
        $metadataBackup = [ordered]@{File=$backup;Sha256=(Get-Sha256 $backup);GameAssemblySha256=(Get-Sha256 (Join-Path $root 'GameAssembly.dll'))}
    }
    $temp = $target + '.install-'+[guid]::NewGuid()
    try {
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $source -Destination $temp
        Move-Item -LiteralPath $temp -Destination $target -Force
        if ((Get-Sha256 $target) -ne $release.sha256) { throw 'Installed checksum mismatch.' }
        $state = [ordered]@{schema=1;gameRoot=$root;version=$release.version;modPath=$relative;modSha256=$release.sha256;metadataBackup=$metadataBackup}
        $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($statePath+'.new') -Encoding UTF8
        Move-Item -LiteralPath ($statePath+'.new') -Destination $statePath -Force
    }
    catch {
        if ($before) { Copy-Item -LiteralPath $before -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
        throw
    }
    finally { if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp} }
    return "Installed Character Sheet $($release.version). Prerequisites were left unchanged."
}
function Uninstall-CharacterSheet([string]$GameRoot,[string]$PackageRoot,[switch]$Prerequisites,[string]$StateBase="$env:LOCALAPPDATA\GunfireCharacterSheet\installs") {
    $root = Get-GameRoot $GameRoot
    Assert-GameClosed
    $statePath = Get-InstallStatePath $root $StateBase
    if (!(Test-Path -LiteralPath $statePath)) { throw 'No installer record exists for this game folder. Follow the manual removal instructions instead.' }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.gameRoot -ne $root -or $state.modPath -ne 'BepInEx/plugins/GunfireStatsDiagnostic/GunfireStatsDiagnostic.dll') { throw 'Invalid installation record.' }
    $mod = Get-SafePath $root $state.modPath
    if ((Test-Path -LiteralPath $mod) -and (Get-Sha256 $mod) -ne $state.modSha256) { throw 'The installed mod has changed. It was preserved; remove it manually if intended.' }
    $remove = @()
    if ($Prerequisites) {
        $plugins = Join-Path $root 'BepInEx\plugins'
        $other = @(Get-ChildItem -LiteralPath $plugins -Recurse -Filter '*.dll' | Where-Object { $_.FullName -ne $mod -and !$_.FullName.StartsWith((Join-Path $plugins 'EmberCloak')+'\',[StringComparison]::OrdinalIgnoreCase) })
        if ($other.Count) { throw 'Other plugins depend on this loader. Remove Character Sheet only, or manage the other mods first.' }
        $compatibility = Get-Content -LiteralPath (Join-Path $PackageRoot 'compatibility.json') -Raw | ConvertFrom-Json
        foreach ($component in $compatibility.components | Where-Object { $_.id -ne 'metadata' }) {
            foreach ($entry in $component.files) {
                $path = Get-SafePath $root $entry.path
                if (Test-Path -LiteralPath $path -PathType Leaf) {
                    if ((Get-Sha256 $path) -ne $entry.sha256) { throw "Prerequisite file has changed; no files removed: $($entry.path)" }
                    $remove += $path
                }
            }
        }
    }
    # Validate metadata restoration before removing any component.
    $metadataRestored = $false
    $restoreFrom = $null
    $metadata = Get-SafePath $root 'Gunfire Reborn_Data/il2cpp_data/Metadata/global-metadata.dat'
    if ($Prerequisites -and $state.metadataBackup) {
        $backup = $state.metadataBackup
        $safeBackup = Get-SafePath (Split-Path $statePath) ([IO.Path]::GetFileName($backup.File))
        if ($safeBackup -ne $backup.File) { throw 'Invalid metadata backup path.' }
        if ((Get-Sha256 $safeBackup) -eq $backup.Sha256 -and (Get-Sha256 (Join-Path $root 'GameAssembly.dll')) -eq $backup.GameAssemblySha256 -and (Test-Path -LiteralPath $metadata)) {
            $current = Get-Sha256 $metadata
            if ($current -eq $backup.Sha256) { $metadataRestored = $true }
            elseif ($current -eq $compatibility.components[2].files[0].sha256) { $restoreFrom = $safeBackup }
        }
    }
    # Back up every planned change before deleting anything, then roll back
    # on an I/O failure. Never recursively delete a game or loader directory.
    if (Test-Path -LiteralPath $mod) { $remove = @($mod)+$remove }
    $undo = @()
    $undoRoot = Join-Path (Split-Path $statePath) ('uninstall-backup-'+[guid]::NewGuid())
    New-Item -ItemType Directory -Path $undoRoot | Out-Null
    $index = 0
    foreach ($path in @($remove)+$(if($restoreFrom){@($metadata)}else{@()})) {
        $copy = Join-Path $undoRoot ([string]$index++)
        Copy-Item -LiteralPath $path -Destination $copy
        $undo += [pscustomobject]@{Target=$path;Backup=$copy}
    }
    try {
        foreach ($path in $remove) { Remove-Item -LiteralPath $path }
        if ($restoreFrom) { Copy-Item -LiteralPath $restoreFrom -Destination $metadata -Force; $metadataRestored=$true }
    }
    catch {
        foreach ($entry in $undo) { Copy-Item -LiteralPath $entry.Backup -Destination $entry.Target -Force }
        throw
    }
    if ($Prerequisites) { return "Removed the mod and verified prerequisite files. $(if($metadataRestored){'Original metadata restored.'}else{'Use Steam verification to restore vanilla metadata before playing.'}) Inactive caches, settings, backups, and logs were preserved." }
    return 'Removed Character Sheet. Prerequisites, settings, backups, and logs were preserved.'
}
