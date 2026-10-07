# COSMOS layout

`Assets/Scenes/S-9_COSMOS.unity` is the user's duplicated LATTICE scene, now with
an oval arena and an evolving cosmic web background.
The inherited encounters and shared match/score systems remain available for testing.

The field and goals share one exact ellipse. The user has tuned it to 36 units
wide and 13 units high, with playable separators at approximately X = +/-13.846.
The original installation defaults were 31.2 x 14; the current dimensions are
authored on the grid and must not be reset by installation tools.
The original camera size (9), HUD, players and all other authored poses are retained.
Three unused spawn markers moved by the first prototype were restored to their
original positions (migration revision 3); the actual Respawn objects were never moved.
The camera stays fixed, including when the chosen oval extends beyond its view.

`ArenaBoundsFromVectorGrid` owns the optional oval profile. Rectangular scenes default
to their original behavior. Queries, padded placement, and recovery use this profile;
`CosmosArenaLayout` builds transient solid walls and an outline mesh from it. The
grid's simulation rest positions and production GPU sampler both curve each row:
vertical row spacing is widest at the centre and condenses to about 64% at the ends.
Player, attack and grid forces operate in those physical coordinates. Displaced
strokes and simulation nodes are constrained by the same ellipse. No generated
mesh or collider hierarchy is saved into scene YAML. COSMOS disables the original
four-wall builder and its old wall objects. The outer goal-cap outline is generated
without colliders. The original goal Discs remain transparent size templates.
The existing mask planes retain their transforms and depth; a scene-specific mask
material opens the full ellipse so they do not cut the taller grid back to a rectangle.
`CosmosGoalShape` maps score fills and animated promotion rings into the elliptical
cap, so they no longer use a semicircular outline inside an elliptical outer frame.

Goal transforms, capture/attraction radii, score size limits and local capture-blast
dimensions follow the goal-height ratio. The score renderer independently fits
the cap's width and height; the score shader and promotion mesh share the same
half-disc-to-ellipse-cap mapping. The legacy
ScoreSphereScript targets the separate visual child so increasing score cannot
resize the Core capture trigger. Score economy, tier thresholds and promotion
timing stay shared.

Editor commands under **MASSIVE > COSMOS**:

- **Select layout controls** (`Ctrl+Shift+Alt+F8`): select the grid's
  `CosmosArenaLayout` component. Its **Overall Oval Width** (16–48) and
  **Overall Oval Height** (6–24) Inspector sliders resize the full ellipse in
  world units, including both goals. They preserve the separator proportion,
  update the grid, walls, goal fills, score growth limits and capture effects
  immediately in Edit Mode, and support Undo/Redo. Save the scene to retain edits.
- **Apply oval layout** (`Ctrl+Shift+Alt+F10`): apply/reconcile goal scaling and scene
  identity, then save COSMOS. Reapplying does not compound scaling or change cameras
  or non-arena poses. Shape defaults are installed once (layout revision 2). A pre-install
  scene backup is retained at `Library/CosmosLayout/COSMOS-before.unity`.
- **Validate layout in Play Mode** (`Ctrl+Shift+Alt+F11`): direct-launch startup,
  production GPU row sampling and rectangular fallback, padded bounds, respawns,
  all physical wall segments, moving rigidbody contacts,
  score/tier previews, fixed capture volumes, and actual Core captures for both
  teams. Returns to Edit Mode. Reports and renders are in `Library/CosmosLayout`.
- **Inspect current layout** (`Ctrl+Shift+Alt+F9`): save a hierarchy/configuration
  inventory to that folder.

For overall shape tuning, use the sliders on `CosmosArenaLayout` at
`02_Arena/Surface/GRID/VectorGridGPU`; no Apply step is needed. For separate
goal/field proportions, edit the bounds profile and run **Apply oval layout**.
The camera stays fixed while sizing; its current framing targets the cabinet's 16:9 format.
The level has its own definition/stage assets for direct launch; carousel icon,
instructions and catalog registration remain later work.

## Enemy placement in the oval

The existing encounter timeline and formation assets are reused unchanged.
`EnemyArenaLayout` maps their normalized positions through the same oval columns
as the grid. Top/bottom rows bend toward the goal separators; rings and phalanxes
deform smoothly with their location. Facing directions for mobile enemies retain
their authored angles. Goal caps remain outside the playable spawn area.

