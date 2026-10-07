# Scientific Dynamo prototype

This prototype displays **SWMF/BATS-R-US 2023 magnetic and plasma fields driven by observed solar-wind inputs**. Unity samples prepared model output; it does not run the MHD solver. The first included encounter is a pressure-pulse excerpt on **2026-01-03, 02:50â€“03:30 UTC**. It is not a complete geomagnetic storm or a representative statistical catalogue.

## Organic field stability and continuity (2026-10-07)

### Standard level composition

`S-6_DYNAMO` follows LATTICE's six scene roots: `00_Systems`,
`01_Presentation`, `02_Arena`, `GameplayObjects`, `04_UI`, and `90_EditorOnly`.
The existing field, core, storm controller, flow blanket and selective bloom
live under `GameplayObjects/LevelContent/DYNAMO`. Inactive legacy/scientific
versions remain in its `Legacy and scientific previews` group. The original
actors, arena, camera poses, materials and storm tuning are retained.

The scene now owns an explicit match context and score service, a phase-gated
power-up spawner group, the shared enemy timeline, the managed amplifier and
345 Hz resonance encounter, goal treatments, timer and team capture feedback.
These use Dynamo's existing grid, goals and player roster. The enemy timeline
and encounter assets are the same baseline used by LATTICE; balance can be
authored separately later. All four player storm adapters have explicit local
references, including inactive roster slots. Input starts through
`LevelSceneContext`, with one audio listener and one scene score authority.
Storm exposure ignores inactive roster slots so unselected players cannot lose
mass or start respawn coroutines during a two-player match.
The Dynamo definition remains `STAGE_005` and resolves `S-6_DYNAMO`; its dedicated
stage profile does not schedule unrelated NOVA bonuses.

`MASSIVE > Dynamo > Validate saved scene layout` checks structure and serialized
references. `DynamoSceneRuntimeValidation.Inspect()` reports live startup and
encounter state without modifying it. The one-time composition helper checks
original world poses, activation, tuning and references before saving.

### Menu and instructions presentation

`LS_DYNAMO icon.prefab` uses a bounded 3D dipole mesh with 112 surface-to-
surface arcs. Uneven layers carry smooth traveling bends, local bundle gathering
and release, and traveling energy accents. These suggest reconnection visually;
they do not simulate magnetic reconnection. Both footpoints remain fixed, and
inward breathing and bounded azimuth motion keep every pose self-contained.
Animation runs in the icon shader with no per-frame mesh rebuilds. Its material
exposes Strand motion, Motion speed and Traveling energy controls. Use
`MASSIVE > Dynamo > Refresh icon field only` to rebuild its baked strand data
without recopying the instructions demonstration. It retains the core
artwork and magnetic palette; the former particle-ring rig and icon physics are
removed. The same nested prefab appears at a smaller, framed scale in
`InstructionsPanel-DYNAMO.prefab`.

The right-hand panel uses `DynamoInstructionsDemo` to render the current GPU
field, tapered storm blanket and selective bloom into a private HDR texture at
30 Hz. Its isolated clock cycles calm, arrival, compression and recovery, then
reverses direction. It has no gameplay controller, players, collisions or damage.
Both renderers expose a nonserialized presentation-owner switch to avoid their
normal Update loops also advancing this demonstration. Gameplay defaults to its
existing clock. The copy explains the shifting shelter, external push and mass
drain. Live menu-to-instructions, both wind directions, text fit and
disable/re-enable resource cleanup were checked without new Console errors.

`MASSIVE > Dynamo > Refresh icon and instructions` rebuilds these presentation
assets from the current gameplay visual settings; it does not change the level.

The active GPU field now resolves the small pole region with a distance-limited
step. Previously the .08 trace step could jump across the .03 planet radius,
escape along the opposite pole and amplify tiny seed movement into large jumps.
Boundary draping now uses the envelope's changing along-wind scales when finding
its normal, and retains the strand's direction of travel near radial/tangent
degeneracies. Visible segments share endpoints even when the numerical trace
restarts slightly inside the boundary. A single shared append path keeps that
continuity consistent across divergent GPU branches.

The dipole, gameplay envelope, seed count, layered depth, warble/spin settings and
storm response parameters are unchanged. Individual faulty strands can change
their route or terminate correctly at a pole; no temporal blur or trail is used.

