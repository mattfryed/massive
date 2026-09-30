# Cabinet watchdog - September 29, 2026

Implemented in the active cabinet checkout, C:/Users/mattf/massive. Baseline commit
2aa4c942. The initial working-tree change was a user layout. Additional Carrier,
Drone, Action Lab and editor-state changes appeared concurrently; they are not part
of this watchdog change and were left untouched.

## Operation

The source templates live in Tools/Cabinet. In Unity use
**MASSIVE > Cabinet > Build Watchdog Player** to produce a dated standalone build with
Start-Cabinet.cmd, Stop-Cabinet.cmd, Cabinet-Status.cmd and its configuration.
The build command preserves earlier builds. It uses the enabled build scenes and
requires Attract first. See [operator instructions](../../Tools/Cabinet/README.md).

CabinetHeartbeat.cs installs itself only in an opted-in Windows standalone, before
the first scene, and persists through scene changes. A nonblocking, loopback-only
UDP signal comes from Update once per real second. Each launch has a new token and
strictly increasing sequence. Editor Play Mode and direct exe launches are unaffected.

Default recovery: 120 seconds of startup grace, then 60 seconds without a fresh
heartbeat; 5 seconds between attempts; at most 3 automatic restarts within 10 minutes.
A fourth failure latches maintenance. Stop latches maintenance before closing the
owned child; after 10 seconds it can terminate only that child. Start explicitly
resumes. Duplicate starts and existing unsupervised MASSIVE players are refused
without terminating them. Settings retain 4K, Very High, DX11 and BitBlt; VSync/MSAA
settings and all scene/prefab assets are unchanged by this feature.

## Validation completed

- The main project's Editor compiled the new files with zero Console errors after
  correcting the build-report field types. Three unrelated existing Editor warnings
  were visible (TMP word-wrapping deprecations and a reference-comparison warning).
- 26 process integration checks passed under Windows PowerShell 5.1. They exercise
  the actual supervisor with separately compiled disposable child processes:
  healthy liveness; command-line quoting; duplicate starts; crash recovery; frozen
  process recovery; startup grace; absent, invalid and repeated heartbeat rejection;
  restart limits; persistent maintenance and resume; stop during startup and retry
  delay; missing executable; existing managed/unmanaged builds; log pruning; and a
  status file held open exclusively by another process.
- A separate Unity 6000.0.28f1 Windows x64 Mono fixture built successfully with
  **0 errors and 0 warnings**, containing a byte-identical CabinetHeartbeat.cs.
- Seven end-to-end checks with that player passed: reached a second scene at
  timeScale=0; heartbeats continued; exactly one heartbeat object survived the scene
  transition; a deliberately blocked Unity main thread was detected and terminated;
  a replacement instance became healthy and changed scenes; a forced process exit
  relaunched successfully; maintenance stopped the final instance.
- All test processes were stopped. No production cabinet startup or sign-in setting
  was changed.

Process tests: Logs/WatchdogTests-20260929-f/passed.txt and per-case supervisor logs.
Reproduce with Windows PowerShell 5.1:
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Cabinet/Tests/Test-Watchdog.ps1`

Unity integration evidence is outside the project at:
`C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/cabinet-watchdog-20260929`.
It includes the isolated fixture project, build.log, build-result.txt,
Test-UnityHeartbeat.ps1, unity-runtime-passed.txt and per-run player/supervisor logs.
The headless fixture logs contain two unsupported Sprite shader messages because
it runs with -nographics; it is not a rendering or performance test. Its lack of a
window also exercised forced-stop fallback, rather than graceful window close.

## Remaining deployment validation

The full MASSIVE player has not yet been rebuilt with the heartbeat, and its
existing cabinet launchers still point to the prior LipFlow build. While working,
the shared Editor entered Play Mode for separate development and later showed an
unsaved Action Lab scene. Approval to stop that session was requested; no reply had
arrived. This task did not stop it, save/discard its scene changes, or replace its
existing running/build setup.

When the shared Editor is available, build via the new menu, smoke-test that new
MASSIVE player through Start-Cabinet, verify graceful Stop and real gameflow loading,
then point the cabinet's usual launcher at the new build. An eventual Windows
logon task should invoke Run without -Resume to honor maintenance. Automatic
sign-in, logon task registration, OS freeze recovery and logical gameflow timeouts
are separate work.

This watchdog detects process exit and main-thread stalls. It does not diagnose a
state machine stuck while frames continue, missing audio/controllers/display,
Windows freezes, or termination of the watchdog itself. Restart returns to Attract
and discards the current match. Current player-log size is not capped; only prior
run count is limited. No gameplay-wide performance claim is made.