On `LATTICE Enemy Timeline`, **Enemy Arena Layout > Oval Spawn Inset** defaults to
0.25 world units. It adds a continuous inset to the mapped territory, accounting
for the curved wall's slope and grid scale. The Director's enemy-radius, border,
obstacle, player, reservation, and population checks still apply. Large inset
values or small arena dimensions can leave formations blocked; the existing
bounded placement adjustments and expiry rules remain authoritative.

Wall sockets bypass the formation inset and stay on the physical boundary.
Top/bottom sockets face the ellipse's inward normal while retaining any authored
aim offset and aim arc. Side sockets remain on the playable goal separators.
Warnings and spawns consume the same resolved pose; announced positions stay
locked. Width/height edits are read for subsequent placements. Rectangular levels
retain the original mapping and ignore Oval Spawn Inset.

The Scene gizmos and Encounter Composer preview show the curved territory.
**MASSIVE > COSMOS > Validate oval enemy placements** exercises the saved scene
with the existing formations in Play Mode and writes its report and captures to
`Library/CosmosEnemyLayoutValidation`. All test configuration is temporary.
The inherited optional `05b Turret Side Corners` formation references
`SideLeftTop` and `SideRightBottom`, which are absent from this scene's layout.
That pre-existing configuration is reported by validation and remains unchanged;
oval mapping does not create mounts or substitute encounters.

## Cosmic web

`CosmicWebBackground` is on the same `VectorGridGPU` object as the layout controls.
It uses 56,000 persistent matter/galaxy tracers, rendered in one procedural GPU draw.
No particle GameObjects, colliders, gameplay forces or per-frame buffer uploads are
created. The buffer/material are transient and released on disable or scene unload.
Changing the seed, particle count or Organic Strength rebuilds the distribution;
dimensions, padding, colors, brightness, evolution variation, edge condensation and
preview age only update shader parameters.
`Drift Strength` changes continuous motion without rebuilding the distribution.
`Cluster Turbulence` and `Turbulence Strength` also update shader parameters only.
`Early Heat` and `Early Heat Brightness` change only early color and light.
All appearance controls below update live without regenerating the topology.

- **Particle rendering > Opaque Particles** toggles an alternate solid, unlit
  appearance. Off is the original additive web and remains the default. On uses
  full-opacity circular cutouts, replacement color (One/Zero), depth writes and
  the cutout render queue; it bypasses the soft radial glow and early plasma halo.
  Circle/oval boundaries discard pixels outright rather than fading alpha.
  **Opaque Min Particle Size** and **Opaque Max Particle Size** set world-space
  diameters (defaults **0.015–0.05**). Each particle keeps a stable random size
  within this range, independent of former haze/glow, brightness, depth or Age.
  Equal limits give uniform dots; reversed limits are sorted for rendering.
  Opaque mode uses only the chosen filament/cluster and Early Heat palettes,
  including their existing color transitions, multiplied by overall Brightness.
  It omits the additive mode's white highlights, brightness variance, early heat
  brightness boost, formation/crowding dimming, vignette and radial falloff.
  **Galaxy Size**, **Brightness Variance**, **Early Heat Brightness** and
  **Edge Vignette** continue to control additive mode only. Particle positions,
  early heat timing and evolving motion stay shared.
  Switching updates only the owned material and does not reset age, motion,
  topology, supernova sources or the particle buffer. No extra draw or buffer is
  added. The Level Select icon and supernova effects keep their own rendering.
  Validation: Unity compilation and shader diagnostics are clean; isolated
  renders at ages 0, 0.5 and 1 were inspected in both modes. The additive output
  matched the preceding shader's renders byte-for-byte at all three ages. Live
  Edit Mode switching preserved the buffer and generation. Play Mode and target
  performance checks remain deferred.
