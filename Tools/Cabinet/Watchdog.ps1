#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Run', 'Stop', 'Status')][string]$Action = 'Run',
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'watchdog.json'),
    [switch]$Resume
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Full-Path([string]$Path, [string]$Base) {
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    if (![IO.Path]::IsPathRooted($expanded)) { $expanded = Join-Path $Base $expanded }
    return [IO.Path]::GetFullPath($expanded)
}
function Quote-Argument([string]$Value) {
    # Windows CommandLineToArgvW/CRT quoting. Never pass arguments through cmd.exe.
    return '"' + (($Value -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}
function Mutex-Name([string]$Value) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $digest = $hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value.ToLowerInvariant())) }
    finally { $hash.Dispose() }
    return 'Local\MASSIVE-Watchdog-' + ([BitConverter]::ToString($digest)).Replace('-', '')
}
function Write-Event([string]$Message) {
    $line = [DateTime]::UtcNow.ToString('o') + ' ' + $Message
    if ((Test-Path -LiteralPath $script:eventLog) -and (Get-Item -LiteralPath $script:eventLog).Length -gt 2MB) {
        Move-Item -LiteralPath $script:eventLog -Destination ($script:eventLog + '.previous') -Force
    }
    Add-Content -LiteralPath $script:eventLog -Value $line -Encoding UTF8
}
function Write-Status([string]$State, [string]$Reason) {
    $playerId = $null
    if ($null -ne $script:player -and !$script:player.HasExited) { $playerId = $script:player.Id }
    $status = [ordered]@{
        state = $State; reason = $Reason; updatedUtc = [DateTime]::UtcNow.ToString('o')
        watchdogPid = $PID; playerPid = $playerId; executable = $script:exe
        lastHeartbeatUtc = $script:lastHeartbeatUtc; launchCount = $script:launchCount
    } | ConvertTo-Json
    # Same-volume atomic replacement prevents readers seeing half a JSON document.
    $temp = $script:statusPath + '.tmp'
    [IO.File]::WriteAllText($temp, $status)
    try {
        if (Test-Path -LiteralPath $script:statusPath) { [IO.File]::Replace($temp, $script:statusPath, [System.Management.Automation.Language.NullString]::Value) }
        else { [IO.File]::Move($temp, $script:statusPath) }
    } catch [IO.IOException] {
        # A reader/antivirus can briefly lock the snapshot. Monitoring continues;
        # the next publication replaces it instead of restarting a healthy game.
        Write-Warning ('Status snapshot busy: ' + $_.Exception.Message)
    }
}
function Stop-OwnedPlayer {
    if ($null -eq $script:player -or $script:player.HasExited) { return }
    Write-Event ('Stopping owned player PID ' + $script:player.Id)
    # Retain the original Process handle: never terminate a PID from a status file.
    [void]$script:player.CloseMainWindow()
    if (!$script:player.WaitForExit([int]($config.stopGraceSeconds * 1000))) {
        Write-Event 'Graceful stop timed out; terminating the owned player.'
        $script:player.Kill()
        if (!$script:player.WaitForExit(10000)) { throw 'Owned player could not be stopped; refusing another launch.' }
    }
}
function Assert-NoExistingPlayer {
    $name = [IO.Path]::GetFileNameWithoutExtension($script:exe)
    foreach ($candidate in [Diagnostics.Process]::GetProcessesByName($name)) {
        try {
            try { $candidatePath = $candidate.MainModule.FileName }
            catch { if (!$candidate.HasExited) { throw 'Cannot inspect an existing player; refusing a duplicate.' }; continue }
            if ($candidatePath) {
                throw "A $name player is already running (PID $($candidate.Id)). Close it before starting the watchdog."
            }
        } finally { $candidate.Dispose() }
    }
}
function Trim-PlayerLogs {
    # Only these supervisor-created logs, never saves or other application files.
    Get-ChildItem -LiteralPath $script:logDirectory -Filter 'Player-*.log' -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -Skip ([int]$config.retainedPlayerLogs - 1) |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName }
}

$configFile = [IO.Path]::GetFullPath($ConfigPath)
$config = Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
if ($config.schemaVersion -ne 1) { throw 'Unsupported watchdog configuration version.' }
$base = Split-Path -Parent $configFile
$script:exe = Full-Path $config.executable $base
$stateDirectory = Full-Path $config.stateDirectory $base
[void][IO.Directory]::CreateDirectory($stateDirectory)
$maintenance = Join-Path $stateDirectory 'maintenance.flag'
$script:statusPath = Join-Path $stateDirectory 'status.json'
$script:eventLog = Join-Path $stateDirectory 'watchdog.log'
$script:logDirectory = Join-Path $stateDirectory 'PlayerLogs'
$script:player = $null
$script:lastHeartbeatUtc = $null
$script:launchCount = 0

