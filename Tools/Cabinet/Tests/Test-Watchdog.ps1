#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $env:TEMP ('MASSIVE-WatchdogTests-' + [Guid]::NewGuid().ToString('N'))))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run this test with Windows PowerShell 5.1 (powershell.exe), not pwsh.' }
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh test output directory.' }
[void][IO.Directory]::CreateDirectory($root)
$supervisor = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Watchdog.ps1'))
$psExe = Join-Path $PSHOME 'powershell.exe'
$fixtureName = 'WatchdogFixture' + [Guid]::NewGuid().ToString('N')
$fixtureExe = Join-Path $root ($fixtureName + '.exe')
Add-Type -Path (Join-Path $PSScriptRoot 'FakePlayer.cs') -OutputAssembly $fixtureExe -OutputType ConsoleApplication
$checks = [Collections.Generic.List[string]]::new()
$owned = [Collections.Generic.List[Diagnostics.Process]]::new()

function Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw "FAIL: $Message" }
    $checks.Add($Message)
    Write-Output "PASS: $Message"
}
function Await([scriptblock]$Condition, [string]$Label, [double]$Seconds = 15) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 100
    } while ($timer.Elapsed.TotalSeconds -lt $Seconds)
    throw "Timed out: $Label"
}
function Read-Status($Case) {
    $path = Join-Path $Case.state 'status.json'
    if (Test-Path -LiteralPath $path) {
        try { return [IO.File]::ReadAllText($path) | ConvertFrom-Json }
        catch [IO.IOException] { return $null }
    }
    return $null
}
function New-Case([string]$Name, [string]$Mode = 'healthy', [int]$MaxRestarts = 3) {
    $directory = Join-Path $root $Name
    [void][IO.Directory]::CreateDirectory($directory)
    $exe = Join-Path $directory ($fixtureName + '.exe')
    Copy-Item -LiteralPath $fixtureExe -Destination $exe
    [IO.File]::WriteAllText((Join-Path $directory 'watchdog-build.json'), '{"heartbeatProtocol":1}')
    $control = Join-Path $directory 'control.txt'
    [IO.File]::WriteAllText($control, $Mode)
    $config = [ordered]@{
        schemaVersion=1; executable=$exe; stateDirectory=(Join-Path $directory 'state')
        arguments=@('--control',$control,'Very High','trailing\','embedded"quote')
        startupGraceSeconds=2.5; heartbeatTimeoutSeconds=0.8
        restartDelaySeconds=0.3; maxRestarts=$MaxRestarts; restartWindowSeconds=60
        stopGraceSeconds=0.2; pollMilliseconds=100; retainedPlayerLogs=3
    }
    $path = Join-Path $directory 'watchdog.json'
    $config | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding UTF8
    return [pscustomobject]@{ name=$Name; directory=$directory; exe=$exe; control=$control; config=$path; state=$config.stateDirectory; process=$null }
}
function Start-Case($Case, [bool]$Resume = $true) {
    $args = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $supervisor + '" -ConfigPath "' + $Case.config + '"'
    if ($Resume) { $args += ' -Resume' }
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0,8)
    $process = Start-Process -FilePath $psExe -ArgumentList $args -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $Case.directory ("supervisor-$suffix.out")) -RedirectStandardError (Join-Path $Case.directory ("supervisor-$suffix.err"))
    $null = $process.Handle # Retain the exit code even after a very short-lived duplicate exits.
    $owned.Add($process)
    $Case.process = $process
    return $process
}
function Stop-Case($Case) {
    & $psExe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $supervisor -ConfigPath $Case.config -Action Stop | Out-Null
    Check ($LASTEXITCODE -eq 0) ($Case.name + ': Stop command succeeded')
    Await { $Case.process.HasExited } ($Case.name + ' stopped')
}
function Running($Case) { $s = Read-Status $Case; return ($null -ne $s -and $s.state -eq 'Running' -and $null -ne $s.lastHeartbeatUtc) }