- **Filament Color Subsets** and **Cluster Color Subsets**, immediately below
  their respective main colors, add independently weighted palettes in opaque
  mode. Expand a list, add an element, choose its HDR **Color**, and set its
  **Percentage**. For example, 20% gold + 10% violet leaves 70% of that palette
  using the main color. Zero disables an entry; totals above 100 normalize the
  subsets to 100%, leaving no base-color share. Empty lists preserve the previous
  appearance. Percentages describe approximate shares of persistent particle
  choices, not a single averaged paint color. The existing filament-to-cluster
  color transition and Early Heat overlay still apply. Choices stay stable as
  particles move and are independent of size; editing palette weights can
  reassign choices. Additive mode and the Level Select icon are unaffected.
  Each main color has a **Color Interpolation** slider, and every subset has an
  **Interpolation (%)** slider, separate from its population **Percentage (%)**.
  **100%** retains the previous continuous filament/cluster mix, **0%** uses only
  exact palette swatches, and intermediate values reduce the tint toward the
  other palette. The selected pair uses its lower interpolation setting, so a
  protected color is never tinted by a more permissive counterpart. At reduced
  interpolation, a stable per-particle choice selects the exact filament or
  cluster swatch according to the existing cluster influence, then blends toward
  the original mixed result by the allowed amount. Lower values therefore make
  color handoffs more distinct as matter becomes clustered; geometry stays fluid.
  Early Heat remains a separate overlay, and overall Brightness still applies.
  Existing and new entries default to 100% without changing their colors or
  population shares. The lookup's alpha channel stores this interpolation amount;
  the rendered particles remain fully opaque. Changing a slider refreshes the
  lookup without regenerating topology. Inspector edits support standard Undo.
  A transient 1024-by-2 point-sampled color lookup (32 KiB GPU pixel data) supports
  the lists without adding draw calls or rebuilding topology. It is created only
  when weighted subsets are used and uploaded only when their settings change;
  palette shares have approximately 0.1-percentage-point lookup resolution.
  The lookup is released with the web on disable, rebuild or scene unload.
- The existing regulation clock owns age: start/countdown = 0, half of regulation =
  0.5, regulation end = 1. Paused regulation, including bonus phases, holds age.
- Early matter is nearly uniform. It gathers onto interconnected filaments and
  pink cluster junctions around the midpoint. Formation and accretion overlap
  through regional timing windows; there is no shared stop/switch at age 0.5.
  Nearby clusters travel toward shared centers of mass in staggered mergers,
  while the entire web is transported by a continuous, coherent flow. Late
  accretion approaches its targets gradually without snapping into a fixed final
  pose. A tenuous filament population and dominant black voids remain.
- The web follows the current oval with a **0.25 world-unit padding** default
  (the user has currently authored **0.05**).
  Outer filament ends are paired with curved return paths on the model's enclosing
  ellipsoid. A smooth compact projection condenses the outer network progressively
  toward the **full ellipse, including both goal caps**. The background ignores
  the playable goal separators: neither its projection nor its fragment mask
  squeezes or clips the left and right sides at those borders. Goals continue to
  occlude the web through the existing depth test; gameplay bounds are separate.
  Projection includes the inset plus a small glow allowance; the fragment mask is
  a final safety boundary for glow, rather than cutting off filament centerlines.
  Its thin depth slab sits behind the grid and
  above the existing ground plane. Camera, HUD and goal poses remain untouched.
- **Preview Age** scrubs all three eras in Edit Mode. Play Mode always follows the
  actual match clock. Palette, density, galaxy size, brightness and padding remain
  Inspector controls. **Organic Strength** controls multiscale curvature and branch
  spread; **Evolution Variation** controls regional collapse timing and infall drift;
  **Edge Condensation** controls the gradual lens-like compression.
  **Lensing Transition Smoothness** changes the geometric transition between the
  middle and condensed rim: **0** tightens compression near the rim, **0.5** retains
  the previous profile, and **1** spreads it into a broader, smoother transition
  inward. It reshapes radial spacing after the existing lens calculation, with a
  monotonic curve that preserves the centre and the full-oval boundary. It changes
  particle placement, independently of edge darkening, without rebuilding the web.
  **Edge Vignette** (previously **Edge Condensation Feathering**) controls the glow fade inward from the padded
  oval boundary in world units: **0.05** retains the original edge, larger values
  soften the condensed rim, and zero gives a crisp edge. This changes the visual
  falloff, not the oval projection or gameplay boundary. Existing feathering values
  migrate to the renamed field. **Brightness Variance**
  controls base emission contrast between tracers: **0** is uniform, **1** retains
  the original variation, and **2** increases contrast. Haze and evolution dimming
  remain applied separately. Save scene edits to keep them.
- **Animate Preview** keeps the selected age flowing in Edit Mode, including 0
  and 1. Turn it off to freeze the current preview. Scrubbing age resets the
  preview flow clock for repeatable comparison. **Drift Strength** controls the
  shared flow amplitude. Play Mode has its own scaled-time flow clock; paused
  regulation bonus phases hold that clock along with evolution. No use is made
  of the global shader `_Time`, so those pauses hold the actual geometry.