`MASSIVE > Dynamo > Validate Field Stability (Edit Mode)` checks connected GPU
geometry across 12 directions/pressures and a controlled small-planet regression.
The live run passed 1,056,565 segments and 112 adaptive pole steps. In fixed
diagonal-storm comparisons, 1,574 gaps became zero and sharp turns over 45 degrees
fell from 8,207 to 6. These are diagnostic samples, not bounds on every setting.
The existing Play Mode field checks also passed 12 depth/pressure/direction cases,
buffer lifecycle and zero managed allocations over 32 field updates.

## Active storm visuals - GPU flow blanket (2026-10-07)

`S-6_DYNAMO` now enables **MAGNETOSPHERE ANOMALY / Storm Flow Blanket**.
The organic magnetic field remains the active field renderer. The old
`StormWindFlowVisualizer` component and its ParticleSystemRenderer are disabled,
with their configuration retained for comparison. No scientific dataset or storm
episode has been connected to this replacement yet.

### Ownership and timing

- `DynamoStormController` owns one scaled clock and publishes `DynamoStormState`:
  strength, direction, travel distance, front, opacity and camera footprint.
  `DynamoFlowBlanketRenderer` consumes that state in LateUpdate. Its streaks have
  no simulated particle lifetimes, physics collisions or CPU position updates.
- The controller advances exactly once per frame. The old Update advanced its
  timer twice. The saved reversed duration pair (26,22) is now interpreted as
  22..26 **actual seconds**, so storms last longer than the old effective 13 s.
- The initial front begins after .08 s and crosses the viewport in 1.2 s.
  Droplet speed (8..16 world units/s) is independent of that crossing time.
  The last .65 s fades the blanket away; stopping clears presentation state.
- The front crossing the calm windward envelope starts the existing four-second
  pressure response ramp. Hazard queries use that same front and the shared
  envelope, so an unreached exterior location is not damaged. The old estimated
  particle delay and optional traveling damage band are used only in legacy mode.
- `SetScreenSourceDirection(Vector2)` uses **where the storm comes from**:
  left=(-1,0), right=(1,0), top=(0,1), bottom=(0,-1). Flow and the magnetic tail
  point downstream consistently. Direction changes retain at most one outgoing
  layout for a .3 s fade, clipped against the current envelope.

### Coverage and rendering

The camera footprint is projected onto the XZ playing plane. Stratified paths
span its crosswind extent and solve a body-fitted streamfunction around the
magnetosphere. Each path records cumulative *visible* arc length. Streaks are
distributed along that visible length, avoiding offscreen travel and diagonal
corner clamping. Disjoint visible spans never get a streak bridging their gap.
Small variations in phase, spacing, length, brightness and path shape keep the
shower from reading as fixed lanes. The field interior remains sheltered.

`MagnetosphereFieldLinesGPU2D.GetGameplayEnvelopeQ`,
`GetGameplayEnvelopeScales` and `GetGameplayEnvelopeRadius` expose the existing
smooth gameplay envelope. A 256-sample cross-section table from that authority
drives GPU paths and pixel clipping; storm visuals do not infer a boundary from
the noisy decorative field lines. This is an authored visual flow around the
gameplay envelope, not an MHD or fluid simulation.

Paths, streaks and indirect draw counts stay on the GPU. Buffers persist across
frames and resize only for capacity changes. At the saved settings, two layouts
reserve approximately 6.5 MiB, including the short direction transition. Work is
capped at 512 paths, 192 samples/path and 128 streak slots/path. The normal frame
does not read GPU data back; explicit validation does.

### Tuning and comparison

- On the **DynamoStormController**, **Use Flow Blanket** switches presentation
  during Play Mode; it stops and clears the old particles when enabled. Arrival,
  crossing, departure and droplet speed have separate controls under Flow Blanket.
  Disable **Randomize Direction Each Storm** to use **Source Screen Direction**.
- On **Storm Flow Blanket**, tune **Paths Per World Unit**, **Streak Spacing**,
  **Streak Length**, **Streak Width**, **Organic Motion**, color and brightness.
  **Boundary Clearance** controls the small gap outside the envelope.
- The scene is wired to **Main Camera**, and no package or render-pipeline change
  is required. Other scenes retain legacy presentation unless explicitly opted in.
- The legacy comparison now uses a material property block for storm intensity,
  avoiding edits to its shared material during playback.

### Visual style experiments - solid streaks and selective bloom (2026-10-07)

In `S-6_DYNAMO`, both experiments are enabled for comparison:

- **MAGNETOSPHERE ANOMALY / Storm Flow Blanket / Opaque Tapered Streaks**
  switches between the original soft additive streaks and solid-color diamonds.
  The solid option uses the blanket's Color and Brightness uniformly, ignores
  alpha, replaces the destination color, and geometrically reaches zero width
  at both tips. Arrival still follows the shared front; departure and direction
  transitions shrink stroke width instead of making the strokes transparent.
- **Sharp Core Intensity**, on that same renderer, independently controls the
  sharp streak contribution: 1 = full core, 0 = bloom only. It scales the core's
  brightness without changing the bloom source, storm timing or opacity. Zero
  omits the core draw entirely. The core now uses camera-driven submission, so
  Editor pause no longer drops the solid layer while retaining bloom.
- **MAGNETOSPHERE ANOMALY / Dynamo Bloom** has independent **Field Bloom** and
  **Flow Bloom** Enabled toggles. Each has Intensity, Threshold, Soft Knee,
  Scatter and Diffusion controls. Increase Intensity for stronger bloom, lower
  Threshold to include dimmer lines, and increase Scatter/Diffusion for wider
  spread. Turning both channels off removes the camera command buffer.

This is selective HDR bloom: each enabled source is rendered with its actual
geometry, color and clipping into an isolated HDR target, tested against the
camera's depth attachment. A soft-knee brightness extraction feeds a multiscale
downsample/upsample filter, then adds the scattered light to the camera image.
The HUD, arena grid and other objects do not contribute to the effect. It is
not a widened duplicate stroke or an emissive-color multiplier. Bloom still
works with the original soft streak style.

The Built-in pipeline implementation uses the Main Camera and the scene's
referenced shader; it requires no new package, layer or global post-processing
volume. Runtime materials are private and cleaned up on disable. Each enabled
channel adds a source draw and blur/composite passes. Temporary HDR targets
match the camera's size and MSAA, so the GPU and transient memory cost scales
with resolution and antialiasing. It is an optional presentation cost, separate
from the GPU path calculation.

`MASSIVE > Dynamo > Validate Visual Styles (Play Mode)` requires a visible storm.
It checks black/subthreshold rejection, HDR hue preservation, spatial light
spread, all four bloom combinations, camera detach/re-enable, private materials,
and zero managed allocations over 32 warmed bloom recordings. It restores its
temporary settings. The original 48-case flow validation also passes with the
solid style enabled. Captured field-only, flow-only, both-on and both-off views
confirmed independent image contributions and an unchanged HUD header.

At the 3840x2160 Windows/D3D11 Editor view on the RTX 4090, the four combinations
measured roughly 7.9-8.4 ms median whole-frame GPU time, with the blanket CPU
submission near .18 ms. These short Editor captures are too noisy to isolate a
precise bloom increment; they are not a standalone-player or target-device
performance guarantee. No new compilation or rendering errors were observed.

### Visual Preview without Play Mode (2026-10-07)

Open **MASSIVE > Dynamo > Visual Preview**, or click **Open Visual Preview** on
the **Dynamo Bloom** Inspector, then press **Start Visual Preview**. The window
renders the field and flow in their arena without starting the match.

- Scrub **Progress** to any point in the storm. Duration and Peak Strength are
  preview overrides, initially taken from the controller. Calm, Arrival, Peak
  and Recovery buttons provide starting points.
- Set **Comes From** in screen coordinates or use the four direction buttons.
  The preview samples the controller's authored front, easing, response delay,
  pressure ramp and departure. It integrates travel analytically, so jumping
  forward/backward needs no particle warm-up. Runtime travel is accumulated per
  frame; tiny phase differences between a scrubbed image and a running storm
  are expected.
- The selected storm moment stays fixed. **Animate at Held Moment** advances
  visual motion while holding storm strength, pressure and front position.
- The appearance and bloom controls edit the original scene components through
  Unity's serialized Inspector workflow, including Undo. Detailed field tuning
  remains in the field Inspector and is copied into the preview on refresh.
  Save the scene normally to keep appearance edits.
- Preview time, direction, duration and peak do not overwrite the storm
  controller. The tool owns hidden temporary renderers, a private field material
  and a separate camera. It does not tick gameplay, damage, players or the match
  clock. Other level objects are shown in their editor state.
- Stop Preview, close the window, enter Play Mode or reload scripts to release
  the temporary objects, GPU buffers and camera work. Preview controls alone do
  not dirty the scene. Preview resolution affects pixel-scale bloom spread;
  use the Game View's resolution when comparing appearance precisely.

