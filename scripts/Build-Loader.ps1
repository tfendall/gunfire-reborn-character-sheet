param(
    [string]$Dotnet=$(if(Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe"){"$env:USERPROFILE\.dotnet\dotnet.exe"}else{'dotnet'}),
    [string]$SourceDir='',
    [string]$OutputDir=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$commit='5b766a3b7f6c164d4798924a93f3acf4db769d06'
$patch=Join-Path $root 'dependencies\bepinex-net10.patch'
if(!$SourceDir){$SourceDir=Join-Path $root 'artifacts\loader-source'}
if(!$OutputDir){$OutputDir=Join-Path $root 'artifacts\custom-loader'}
$SourceDir=[IO.Path]::GetFullPath($SourceDir)
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
if(!(Test-Path (Join-Path $SourceDir '.git'))){
    if(Test-Path $SourceDir){throw 'Source directory exists without a Git checkout. Choose an empty path.'}
    git clone --no-checkout --depth 1 https://github.com/BepInEx/BepInEx.git $SourceDir
    if($LASTEXITCODE -ne 0){throw 'BepInEx source clone failed.'}
    git -C $SourceDir fetch --depth 1 origin $commit
    if($LASTEXITCODE -ne 0){throw 'Pinned source fetch failed.'}
    git -C $SourceDir checkout --detach $commit
    if($LASTEXITCODE -ne 0){throw 'Pinned source checkout failed.'}
}
$actual=git -C $SourceDir rev-parse HEAD
if($LASTEXITCODE -ne 0 -or $actual -ne $commit){throw 'Source is not the pinned BepInEx 788 commit.'}
git -C $SourceDir apply --reverse --check $patch 2>$null
if($LASTEXITCODE -ne 0){
    git -C $SourceDir apply --check $patch
    if($LASTEXITCODE -ne 0){throw 'Source changes conflict with the loader patch.'}
    git -C $SourceDir apply $patch
    if($LASTEXITCODE -ne 0){throw 'Loader patch failed.'}
}
& $Dotnet build (Join-Path $SourceDir 'Runtimes\Unity\BepInEx.Unity.IL2CPP\BepInEx.Unity.IL2CPP.csproj') -c Release '-p:GeneratePackageOnBuild=false' "-p:SolutionDir=$SourceDir\" '-p:VersionSuffix=gunfire-net10.1' "-p:PathMap=$SourceDir=/_/BepInEx" '-p:NoWarn=1591' -o $OutputDir --nologo
if($LASTEXITCODE -ne 0){throw 'Custom loader build failed.'}

# Dobby is the unchanged native detour provider supplied by the official build.
$cache=Join-Path $root 'artifacts\loader-upstream-788'
$archive=Join-Path $cache 'BepInEx-788.zip'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
if(!(Test-Path $archive)){
    Invoke-WebRequest -UseBasicParsing -Uri 'https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip' -OutFile $archive
}
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne 'F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A'){throw 'Official loader archive checksum mismatch.'}
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $cache 'package') -Force
Copy-Item -LiteralPath (Join-Path $cache 'package\BepInEx\core\dobby.dll') -Destination $OutputDir -Force
Copy-Item -LiteralPath (Join-Path $SourceDir 'LICENSE') -Destination (Join-Path $OutputDir 'BepInEx-LICENSE.txt') -Force
Copy-Item -LiteralPath $patch -Destination $OutputDir -Force
[ordered]@{version='6.0.0-gunfire-net10.1';upstreamCommit=$commit;patchSha256=(Get-FileHash $patch).Hash.ToLowerInvariant();runtime='10.0.12';harmonyX='2.16.1';target='Windows Steam x64 IL2CPP';core=@(Get-ChildItem $OutputDir -Filter '*.dll' | Sort-Object Name | ForEach-Object {[ordered]@{name=$_.Name;sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDir 'build-info.json') -Encoding UTF8
Write-Output "Built the local BepInEx .NET 10 variant at $OutputDir. Install it using docs/DEPENDENCIES.md."