- **Early Heat** (default **1**, range 0–1) gives the initial soup a mottled
  cyan/blue, gold, orange and red palette inspired by the supplied microwave-sky
  reference. **Early Heat Brightness** (default **1.55**) multiplies the existing
  brightness only during that early phase. **Early Heat Transition Point** is the
  age at which heat finishes fading (default **0.45**). **Early Heat Transition
  Duration** sets the fade length (default **0.43**, so fading starts at **0.02**).
  The start is clamped to Age 0; a zero duration switches at the transition point.
  The defaults preserve the midpoint and late palette, while later transition
  points can deliberately carry heat further into the timeline.
  Setting Early Heat to zero restores the original early appearance. Three scales
  of coherent noise provide patches and fine grain without making density clumps;
  overlapping depths share a color field, and its slow evolution follows the
  existing motion clock. The existing haze gets a softer early glow within the
  same particle footprint to retain the luminous soup between pinpoints.
  Positions, sizes, particle count, formation, mergers and
  turbulence are unaffected. These are artistic heat-map colors, not calibrated
  temperatures or a change to the evolution model. No new textures, buffers or
  draw calls are used; the heat noise is bypassed after it fades out.
- **Early Heat colors and distribution** exposes the four heat colors and a
  coverage slider beneath each. **Blue Coverage** (default **1**, range 0–1)
  controls how much of the remaining coolest color stays blue. Lowering it
  replaces that share with cyan; **0** removes blue entirely, and **1** preserves
  the original balance. Gold and red retain their temperature ranges. This works
  with both render modes and with distinct or interpolated heat colors.
  **Cyan Coverage**, **Gold Coverage**, and **Red Coverage** let each color reach
  further into cooler patches. These are relative ranges and weights, not exact
  screen percentages. Blue is the base; cyan, then gold, then red blend over it.
  Zero excludes an additional band and one fully applies it before later bands;
  cyan also fills any blue removed by Blue Coverage. Cyan/gold/red defaults
  (**0.78 / 0.525 / 0.23**) retain the original balance. Color and coverage changes
  affect the heat overlay only, not the filament/cluster palette.
- **Early Heat Interpolation** is one **0–400%** slider under **Early universe
  heat**, for opaque mode. **100%** (the default) preserves the original smooth
  mixing of heat colors and their fade into the normal web. **0%** chooses exact
  heat swatches using the same spatial coverage weights, and transfers particles
  individually back to the normal web palette during the authored fade interval.
  Settings between 0 and 100 blend between these distinct and smoothly mixed
  results. Above 100, the slider broadens the temperature bands where adjacent
  heat colors mix: **200% = twice the original band widths**, **400% = four times**.
  Try **200–300%** for more intermediate hues across the cloud rather than only
  at narrow boundaries. Coverage centers remain in place, while wider overlap
  naturally reduces areas of pure color; coverage at 0 or 1 still excludes or
  fully applies a band. The fade interval does not stretch with band width.
  Particle choices use stable seeds, so lowering interpolation does not introduce
  random frame-to-frame flicker. Overall Brightness still applies. Early Heat
  strength, timing, color coverage, geometry and motion retain their existing
  controls; additive mode is unaffected. No new textures or draws are added.
- **Cluster Turbulence** enables irregular local flow and gentle attraction around the
  moving cluster centers (enabled by default). **Turbulence Strength** defaults to
  **0.7**, with a 0–2 range. Turn the toggle off, or set strength to zero, to bypass
  the local turbulence calculations and compare the previous flow. The same seeded
  structure is retained, and changing these controls does not rebuild the buffer.
  Three coupled noise-driven shears at different scales stretch and fold streams,
  with a different flow orientation and rate per cluster and stronger stirring
  during mergers. Smoothly changing noise replaces the shared circular rotation;
  there are no accumulating orbital angles or repeating sine-driven orbit paths.
  Influence fades smoothly with distance from
  the cluster and with early formation; residual filaments remain unaffected.
  Attraction is bounded to retain finite cores, and motion continues at age 1.
  It shares the preview/match motion clock, so **Animate Preview** and bonus pauses
  freeze it along with the existing drift. This is a visual approximation with one
  assigned attractor per tracer, not a galaxy-to-galaxy force simulation.

### Scientific basis and artistic interpretation