`MASSIVE > Dynamo > Validate Visual Preview (Edit Mode)` passed 24 direction/time
samples, held-state determinism, independent motion, resizing, stable buffers,
shared-material/source-setting protection, unchanged randomness and full cleanup.
Entering Play Mode with the window active also removed the preview automatically.
The updated Play Mode style check covers core intensity at 1, .25 and 0. A matched
paused/running image check with bloom disabled retained all 64,116 cyan core pixels
in the tested upstream region, with zero pixel difference. The original 48-case
flow validation continues to pass with zero steady managed allocations.

### Flow blanket validation

`MASSIVE > Dynamo > Validate Flow Blanket (Play Mode)` checks 16 directions at
three pressures, finite exterior paths, visible coverage, GPU draw counts,
front/clock/pause behavior, camera shapes, capacity changes and resource cleanup.
Run restores its temporary settings. `DynamoFlowBlanketValidation.Preview` holds
a runtime storm for visual inspection; exit Play Mode to restore scene settings.
Profiling marker: `Dynamo.FlowBlanket.Update`.

The completed Windows/D3D11 Editor pass checked 3,479,040 finite exterior path
samples across those 48 cases. All sampled exterior cells of an 8x4 viewport grid
contained streaks in every case (a coverage check, not a guarantee about every
pixel). Visible counts ranged from 1,746 to 10,727 as the sheltered area changed.
Steady rendering allocated zero managed bytes over 32 calls and retained its GPU
buffers. Rapid reversals, legacy-particle shutdown, portrait/landscape/ultrawide
footprints, resizing, disable/re-enable and storm end passed. The existing organic
field diagnostic also passed after integration, including 1,270,218 segments
across its 12 depth/pressure/direction cases.

At 3840x2160, held .35 pressure and diagonal wind, the same-scene presentation
toggle measured whole-frame GPU medians of **22.334 ms for legacy particles**
(23 positive samples, 5,942 particles alive at capture end) and **7.165 ms for the
blanket** (96 positive samples). Earlier captures measured 23.215 ms and 7.434 ms,
respectively. The blanket's median CPU submission was .188 ms. These are live
Editor measurements on the RTX 4090/i3-12100F machine, with GameManager's inactivity
transition disabled temporarily for both captures. They are not player-build or
target-cabinet guarantees; overall CPU means include Editor/tool stalls. Existing
duplicate AudioListener warnings and inactive Player 2/4 damage-coroutine errors
were observed in Play Mode; no C# or shader compilation errors remain from this
change. No standalone player build was run.

## Active gameplay field - organic rendering with loop depth (2026-10-07)

`S-6_DYNAMO` now uses the original `MagnetosphereFieldLinesGPU2D` appearance again:
256 seeds, layered line detail, the original colors and pole highlights, animated
seeding and path warble. The initial field pass retained the existing storm
system; the subsequent flow-blanket pass above replaces its presentation and
unifies front timing. The new loop-depth option is enabled on
`MAGNETOSPHERE ANOMALY / Magneto GPU` (45 degree spread, .18 depth scale).

Loop depth revolves each complete planar trace around the fixed magnetic axis,
using a stable angle per seed. This gives coherent front/rear geometry without
multiplying the line count or sampling a scientific volume. Revolving an ideal
planar dipole generates a 3D dipole meridian; the actual gameplay curves retain
artistic envelope shaping, warble and reduced vertical scale. This is not a full
three-dimensional MHD solution. Projected points are kept inside the existing
hazard/particle envelope, including oblique winds.

To compare styles on **Magneto GPU**, turn **Coherent Loop Depth** off to recover
the original independent depth waves. **Pseudo 3D Enabled** gates both depth
styles; **Loop Spread Degrees** and **Loop Depth Scale** control the new option.
No shared material or rendering shader was changed in this pass.

The scientific renderer and its `Scientific Field Source` component are disabled
in the saved scene, retaining their references and settings. Consequently normal
play loads no scientific snapshot textures. To compare the recorded-field hybrid,
enable the source component and then `DynamoScientificFieldRenderer`; disable both
to return to the organic field. The separate scientific prefab/demo and inactive
`Magnetosphere Viz` remain intact.

### Rendering costs and validation

- Buffers allocate only when capacity changes, on re-enable, or on switching back
  from the scientific reference. Pole-return assistance no longer reallocates
  every frame. Changing seed/step settings still resizes safely.
- The append count and indirect draw arguments stay on the GPU. Normal drawing
  no longer waits for a CPU readback. Explicit segment-count debugging still
  performs its requested readback at the selected interval.
