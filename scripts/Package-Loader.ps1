param([string]$LoaderCore='', [string]$SourceDir='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$LoaderCore){$LoaderCore=Join-Path $root 'artifacts\custom-loader'}
if(!$SourceDir){$SourceDir=Join-Path $root 'artifacts\loader-source'}
$version=[regex]::Match((Get-Content (Join-Path $root 'src\CharacterSheet\Plugin.cs') -Raw),'ModVersion = "([^"]+)"').Groups[1].Value
$base=Join-Path $root 'artifacts\loader-upstream-788\package\BepInEx\core'
if(!(Test-Path (Join-Path $LoaderCore 'build-info.json')) -or !(Test-Path $base)){throw 'Build the custom loader first with Build-Loader.ps1.'}
$stage=Join-Path $root ('artifacts\loader-package-'+[guid]::NewGuid())
$overlay=Join-Path $stage 'overlay';$source=Join-Path $stage 'source'
New-Item -ItemType Directory -Force -Path "$overlay\BepInEx\core","$overlay\BepInEx\loader-licenses","$source\BepInEx-src","$source\scripts","$source\dependencies","$source\docs" | Out-Null
$changed=@(Get-ChildItem $LoaderCore -File | Where-Object Extension -in '.dll','.xml','.config' | Where-Object {!(Test-Path (Join-Path $base $_.Name)) -or (Get-FileHash $_.FullName).Hash -ne (Get-FileHash (Join-Path $base $_.Name)).Hash})
$changed | Copy-Item -Destination "$overlay\BepInEx\core"
Get-ChildItem (Join-Path $root 'dependencies\loader-licenses') -File | Copy-Item -Destination "$overlay\BepInEx\loader-licenses"
Copy-Item (Join-Path $root 'dependencies\BepInEx-LICENSE.txt') "$overlay\BepInEx\loader-licenses"
Copy-Item (Join-Path $LoaderCore 'build-info.json') "$overlay\BepInEx\loader-licenses"
Copy-Item (Join-Path $root 'docs\LOADER-INSTALL.txt') "$overlay\LOADER-INSTALL.txt"

# Supply the complete modified BepInEx source tree, without Unity/game reference
# binaries. The build recipe fetches those compile references from pinned upstream.
$files=@(git -C $SourceDir ls-files)+@('BepInEx.Core/PlatformHelper.cs','Runtimes/Unity/BepInEx.Unity.IL2CPP/Hook/NativeTrampoline.cs')
foreach($relative in $files | Select-Object -Unique){
    if([IO.Path]::GetExtension($relative) -in '.dll','.exe','.zip','.nupkg','.png','.jpg','.ico'){continue}
    $input=Join-Path $SourceDir $relative
    if(!(Test-Path -LiteralPath $input -PathType Leaf)){continue}
    $target=Join-Path "$source\BepInEx-src" $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $input -Destination $target
}
foreach($name in @('bepinex-net10.patch','BepInEx-LICENSE.txt','README.md')){Copy-Item (Join-Path $root ('dependencies\'+$name)) "$source\dependencies"}
Copy-Item (Join-Path $root 'dependencies\loader-licenses') "$source\dependencies\loader-licenses" -Recurse
Copy-Item (Join-Path $root 'scripts\Build-Loader.ps1') "$source\scripts"
Copy-Item (Join-Path $root 'global.json') $source
Copy-Item (Join-Path $root 'docs\DEPENDENCIES.md') "$source\docs"
Copy-Item (Join-Path $root 'docs\LOADER-SOURCE.txt') "$source\README.txt"
Copy-Item (Join-Path $LoaderCore 'build-info.json') $source
$deps=Get-Content (Join-Path $LoaderCore 'BepInEx.Unity.IL2CPP.deps.json') -Raw | ConvertFrom-Json
$packages=@($deps.libraries.PSObject.Properties | Where-Object {$_.Value.type -eq 'package'} | ForEach-Object {[ordered]@{package=$_.Name;cachePath=$_.Value.path;sha512=$_.Value.sha512}})
$packages | ConvertTo-Json -Depth 4 | Set-Content "$source\third-party-packages.json" -Encoding UTF8
$dist=Join-Path $root "dist\$version"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Compress-Archive -Path "$overlay\*" -DestinationPath "$dist\CharacterSheet-$version-LoaderOverlay.zip" -Force
Compress-Archive -Path "$source\*" -DestinationPath "$dist\CharacterSheet-$version-LoaderSource.zip" -Force
Get-ChildItem $dist -Filter '*.zip' | Sort-Object Name | ForEach-Object {(Get-FileHash $_.FullName).Hash.ToLowerInvariant()+'  '+$_.Name} | Set-Content "$dist\SHA256SUMS.txt" -Encoding ASCII
Write-Output "Packaged loader overlay ($($changed.Count) changed files) and corresponding source at $dist"