This is a seeded geometric model of structure formation, not a downloaded JWST
catalogue, an N-body gravity calculation or an exact reconstruction of our sky.
Irregularly seeded three-dimensional Voronoi void cells define a coarse connected
skeleton. Several octaves of coherent spatial noise curve that skeleton. Each
filament has six sampled routes: a main strand and finer tributaries that leave
and rejoin from either end. Correlated density changes trunk thickness and light;
small galaxy groups punctuate longer filaments between the main junctions. A
faint particle population spans neighboring routes as diffuse ribbons/sheets.
Cluster sizes vary coherently at shared junctions, with a mildly elongated profile
and fewer large concentrations. This hierarchy retains open voids at the existing
56,000-particle budget and does not add draw calls or per-frame uploads.
Continuous trajectories carry the same particles from a homogeneous distribution
to filaments and knots. Coherent regional timing differences, curved infall and
curved accretion paths make evolution uneven. Age 0.5 remains a close structural
reference, rather than a mandatory stationary anchor. Neighboring junctions are
paired by proximity and richness for local mergers; both members move. Their
shared center is also carried by the flow. This is an illustrative kinematic
model, not gravitational integration.
No time-varying random reseeding or particle respawning is involved.

The closed outer return paths and lens-like oval projection are arena presentation
choices, not a claim that the universe has an oval boundary or a physical simulation
of gravitational lensing. NASA's simulation visualizations and Hubble mapping work
inform the hierarchical filaments, irregular voids and dense junctions.

NASA's January 2026 JWST/COSMOS weak-lensing map covers a limited observed sky
region and shows clumpy dark matter associated with galaxies. NASA's 2023 early
filament observation and CMB material support the homogeneous-to-filamentary
progression. The CMB era predates stars: the initial luminous dots represent
matter tracers. Blue/pink are the supplied reference's illustrative palette, not
literal visible-light dark matter colors. The mid-match "present" and late void
dominance are deliberately compressed artistic epochs, not a calibrated age scale
or a prediction of future cosmic evolution.

References:
- https://svs.gsfc.nasa.gov/14598/
- https://science.nasa.gov/universe/galaxies/large-scale-structures/
- https://science.nasa.gov/missions/hubble/slime-mold-simulations-used-to-map-dark-matter-holding-universe-together/
- https://www.nasa.gov/missions/webb/nasa-reveals-new-details-about-dark-matters-influence-on-universe/
- https://www.nasa.gov/universe/nasas-webb-identifies-the-earliest-strands-of-the-cosmic-web/
- https://imagine.gsfc.nasa.gov/observatories/satellite/wmap/anisotropy.html

**MASSIVE > COSMOS > Cosmic web > Install background** (`Ctrl+Shift+Alt+F7`)
adds/wires this scene-local component while preserving existing transforms and
the user's oval dimensions. Reinstallation preserves authored web settings.
**Validate evolution in Play Mode** (`Ctrl+Shift+Alt+F12`) verifies topology,
boundary reconnection, CPU/GPU trajectory agreement, padded full-ellipse projection,
coverage behind both goals, independence from separator positions, nonlinear
regional timing, occupancy progression, clock binding, paused-clock behavior and
resource lifecycle. It captures five eras including both transitions and returns to
Edit Mode. Reports and captures
are in `Library/CosmosWeb`; the pre-install scene backup is `BeforeWeb.unity` there.

Earlier full-oval/structure revision validated in Unity 6000.0.28f1 / Direct3D11 on
2026-10-04: **32 checks passed**, with no runtime errors or exceptions. The GPU
probe covers nine ages, stays inside the padded ellipse, populates both goal caps,
and produces identical positions when only the gameplay separators change.
The CPU/GPU position difference was at most 0.000005 local units. All five era
captures were visually reviewed. The 636-filament network includes 265 secondary
galaxy groups; sampled empty-cell fractions progress from 0.1% to 18.1% to 68.7%.
The saved scene is byte-identical, retaining the 36 x 13 oval and camera size 9.
No standalone build or cabinet performance qualification was run.

Fluid-motion revision (2026-10-04): 41 Play Mode checks passed in the same Editor
and graphics backend, with no runtime errors/exceptions. The current seed has
203 local merger pairs. 99.8% of sampled tracers keep moving across age 0.5;
maximum sampled velocity change across that boundary is 0.00048 model units/sec.
Mean midpoint displacement from the previous authored structure is 0.128 model
units. At fixed ages 0, 0.5 and 1, eight seconds of flow produces approximately
0.2 model units of mean drift. CPU/GPU disagreement is at most 0.000006 local
units. Full-oval coverage, bonus-clock pause, buffer reuse/cleanup, deterministic
seeding and the saved scene's byte preservation pass. The persistent particle
buffer now uses 96 bytes per tracer (one extra vector for the merger), retaining
the existing single draw and particle count. Live Edit Mode also advances the
shader motion clock without regenerating topology or dirtying the scene.
Standalone builds and cabinet performance remain untested.