- The segment buffer now matches the shader's 28-byte layout instead of reserving
  48 bytes per element. At this scene's 266,240-segment capacity it uses 7,454,720
  bytes instead of 12,779,520 bytes. Trace density is unchanged.
- The new GPU arguments were checked against the actual append count, including
  empty output, resizing, disabling/re-enabling and scientific fallback/recovery.
- With the new depth option off, all 145,805 calm segments exactly matched the
  original tracing kernel under identical frozen inputs (unordered append output
  sorted before comparison). Depth tests covered 1,411,843 finite segments across
  four wind directions and three pressure levels, including envelope containment.
- Stable repeated updates retained the same buffers and allocated zero managed
  bytes over 32 updates. Calm, peak/oblique and live storm captures were inspected.
  The existing controller ran through storms with one field-buffer allocation.
  The final Console check reported no errors.

Profiling used the live 3840x2160 Windows Editor, Direct3D 11, RTX 4090 and
Core i3-12100F, with CPU/GPU profiling enabled. Matched appearance comparisons
froze motion and held pressure/direction fixed. The original field's median CPU
update/submission was 2.308 ms; optimized original depth waves were .039 ms.
The coherent-depth option was also .039 ms in calm and .040 ms at peak pressure.
These are field CPU costs, not total frame times or an FPS multiplier.

| Optimized presentation | Calm whole-frame GPU median | Peak oblique whole-frame GPU median |
| --- | ---: | ---: |
| Original depth waves | 8.194 ms | 8.600 ms |
| Coherent loop depth | 8.277 ms | 8.258 ms |

Positive sample counts ranged from 90 to 200 for these comparisons. GPU timing
varied between captures (calm p95: 9.775 ms versus 12.546 ms), so the measurements
show no clear typical penalty from this modest depth, not proof that it is free.
Whole-frame CPU means are unsuitable because Editor/tool stalls contaminate them.
A capture interrupted by the game's unscaled inactivity return was discarded;
peak comparisons were repeated with that transition disabled for both styles.
All temporary test settings were restored by exiting Play Mode. No standalone
build, target-cabinet performance claim or competitive-readability certification
is implied.

Validation menu: `MASSIVE > Dynamo > Validate Organic Field (Play Mode)`.
`DynamoOrganicFieldValidation.BeginCapture/EndCapture` supplies explicit Editor
profiling captures; enable CPU/GPU profiling first and use matching scene inputs.
The tests do not save runtime changes.

The next scientific step is to derive storm profiles/deformation references from
observed inputs and the recorded simulation while preserving this visual design.
That storm-system adaptation has not been implemented in this rendering pass.

## Optional recorded-field hybrid reference - S-6_DYNAMO (2026-10-07)

The active `MAGNETOSPHERE ANOMALY / Magneto GPU` has an optional
`DynamoScientificFieldRenderer`. Its child `Scientific Field Source` supplies the
first January pressure-pulse snapshot, paused at 02:50 UTC. **That snapshot is
already stressed**: upstream dynamic pressure is approximately 1.0655 nPa. It is
used as a source of disturbance, not as the calm geometry.

### Reconstructing an unstressed baseline

The archived `ScientificData/January2026/source-run.json` specifies
`b_dipole = -3.11e-05` tesla. The reconstruction uses that signed internal dipole
coefficient (-31,100 nT at 1 Re) and the snapshot's exported dipole axis to
separate its analytic internal field from the stored total B field:

```
recorded residual = recorded total B - recorded internal dipole
rendered B(p) = fixed-axis internal dipole(p)
              + storm weight * rotated recorded residual(p)
```

`DynamoReconstructedFieldLines.compute` integrates this reconstructed vector
field in three dimensions. At zero gameplay pressure, the recorded residual is
completely removed. The field is a centered, symmetric dipole with no dayside
compression or swept-back tail. Its axis follows the existing gameplay field's
`Dipole Axis`; changing wind direction does not rotate the magnetic poles or
change calm geometry. The old analytic field-line drawing is not used to make
these loops: they are GPU traces of the internal dipole's vector field.

Storm weight rises from zero to one as the existing delayed `pressureGain`
rises from zero to `Recorded Disturbance At Pressure Gain` (default .35).
The residual turns smoothly toward the authored wind direction; only the
intrinsic dipole remains fixed. Traces are recomputed when this field changes,
not morphed between line endpoints. At .35 the entire recorded residual is
present. Above .35, additional authored envelope deformation ramps in, reaching
`Storm Deformation` at pressure gain 1. This avoids applying extra geometric
compression while the recorded disturbance is still being restored. These are
gameplay drive values, not a conversion from nPa or a physical pressure law.

