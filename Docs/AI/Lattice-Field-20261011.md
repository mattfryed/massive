# LATTICE field optimization 2026 10 11

LATTICE now restricts its dot-to-contour distance search to nearby grid nodes.
The existing jitter-depth limit means farther segments cannot change a dot's
result. The optimized loop visits segments in the original order and keeps the
same distance arithmetic, noise predicate, contour resolution, and 20 Hz cache.
No scene tuning, movement rules, shader, particle count, or visual update rate
changes in this pass.

## Production change

`Assets/Scripts/Anomalies/LATTICE/LatticeDisruptionField.Boundary.cs`,
`RefreshDotDepths`: initialize squared distances once, traverse each contour
segment's grid bounds expanded by the maximum relevant distance, and convert
the finished distances to grid units. A one-cell conservative margin guards
floating-point conversion of the bounds. The existing array is reused.

The main field and Resonance source files are unchanged. LATTICE's scene,
instructions prefab, player settings and build-scene settings were checked
against their pre-change SHA-256 hashes and remain identical. Other pending
optimizations in the checkout are preserved.

## Controlled CPU benchmark

Actual S-1_LATTICE field, 28 by 12 arena, spacing 1, 377 nodes, 712 edges;
noise frequency 0.19, threshold 0.51, seed 173, jitter depth 3. Editor-only
boundary guide disabled in the in-memory fixture to match the player's contour
resolution. Three repetitions per version; reported values are medians of the
per-repetition means. Full-field samples advance by 1/60 second after warmup.
Separate contour and dot-distance samples force a refresh every 0.061 seconds.

| Operation | Before | After | Reduction |
|---|---:|---:|---:|
| Dot distances per contour refresh | 6.195 ms | 1.293 ms | 79.1% |
| Full field update, averaged across frames | 4.823 ms | 3.638 ms | 24.6% |
| Full field update, median repetition p95 | 12.037 ms | 7.118 ms | 40.9% |
| Contour rebuild per refresh | 3.197 ms | 3.250 ms | Essentially unchanged |

These are graphics-enabled Unity batch benchmark timings, not standalone
frame rates. The contour and dot-distance costs occur on refreshes, rather
than every frame. The full update includes those refreshes and cheaper cached
frames. No allocation claim is inferred from the benchmark's recorder.

## Correctness and gameplay validation

New Editor command: **MASSIVE > LATTICE > Validate field optimization**.
It compares the new result to an exhaustive implementation of the original
calculation using exact floating-point bits. **261 cases and 71,831 node
comparisons passed**, including different grid sizes and spacing, noise
frequencies, seeds, jitter depths, thresholds, connected/disconnected modes,
time resets, stationary contour caching, RGB jitter toggling, disable/enable,
and grid resizing. Fixtures preserve the default gameplay grid singleton.

The existing **Validate prototype** Play Mode suite passed **216 checks**
(including repeated assertions), covering canonical-player eight-direction
movement, quantized attacks, collision blocking, pause, lifecycle, scene-system
bindings, RGB rendering, ghost trails, and live Resonance rendering. Fresh
gameplay imagery was visually reviewed. This is automated validation, not a
new human joystick feel playtest.

## Standalone build and comparison

Unity 6000.0.28f1, Windows x64, Mono, Development, all 13 enabled scenes and
eight catalog entries. Build succeeded with **zero errors and 106 warnings**,
the same warning count as the preceding NOVA development build.
Build GUID: `54d2d44f0b5344678e7772a830ae98b6`.

Build: `Builds/Cabinet-Lattice-Field-20261011/MASSIVE-Development.exe`.
Normal play: `Builds/Cabinet-Lattice-Field-20261011/Launch-Cabinet-Development.cmd`.

The comparison build contains an opt-in audit and the original dot-distance
method behind `-audit-lattice-baseline`. Both versions run in the same binary
with the same other optimizations. Normal launch uses the new calculation and
ordinary player controls. Temporary instrumentation was archived outside
Assets after building; production source retains only the optimization and
Editor validation tool.

