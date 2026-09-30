# MASSIVE cabinet watchdog

Build from **MASSIVE > Cabinet > Build Watchdog Player** in Unity, outside Play Mode.
It creates a new dated folder under Builds, starting at Attract and using the enabled
Build Settings scenes. Existing builds are preserved. The result is a normal Windows
x64 player (Development Build, profiler autoconnect and script debugging are off).
The supervisor uses Windows PowerShell 5.1 already supplied by Windows; no installation
or administrator rights are needed.

In the finished build folder:

- **Start-Cabinet.cmd** explicitly resumes the cabinet and launches the hidden watchdog.
- **Stop-Cabinet.cmd** latches maintenance, closes the watchdog-owned game, and waits
  for the watchdog to stop. Use this before development, updates or shutdown.
- **Cabinet-Status.cmd** shows the last status and the log directory.
- Launching **MASSIVE.exe** directly is an ordinary, unsupervised development session.

Closing the supervised game with Alt+F4 or Task Manager counts as an unexpected exit
and relaunches it. Use Stop-Cabinet for intentional exits. Start is idempotent while
the watchdog runs. If Stop is still in progress, wait for it to finish before Start.
An already running player is never adopted or killed by a new watchdog.

## Defaults and tuning

Edit watchdog.json beside the built player. Settings are read when the watchdog starts.

| Setting | Default | Meaning |
| --- | --- | --- |
| startupGraceSeconds | 120 | No hang recovery during the first two minutes of each launch |
| heartbeatTimeoutSeconds | 60 | Restart after no new main-thread heartbeat for this long, once startup grace has elapsed |
| restartDelaySeconds | 5 | Pause between attempts |
| maxRestarts / restartWindowSeconds | 3 / 600 | Three automatic restarts per rolling ten minutes; a fourth failure latches maintenance |
| stopGraceSeconds | 10 | Time for graceful exit before terminating only the owned player |
| pollMilliseconds | 500 | Supervisor polling interval |
| retainedPlayerLogs | 10 | Keep the ten latest player logs, pruning before each launch |

Normal fullscreen arguments retain 3840 x 2160, Very High, DX11 and the BitBlt override.
VSync and MSAA remain controlled by the existing project quality settings.

State defaults to **%LOCALAPPDATA%/MASSIVE/Watchdog**:
status.json, maintenance.flag, watchdog.log and PlayerLogs/Player-*.log.
The watchdog log rotates at 2 MiB with one previous copy. Player logs from completed
runs are count-limited; the currently open Unity log is not truncated or size-capped.
Status is refreshed every five seconds and on transitions. Its timestamp matters:
a saved Running status alone does not prove that the supervisor is still alive.
Start clears the maintenance latch only after acquiring ownership and validating
the player/configuration. A non-Resume invocation respects the latch.

## What is monitored

CabinetHeartbeat is bootstrapped automatically before the first scene only in a
Windows standalone launched with the supervisor's environment variables.
Its persistent object sends one small, nonblocking localhost UDP packet per second
from Update using unscaled real time. There is no scene setup, disk write, background
heartbeat thread, external server, public listening address or fixed port.
Each launch uses a fresh random token and increasing sequence, so another launch's
or repeated stale packets do not count as progress. Ordinary Editor Play Mode and
direct player launches do not create a heartbeat object.

A process exit or stalled main thread restarts the game at Attract, losing the current
match. Long synchronous work over the timeout can also trigger recovery; adjust the
timeout if measured legitimate loads require it. A gameflow bug that keeps rendering
and updating, lost controller/audio/display connections, a stopped watchdog, an OS
freeze or power failure are outside this check. The supervisor does not reboot Windows.

The supervisor keeps its original child process handle. It never kills a PID read from
disk. Named locks prevent duplicate supervision of the same state directory or
executable in the current Windows session. Keep one interactive cabinet session.
If the supervisor itself is force-killed and its player remains, close that orphaned
player manually before restarting; automatic adoption would be unsafe.

## Recovery and deployment

After a repeated-failure stop, inspect watchdog.log and the most recent Player log,
correct the problem (or run a previous known-good build), then use Start-Cabinet.
Failures remain latched across later Run invocations and logons until an explicit Start.
Keep a previous build for manual rollback. Boot/login integration is a separate setup
step: this change does not install a task, alter sign-in, or modify Windows settings.
An eventual logon task should run Watchdog.ps1 -Action Run **without -Resume** so it
respects maintenance; the operator Start launcher is the explicit resume command.

## Validation

Run powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Cabinet/Tests/Test-Watchdog.ps1 from the repository root (Windows PowerShell 5.1). It compiles an
isolated fake player into a temporary folder and exercises the real supervisor with
short test deadlines. It never launches or terminates MASSIVE or changes cabinet state.
Actual Unity player/build smoke evidence is recorded in Docs/AI/CabinetWatchdog.md.
