# DYNAMO bloom optimization 2026 10 11

DYNAMO now combines the finished magnetic-field glow and solar-wind glow in
one full-resolution additive pass. Each channel retains its own source,
threshold, soft knee, intensity, diffusion and scatter. Full-resolution depth
masking, HDR precision and MSAA are unchanged. The change reduces presentation
work without adjusting enemy counts, storm timing, damage or movement.

## Implementation and tradeoff

`DynamoSelectiveBloom.cs` retains each channel's final half-resolution image
until the combined pass, then releases both. Intermediate blur targets remain
shared and released per channel. A single active channel uses its original
composite; both off remove the camera commands. `DynamoSelectiveBloom.shader`
adds the two-channel composite pass; prefilter and blur shaders are unchanged.

Holding the extra 1920 x 1080 ARGBHalf image adds about 15.8 MiB of transient
render-target storage at 4K. It avoids a second 3840 x 2160 camera blend.
The camera receives one addition instead of two, so HDR rounding can differ
slightly. The scene, instructions prefab, stage profile, player settings and
build-scene settings match their pre-change SHA-256 hashes. Other pending
optimizations in the checkout are preserved.

## Controlled rendering evidence

Graphics-enabled Unity batch test of the actual DYNAMO visual-preview sources,
held at 0, 3.6 and 12 seconds of a 24-second storm with peak 0.35 and a leftward
source direction. Output is 3840 x 2160 ARGBHalf with 4x MSAA. Each timed sample
includes Camera.Render plus a synchronous one-pixel readback. These are relative
render wall times, **not standalone FPS or GPU-only measurements**.

Four paired repetitions per state, alternating variant order; eight warmup
renders then 48 measured renders per repetition. Mean of repetition means:

| Held state | Separate composites | Combined composite |
|---|---:|---:|
| Calm (one channel) | 10.932 ms | 10.795 ms |
| Storm onset (two channels) | 12.561 ms | 11.651 ms |
| Mid-storm (two channels) | 11.625 ms | 10.913 ms |

The initial channel-isolation test placed the original complete bloom cost at
roughly 3-5 ms in this fixture. The retained change saves about 0.7-0.9 ms when
both channels are active. The one-channel calm result is essentially unchanged.

## Visual and lifecycle validation

- Compared HDR output for all four channel combinations at all three held
  states: 12 comparisons of 8,294,400 pixels each. No clamped linear-RGB difference
  exceeded one 8-bit color step (1/255). With both channels visible, the maximum
  HDR difference was 0.001953 at onset and 0.003906 mid-storm; mean maximum-channel
  differences were 0.0000183 and 0.0000228. Tiny differences also occurred in
  unchanged single/no-channel cases, so this is not a bit-identical claim.
- Inspected full-scene before/after storm images and standalone gameplay.
- Existing preview validation passed 24 direction/time samples, calm/peak/
  recovery, resizing, deterministic held state, buffer reuse, isolated source
  settings/materials/randomness, command cleanup and no scene dirtying.
- Extended the existing visual-style validation with combined-pass GPU pixel
  checks for independent intensities, additive HDR color and preserved camera
  alpha. The Play Mode suite also passed four toggle combinations, camera
  detach/re-enable, private opaque/additive materials, sharp-core values
  1/0.25/0 and zero managed bytes over 32 warmed bloom recordings.

## Standalone comparison

Unity 6000.0.28f1, Win64 Mono Development; all 13 enabled scenes and eight
catalog levels. Build succeeded with **0 errors and 106 warnings**,
matching the previous build's warning count. GUID: `2b212cd9bd7c4be8b17046837051c9a2`.

Build: `Builds/Cabinet-Dynamo-Bloom-20261011/MASSIVE-Development.exe`.
Normal play: `Builds/Cabinet-Dynamo-Bloom-20261011/Launch-Cabinet-Development.cmd`.

Both captures use the same binary. An opt-in `-audit-dynamo-baseline` switch
restores separate composites; normal launch uses the combined pass and normal
controls. The temporary harness and comparison switch were archived outside
Assets after building. Production retains the optimization and Editor tests.

Cabinet: i5-11400F, RTX 2060, 3840 x 2160 at 120 Hz, Very High, 4x MSAA,
VSync 1, DX11 borderless with BitBlt override. Unity Editor closed. Four scripted
players use the normal instructions, gameplay, results and return flow.
One complete match per variant; combat and storm scheduling are not deterministic
replays, so whole-match differences include ordinary run variation. Controlled
held-state measurements provide stronger attribution to this specific change.

A hidden-window launch was discarded without performance claims. Fresh visible
`baseline-visible` and `optimized` captures were verified before gameplay.
Exclude the first ten seconds of gameplay, focus loss, capture/profiling
intervals and one second of recovery. FPS is frames divided by valid elapsed
frame time. Early is scene age 10-60 seconds; late is 120-180 seconds.

| Measurement | Separate composites | Combined composite |
|---|---:|---:|
| Instructions FPS | 119.9 | 120.0 |
| Regulation FPS | 49.8 | 50.0 |
| Early gameplay FPS | 48.2 | 49.0 |
| Final-minute FPS | 48.9 | 48.7 |
| 95th percentile frame time, ms | 27.03 | 27.01 |
| Median GPU time, ms | 18.33 | 18.21 |
| Final-minute median GPU time, ms | 19.65 | 19.73 |
| Median main-thread time, ms | 8.25 | 8.24 |

Both complete matches exited normally, with no runtime errors. Each recorded
the existing level-select convex Sphere warning once. Gameplay was preloaded
during instructions; the baseline activation took about 174 ms.

## Remaining performance work

The full-match FPS change is within ordinary run variation; no material FPS
benefit is established by these two matches. The held-state savings are real
for that fixture, but do not establish a steady 60 FPS. GPU time is
still the main constraint. Full-resolution HDR/MSAA source rendering and each
channel's blur chain remain. A next controlled experiment should separate
source-draw cost from blur cost before reducing precision or resolution.
Simply shrinking the source target would break its match with camera depth/MSAA.
Any lower-resolution glow option needs explicit occlusion and visual validation.

Rendering load also grows during combat: in the baseline, median triangles
increased from about 1.02 million early to
2.71 million late, and draws from
292 to 606. This suggests
additional geometry/effect cost beyond the fixed bloom passes; it does not by
itself identify which enemy or effect should be reduced. No particle-density or
mesh-quality reduction was made in this pass.

Local evidence: `C:\Users\mattf\.codex\visualizations\2026\09\26\01a0df00-fc1e-7373-be83-d53f2fe84bda\dynamo-bloom-20261011`. Includes build logs, raw frame CSVs, channel timings,
paired composite timings, HDR comparison statistics, images, test results,
asset hashes and archived opt-in audit sources.

Final clean-source Editor check: DYNAMO reopened in Edit Mode with no scene
modification marker, zero Console errors and four existing warnings. Unity's
cached compiler initially referenced the archived test runner, then rebuilt
successfully in the same refresh. The temporary validation runner is also
archived outside Assets. Protected asset hashes still match after reopening.
