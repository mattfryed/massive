# NOVA particle trail optimization 2026 10 11

NOVA's flare trails now use a minimum vertex distance of 0.06 instead of 0.03.
The conservative setting reduced total star trail geometry by 35.1% in a
controlled Unity benchmark. Particle density, lifetime, noise, motion, color,
and gameplay settings are unchanged. The trails approximate the same particle
paths with fewer segments, so this is not a pixel-identical rendering change.

## Production change

Exactly one serialized value changes in each of these prefabs:

- `Assets/Prefabs/Levels/NOVA_Feature.prefab`
- `Assets/Scripts/Instructions/InstructionsPanel-NOVA.prefab`
- `Assets/Scripts/Anomalies/NOVA/PF_NovaBounceSlot.prefab`

The change applies only to **Flare particle system > Trails > Minimum Vertex
Distance**. Corona systems, finale/explosion effects, and separate Level Select
icon copies are unchanged. The gameplay scene inherits the main star prefab
without a trail-spacing override. Particle collision and trigger modules are
disabled on these systems; star mechanics are implemented separately.

Each file was checked byte-for-byte against its pre-change backup after replacing
that single numeric value. No GUIDs, references, scripts, scene files, or project
settings changed during this pass. Earlier uncommitted optimizations are preserved.
Restore 0.03 in these three fields to undo this pass.

## Controlled geometry and simulation benchmark

Actual NOVA_Feature prefab, stage 4, deterministic particle seed 177, ten-second
warmup, 120 simulation steps at 1/60 second, three repetitions per setting.
The second repetition reversed setting order. Measurements below are medians
across repetitions. GetParticles completes simulation jobs before timing ends.

| Flare spacing | Total star trail vertices | Particle simulation and readback | Trail mesh snapshot |
|---|---:|---:|---:|
| 0.03 original | 389,542 | 4.78 ms | 21.18 ms |
| **0.06 retained** | **252,916** | **4.28 ms** | **14.00 ms** |
| 0.09 tested | 204,700 | 4.16 ms | 11.31 ms |
| 0.12 tested | 179,347 | 4.10 ms | 9.76 ms |

Total includes unchanged Corona trails as well as Flare trails. All twelve
conditions had the same mean particle count (28,139), final particle count,
and position/lifetime/seed checksum. The checksum is a consistency check,
not a bitwise comparison of every particle field.

The retained setting reduced geometry by 35.1%, particle simulation/readback
time by approximately 10.4%, and mesh snapshot time by approximately 33.9%.
`BakeTrailsMesh` includes mesh generation and copying into a CPU-readable Mesh.
Its timing is a geometry-cost proxy, not normal rendering time: the 7.18 ms
snapshot reduction must not be presented as a frame-time saving or FPS gain.

Deterministic 1920x1080, 4x MSAA images at 0.03, 0.06, and 0.12 were inspected.
The retained 0.06 setting looked very close to the original. This isolated
camera crops the top and bottom of the full-size star; it does not replace
visual inspection of actual gameplay and instructions at cabinet resolution.

## Standalone performance

Same development binary, two sequential full matches, four scripted players,
Editor closed, 3840x2160 at 120 Hz, Very High, 4x MSAA, VSync 1, borderless
DX11 with the BitBlt override. Hardware: i5-11400F and RTX 2060. Raw/deep
profiling was off. Instrumentation sampled frame timings and particle settings;
it verified 0.03 throughout the baseline and 0.06 throughout the optimized run,
with High noise quality retained in both instructions and gameplay.

| Measured interval | Original FPS | Optimized FPS | Original p95 frame | Optimized p95 frame |
|---|---:|---:|---:|---:|
| Instructions preview | 49.0 | 71.2 | 22.56 ms | 15.78 ms |
| Gameplay age 65–180 seconds, regulation only | 43.1 | 50.1 | 38.25 ms | 30.37 ms |
| Gameplay age 120–180 seconds, regulation only | 34.4 | 42.5 | 42.45 ms | 33.38 ms |
| Core Collapse bonus | 50.0 | 59.8 | 22.74 ms | 17.07 ms |

