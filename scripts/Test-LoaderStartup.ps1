param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [ValidateRange(15,180)][int]$Seconds=90,
    [switch]$NativeProbe,
    [switch]$RegenerateInterop,
    [string]$Steam=(Join-Path ${env:ProgramFiles(x86)} 'Steam\steam.exe'),
    [string]$Dotnet=$(if(Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe"){"$env:USERPROFILE\.dotnet\dotnet.exe"}else{'dotnet'})
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
. (Join-Path $root 'installer\Core.ps1')
$gameRoot=Get-GameRoot $GameDir
Assert-GameClosed
$output=Join-Path $root ('artifacts\loader-validation\'+[guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $output | Out-Null
$probeDir=Join-Path $gameRoot 'BepInEx\plugins\LoaderCompatSmoke'
$probeInstalled=$false
$interopBackup=$null
$passed=$false
$owned=@{}
$started=$null
$seen=$false
$section=''
$logPath=Join-Path $gameRoot 'BepInEx\LogOutput.log'
$baselineLength=0
if(Test-Path $logPath){$baselineLength=(Get-Content -LiteralPath $logPath -Raw).Length}
try {
    if($NativeProbe){
        if(Test-Path $probeDir){throw 'A LoaderCompatSmoke folder already exists; preserve or remove it before this test.'}
        & $Dotnet build (Join-Path $root 'tests\LoaderCompatibility\Native\NativeCompatibility.csproj') -c Release "-p:GameDir=$gameRoot" -o (Join-Path $output 'probe-build') --nologo
        if($LASTEXITCODE -ne 0){throw 'Native compatibility probe build failed.'}
        New-Item -ItemType Directory -Path $probeDir | Out-Null
        $probeInstalled=$true
        Copy-Item -LiteralPath (Join-Path $output 'probe-build\LoaderCompatSmoke.dll') -Destination $probeDir
    }
    if($RegenerateInterop){
        $interop=Get-SafePath $gameRoot 'BepInEx/interop'
        $interopBackup=Join-Path $output 'previous-interop'
        if(Test-Path $interop){Move-Item -LiteralPath $interop -Destination $interopBackup}
    }
    $started=Get-Date
    Start-Process -FilePath $Steam -ArgumentList '-applaunch 1217060' -WindowStyle Hidden
    while(((Get-Date)-$started).TotalSeconds -lt $Seconds){
        foreach($process in @(Get-Process -Name 'Gunfire Reborn','UnityCrashHandler64' -ErrorAction SilentlyContinue)){
            if($process.StartTime -ge $started -and $process.Path -like "$gameRoot\*"){
                $owned[$process.Id]=$process.StartTime
                if($process.ProcessName -eq 'Gunfire Reborn'){$seen=$true}
            }
        }
        if(Test-Path $logPath){
            $log=Get-Content -LiteralPath $logPath -Raw
            $fresh=if($log.Length -ge $baselineLength){$log.Substring($baselineLength)}else{$log}
            $index=$fresh.LastIndexOf('[Message: Preloader] BepInEx 6.0.0-gunfire-net10.1')
            if($index -ge 0){$section=$fresh.Substring($index)}
        }
        $running=@(Get-Process -Name 'Gunfire Reborn' -ErrorAction SilentlyContinue | Where-Object {$owned.ContainsKey($_.Id)})
        if($seen -and !$running.Count){break}
        Start-Sleep -Seconds 2
    }
    $running=@(Get-Process -Name 'Gunfire Reborn' -ErrorAction SilentlyContinue | Where-Object {$owned.ContainsKey($_.Id)})
    Set-Content -LiteralPath (Join-Path $output 'startup.log') -Value $section
    $result=[ordered]@{started=$started.ToString('o');elapsedSeconds=[math]::Round(((Get-Date)-$started).TotalSeconds,1);observedGame=$seen;chainloaderComplete=$section.Contains('Chainloader startup complete');survived=$running.Count -gt 0;processIds=@($owned.Keys);runtime10=$section.Contains('Runtime version: 10.0.12');pluginLoaded=$section.Contains('OK plugin-load');nativeProbePassed=$section.Contains('PASS HarmonyX 2.16.1 native IL2CPP getter');errors=@($section -split "`n" | Where-Object {$_ -match '\[Error|Exception|Fatal|FAIL native'})}
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json')
    $result | ConvertTo-Json -Depth 4
    if(!$result.chainloaderComplete -or !$result.runtime10 -or !$result.pluginLoaded -or !$result.survived -or ($NativeProbe -and !$result.nativeProbePassed) -or $result.errors.Count){throw "Loader startup check failed. Evidence: $output"}
    $passed=$true
} finally {
    # Only close this test's game/crash-handler instances; verify creation times
    # so a reused process ID cannot identify a different application.
    foreach($id in @($owned.Keys)){
        $process=Get-Process -Id $id -ErrorAction SilentlyContinue
        if($process -and $process.StartTime -eq $owned[$id] -and $process.Path -like "$gameRoot\*"){
            Stop-Process -Id $id -ErrorAction SilentlyContinue
        }
    }
    if($probeInstalled -and (Test-Path $probeDir)){
        $resolved=[IO.Path]::GetFullPath($probeDir)
        if($resolved -ne "$gameRoot\BepInEx\plugins\LoaderCompatSmoke"){throw 'Unexpected probe cleanup path.'}
        Move-Item -LiteralPath $resolved -Destination (Join-Path $output 'installed-probe')
    }
    if(!$passed -and $interopBackup -and (Test-Path $interopBackup)){
        $interop=Get-SafePath $gameRoot 'BepInEx/interop'
        if(Test-Path $interop){Move-Item -LiteralPath $interop -Destination (Join-Path $output 'failed-interop')}
        Move-Item -LiteralPath $interopBackup -Destination $interop
    }
    Write-Output "Test game processes closed; evidence saved at $output"
}