This is an **art-directed reconstruction**, not a unique inverse of the MHD
simulation or a newly solved equilibrium. The residual contains external-current
contributions, other non-dipole structure and resampling error; it cannot be
identified exclusively with solar-wind pressure. Reorienting it independently of
the fixed internal dipole does not reproduce the original total field exactly.
The calm state represents an idealized zero-wind dipole, not ordinary quiet
Earth, which still experiences solar wind. The source data are unchanged.

Tracing uses the same excluded inner radius (3.25 Re) and a centered outer sphere
inside the recorded sample volume (17.625 Re for this episode). A symmetric
trace domain prevents the rectangular archive's unequal sunward/tailward extents
from creating false asymmetry in calm. The outer cutoff is a display limit, not
a physical magnetopause; the ideal dipole itself has no finite outer boundary.

### Existing gameplay and presentation

`MagnetosphereFieldLinesGPU2D` remains the authority for the envelope and every
existing controller, storm particle and hazard reference. Its old drawing yields
to the new renderer when ready, and resumes if the replacement or source is
disabled. The inactive `Magnetosphere Viz` and standalone `Scientific Dynamo`
remain available for reference. Storm scheduling, particle behavior, damage and
player drift are unchanged.

Depth is reduced for the top-down view. Additional dayside contraction, flank
pinch and tail extension use the existing envelope. A positive, integrated axial
scale keeps this presentation mapping monotonic. Lines fade at that envelope,
which remains a gameplay approximation rather than a scientifically inferred
magnetopause. Line density is not a calibrated safe-zone indicator.

The source stays at 02:50 UTC; gameplay pressure does not scrub its timeline.
Changing the preview position selects a different recorded residual. Playback
remains available for experiments but is not the integrated default. The
standalone scientific prefab, original tracer and recorded-data renderer are
unchanged and continue to display the original total field.

Tune **Dynamo Scientific Field Renderer** on **Magneto GPU**:

- `Internal Dipole Coefficient Nt` (-31,100): the signed coefficient from this run;
  match it to the source configuration if replacing the episode.
- `Recorded Disturbance At Pressure Gain` (.35): gameplay drive at which the full
  recorded residual returns; additional presentation compression starts here.
- `World Units Per Earth Radius` (.5): arena scale.
- `Depth Scale` (.3): out-of-plane curvature for the top-down camera.
- `Storm Deformation` (.85): additional envelope deformation above the reference drive.
- `Direction Response Seconds` (1.25): response of the disturbance to wind turns.
- `Width Pixels` (1.8), `Brightness` (1.55), colors and rear brightness: appearance.
- 144 seeds, 240 steps and .22 Re steps: trace quality. Runtime rendering uses no
  GPU readback. Storm transitions retrace; an unchanged calm field is cached.

Disable only the new renderer to compare with the original field. Both use the
same storm controller. The new material is specific to this integration.

### Integration validation

Play Mode menu checks:

- `MASSIVE > Dynamo > Validate Destressed Calm Field (Play Mode)` checks the
  analytic dipole-shell invariant, symmetric bounds, unchanged geometry after
  wind reversal, partial residual restoration and exact return to calm.
- `MASSIVE > Dynamo > Validate Gameplay Scientific Field (Play Mode)` checks
  finite volumetric traces, independent CPU/GPU reconstructed-field tangents,
  inner exclusion, pressure-driven retracing and all 257 envelope samples.
- `DynamoGameplayFieldValidation.ValidateLifecycle()` checks renderer/source
  disable and recovery, buffer release and the original renderer's fallback.

These passed in the live Editor. Calm contained 14,292 valid segments and passed
127 independent tangent checks. Full recorded disturbance and maximum gameplay
pressure also passed (segment counts depend on wind orientation). Calm, restored
disturbance and peak-pressure captures were inspected. Removing pressure restored
the exact calm GPU positions; changing wind at zero pressure did not retrace them.
The earlier data validation passed six spatial checks, three timeline checks and
all nine snapshot checksums; this correction does not modify the data.
C# and the shaders compiled without new errors. Known inactive Player 2/4
damage-coroutine errors and duplicate AudioListener warnings predate this field
pass. No standalone build or cabinet performance certification was performed.