try {
    $healthy = New-Case 'healthy and quoting'
    $first = Start-Case $healthy
    Await { Running $healthy } 'healthy heartbeat'
    Start-Sleep -Seconds 3
    $s = Read-Status $healthy
    Check ($s.launchCount -eq 1 -and !$first.HasExited) 'Healthy process survives beyond both deadlines'
    $playerId = $s.playerPid
    $lock = [IO.File]::Open((Join-Path $healthy.state 'status.json'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    try { Start-Sleep -Seconds 6; Check (!$first.HasExited -and !(Get-Process -Id $playerId).HasExited) 'A locked status snapshot does not stop a healthy game' }
    finally { $lock.Dispose() }
    $receivedArgs = @(Get-Content -LiteralPath (Join-Path $healthy.directory ("args-$playerId.txt")))
    Check ($receivedArgs[2] -eq 'Very High' -and $receivedArgs[3] -eq 'trailing\' -and $receivedArgs[4] -eq 'embedded"quote') 'Arguments preserve spaces, trailing slashes and quotes'
    $duplicate = Start-Case $healthy
    Await { $duplicate.HasExited } 'duplicate supervisor exit'
    Check ($duplicate.ExitCode -eq 0 -and !(Get-Process -Id $playerId).HasExited) 'Duplicate Start leaves the original player running'
    $healthy.process = $first

    # Seed only test-owned files, then force a real crash and verify recovery/retention.
    $logs = Join-Path $healthy.state 'PlayerLogs'
    1..8 | ForEach-Object { [IO.File]::WriteAllText((Join-Path $logs ("Player-seeded-$_.log")), 'test') }
    [IO.File]::WriteAllText((Join-Path $logs 'keep.txt'), 'not a player log')
    [IO.File]::WriteAllText($healthy.control, 'crash')
    Await { !(Get-Process -Id $playerId -ErrorAction SilentlyContinue) } 'crashed fixture exits'
    [IO.File]::WriteAllText($healthy.control, 'healthy')
    Await { $s=Read-Status $healthy; $null -ne $s -and $s.launchCount -ge 2 -and $s.state -eq 'Running' } 'crash recovery'
    $s = Read-Status $healthy
    Check ($s.playerPid -ne $playerId -and !$first.HasExited) 'Unexpected exit launches a new player and receives heartbeats'
    Check (@(Get-ChildItem -LiteralPath $logs -Filter 'Player-*.log').Count -le 3 -and (Test-Path -LiteralPath (Join-Path $logs 'keep.txt'))) 'Log retention deletes only old player logs'
    $playerId = $s.playerPid
    [IO.File]::WriteAllText($healthy.control, 'freeze')
    Await { !(Get-Process -Id $playerId -ErrorAction SilentlyContinue) } 'frozen fixture terminated'
    [IO.File]::WriteAllText($healthy.control, 'healthy')
    Await { $s=Read-Status $healthy; $null -ne $s -and $s.launchCount -ge 3 -and $s.state -eq 'Running' } 'freeze recovery'
    Check ((Get-Content -LiteralPath (Join-Path $healthy.state 'watchdog.log') -Raw).Contains('Main-thread heartbeat stalled.')) 'A living but frozen player is detected and restarted'
    $playerId = (Read-Status $healthy).playerPid
    Stop-Case $healthy
    Check (!(Get-Process -Id $playerId -ErrorAction SilentlyContinue) -and (Read-Status $healthy).state -eq 'Maintenance') 'Maintenance stops the owned player without restart'
    $latched = Start-Case $healthy $false
    Await { $latched.HasExited } 'latched Run exits'
    Check ($latched.ExitCode -eq 0 -and (Read-Status $healthy).state -eq 'Maintenance') 'A non-resume launch respects persistent maintenance'
    [void](Start-Case $healthy)
    Await { Running $healthy } 'explicit resume'
    Check (!(Test-Path -LiteralPath (Join-Path $healthy.state 'maintenance.flag'))) 'Explicit Start resumes after maintenance'
    Stop-Case $healthy

    $delay = New-Case 'startup grace' 'delay'
    [void](Start-Case $delay)
    Await { Running $delay } 'delayed first heartbeat'
    Check ((Read-Status $delay).launchCount -eq 1) 'Startup grace tolerates a first heartbeat later than the steady-state timeout'
    Stop-Case $delay

    foreach ($mode in @('silent', 'wrong', 'repeat')) {
        $case = New-Case $mode $mode 0
        [void](Start-Case $case)
        Await { $case.process.HasExited } ($mode + ' timeout')
        $s = Read-Status $case
        Check ($case.process.ExitCode -eq 2 -and $s.state -eq 'Faulted' -and $s.launchCount -eq 1) ($mode + ': missing, invalid or stale heartbeat cannot keep a player alive')
    }

    $burst = New-Case 'restart limit' 'crash' 2
    [void](Start-Case $burst)
    Await { $burst.process.HasExited } 'restart limit'
    Check ((Read-Status $burst).launchCount -eq 3 -and $burst.process.ExitCode -eq 2) 'Repeated failure stops after the configured number of restarts'
    Check (Test-Path -LiteralPath (Join-Path $burst.state 'maintenance.flag')) 'Repeated failure latches maintenance on disk'

    $retry = New-Case 'stop during recovery' 'crash'
    $retryConfig = Get-Content -LiteralPath $retry.config -Raw | ConvertFrom-Json
    $retryConfig.restartDelaySeconds = 10
    $retryConfig | ConvertTo-Json | Set-Content -LiteralPath $retry.config -Encoding UTF8
    [void](Start-Case $retry)
    Await { $s=Read-Status $retry; $null -ne $s -and $s.state -eq 'Recovering' } 'restart delay'
    Stop-Case $retry
    Check ((Read-Status $retry).launchCount -eq 1) 'Stop interrupts restart backoff without relaunching'

    $pending = New-Case 'stop during startup' 'silent'
    [void](Start-Case $pending)
    Await { $s=Read-Status $pending; $null -ne $s -and $s.state -eq 'Starting' } 'startup state'
    Stop-Case $pending
    Check ((Read-Status $pending).launchCount -eq 1) 'Stop interrupts startup without waiting for startup grace'

    $bad = New-Case 'missing executable'
    Remove-Item -LiteralPath $bad.exe
    [void](Start-Case $bad)
    Await { $bad.process.HasExited } 'invalid configuration'
    Check ($bad.process.ExitCode -eq 1 -and (Read-Status $bad).state -eq 'Faulted') 'Missing player reports a fault and never enters a restart loop'

    $blocked = New-Case 'unmanaged existing player'
    $fakeStart = [Diagnostics.ProcessStartInfo]::new()
    $fakeStart.FileName = $blocked.exe
    $fakeStart.UseShellExecute = $false
    $fakeStart.CreateNoWindow = $true
    $fakeStart.Arguments = '--control "' + $blocked.control + '"'
    $fakeStart.EnvironmentVariables['MASSIVE_WATCHDOG_PORT'] = '9'
    $fakeStart.EnvironmentVariables['MASSIVE_WATCHDOG_TOKEN'] = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($blocked.control, 'silent')
    $unmanaged = [Diagnostics.Process]::Start($fakeStart)
    $owned.Add($unmanaged)
    [void](Start-Case $blocked)
    Await { $blocked.process.HasExited } 'existing player refusal'
    Check ($blocked.process.ExitCode -eq 1 -and !$unmanaged.HasExited) 'An unmanaged existing player is neither duplicated nor terminated'
    $otherBuild = New-Case 'another build is running'
    [void](Start-Case $otherBuild)
    Await { $otherBuild.process.HasExited } 'different-build player refusal'
    Check ($otherBuild.process.ExitCode -eq 1 -and !$unmanaged.HasExited) 'An unmanaged player from another build also blocks duplicate launch'
    $unmanaged.Kill()
    $unmanaged.WaitForExit()
    $checks | Set-Content -LiteralPath (Join-Path $root 'passed.txt') -Encoding UTF8
    Write-Output ("ALL " + $checks.Count + " CHECKS PASSED. Evidence: " + $root)
} finally {
    # Only handles launched here, and leftover fixture processes verified by exact
    # executable path inside this unique test directory; never game/editor processes.
    foreach ($process in $owned) {
        if (!$process.HasExited) { $process.Kill(); [void]$process.WaitForExit(10000) }
        $process.Dispose()
    }
    foreach ($process in [Diagnostics.Process]::GetProcessesByName($fixtureName)) {
        try {
            $path = [IO.Path]::GetFullPath($process.MainModule.FileName)
            if ($path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                $process.Kill(); [void]$process.WaitForExit(10000)
            }
        } finally { $process.Dispose() }
    }
}