Cabinet settings: 3840 x 2160 at 120 Hz, Very High, 4x MSAA, VSync 1,
DX11 borderless with the BitBlt presentation override. Unity Editor closed.
Four scripted players run the normal game flow, attacks, and shields.
Matches are not deterministic replays; overall FPS differences can include
spawn/combat variation. The isolated benchmark and exact-result validation
provide the stronger evidence for the algorithmic improvement.

## Standalone measurements

Fresh unobscured `baseline-clean` and `optimized` captures, one complete match
per variant. Excluded the first ten seconds of gameplay, loss of focus,
instrumented snapshots/profiling, and a one-second recovery period after
disturbances. FPS is total valid frames divided by their elapsed frame time.
Regulation covers approximately 173.6 valid seconds per capture. Early means
scene age 10-60 seconds; late means 120-180 seconds. Instructions cover about
20 valid seconds each.

| Measurement | Original search | Optimized search |
|---|---:|---:|
| Instructions FPS | 120.0 | 120.0 |
| Regulation FPS | 56.2 | 60.7 |
| Early gameplay FPS | 57.9 | 62.9 |
| Final-minute FPS | 52.5 | 58.6 |
| Regulation frame-time p95 | 25.22 ms | 20.27 ms |
| Final-minute frame-time p95 | 28.16 ms | 21.65 ms |
| Mean field CPU per frame | 5.41 ms | 3.70 ms |
| Field CPU p95 | 13.44 ms | 7.28 ms |
| Dot search per contour refresh | 6.43 ms | 1.22 ms |
| Contour rebuild per refresh | 3.40 ms | 3.33 ms |

The measured mean field cost fell **31.6%** and its p95 fell **45.8%**.
The standalone dot-distance search was approximately **81% cheaper** on refresh
frames. Unlike the fixed-step benchmark, each standalone run refreshes the
contour over a different number of rendered frames, which contributes to the
difference between the mean field savings in these two measurement methods.

Overall FPS improved about **8.0%**, with about **11.7%** improvement in the
last minute. The optimized run averages slightly above the 60 FPS target, but
late gameplay is still below it and frame-time spikes remain. **This is not a
locked-60-FPS certification.** One match per variant cannot isolate every
random spawn, combat, or thermal difference. Late-match median triangle counts
were 2.28M before and 2.34M after; draw counts were 512 and 575. Geometry was
not reduced by the code change.

The optimized late-match medians were approximately 14.82 ms on the main thread
and 13.00 ms on the GPU. Earlier gameplay had a 15.62 ms GPU median. There is
limited CPU and GPU headroom; further CPU savings alone may not remove every
slow frame. FrameTiming values are observational, not additive measurements of
independent bottlenecks.

Both clean captures completed instructions, four-player regulation, Results,
and return to Attract, exiting normally. Neither logged runtime errors or
exceptions. Each logged one existing Level Select convex-mesh warning. The
optimized standalone gameplay capture was visually reviewed alongside the
Editor validation images. The initial `baseline` capture was obscured by a
Windows Firewall prompt and is excluded entirely from FPS conclusions. The
prompt was no longer present on subsequent launches; no firewall settings were
changed by this task.

After temporary instrumentation was removed, Unity successfully rebuilt the
production game and Editor assemblies and exited batch mode with code 0.
Its initial cached compile referenced the archived temporary files (CS2001);
the same asset refresh regenerated the compiler inputs and rebuilt successfully.
No production source repair was needed. `git diff --check` passed.

Final interactive Editor check: **S-1_LATTICE in Edit Mode**, no unsaved-scene
marker, Console **0 errors / 1 existing convex-mesh warning**. The scene is left
open for review.

## Remaining work

Contour rebuilding and repeated noise samples for connection boundaries remain
the main field CPU candidates. They also drive movement, so further caching
needs corresponding correctness tests. This pass does not lower their sample
counts or update rates. DYNAMO's bloom is the next separate scene-level item
from the earlier performance review.

Evidence directory:
`C:\Users\mattf\.codex\visualizations\2026\09\26\01a0df00-fc1e-7373-be83-d53f2fe84bda\lattice-field-20261011`.
Contains before-source backups, benchmarks, exact validation, fresh prototype
captures and report, build logs, temporary instrumentation, and capture scripts.
