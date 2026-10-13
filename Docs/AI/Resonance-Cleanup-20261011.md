# Resonance collision cleanup â€” 2026-10-11

## Scope and implementation

This pass addresses the resonance cleanup spikes identified in the ORBITAL standalone profile. It preserves the earlier contact-query, COSMOS baking and ORBITAL cloud changes.

`ResonanceInteractionDriver` now stores ignored collider pairs on their owning actor. Restoring an actor traverses only that actor's pairs and clears its list once. Shutdown releases actors in reverse acquisition order, which measured substantially faster in Unity 6000.0.28f1. Cleanup remains synchronous; surviving colliders recover their prior ignore settings before it returns.

No scene, prefab, collision geometry, layer matrix, enemy population, movement tuning, visual effect or timing setting was changed. Pre-existing ignores and other movement-influence sources are preserved. Two markers, `Resonance.RestoreCollisionPairs` and `Resonance.Shutdown`, remain for future profiling.

## Root cause and controlled measurement

The existing implementation scanned one global pair list for each actor and repeatedly removed entries from its middle. However, instrumentation disproved the assumption that managed list work explained most of the stall: in the 48-actor fixture, approximately 878 ms of a 959 ms cleanup was spent in Unity collision restoration and 73 ms in list removal.

An isolated preview physics scene instantiates the actual **345 Hz Particle Encounter** prefab, including all **227 solid wall colliders**, plus 8, 24 or 48 sphere-collider rigidbodies. Every native pair is checked after cleanup. Particle budget alone is reduced for fixture setup. The before implementation is a frozen copy of the source at the start of this task with timing instrumentation.

Three alternating before/after repetitions per size:

| Actors | Ignored pairs | Before cleanup mean | After cleanup mean |
| --- | --- | --- | --- |
| 8 | 1,816 | 28.13 ms | 14.48 ms |
| 24 | 5,448 | 253.14 ms | 125.53 ms |
| 48 | 10,896 | 1,019.02 ms | 515.20 ms |

At 48 actors, the old managed loop examined 266,952 entries. The new loop visits its 10,896 pairs once. The combined change cuts measured cleanup roughly in half. This is an Editor physics microbenchmark, not a standalone frame-rate result.

Disabling wall colliders first, deactivating their GameObjects, destroying them immediately, wall-major traversal, and swapping collider argument order did not eliminate the native cost. Only the measured reverse order and actor-owned storage were adopted. Destroying the colliders moved native work into destruction rather than removing it.

**Remaining limitation:** restoring thousands of native collision pairs is still expensive. This patch reduces the hitch; it does not make dense-pattern shutdown fit within a 16.7 ms frame. A larger improvement needs fewer native ignore pairs or a different collision-filtering design, with separate compatibility checks for hard walls, players, Cores, enemies and pickups.

## Correctness validation

Unity batch validation completed successfully:

- 20 new native collider-pair checks: compound actors, sensor exclusion, prior ignore preservation, independent actors, movement-source ownership, response changes, disabled/re-enabled colliders, destroyed walls/actors, overlapping patterns and repeated shutdown/restart.
- 105 existing resonance prototype checks.
- 31 existing manifestation lifecycle checks.
- 16,384 exact contact, 16,384 limited contact and 16,384 swept visual differential comparisons.

The new persistent menu is **MASSIVE > Resonance > Validate Collision Cleanup**. These are isolated Editor checks; lifecycle methods in the new fixture are explicitly invoked. The standalone match below exercises the actual game callbacks.

## Standalone validation

Windows x64 Mono Development build succeeded with **zero errors** (GUID
`2f70fc7c3f8e4c50ac430703828352a4`). It includes the existing 13-scene gameflow.
There are 106 reported warnings: the previous 74 build warnings plus 32 compiler
warnings in unchanged legacy, add-on and other gameplay scripts, surfaced by
recompilation. No warning points to the changed resonance code.

Build: `Builds/Cabinet-Resonance-Cleanup-20261011/MASSIVE-Development.exe`.
Normal launcher: `Launch-Cabinet-Development.cmd` beside the executable.
Cabinet settings remain 3840 × 2160, Very High, 4× MSAA, VSync 1, DX11 BitBlt.

The automated ORBITAL match completed naturally through Results and returned to
Attract, exiting with code 0 after 269.98 seconds. No runtime errors were captured;
the only runtime warning was the existing level-select Sphere convex-hull warning.
Windows displayed a firewall permission dialog over the run. The user has been
asked to dismiss the dialog. This capture is
not accepted as a clean FPS comparison, even though Unity reports focused=true.
A clean standalone timing comparison remains pending.

Temporary audit assets and timing hooks have been removed from the source
project. The compiled diagnostic harness is opt-in via `-massive-audit`; the
normal launcher does not enable it.

## Evidence

Local evidence folder:
`C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/resonance-cleanup-20261011`

Key files: `baseline-benchmark.json`, `order-benchmark.json`, `native-benchmark.json`, `swap-benchmark.json`, `comparison-benchmark.json`, `validation.txt`, `validation.log`. Temporary benchmark sources are archived there, outside Assets.

The completed capture is retained under `optimized/` for lifecycle and diagnostic
evidence only. `diagnostic-timing.json` records cleanup spikes with the same
qualification. Do not use this capture to claim a standalone FPS improvement.


The diagnostic capture still contains a frame with **953.5 ms in RestorePairs**
(52 calls, 45 active enemies). Although this is not a clean FPS comparison, it
confirms that substantial native cleanup stalls remain after the managed
improvement. Reducing the number of native ignored pairs is the next substantive
step; changing restoration order alone cannot solve the entire hitch.

After removing the temporary harness, the Editor recompiled and reopened
**S-9_COSMOS in Edit Mode**, with **0 Console errors and 28 warnings**, matching
the initial warning count. Targeted `git diff --check` passed. The source change,
new regression test and report are uncommitted.
