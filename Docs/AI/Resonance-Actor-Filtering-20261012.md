# Resonance actor collision filtering — 2026-10-12

## Result

The remaining player/Core/pickup collision-restoration hitch is fixed for the
uniformly traversable resonance fields used by the current game. In matching
standalone ORBITAL runs, peak measured restoration fell from
121.82 ms to 0.29 ms.
No native collision pairs were restored anywhere in the optimized ORBITAL capture
(61,290 in the complete baseline capture; 59,701 in its clean regulation sample).

This targets intermittent stalls. The observed overall frame rate improved but
does not meet a sustained 60 FPS: other rendering/gameplay costs remain.

## Change and behavior

Reserve physics layer 14, `ResonanceActorPassThrough`, for solid contact children
of fields where all tracked actors already bypass hard collision: Player Sludge
or Pass Through, non-default Core response, and Enemy Sludge enabled. Default
obstacle-layer permissions must match both reserved contact layers. Renderers,
trigger-only contacts, geometry, particle counts and motion math are unchanged.

Each participating non-enemy solid collider uses one exclusion instead of a
native pair per wall segment. Shared reference counts preserve overlapping fields;
last release removes only owned bits. A disabled driver's surviving walls return
to their original contact layer before filters release. Generated drivers shut
down before their collider lists are cleared. Pickup hierarchy changes, disabled
colliders, pooling, destroyed actors and repeated formation gates release state.

Mixed responses, custom obstacle layers, incompatible matrices and contact
objects sharing solid/trigger colliders retain the original native-pair fallback.
Enemy bodies and avoidance queries now exclude both reserved contact layers.
Global collision-matrix values are not changed by runtime code.

The newly merged `IsTurningCore` and `ReleaseCoreTurn` helpers, all movement,
sludge, contact visualization and magnetic trajectory calculations are textually
unchanged from the pre-task merged source. Tests exercise actual Core possession,
curved shots, stop-and-reflect zero-speed flight, shields and capture transactions
under both the original and optimized response configurations.

## Controlled benchmark

Real 345 Hz encounter geometry: 227 hard wall colliders. Three repetitions per
case, using the exact pre-task merged driver as baseline. Values below are medians
of synchronous driver shutdown, including layer restoration in the optimized path.
Actors are synthetic solid colliders in an isolated physics scene; this measures
pair-count scaling rather than a specific live match population.

| Solid actor colliders | Native pairs before / after | Cleanup before | Cleanup after |
|---|---:|---:|---:|
| 9 | 2,043 / 0 | 22.50 ms | 0.144 ms |
| 15 | 3,405 / 0 | 46.27 ms | 0.103 ms |
| 33 | 7,491 / 0 | 238.35 ms | 0.074 ms |

## Standalone comparison

Same development player (build GUID `d372561d53f74eb9bbaed150cd4715ba`),
3840×2160 at 120 Hz, Very High, 4× MSAA, VSync 1, Direct3D 11, borderless,
BitBlt. Unity was closed. Both players were visibly unobstructed, remained focused
through every captured ORBITAL frame, completed regulation naturally, reached
Results and returned to Attract. Baseline duration 269.76 s; optimized 265.42 s;
both exited normally with code 0. No firewall prompt occurred.

The diagnostic-only baseline switch disables the new actor-layer policy in the
same binary; the previous enemy optimization stays enabled. Both runs use the
same four-player scripted control harness. Live physics/spawn outcomes differ,
so frame-rate changes are observations from one run per variant, not a guaranteed
repeatable FPS uplift. The deterministic isolated benchmark establishes causality.

“Clean regulation” excludes startup, non-regulation phases, capture/snapshot
perturbations, and one second after any such flagged frame. The method timing
recorder measures elapsed CPU time and call counts, not GPU time.