if ($Action -eq 'Status') {
    Write-Output "Maintenance latch: $(Test-Path -LiteralPath $maintenance)"
    if (Test-Path -LiteralPath $script:statusPath) {
        Get-Content -LiteralPath $script:statusPath
        Write-Output 'Status is a recorded snapshot; check updatedUtc before treating it as live.'
    } else { Write-Output 'No watchdog run has recorded status yet.' }
    Write-Output "Logs: $stateDirectory"
    exit 0
}
if ($Action -eq 'Stop') {
    [IO.File]::WriteAllText($maintenance, [DateTime]::UtcNow.ToString('o'))
    Write-Output 'Maintenance enabled. The watchdog will close its player and stay stopped.'
    # Wait on the supervisor lock, not an untrusted/reused PID.
    $stopMutex = [Threading.Mutex]::new($false, (Mutex-Name $stateDirectory))
    try {
        try { $stopped = $stopMutex.WaitOne([int](($config.stopGraceSeconds + 15) * 1000)) }
        catch [Threading.AbandonedMutexException] { $stopped = $true }
        if (!$stopped) { throw 'Stop is pending. Inspect watchdog.log; do not start another player yet.' }
        $stopMutex.ReleaseMutex()
    } finally { $stopMutex.Dispose() }
    exit 0
}