Project context: Unity 6000.0.28f1, built-in renderer, Windows, local Rewired
control, existing MonoBehaviour/ScriptableObject architecture, connected Coplay
Unity MCP. S-6_DYNAMO is enabled in build settings. No packages, build settings,
assembly definitions or input settings changed. Design context came from the
current MASSIVE Notion GDD's 2.5D field-lab camera, controlled energy colors and
shared arena. Cabinet performance and competitive readability need a human playtest.

## Open and use the standalone prototype

- `Assets/Scenes/Dynamo Scientific Demo.unity`: isolated 3D demonstration. Enter Play Mode. Space pauses playback, R restarts, right-drag orbits, and the wheel zooms. The timeline supports scrubbing. Pink lines follow B; cyan traces follow local plasma flow.
- `Assets/Scenes/S-8_DYNAMO-SCIENTIFIC.unity`: a separate copy of the existing gameplay prototype, with the scientific field and local-flow player drift wired in. Its legacy Dynamo storm scheduler and authored flow visuals are disabled. Other gameplay content comes from the source prototype.
- `Assets/Scripts/Anomalies/Magnetosphere/Scientific Dynamo.prefab`: reusable field and rendering components.
- `MASSIVE > Dynamo > Build Scientific Prototype`: imports the prepared episode and creates missing scenes. Existing demo/gameplay scenes are deliberately not overwritten on subsequent runs.
- `MASSIVE > Dynamo > Validate Scientific Data and Sampling`: dependency-free Editor validation.

`ScientificMagnetosphere` owns the episode, scientific clock, coordinate transform and two neighboring snapshots. `ScientificFieldLineRenderer` uses that source for both magnetic and plasma traces. `PlayerStormPush` accepts an optional explicit reference to the same source; its existing behavior remains available when no scientific source is assigned.

## Scientific provenance

