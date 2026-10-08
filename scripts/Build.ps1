param([Parameter(Mandatory=$true)][string]$GameDir,[string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root 'artifacts\mod'
$version=[regex]::Match((Get-Content -LiteralPath (Join-Path $root 'src\CharacterSheet\Plugin.cs') -Raw),'ModVersion = "([^"]+)"').Groups[1].Value
if(!$version){throw 'Plugin version not found.'}
& $Dotnet build (Join-Path $root 'src\CharacterSheet\CharacterSheet.csproj') -c Release "-p:GameDir=$GameDir" "-p:Version=$version" "-p:AssemblyVersion=$version.0" -o $output --nologo
if($LASTEXITCODE -ne 0){throw 'Mod build failed.'}
