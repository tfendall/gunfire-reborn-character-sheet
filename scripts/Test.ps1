param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
& $Dotnet run --project (Join-Path $root 'tests\Managed\ManagedTests.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Managed tests failed.'}
& (Join-Path $root 'tests\Installer.Tests.ps1')