$stateMutex = [Threading.Mutex]::new($false, (Mutex-Name $stateDirectory))
$exeMutex = [Threading.Mutex]::new($false, (Mutex-Name $script:exe))
$ownsState = $false
$ownsExe = $false
$listener = $null
$exitCode = 0
try {
    try { $ownsState = $stateMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsState = $true }
    if (!$ownsState) { Write-Output 'The cabinet watchdog is already running.'; exit 0 }
    try { $ownsExe = $exeMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsExe = $true }
    if (!$ownsExe) { throw 'Another watchdog already owns this executable.' }

    foreach ($name in @('startupGraceSeconds','heartbeatTimeoutSeconds','restartDelaySeconds',
                         'restartWindowSeconds','stopGraceSeconds','pollMilliseconds','retainedPlayerLogs')) {
        $number = [double]$config.$name
        if ([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -le 0) { throw "Invalid $name." }
    }
    if ($config.maxRestarts -lt 0 -or [int]$config.maxRestarts -ne $config.maxRestarts) { throw 'Invalid maxRestarts.' }
    if ($config.pollMilliseconds -gt 1000 -or $config.pollMilliseconds -lt 50) { throw 'pollMilliseconds must be 50-1000.' }
    if ($config.retainedPlayerLogs -lt 1 -or [int]$config.retainedPlayerLogs -ne $config.retainedPlayerLogs) { throw 'Invalid retainedPlayerLogs.' }
    if (!(Test-Path -LiteralPath $script:exe -PathType Leaf)) { throw "Player not found: $script:exe. Build using MASSIVE > Cabinet > Build Watchdog Player." }
    $manifestPath = Join-Path (Split-Path -Parent $script:exe) 'watchdog-build.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.heartbeatProtocol -ne 1) { throw 'This player does not declare heartbeat protocol 1. Rebuild it.' }
    Assert-NoExistingPlayer
    if ($Resume -and (Test-Path -LiteralPath $maintenance)) { Remove-Item -LiteralPath $maintenance }
    if (Test-Path -LiteralPath $maintenance) {
        Write-Status 'Maintenance' 'Use Start-Cabinet to resume explicitly.'
        exit 0
    }
    [void][IO.Directory]::CreateDirectory($script:logDirectory)
    Write-Event 'Watchdog started.'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $failures = [Collections.Generic.Queue[double]]::new()
    while (!(Test-Path -LiteralPath $maintenance)) {
        Assert-NoExistingPlayer
        Trim-PlayerLogs
        $listener = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback, 0))
        $listener.Client.Blocking = $false
        $token = [Guid]::NewGuid()
        $tokenBytes = $token.ToByteArray()
        $port = ([Net.IPEndPoint]$listener.Client.LocalEndPoint).Port
        $playerLog = Join-Path $script:logDirectory ('Player-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '.log')
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $script:exe
        $start.WorkingDirectory = Split-Path -Parent $script:exe
        $start.UseShellExecute = $false
        $start.Arguments = (@(@($config.arguments) + @('-logFile', $playerLog)) |
            ForEach-Object { Quote-Argument ([string]$_) }) -join ' '
        $start.EnvironmentVariables['MASSIVE_WATCHDOG_PORT'] = [string]$port
        $start.EnvironmentVariables['MASSIVE_WATCHDOG_TOKEN'] = $token.ToString('N')
        # Do not let an unrelated PowerShell console appear for a console test player.
        $start.CreateNoWindow = $true
        if (Test-Path -LiteralPath $maintenance) { break }
        $script:player = [Diagnostics.Process]::Start($start)
        $script:launchCount++
        $launched = $clock.Elapsed.TotalSeconds
        $lastBeat = $launched
        $sequence = [long]0
        $script:lastHeartbeatUtc = $null
        $nextStatus = 0.0
        $reason = ''
        Write-Event "Launched player PID $($script:player.Id); player log $playerLog"
        Write-Status 'Starting' 'Waiting for main-thread heartbeat.'
        while (!(Test-Path -LiteralPath $maintenance)) {
            if ($script:player.HasExited) { $reason = "Player exited (code $($script:player.ExitCode))."; break }
            # Bound the drain so bad local traffic cannot starve stop/hang checks.
            for ($i = 0; $i -lt 128 -and $listener.Available -gt 0; $i++) {
                $remote = [Net.IPEndPoint]::new([Net.IPAddress]::Loopback, 0)
                try { $packet = $listener.Receive([ref]$remote) }
                catch [Net.Sockets.SocketException] {
                    if ($_.Exception.SocketErrorCode -eq [Net.Sockets.SocketError]::WouldBlock) { break }
                    throw
                }
                if ($packet.Length -ne 24 -or ![Net.IPAddress]::IsLoopback($remote.Address)) { continue }
                $valid = $true
                for ($b = 0; $b -lt 16; $b++) { if ($packet[$b] -ne $tokenBytes[$b]) { $valid = $false; break } }
                $receivedSequence = [BitConverter]::ToInt64($packet, 16)
                if ($valid -and $receivedSequence -gt $sequence) {
                    if ($sequence -eq 0) { Write-Event 'Main-thread heartbeat received.' }
                    $sequence = $receivedSequence
                    $lastBeat = $clock.Elapsed.TotalSeconds
                    $script:lastHeartbeatUtc = [DateTime]::UtcNow.ToString('o')
                }
            }
            $now = $clock.Elapsed.TotalSeconds
            if ($now - $launched -ge $config.startupGraceSeconds -and
                ($sequence -eq 0 -or $now - $lastBeat -ge $config.heartbeatTimeoutSeconds)) {
                $reason = if ($sequence -eq 0) { 'Startup heartbeat timed out.' } else { 'Main-thread heartbeat stalled.' }
                break
            }
            if ($now -ge $nextStatus) {
                $state = if ($sequence -gt 0) { 'Running' } else { 'Starting' }
                Write-Status $state 'Monitoring the owned player.'
                $nextStatus = $now + 5
            }
            Start-Sleep -Milliseconds ([int]$config.pollMilliseconds)
        }
        $listener.Close()
        $listener = $null
        if (Test-Path -LiteralPath $maintenance) { break }
        Write-Event $reason
        Write-Status 'Recovering' $reason
        Stop-OwnedPlayer
        $script:player.Dispose()
        $script:player = $null
        $now = $clock.Elapsed.TotalSeconds
        while ($failures.Count -gt 0 -and $now - $failures.Peek() -gt $config.restartWindowSeconds) { [void]$failures.Dequeue() }
        $failures.Enqueue($now)
        if ($failures.Count -gt $config.maxRestarts) {
            [IO.File]::WriteAllText($maintenance, 'Repeated failures: ' + $reason)
            Write-Event 'Restart limit reached; latched in maintenance until an explicit Start.'
            Write-Status 'Faulted' 'Restart limit reached. Inspect logs, then use Start-Cabinet.'
            $exitCode = 2
            break
        }
        $retryAt = $clock.Elapsed.TotalSeconds + $config.restartDelaySeconds
        while ($clock.Elapsed.TotalSeconds -lt $retryAt -and !(Test-Path -LiteralPath $maintenance)) {
            Start-Sleep -Milliseconds ([int]$config.pollMilliseconds)
        }
    }
    if ($exitCode -eq 0) {
        Stop-OwnedPlayer
        Write-Status 'Maintenance' 'Stopped intentionally; use Start-Cabinet to resume.'
        Write-Event 'Maintenance stop completed.'
    }
} catch {
    $failureRecord = $_
    $failureMessage = $_.Exception.Message
    $exitCode = 1
    if ($ownsState) {
        # Persist a latch so an eventual logon task cannot hammer a broken setup.
        try { [IO.File]::WriteAllText($maintenance, $failureMessage) } catch {}
        try { Write-Event ('Watchdog error: ' + $failureMessage) } catch {}
        try { Stop-OwnedPlayer } catch { Write-Warning $_.Exception.Message }
        try { Write-Status 'Faulted' $failureMessage } catch {}
    }
    Write-Error -ErrorRecord $failureRecord -ErrorAction Continue
} finally {
    if ($null -ne $listener) { $listener.Close() }
    if ($null -ne $script:player) { $script:player.Dispose() }
    if ($ownsExe) { $exeMutex.ReleaseMutex() }
    if ($ownsState) { $stateMutex.ReleaseMutex() }
    $exeMutex.Dispose()
    $stateMutex.Dispose()
}
exit $exitCode
