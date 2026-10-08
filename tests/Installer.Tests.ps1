$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot) 'installer\Core.ps1')
# Test-only override: never inspect or mutate a running real installation.
function Assert-GameClosed {}
function Check([bool]$Condition,[string]$Name){if(!$Condition){throw "FAIL $Name"};Write-Output "PASS $Name"}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('CharacterSheetTests-'+[guid]::NewGuid())
$root=Join-Path $fixture 'game with spaces';$package=Join-Path $fixture 'package';$stateBase=Join-Path $fixture 'state'
New-Item -ItemType Directory -Force -Path $root,"$package\payload","$root\BepInEx\plugins\EmberCloak","$root\Gunfire Reborn_Data\il2cpp_data\Metadata" | Out-Null
try {
    Set-Content -LiteralPath "$root\Gunfire Reborn.exe" -Value 'fake executable'
    Set-Content -LiteralPath "$root\GameAssembly.dll" -Value 'fake game'
    Set-Content -LiteralPath "$root\winhttp.dll" -Value 'fake loader'
    Set-Content -LiteralPath "$root\BepInEx\plugins\EmberCloak\EmberCloak.dll" -Value 'fake dependency'
    Set-Content -LiteralPath "$root\Gunfire Reborn_Data\il2cpp_data\Metadata\global-metadata.dat" -Value 'restored'
    Set-Content -LiteralPath "$package\payload\mod.dll" -Value 'fake mod'
    $components=@()
    foreach($entry in @(@('bepinex','winhttp.dll'),@('embercloak','BepInEx/plugins/EmberCloak/EmberCloak.dll'),@('metadata','Gunfire Reborn_Data/il2cpp_data/Metadata/global-metadata.dat'))){
        $components+=@{id=$entry[0];name=$entry[0];version='test';url='https://example.invalid';instructions='fixture';required=@($entry[1]);files=@(@{path=$entry[1];sha256=(Get-Sha256 (Join-Path $root $entry[1]))})}
    }
    $compat=@{game=@{gameAssemblySha256=(Get-Sha256 "$root\GameAssembly.dll")};components=$components}
    $compat | ConvertTo-Json -Depth 8 | Set-Content "$package\compatibility.json"
    @{version='test';payload='payload/mod.dll';sha256=(Get-Sha256 "$package\payload\mod.dll")} | ConvertTo-Json | Set-Content "$package\release.json"
    Check (@(Get-PrerequisiteStatus $root $compat | Where-Object Status -ne 'Verified').Count -eq 0) 'verified prerequisites detected by hashes'
    $escaped=$false;try{Get-SafePath $root '../outside.dll' | Out-Null}catch{$escaped=$true};Check $escaped 'manifest traversal rejected'
    Install-CharacterSheet $root $package -StateBase $stateBase | Out-Null
    $mod="$root\BepInEx\plugins\GunfireStatsDiagnostic\GunfireStatsDiagnostic.dll"
    Check ((Get-Sha256 $mod) -eq (Get-Sha256 "$package\payload\mod.dll")) 'custom game path installs exact payload'
    Install-CharacterSheet $root $package -StateBase $stateBase | Out-Null
    Check (Test-Path -LiteralPath (Get-InstallStatePath $root $stateBase)) 'repeat installation keeps a usable record'
    Uninstall-CharacterSheet $root $package -StateBase $stateBase | Out-Null
    Check (!(Test-Path -LiteralPath $mod) -and (Test-Path -LiteralPath "$root\winhttp.dll")) 'mod-only uninstall preserves prerequisites'
    Install-CharacterSheet $root $package -StateBase $stateBase | Out-Null
    Set-Content -LiteralPath "$root\BepInEx\plugins\another.dll" -Value 'other mod'
    $blocked=$false;try{Uninstall-CharacterSheet $root $package -Prerequisites -StateBase $stateBase | Out-Null}catch{$blocked=$true}
    Check ($blocked -and (Test-Path -LiteralPath $mod) -and (Test-Path -LiteralPath "$root\winhttp.dll")) 'full uninstall preflight preserves all files when other mods exist'
    Remove-Item -LiteralPath "$root\BepInEx\plugins\another.dll"
    Set-Content -LiteralPath "$root\winhttp.dll" -Value 'changed dependency'
    $blocked=$false;try{Uninstall-CharacterSheet $root $package -Prerequisites -StateBase $stateBase | Out-Null}catch{$blocked=$true}
    Check ($blocked -and (Test-Path -LiteralPath $mod)) 'changed prerequisite blocks full removal before any files are deleted'
    Set-Content -LiteralPath "$root\winhttp.dll" -Value 'fake loader'
    $script:failRemoval=$true
    function Remove-Item {
        param([string]$LiteralPath,[switch]$Recurse,[switch]$Force)
        if($script:failRemoval -and $LiteralPath -eq "$root\winhttp.dll"){$script:failRemoval=$false;throw 'Simulated removal failure'}
        Microsoft.PowerShell.Management\Remove-Item -LiteralPath $LiteralPath -Recurse:$Recurse -Force:$Force
    }
    $blocked=$false;try{Uninstall-CharacterSheet $root $package -Prerequisites -StateBase $stateBase | Out-Null}catch{$blocked=$true}
    Check ($blocked -and (Test-Path -LiteralPath $mod) -and (Get-Sha256 "$root\winhttp.dll") -eq $components[0].files[0].sha256) 'partial uninstall I/O failure rolls back removed files'
    $script:failRemoval=$false
    $payloadHash=Get-Sha256 "$package\payload\mod.dll"
    Set-Content -LiteralPath "$package\payload\mod.dll" -Value 'tampered payload'
    $blocked=$false;try{Install-CharacterSheet $root $package -StateBase $stateBase | Out-Null}catch{$blocked=$true}
    Check ($blocked -and (Get-Sha256 $mod) -eq $payloadHash) 'tampered payload cannot replace the installed mod'
    Set-Content -LiteralPath "$package\payload\mod.dll" -Value 'fake mod'
    $original=Join-Path $fixture 'vanilla.dat';Set-Content -LiteralPath $original -Value 'vanilla'
    Install-CharacterSheet $root $package -OriginalMetadata $original -StateBase $stateBase | Out-Null
    Uninstall-CharacterSheet $root $package -Prerequisites -StateBase $stateBase | Out-Null
    Check (!(Test-Path -LiteralPath $mod) -and !(Test-Path -LiteralPath "$root\winhttp.dll") -and (Get-Sha256 "$root\Gunfire Reborn_Data\il2cpp_data\Metadata\global-metadata.dat") -eq (Get-Sha256 $original)) 'full removal restores a matching original metadata backup'
}
finally {
    $full=[IO.Path]::GetFullPath($fixture)
    $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(!$full.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $full -Leaf).StartsWith('CharacterSheetTests-')){throw 'Unsafe fixture cleanup path'}
    Remove-Item -LiteralPath $full -Recurse -Force
}