The optimized regulation run averaged 52.8 FPS from gameplay age 10 onward;
its first measured 50 seconds averaged 59.3 FPS. There is no valid corresponding
whole-regulation baseline, so those figures are current performance only.
NOVA still misses a sustained 60 FPS target under heavy late-game load.

The initial baseline launch displayed a Windows Firewall prompt. It was later
gone, and foreground gameplay was visually confirmed at approximately age 60
seconds and again in the busy final minute and bonus. The initial opening scenes
and gameplay before age 65 are excluded. A separate short baseline run repeated
the complete instructions interval unobstructed, then closed at gameplay entry.
The optimized run was visibly unobstructed at Attract, instructions, regulation,
and final core entry. Application.isFocused alone was not accepted as proof.

Instructions statistics exclude the first three seconds and snapshot disturbance
plus cooldown, leaving approximately 20 seconds per condition. Gameplay excludes
warmup, focus loss, profiling/snapshot disturbance, and cooldown. Instructions
median triangles fell from 783,922 to 507,942 while draw calls stayed at 52.
Both game captures use the same scripted control routine, but their random
spawns and simulation outcomes differ. The observed 23.7% late-game FPS gain is
indicative, not a deterministic causal measurement. The repeated fixed-seed
geometry benchmark establishes the trail reduction independently.

Both full captures completed Attract → How To Play → Level Select → Instructions
→ NOVA regulation → Core Collapse bonus → Results → Attract and exited with code 0
(296.61 and 292.48 seconds). The baseline-preview repeat exited with code 0 after
48.46 seconds. No runtime errors or exceptions were captured; each run had only
the known Level Select Sphere convex-hull warning. Preloading was ready after
approximately 0.37 seconds in the original full run and 0.25 seconds in the
optimized run; gameplay activation took approximately 0.42 and 0.35 seconds.
These load times are observational, not an intended benefit of trail spacing.

Actual cabinet-resolution instructions, earlier star growth, the fully expanded
star, and bonus transition were visually inspected. No obvious visual regression
was observed. Human approval of the subtle trail interpolation difference is
still subjective; the original setting remains easy to restore in the Inspector.

## Build validation

Unity 6000.0.28f1, Windows x64, Mono, Development build with all 13 enabled
scenes and eight LevelCatalog entries. Build succeeded in 63.53 seconds with
zero errors and 106 warnings, matching the preceding development build count.
Build GUID: `da15b308f5164bf486ceff3936ce6265`.

Build: `Builds/Cabinet-Nova-Trails-20261011/MASSIVE-Development.exe`.
Normal play: `Builds/Cabinet-Nova-Trails-20261011/Launch-Cabinet-Development.cmd`.
The temporary audit only activates with `-massive-audit`; normal launch keeps
player controls. Temporary scripts are archived outside Assets after building.

Final Editor check: S-9_COSMOS restored in Edit Mode, no unsaved scene marker,
Console **0 errors and 28 warnings**, matching the earlier Editor baseline.
The initial cached compilation briefly referenced the archived audit sources;
Unity's asset refresh regenerated the compilation inputs and cleared those
temporary missing-source errors. Production code compiled after instrumentation
removal. `git diff --check` passes.

## Remaining priorities

LATTICE's field updates and DYNAMO's bloom rendering remain next on the earlier
cross-scene list. LATTICE's repeated field samples and all-segment dot-depth
search are candidates for exact-result caching or spatial rejection; these
require separate measurement and validation because connection state also
controls movement. DYNAMO's earlier GPU cost requires isolating bloom passes
before changing render resolution or appearance. Neither was edited in this pass.

ORBITAL's enemy filtering is substantially improved, but sustained 60 FPS
remains unverified. Its remaining non-enemy collision restoration hitches and
approximately 4.4 ms cloud visual updates remain documented in
`Resonance-Enemy-Filtering-20261011.md`.

Evidence directory:
`C:\Users\mattf\.codex\visualizations\2026\09\26\01a0df00-fc1e-7373-be83-d53f2fe84bda\nova-particles-20261011`.
Contains before-asset backups, exact-change checks, particle inventory,
controlled benchmark code/results/images, standalone instrumentation, captures,
build output, and analysis scripts.
