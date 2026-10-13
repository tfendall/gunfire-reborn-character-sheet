param([string]$Dotnet=$(if(Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe"){"$env:USERPROFILE\.dotnet\dotnet.exe"}else{'dotnet'}))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
& $Dotnet run --project (Join-Path $root 'tests\Managed\ManagedTests.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Managed tests failed.'}
& (Join-Path $root 'tests\Installer.Tests.ps1')
