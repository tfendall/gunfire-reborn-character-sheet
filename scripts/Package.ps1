param([string]$BuiltDll='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$BuiltDll){$BuiltDll=Join-Path $root 'artifacts\mod\GunfireStatsDiagnostic.dll'}
if(!(Test-Path -LiteralPath $BuiltDll)){throw 'Build the mod first.'}
$version=[regex]::Match((Get-Content -LiteralPath (Join-Path $root 'src\CharacterSheet\Plugin.cs') -Raw),'ModVersion = "([^"]+)"').Groups[1].Value
if(!$version){throw 'Plugin version not found.'}
$assembly=[Reflection.AssemblyName]::GetAssemblyName((Get-Item -LiteralPath $BuiltDll).FullName)
if($assembly.Name -ne 'GunfireStatsDiagnostic'){throw 'Wrong assembly provided.'}
if($assembly.Version.ToString(3) -ne $version){throw 'Built DLL version differs from source. Rebuild using Build.ps1.'}
$stage=Join-Path $root ('artifacts\package-'+[guid]::NewGuid())
$guided=Join-Path $stage 'guided';$manual=Join-Path $stage 'manual'
New-Item -ItemType Directory -Force -Path "$guided\payload","$manual\BepInEx\plugins\GunfireStatsDiagnostic" | Out-Null
Copy-Item -LiteralPath $BuiltDll -Destination "$guided\payload\GunfireStatsDiagnostic.dll"
Copy-Item -LiteralPath $BuiltDll -Destination "$manual\BepInEx\plugins\GunfireStatsDiagnostic\GunfireStatsDiagnostic.dll"
Get-ChildItem -LiteralPath (Join-Path $root 'installer') -File | Copy-Item -Destination $guided
Copy-Item -LiteralPath (Join-Path $root 'docs\INSTALL.md') -Destination "$guided\INSTALL.md"
Copy-Item -LiteralPath (Join-Path $root 'docs\INSTALL.md') -Destination "$manual\INSTALL.md"
foreach($destination in @($guided,$manual)){Copy-Item -LiteralPath (Join-Path $root 'docs\THIRD_PARTY.md') -Destination $destination}
[ordered]@{version=$version;payload='payload/GunfireStatsDiagnostic.dll';sha256=(Get-FileHash -LiteralPath $BuiltDll -Algorithm SHA256).Hash.ToLowerInvariant();experimental=$true} | ConvertTo-Json | Set-Content -LiteralPath "$guided\release.json" -Encoding UTF8
$dist=Join-Path $root "dist\$version"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Compress-Archive -Path "$guided\*" -DestinationPath "$dist\CharacterSheet-$version-Setup.zip" -Force
Compress-Archive -Path "$manual\*" -DestinationPath "$dist\CharacterSheet-$version-ModOnly.zip" -Force
Get-ChildItem -LiteralPath $dist -Filter '*.zip' | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.Name } | Set-Content -LiteralPath "$dist\SHA256SUMS.txt" -Encoding ASCII
Write-Output "Release packages: $dist"