| Metric, clean regulation | Baseline | Optimized |
|---|---:|---:|
| Average FPS | 53.2 | 55.4 |
| Late-game FPS, scene age 120–180 s | 48.7 | 50.5 |
| 95th-percentile frame, ms | 27.00 | 24.23 |
| Worst collision restoration, ms | 121.820 | 0.287 |
| Worst full driver shutdown, ms | 121.882 | 0.035 |
| Worst filter configuration, ms | 72.289 | 0.473 |
| Frames over 50 ms | 62 | 10 |
| Frames over 100 ms | 3 | 0 |

Across all captured ORBITAL phases, optimized shutdown peaked at 0.068 ms.
The optimized capture released 264 shared collider leases and zero native pairs;
259 leases were released within the clean regulation sample.

## Remaining costs and limitations

- The optimized clean sample still contains a 91.55 ms frame near regulation end.
  Restoration was 0.0028 ms in that frame, so this collision cleanup no longer
  explains that hitch. This capture does not fully attribute its remaining cost.
- Cloud visual updates still average approximately 4.03 ms per regulation frame.
  Late-game average remains about 50.5 FPS in this particular run. Enemy/rendering
  load and other lifecycle work remain candidates for a separate profile.
- Both runs log the same existing warnings: one convex Sphere hull exceeds Unity's
  polygon limit in Level Select; four linear and four angular velocity writes target
  a kinematic Core during retirement. No runtime errors or exceptions were recorded.
- Custom/mixed collision policies intentionally retain pair cleanup and therefore
  can still incur its cost. Current ORBITAL exercised no such native restorations.
- The build succeeded with zero errors and 107 warnings (existing compiler/asset
  warning categories; details retained in the evidence). This is not a warning-free
  project certification or a new all-scene performance audit.

## Validation and preservation

- 49,152 exact, limited and swept query comparisons passed.
- Existing 183 resonance checks passed (20 cleanup, 27 enemy filtering,
  105 prototype, 31 formation lifecycle).
- 34 new native physics/ownership checks passed, including actual pass/block
  behavior, ordinary walls, compound colliders, untouched sensors, overlapping
  fields, authored exclusions, mode changes, rebuild and matrix/custom fallback.
- 241 possession/shot checks passed, now covering both response policies.
- 17 pickup Play Mode checks passed under each policy: 34 total.
- 25 HIGGS vacancy-forgiveness checks passed. The previously documented unrelated
  HIGGS 120-vs-180-second assertion was not changed or rerun.
- Final production-source startup completed successfully with no C# errors,
  shader errors or exceptions after removal of temporary instrumentation.
  Unity first refreshed stale references to removed audit sources; the subsequent
  clean-startup log contains no such errors.
- Unrelated pending source/asset changes were preserved byte-for-byte. Temporary
  test/build serialization churn in previously clean physics settings was backed
  up and reverted; the HIGGS prefab was restored to its exact pre-task bytes. No commits or pushes were made.

Runtime changes: `ResonanceCollisionPolicy.cs`, `ResonanceInteractionDriver.cs`,
`ResonancePatternController.cs`, and `ProjectSettings/TagManager.asset`.
Regression coverage: `ResonanceActorFilteringValidation.cs`,
`AmplifierPickupInteractionPlayValidation.cs`, `AmplifierPossessionValidation.cs`.
Documentation: resonance README and this report.

## Artifacts

The refreshed development player remains at the established launcher path:
`Builds/Cabinet-Dynamo-Curves-20261011/Launch-Cabinet-Development.cmd`.
The dated folder name is retained to preserve the firewall-approved executable
path. Its audit harness is dormant without `-massive-audit`; normal launch uses
the optimized policy. Temporary profiling sources are removed from Assets.
The previous complete build is retained at
`Builds/Archive/Before-Resonance-Actors-20261012`.

Evidence directory:
`C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/resonance-actor-filter-20261012/`

Key files: `benchmark.json`, `comparison.json`, `baseline/targeted.csv`,
`optimized/targeted.csv`, both `frames.csv` / `issues.json` / `process.json`,
`validation-final/edit-results.txt`, `validation-final/play-results.txt`,
`pickup-results.txt`, `build-result.json`, `clean-startup.log`,
`gameplay-preservation.json`, `preservation.json`, and the retained before-files.