Optional cluster-turbulence revision: the persistent buffer now uses **128 bytes
per tracer** for a shared attractor position/radius and seeded flow orientation/rate.
At 56,000 tracers that adds 1,792,000 bytes (about 1.71 MiB), with the existing
single draw, particle count and no per-frame buffer uploads. Disabling turbulence
bypasses its shader math; the buffer layout and memory remain the same. Tests and
performance profiling for this addition are deferred at the user's request for
evaluation on the target machine. The earlier validation results above predate
this effect; the existing CPU/GPU baseline probes default to turbulence disabled.

The irregular-flow revision (2026-10-05) replaces radial rotation with coupled
spatial noise in a seeded local frame. Successive shears act on the preceding
pass's displaced coordinates, producing changing asymmetric clumps and streams.
Smaller flow scales emerge as the clusters condense; the finite displacement and
gentle inward pull keep the effect local. Its three noise samples have fixed cost
per affected tracer. Particle count, buffer stride, draw count, clock behavior and
the existing toggle/strength controls are unchanged. Visual and performance
testing remain deferred to the user's machine; earlier test results do not qualify
this revision. Visual reference: the 20:24–20:35 segment of
https://www.youtube.com/watch?v=UCgVlHlNWpA&t=1224s (used for appearance only).

## Normal and superluminous supernovae

`VectorGridGPU` in `S-9_COSMOS.unity` has two **Cosmic Supernovae** components,
labelled **Normal supernovae** and **Superluminous supernovae** in the Inspector.
Both reference the existing web and share the same scheduling/rendering code,
with independent controls, random streams and pickup pools. **Frequency Over Age**
curve maps normalized simulation Age (X, 0–1) to a frequency multiplier (Y).
The bell curves start/end at zero and peak at Age 0.5. The saved scene's normal
curve peaks at 1 and superluminous at 0.5; both use **Peak Explosions Per Second**
of 3, giving maximum rates of **3/sec** and **1.5/sec**. Curve previews share a
0–1 vertical scale so the half-height curve stays visible. The superluminous
curve was initialized from the normal curve with both values and tangents halved;
subsequent edits are independent. The Inspector also shows the rate at Age 0.5.
Random
exponential waiting intervals are integrated against the advancing regulation
clock, giving irregular timing instead of synchronized or evenly spaced pulses.
Countdown, stopped regulation time and completed matches do not generate bursts.
Timeline rewinds reset the event schedule and clear owned pickups/flashes.
Warning glows are scheduled ahead by their buildup duration so explosion peaks
still follow the authored Age curve. Warnings too close to the match's end are
skipped.

**Cluster Bias** defaults to 0.92. Sampling uses a compact set of 2,048 actual
web tracers and their current formation/accretion weights, with the same CPU
motion, turbulence and oval lensing as the rendered web. This follows drifting
and merging clusters without a GPU readback or another topology generation.
Early unformed matter stays dispersed; occasional filament events remain possible.
Candidates beyond the playable goal separators or within **Wall Clearance** are
rejected rather than moved away from their source galaxy.

Each event begins with a subtle localized glow that follows its source galaxy.
**Build Up Seconds** defaults to 1.8, with **Build Up Brightness** at 0.12 of
flash brightness. **Flicker Strength** (0.35) and **Flicker Speed** (7) control
smooth, irregular noise during the rise. Setting buildup time to zero restores
an immediate burst. At the peak, the flash and expanding effect
release exactly one existing mass pickup directly above that point on the XZ
gameplay plane. Its direction is uniform around the plane, with **Ejection Speed**
randomized between 2 and 5 world units/second by default. The existing pickup
handles collection rewards, damping, wall bounces and despawn. Normal events use
`ORBITAL/Mass Nugglet.prefab` with a 0.1 reward multiplier; superluminous events use
`ORBITAL/Orbital Mass Nugget.prefab` with the full 1.0 multiplier and existing
pooled lifetime behavior. **Pickup Lifetime** defaults
to 20 seconds. Up to 32 pickups per layer are pooled; each warning reserves a slot until
its explosion. When all slots are occupied or reserved, new events are skipped
without removing a collectible. Capacity is allocated at level start. If the
source galaxy leaves the playable area during buildup, its warning fades away
and releases the reservation without spawning a pickup.

Normal **Die Off Seconds** remains 0.3, with flash duration 0.4 seconds, radius
0.75 world units and brightness 4. **Randomize Color** chooses uniformly between
the existing gold **Flash Color**, slightly cyan white, cyan-blue and light purple.
All four are editable HDR colors. An event retains its chosen color through the
buildup, burst and fade; disabling randomization uses only Flash Color.