- Model: [SWMF/BATS-R-US 2023](https://ccmc.gsfc.nasa.gov/models/SWMF~2023/), University of Michigan, hosted by NASA CCMC.
- Run: [Wei_Liu_090426_GM_1](https://ccmc.gsfc.nasa.gov/ror/results/viewrun.php?runnumber=Wei_Liu_090426_GM_1), requested by Wei Liu and published by CCMC on 2026-09-06. The full simulation covers January 3, 2026. It uses OMNI inputs, a time-updated dipole, corotation, a 2.5 Earth-radius inner boundary, and the Rusanov solver. It does not include a coupled ring-current model.
- Inputs: [High Resolution OMNI](https://omniweb.gsfc.nasa.gov/html/omni_min_data.html). `source-IMF.txt` retains the run's actual input sequence; `source-run.json` retains the model configuration. These are model boundary inputs, not local plasma measurements.
- Coordinates: [NASA's coordinate definitions](https://sscweb.gsfc.nasa.gov/users_guide/Appendix_C.html); offline conversions use [GEOPACK](https://github.com/tsssss/geopack).
- Each prepared frame has a UTC timestamp, source filename, source SHA-256, and prepared-file SHA-256 in `manifest.json`.

Credit the University of Michigan SWMF team, NASA CCMC, the run provider and OMNI when presenting these results. Consult [CCMC's publication policy](https://ccmc.gsfc.nasa.gov/publication-policy/) before the talk; the project has not contacted the model providers or CCMC.

## What the equations control

The source solver evolves three-dimensional magnetohydrodynamics. Its stored total magnetic field already contains the internal planetary contribution; the renderer does not add a second dipole or apply the old authored compression/warble.

Field lines integrate `dx/ds = Â±B/|B|` with midpoint stepping in three dimensions. The camera projects the resulting curves. Thin camera-facing strips provide a pixel-width core and antialiased edges with additive emission. No scene lighting is needed to make the lines bright.

The input pressure shown in the demo is `rho * |v|Â²`, with the proton-mass conversion from cm^-3 and km/s to nPa. It is approximately 1.1 nPa before this pulse and 2.4 nPa at its sampled peak. Stronger visual changes are determined by the model output, not by multiplying the geometry by this number.

The default Earth-fixed view rotates the full field from GSM into GEO using the event timestamp. Scientific Z becomes Unity Y. The optional Sun-oriented view retains GSM axes. Camera orbit is a separate presentation choice. In Earth-fixed mode, local flow uses `u_relative = u_GSM - omega_GSM Ã— r_km` before rotation, including Earth's rotation when interpreting plasma velocity relative to the arena.

Player drift samples local model velocity, projects it onto world XZ, maps 700 km/s to the existing maximum drift speed, and clamps that target. The existing bounded acceleration then follows the target. The speed conversion, response and cap are gameplay choices. Magnetic fields do not directly exert this drag on the player. Missing data, disabled source and the inner excluded region produce no scientific drift; they do not fall back to an unrelated wind direction.

## Approximation boundaries

- The archive's native adaptive grid is resampled to 65 Ã— 49 Ã— 49 nodes at 0.75 Re spacing, covering GSM X = -30â€¦18 Re and Y/Z = Â±18 Re. The exporter uses inverse-distance-squared weighting of the nearest eight native cell centres. This is a display approximation, not a divergence-preserving numerical method.
- The source inner boundary is 2.5 Re. Display and gameplay sampling exclude radii below 3.25 Re to reduce coarse-grid boundary artifacts. The central sphere depicts Earth's true relative radius; the empty inner region is an excluded model domain, not an unmagnetized cavity.
- Nine snapshots at five-minute cadence are interpolated in space and time. Lines are retraced through the interpolated field; their endpoints are not morphed between different field-line topologies. This does not resolve rapid reconnection or sub-minute fluctuations.
- The simulation's prehistory is contained in its snapshots. The excerpt starts after almost three simulated hours and stops at its last recorded snapshot. There is no automatic jump to the beginning or blend into an unrelated event.
- Flow traces show instantaneous streamlines. Their moving highlights are direction cues with artistic timing, not charged-particle trajectories. Line density and color are artistic; spacing is not a calibrated measure of magnetic flux.
- The first excerpt provides modest timestamp-derived angular variation. Broad directional and difficulty coverage requires additional observed episodes. `SelectObservedEpisode` selects a complete configured episode; no random strength multiplier or invented upstream direction is used.
- This prototype supplies local-flow drift. It does not infer a damaging magnetopause boundary from a field-strength threshold, and it does not implement storm damage or a radiation-dose model.
- Snapshot loading/decompression and texture uploads occur when crossing frame boundaries. The prototype keeps two decoded snapshots on the CPU and GPU, but the episode's compressed assets are also resident. Profile on the intended show hardware before choosing higher resolution or more episodes.

## Reproduce and expand

`Assets/Editor/DynamoScientificTools~/prepare_episode.py` prepares data outside Unity. It needs Python, NumPy, SciPy and GEOPACK; these are not Unity runtime dependencies. Pass `--cache` pointing outside Assets and `--output` pointing to an episode data directory. The defaults select this run and excerpt. `--run`, `--start`, `--end` and `--cadence-minutes` select another suitable run. Use a separate cache per run. The exporter requires observed OMNI input, updated dipole orientation, downloadable files, recognized units, finite samples and orthogonal coordinate transforms.

Runtime frame format: gzip-compressed little-endian float32 values, eight channels per node in this order: Bx, By, Bz [nT]; Ux, Uy, Uz [km/s]; density [amu/cmÂ³]; thermal pressure [nPa]. X is the fastest dimension, then Y, then Z. `manifest.json` describes dimensions, spacing, units by schema, transforms, metadata and checksums.

For a broader library, select real episodes covering the desired measured pressure/field conditions and Earth-fixed approach angles. Keep full multivariate sequences together. Future high-fidelity exports should use the model's native interpolator and finer/adaptive sampling near the magnetopause; include convergence and divergence diagnostics before using the display quantitatively.

## Validation performed

- Unity 6000.0.28f1 compiled the runtime, Editor scripts, compute shader and line shader without reported errors.
- Six spatial sampling checks and three timeline checks passed. All nine snapshots passed dimension, finite-value and SHA-256 checks.
- Play Mode checks passed at baseline, peak input pressure and the last frame: 13,666, 14,134 and 13,712 valid GPU segments respectively. Checks cover non-planar geometry, finite bounded trace steps, excluded-domain behavior, planar drift bounds and an isolated Rigidbody's expected acceleration.
- Playback stopped at the recorded endpoint. Disabling and re-enabling the source masked its output, reloaded successfully, and left exactly four live snapshot textures.
- The separate gameplay scene passed the same runtime checks with 12 wired player components, two active players and no active legacy storm scheduler. Clearing and restoring the configured episode was also checked.
- The exported magnetic north axis varied by less than 0.06 degrees in GEO across the excerpt, while the measured flow's Earth-fixed azimuth varied from approximately -47 to -67 degrees.
- Baseline and pulse camera captures were inspected. This is a functional prototype validation, not a target-hardware performance certification or validation of the underlying scientific model against spacecraft measurements.