Superluminous defaults are radius **1.8** (2.4× normal), brightness **8** (2×),
**Center Flash Seconds** **0.65** and glow die-off **0.75 seconds**. The cloud
has independent timing: **Cloud Expansion Seconds** defaults to **1.8**, followed
by **Cloud Fade Seconds** of **2.4**. The cloud remains alive after the core flash
and localized glow finish. Pickup release still happens once at the initial peak.

The cloud uses moving noise at several scales, warped density lobes and wisps,
mixing blue and warm ejecta throughout its interior. There are no concentric cloud
shells. **Cloud Distortion** (0.85) controls billowing, **Cloud Mixing Speed** (0.45)
controls internal flow, and **Cloud Fine Detail** (0.7) controls finer gas strands.
**Warm Ejecta Color** sets the orange material; **Flash Color** sets the cool gas.
The latter two cloud color/detail labels retain the serialized `shellColor` and
`shellDetail` fields for compatibility. **Telegraph Distortion** (0.85) gives the
buildup an uneven, evolving gas envelope; each event has its own seed/orientation.
Zero restores a round telegraph envelope. Randomize Color is off for this layer
by default, but the same palette option is available. Appearance references:
[explosion sequence](https://scitechdaily.com/images/Type-Ia-Supernova-1.gif) and
[blue/orange color reference](https://dst.gov.in/sites/default/files/Exploring-Super-luminous-supernovae-exploded-rapidly-and-decayed-slowly-01.jpg).
These guide the VFX, not a physical supernova simulation.

The glow and burst share one quad/material per layer; up to 32 warnings/flashes
per layer can coexist. Flash storage and catch-up
bursts are bounded. Disabling the component clears its effects; destruction
releases the pool and generated mesh/material. A fixed random seed is optional.

**MASSIVE > COSMOS > Supernovae > Install superluminous layer** (Ctrl+Shift+Alt+F6)
adds the layer only if missing and saves the active COSMOS scene. Re-running
preserves existing layer tuning. The initial scene backup and installation report
are in `Library/CosmosSupernovae`. The installer checks that existing transforms,
cameras, web and normal-layer settings stay unchanged.

**Apply cloud and oval mask revision** (Ctrl+Shift+Alt+F4) saves the cloud fields
and installs a continuous black cutout through **Cosmos Arena Layout > Mask
Outside Oval**. It reuses `CosmosArenaMask.shader` and the live full-oval dimensions,
including goal caps. One transient quad spans the cabinet view and covers corners
the original rectangular mask strips missed. Its default depth sits just in front
of the cosmic background slab, behind gameplay and world-space HUD. **Mask Y
Offset** on **Cosmos Arena Layout** raises (positive) or lowers (negative) this
continuous mask in world units relative to that automatic height. **0** preserves
the existing position; edits preview immediately and support Undo. The offset
does not resize the oval or move the web, border, walls, cameras, goals or HUD.
It does not reposition the inactive legacy mask strips. The quad/material are
released with the layout. The
previous saved scene is backed up as `Library/CosmosSupernovae/BeforeCloudRevision.unity`.

Validation (2026-10-05, Unity 6000.0.28f1 / DX11): scripts and shader compiled;
**28 Play Mode checks passed**, including half frequency at 201 sampled ages,
49 normal and 21 superluminous releases during the sampled interval, all four
stable per-event normal colors, both reward multipliers, bounded pools, pause,
rewind, reuse and independent disable/re-enable behavior. No runtime errors or
exceptions occurred; the four existing kinematic player-spawn warnings remain.
Actual shader comparison and live gameplay captures were visually reviewed.
The extended checks verify the independent cloud lifetime and cover all 3,292
sampled spill pixels from staged border bursts. All 5,239 sampled HUD/title pixels
remain visible (the existing unit-label pulse is allowed). Mask/no-mask captures
were compared after the startup reveal, including dim score digits and labels.
The saved scene was unchanged by validation. Run **Validate both layers in Play
Mode** (Ctrl+Shift+Alt+F5) from saved COSMOS to repeat the checks; results and images
are in `Library/CosmosSupernovae/validation.txt`, `comparison.png` and `gameplay.png`.
The comparison rows show normal colors, four telegraph seeds, then cloud ages
0.4, 1.2, 2.2 and 3.4 seconds. Border and HUD comparisons use `border-masked.png`,
`border-unmasked.png`, `hud-masked.png` and `hud-unmasked.png` in that directory.
Collection balance, cabinet performance and standalone builds remain untested.

## Level Select icon

`LS_COSMOS Icon.prefab` is assigned to `LevelDefinition-COSMOS.asset`. It displays
the same seeded web at **Age 0.5**, in a fully 3D ovoid with no grid, goals
or flattened background depth. Its independent unscaled motion clock drives the
existing shared flow and irregular cluster turbulence without advancing Age.
Smooth 3D radial compression shapes the cloud in its own local frame. Its default
length:height:depth proportions are 1.8:1:1, giving a rounded cross-section.
A slow optional tumble rotates the complete ovoid about an oblique axis, revealing
its sides and ends with natural foreshortening. The filaments also move internally;
the ovoid contains a volume of tracers, including its interior, rather than a shell.

The default **8,000 tracers** are about 86% fewer than the 56,000-tracer background.
`Cosmic Web Icon Data.asset` bakes the source topology once in the Editor, avoiding
Voronoi generation when entering the menu. One mesh renderer draws GPU-animated
billboards; there is no runtime capture camera/render texture, particle simulation
on the CPU, or per-frame particle-buffer upload. The particle buffer uses 1,024,000
bytes, with an additional quad mesh and the baked source asset. These are budget
reductions, not measured target-machine performance results.

The icon uses crisp, solid circular particles with full unlit emission. Each
surviving fragment outputs its palette color times **Brightness**, with alpha 1.
Binary circular cutouts, blending disabled, and depth writes/testing enabled
give the dots the same opaque rendering approach as NOVA's particles. The gaps
between particles remain open. There is no diffuse halo, haze, radial brightness
falloff or artificial depth dimming. This replaces the former two-pass coverage
and glow technique with one pass using the same 8,000 tracers and mesh/buffer.

**Size Variance** replaces the icon's former Brightness Variance control, preserving
its saved value through `FormerlySerializedAs`. The existing baked density weights
and stable particle seeds produce smaller dust points and larger knots. Zero
gives uniform sizes; increasing it widens the size distribution. Sizes remain
stable during motion, and **Point Size** scales the full distribution. The palette
and global Brightness still control emitted color; no lights are required.
The obsolete Core Opacity and Haze Opacity material controls have been removed.

Solid-particle revision (2026-10-05): Unity scripts and the shader compiled with
no errors or warnings. Isolated previews using the actual NOVA, SINGULARITY and
COSMOS prefabs checked carousel overlap, the standalone cloud, uniform sizes,
and a later motion pose. The icon retains its fixed Age 0.5, 3D ovoid and existing
motion tuning. Automated tests, Play Mode navigation, standalone builds and
target-machine profiling remain deferred.

On the prefab's **Cosmic Web Cloud** child, tune Particle Count (256–8,000), Cloud
Scale, Oval Aspect, Depth Ratio, Point Size, Size Variance, the palette/brightness controls, Motion Speed, Drift Strength
and Cluster Turbulence. **Animate** freezes all motion; setting **Turn Degrees Per
Second** to zero stops only the overall turn. Age is fixed inside the icon shader.
The runtime mesh and buffer are released when the component is disabled/destroyed.

**MASSIVE > COSMOS > Create and Preview Level Icon** opens an isolated animated
preview and creates only missing assets. It preserves existing prefab tuning and
does not save or replace open scenes. The current icon was baked with seed 90210,
Organic Strength 1 and the source's blue/pink palette, brightness 1.5 and turbulence
strength 2. New icons initialize Size Variance from the source's brightness variance;
the current prefab retains its authored variation value of 1.826. The icon creation menu only creates
missing icon assets and assigns the LevelDefinition icon reference.

COSMOS is registered after SINGULARITY in the main `LevelCatalog`, retaining its
authored **STAGE_009** identity and scale exponent 26. Its definition references
`InstructionsPanel-COSMOS.prefab`, which uses the existing instructions layout and
a nested instance of the animated ovoid icon. The panel describes the evolving
cosmic web and the shared amplifier-core scoring objective. The saved
`Assets/Scenes/S-9_COSMOS.unity` is enabled in Build Settings for the existing
Level Select -> Instructions -> selected-gameplay flow. Other catalog entries,
build entries and the gameplay scene are preserved.
Registration was checked against the saved catalog, definition, instructions
prefab and build list. The COSMOS gameplay scene remains byte-identical; a full
Play Mode navigation run and target-machine testing have not been performed.

Icon revision (2026-10-05): Editor scripts/shader imported without errors and an
isolated still preview was reviewed. Automated tests, Play Mode, standalone builds
and target-machine profiling remain deferred to the user.
