# SINGULARITY — folded-grid level

## Playable level and sandbox

- **STAGE_006 / SINGULARITY** is registered in the shared level catalog and Build
  Settings. Level selection opens its dedicated **FOLDED SPACETIME** instructions
  and then `Assets/Scenes/S-7_SINGULARITY.unity`.
- The playable scene uses the standard roster: P1/P3 in 1v1 and all four players
  in 2v2, spawn completion, countdown, shared score-economy duration, timer and
  POSTGAME results. The existing Core/Resonance cycle waits for regulation scoring.
- The saved `SINGULARITY-PROTOTYPE.unity` remains a separate four-player free-play
  sandbox. Changes made in one scene do not automatically propagate to the other.
- **SINGULARITY Level Context** binds the definition, reserved Stage Profile,
  match owner and portal. At match resolution, active portal transits are cancelled
  before the **GameplayObjects** group is disabled. HUD and score state remain
  available for the normal results handoff.
- No enemy, loose-nugget or extra power-up spawners were added: those still need
  their own folded-surface integration before being introduced here.
- `LevelDefinition-SINGULARITY.asset` holds the editable menu metadata, icon,
  instructions and audio references. Scale exponent **4** is an artistic menu
  placement, not a scientific black-hole size assertion.
- **MASSIVE > SINGULARITY > Create Playable Level and Catalog Entry** creates only
  missing outputs; it does not overwrite an existing playable scene or authored
  instructions. **Validate Playable Level Integration** checks saved wiring and
  conventional layout. **Validate Level Select Icon** checks the presentation prefab.

The prototype-specific notes below describe the free-play sandbox; both scenes
share the same folded-grid and portal implementation.

### Integration validation (2026-09-25)

Unity 6000.0.28f1 compiled with no reported errors/warnings. Passed 37 isolated
icon checks, 75 saved-level checks and 48 Play Mode flow checks. The live test
navigated the shared carousel and armed instructions gate, observed normal
spawn/countdown/scoring gating, transferred a player nonlethally through the
black hole, captured a naturally spawned Core at the Light goal (x2), awarded
multiplied points, and reached POSTGAME in both 1v1 and 2v2. Runtime clocks were
shortened for the end test; the shared 1200-second balance setting was not changed.
Hardware controller input and a standalone player build were not tested.

Screenshots in `Assets/Screenshots/SINGULARITY-playable-*` document the menu,
instructions, gameplay and results. The shared instructions title now has a
single-line size limit so long level names fit. A separate pre-existing shared
POSTGAME presentation issue remains: the black LIGHT WINS label extends beyond
its small white backing disc. Its layout was not changed by this integration.

## Intent

One continuous surface wraps from the front face over the top, down a smaller
rear face, and around the bottom to the front. Holding the same vertical input
continues around the loop: up moves visibly down on the rear. Horizontal control
stays screen-consistent. The center black hole pulls a player or Amplifier Core in, stretches and
shrinks them into its event horizon, then ejects them on the opposite face without
damage. A short ripple and optional controller rumble mark the transfer.

Scene: `Assets/Scenes/SINGULARITY-PROTOTYPE.unity`.
Open with **MASSIVE > SINGULARITY > Open Prototype Scene**.

## Current gameplay slice

- Four actual DYNAMO player rigs, their existing controllers, GPU bodies/nuggets,
  attacks, trails and shields. Original movement and mass tuning are retained.
- Both DYNAMO team score HUDs, multiplier displays, score spheres and goal systems.
- Goals and HUDs use the exact DYNAMO-PROTOTYPE layout, also shared by ORBITAL.
  Goal roots are X = -12.23 / +12.23 and maximum rings retain radius 6. No goal
  shrinking or inward offset compensates for the folded grid's rounded corners.
  Player sizes and match respawn anchors also match DYNAMO; all four stay enabled.
- The authored S-P_PROTOTYPING black-hole visual group, with its lethal script
  removed and its old attraction/collision components disabled on this copy only.
  Its visual child is tilted 80 degrees around X to face the overhead camera;
  the portal parent stays fixed and unrotated.
- Four active players in free play using existing Rewired bindings; no match timer
  or menu/roster-driven scene loading.
- Each player pulls its own nearby part of the continuous grid. Front/rear screen
  overlap does not cause cross-face attraction or physical collision.
- The existing Amplifier/Resonance encounter cycle: front-face particle Resonance,
  a physical Core that travels around both folds, and normal capture at either goal.

Imported groups are independent scene copies of the authored prefab rigs, not
new linked variants. DYNAMO, S-P_PROTOTYPING and source prefabs are unchanged.
Shared player rendering has opt-in hooks: no adapter means ordinary rendering.

## Inspector controls

### SINGULARITY - Surface

| Control | Purpose |
| --- | --- |
| Front Width / Height | Flat face dimensions; the standardized scene uses 28 × approximately 9.375426. |
| Rear Scale | Orthographic size reduction; default 0.875. |
| Depth | Front-to-rear separation; default 3. |
| Curl Reach | Breadth of the top/bottom turns; default 2.1. |
| Backside Brightness | Rear-object RGB brightness; default 0.4 (40% brightness). Front stays at 1, with a continuous gradient through each fold. |

Keep the root at unit scale; use these controls instead.
Backside brightness changes color intensity, not opacity or depth. Player bodies,
outlines, attached nuggets, mapped attack trails, shields, the probe and Amplifier
Core share this control. Per-vertex player effects follow the fold under each
sample; the Core uses its surface anchor. Existing front/rear grid colors remain
independent. Front-only goals, HUD and Resonance keep their original appearance.
The full projected footprint, including both curved turns, is **28 × 12**:
X = -14..14 and Z = -6..6. The Inspector reports **Full XZ footprint** separately
from flat-face height. `ConfigureProjectedBounds` fits this extent while keeping
rear scale, depth and curl reach unchanged; existing `Configure` still takes the
flat-face height. Changing curl reach or rear scale later can change the footprint.

### Loop Grid

- **Equal Rear Dashes And Gaps** makes Gap Length follow Dash Length. Disable for
  independent gap tuning.
- Front/Rear Color and Line Width Pixels control the layer treatment.
- **Side Border Color / Side Border Width Pixels** style only the left/right hard
  edges. Defaults are pure white and 3 pixels. At each bend, the front-facing arc
  remains solid up to the crest; the rear-facing arc continues as dashes.
- **Apply Side Border Style To Rear** also styles the rear-facing arcs and rear
  flat side edges. Off by default; either choice preserves their dash/gap pattern.
- The exact top/bottom crest rows use Front Color and ordinary grid line width,
  with no dashes. Bend sampling explicitly includes each crest rather than
  layering a second row on top of a nearby dashed one.
- Both faces have horizontal rows exactly at Z=0 and a vertical column at X=0.
  Rear rows retain proportionally smaller spacing instead of all overlapping
  the front rows. Each bend is sampled symmetrically in front/rear arc sections
  without duplicate joins.
- Cell Spacing, Samples Per Cell and Surface Samples control lattice density and
  sampling. Generated geometry is transient and vertex-count bounded.
- Show Player Attraction affects registered adapters (capacity 16).
- Front strokes write clipped depth: they and opaque front bodies cover rear
  players. Empty cells remain transparent, not an invisible opaque sheet.

### Loop Grid / Goal Border Extensions and Amplifier Goal Treatments

- Four white straight extensions join the unchanged goal semicircle endpoints to
  the exact top/bottom crests of the folded side borders. They inherit the goal
  outline thickness, remain on its overlay plane and follow border deformation.
- The scene-owned **Amplifier Goal Treatments** component connects both existing
  goal capture components to this renderer. Existing shared treatment presets,
  capture absorption timing, traveling packets, held tint/waves and optional
  grid discharge are reused. No second multiplier award or capture clock is added.
- Effects follow the periodic surface through front, turns and rear, rather than
  stopping at the old planar grid's upper/lower limits. `Grid Coupling` and the
  treatment's existing switches still apply. Base side color returns to white
  wherever no Amplifier tint is active.
- The existing Amplifier Treatments preview window can target this scene component.
  Its in-game control overlay is disabled by default.
- **MASSIVE > SINGULARITY > Connect Goal Borders and Amplifier Effects** repairs
  these scene references without changing goal sizes, transforms or shared presets.

### Players / each player / Singularity Player Adapter

- Attraction Radius, Pull and Response Seconds are independent of mass/movement.
  Defaults are 2.145, 0.8 and 0.06 seconds.
- Side Padding contains the body at the left/right limits.
- Render Lift separates player/nugget rendering from its own grid face.

### Amplifier + Resonance - SINGULARITY Spawn Cycle

- Uses the same Core and ordered pattern prefabs as DYNAMO, with the shared timing
  settings unchanged: 6s initial wait, 8s respawn with -2..+2s variation, 30s active
  lifetime and 5s warning. Capture dissolves the pattern before the next cycle.
- **Amplifier Spawn Region** shows the neutral territory and accepted/rejected
  placement candidates in Scene view. SINGULARITY uses the central 45% of the
  flat front face: the inherited 30% strip had no safe sites once the black hole
  and inner Resonance arcs were excluded. It still checks players, goals, solid
  obstacles and the next pattern. Failed placement waits and retries safely.
- **Singularity Amplifier Encounter** connects the surface, spawn region and live
  grid field. `Pattern Reference Size` (28x12) fits each authored pattern to the
  flat front face (28x9.375426), without changing its source prefab. The Core
  spawns only on the front face, then can wrap freely. Goals remain front-only.
- The black-hole spawn-only trigger reserves its effective attraction radius plus
  .2 padding (3.8 units currently), so a fresh Core cannot immediately fall in.
  It is not a physical obstacle; an active Core can enter and traverse the portal.
- **Resonance - Front Face Edit Preview** retains the existing Manifestation
  Inspector's Edit Mode playback and tuning controls. The coordinator's
  **Edit / Preview Scene Option B Animation** button selects it. This copy is
  suppressed during the live cycle; it is not a second runtime pattern.
- Runtime Cores receive **Singularity Amplifier Adapter**. Physics remains in the
  players' unrolled chart; only the separate 3D visual child follows the surface.
  Spawn/capture scale, shell animation, attack impulses and scoring remain owned
  by the original Core. Side reflection uses its existing restitution setting.
- **Loop Grid / Front-face Resonance attraction** exposes a local conversion gain
  (.06), maximum displacement (.65), and fold/side-edge feather (.8). Arc radius,
  strength, pulse and inner/outer softening come from the active pattern. Formation
  and dissolution disable the field; bends and rear never receive front Resonance
  attraction. If Override Grid Falloff is off, this folded renderer uses smooth
  .8/.5 defaults because it has no VectorGridGPU global falloff setting.
- **MASSIVE > SINGULARITY > Integrate Amplifier and Resonance Cycle** installs the
  wiring once; rerunning preserves an existing integration and its tuning.

### Black Hole - Face Passage / Singularity Black Hole Portal

**Grid attraction — visual only** previews immediately in Edit Mode:

- **Enable Grid Attraction:** turns the steady distortion on/off, independently
  of player attraction and teleport ripples.
- **Grid Attraction Radius:** 4.5 front-face units. Does not change the actor
  attraction radius, capture radius, or Core spawn exclusion.
- **Grid Attraction Pull:** 1.25; zero removes it. Radial compression is softly
  limited and remains finite through the center.
- **Grid Attraction Feather:** 1; broad smooth falloff. Lower values keep a
  stronger interior with a narrower outer transition.
- **Grid Attraction On Rear:** on by default; applies the same proportionally
  scaled pattern at the rear mouth. Both flat faces taper to fixed edges, so
  folds, goal joins and hard side borders retain their authored geometry.

The Loop Grid's explicit **Black Hole** reference connects this field. Field-only
tuning updates shader data without rebuilding the lattice. Disabled, missing,
wrong-scene and out-of-face sources clear the field. Extreme overlapping actor
and Resonance settings can still distort the combined lattice.

Gameplay controls remain separate:

- **Attraction Radius:** 3.6, in front-sized surface coordinates. Both faces use
  the same normalized distances. The radius is bounded to reserve flat-face exit
  space when the field is very large or the portal is moved off-center.
- **Attraction Acceleration:** 18; mass-independent pull so changing Rigidbody
  mass does not retune this field. Normal propulsion remains unchanged outside
  the capture circle.
- **Attraction Feather:** 1; a smooth bell-like falloff across the whole radius.
  Smaller fractions keep more strength inside and concentrate falloff at the edge.
- **Swirl Acceleration:** 7; a tangential force. Set zero for pure inward pull or
  negative to reverse the orbit direction.
- **Centrifugal Acceleration:** 2; an independent outward tendency, strongest
  toward the outer field. Very high values deliberately overcome attraction.
- **Capture Radius:** 1.8; begins the controlled entry sequence. This is outside
  **Event Horizon Radius**, 0.575, which matches the current visible black sphere.
- **Entry Seconds / Exponent:** defaults 0.7 / 3.5 (existing scene tuning is
  preserved). Entry is endpoint-led: the nearest end immediately pulls inward,
  then the far end accelerates after it. Exponent controls their timing separation
  rather than making the actor pause before moving. Center travel and axial
  length derive from those same endpoints, preventing an in-place stretch followed
  by a late snap. **Minimum Visual Scale / Maximum Stretch:** 0.045 / 3. Scale
  retains the trailing length until collapse; the minimum is additionally bounded
  so the stretched body fits inside the horizon. Deep captures already overlapping
  the mouth converge without backing out. Exit timing/shape is unchanged.
- **Exit Seconds / Exponent:** 0.6 / 3.5; fast launch followed by a decelerating
  inverse-exponential curve. **Exit Release Speed:** 2.5; integrated into that
  curve so returning Rigidbody control does not introduce a speed jump. Extremely
  high release speeds are capped to preserve a decelerating path.
- **Exit Padding:** 0.6; emergence finishes beyond the entire attraction field,
  on the opposite radial side from entry, before controls resume.
- **Cooldown Seconds:** 0.8 after exit; leaving the field is also required, so
  waiting inside the mouth can never cause repeated back-and-forth transfers.
- **Preserve Screen Velocity** remains for compatibility with direct face
  transfers. Animated exits use their explicitly shaped release velocity.
- Selection gizmos show attraction (blue), capture (orange), horizon (violet)
  and exit-clearance (green) circles on both faces.

Only the two flat faces are eligible; bends are not entry regions. The actual
face swap happens at the center, after full shrink, not at the capture circle.
Every player has independent transit state. Physics contacts and movement inputs
are leased only during the short sequence; original control mode, mass, life,
stun state and movement settings are unchanged. Attacks/shields are cancelled and
power-up ticking pauses while inside. Death, disable, loss of surface, external
match locks and portal reconfiguration release ownership without enabling a dead
or disabled player. Pause freezes sequence timing.

The Amplifier uses these same settings on both faces: pull, swirl, centrifugal
force, accelerating entry, shrink/stretch, horizon transfer and decelerating exit.
Its collision and ordinary forces are suspended only during controlled transit.
A temporary visual parent preserves the Core's own spawn, shell, warning and
despawn animations. Portal passage never awards a multiplier or captures the
Core. Goal capture works normally after exit. Timeout, disable and cycle cleanup
revoke transit immediately, so the portal cannot strand or resurrect a retired Core.

### Black Hole - Face Passage / Singularity Black Hole Feedback

- **Show Ripple / Duration / Radius / Thickness / Color:** expanding energy rings
  at both mouths when the face swap occurs. Defaults are 0.55 seconds, radius 4,
  thickness 0.045, and warm white.
- **Grid Ripple Amplitude / Width:** bounded deformation around each mouth;
  defaults 0.11 / 0.36. Zero amplitude disables the grid portion independently.
- **Enable Haptics / Entry Rumble / Transfer Rumble / Exit Rumble:** local rumble
  on that player's assigned compatible Rewired joystick(s), defaults 0.28 / 0.6 /
  0.2. All commands expire quickly; no global vibration stop can cancel another
  gameplay effect. Keyboard and non-vibrating controllers simply skip haptics.

Core transfers emit the same mouth/grid ripples, without assigning rumble to an
arbitrary player. Rear mouth ripple RGB also follows Backside Brightness.

The body, nuggets and mapped trails share the same opt-in visual deformation;
collider scale and authored radii are not changed. Front-body outline depth is
written only where a visible rim exists, preventing a rear player's transparent
outline area from erasing the front player's border.

### Main Camera

The scene uses DYNAMO's camera position (0, 60, 0), overhead rotation and
orthographic size 9. Auto Frame is off so it cannot silently move or resize the
standard view. It remains available for manual experiments, but enabling it no
longer guarantees standard framing. Extreme aspect ratios need another HUD check.

## Implementation

`SingularitySurface` maps (across, loop distance) onto XYZ. Front is Y=0, rear is
negative Y. Cubic bends use an arc-length lookup, shared tangents and a periodic
seam.

`SingularityPlayerAdapter` retains real controller/Rigidbody physics on an unrolled
periodic XZ chart. Front coordinates match the original field; rear physics lives
on a separate Z strip. Presentation alone folds. Body, GPU nuggets and trails
map vertices through the same lookup as the grid; shield rings map their samples.

Real-controller chart speed is retained. Rear horizontal screen travel is smaller
because that face is compressed, unlike the old probe's tangent-metric correction.

`SingularityGridRenderer` combines bounded, order-independent player attraction
before folding. Shortest loop distance carries attraction across the seam; only
the genuine side edges feather it.

`SingularityGameplaySession` registers four players, opens MatchScoreService and
starts their existing spawn lifecycle without the full cabinet/GameManager flow.

`SingularityGameplaySceneSetup` imports selected groups together from disposable
source-scene previews and saves only SINGULARITY. Re-running it leaves an existing
import root untouched.

The original **Player - Surface Probe** and legend remain disabled. Re-enable the
probe for its isolated Edit Mode scrub controls; actual rigs use Play Mode.

## Deliberate limitations

- Position wraps, but collision shapes are not duplicated across the chart seam.
  Actors just either side of it do not collide until one crosses. Other same-face
  collisions and front/rear separation retain the existing physics.
- Optional detached sword-plasma overlays and legacy player particle renderers
  are disabled on scene copies pending surface adapters. Attacks, mapped GPU trails
  and shields are retained.
- No enemy/free-floating nugget spawning or match timer is included. The black-hole
  field acts only on registered players and Cores, not arbitrary Rigidbodies.
- VectorGridGPU spring simulation and arbitrary impact forces are not automatically
  mapped to this folded renderer. Players, Cores, the portal, front Resonance and
  Amplifier goal treatments have explicit scene-local bridges.
- Future objects need surface adapters and depth-testing materials. Effects that
  deliberately ignore depth do not inherit front/rear occlusion.
  Mesh objects can opt into `SingularityRendererBrightness` with a shader that
  multiplies RGB by `_SingularityBrightness` (neutral default 1). Player GPU effects
  use the shared surface lookup instead; unrelated scenes do not receive dimming.
- Source player palettes are preserved, including their child-rig convention.
- Not added to Build Settings, level selection, progression or GDD.

## Repeatable validation

Under **MASSIVE > SINGULARITY**:

- **Validate Surface Geometry** — six shapes, continuity, normals and finite metrics.
- **Validate Prototype Player** — original probe loops, braking, preview and cleanup.
- **Validate Surface Rendering** — equal dashes, attractors and GPU stroke occlusion.
- **Validate Real Player Adapter** — controller mapping, properties and shaders.
- **Validate Portal Face Mapping** — front/rear entry coordinates and boundaries.
- **Validate Projected Grid Bounds** — exact full-extent fitting, curve extrema,
  repeated fitting, rejected impossible dimensions and cache/lifecycle behavior.
- **Validate Standard Scene Layout** — compares HUD/goal transforms and dimensions,
  player match spawns, camera and full grid footprint against DYNAMO.
- **Match Standard DYNAMO Layout** — undoable, scene-only layout reset; saves only
  SINGULARITY. Preserves custom labels, gameplay components and references. The
  gameplay importer applies the same layout to new scenes automatically.
- **Validate Centered Lattice And Borders** — exact centers, smaller rear
  spacing, independent side styling, preserved rear gaps and GPU depth behavior.
- **Validate Goal Border Extensions** — four exact crest joins, unchanged goals,
  live geometry updates and camera-visible strokes.
- **Validate Amplifier Border Feedback** — actual goal feedback routing, capture
  and held effects across bends/rear, periodic continuity and disabled cleanup.
- **Validate Player Outline Occlusion** — GPU draw-order, transparent ghost,
  front/rear portal deformation and exact shape-restoration checks.
- **Validate Portal Presentation** — shared CPU/GPU deformation properties,
  unchanged gameplay scale/mass and bounded ripple cleanup.
- **Validate Black Hole Transit** — accelerating/decelerating curves, finite
  feathered forces, both-face sequencing, no early transfer, independent players,
  no damage, control ownership, cancellation, re-entry and continuous exit speed.
- **Validate Core Black Hole Transit** — both mouths, non-scoring ownership,
  actual presentation shrink, lifecycle cancellation, collision restoration and
  re-entry. The live `SingularityCorePortalPlayValidation` additionally exercises
  the real spawn cycle, field physics, timeout during both phases and goal capture.
- **Validate Tidal Entry Animation** — immediate inward motion, faster near-end
  travel, trailing length, ordered silhouettes, exact exit-pose handoff, tuning
  bounds and degenerate captures. Players and Cores call the same pose function.
- `SingularityPlayerBrightnessValidation` and `SingularityCoreBrightnessValidation`
  check the front/rear/fold gradient, actual GPU RGB changes, unchanged opacity,
  and preservation of other per-renderer settings.

Black-hole presentation and ownership changes require a fresh Play
Mode check in addition to these isolated checks. Physical controller haptics must
be judged on a connected gamepad; a successful API command is not a felt test.

Checks use temporary preview objects and do not save gameplay scenes.

### Endpoint-led black-hole entry, 2026-09-25

- Player and Core entry now derive center travel and axial length from a leading
  end and a trailing end. Movement starts immediately; the nearer end leads while
  the tail catches up, with collapse delayed until the trailing length permits it.
- Preserved the scene's authored duration, exponent, stretch, horizon and field
  values. Exit behavior, scoring, collision ownership and source prefabs are
  unchanged. The existing Entry Exponent now controls near/tail timing separation.
- Unity compiled. Passed 41 tidal-pose checks, 58 player-transit checks, 35
  Core-transit checks and 18 portal-presentation checks, followed by 50 live Core
  checks covering both-face transfers, pause, cleanup and normal goal capture.
- Inspected native Play Mode camera renders of the real player and Core at
  start, quarter, half and three-quarter entry. Both then completed their live
  exit on the rear face, restored normal visual shape and returned to dynamic
  physics. Final Console contained no warnings or errors.
- Returned Unity to Edit Mode with time scale restored to 1; no temporary test
  objects or runtime changes were saved. No standalone build or physical gamepad
  test was run.

### Steady black-hole grid attraction, 2026-09-25

- Added independent visual controls on the existing portal; wired the current
  scene and future gameplay-scene setup without changing actor force settings.
- Unity compiled; 110 focused radial, mapping, lifecycle/binding and GPU checks
  passed, plus 23 existing renderer, 24 Resonance-field and 58 transit checks.
- Native Edit Mode camera render inspected with both-face attraction, Resonance,
  folds and fixed borders. Scene saved; no Play Mode or standalone build was
  needed/run for this stateless rendering change. Final Console was clean.

### Backside readability and Core portal integration, 2026-09-25

- Surface-level Backside Brightness defaults to 0.4. Fold gradients affect RGB,
  not alpha, collider size or the existing front-over-back depth rules.
- Unity compilation and GPU validation passed: 73 player-brightness checks,
  77 Core-brightness checks, 35 Core-transit checks and the existing 58 player
  transit, 18 portal presentation and 79 wrapping-Core regression checks.
- Passed 50 live Core checks in SINGULARITY: automatic field pull/swirl, shrink
  and stretch, pause, front-to-rear-to-front travel, unchanged score during transit,
  normal goal award afterward, real timeout during both entry and exit, and portal
  disable cleanup. Runtime timing overrides were restored. No Console warnings
  or errors were reported by this run.
- The existing 19-check live encounter regression also passed, including both
  goals, fold/seam momentum, dissolution and replacement. Native camera renders
  inspected front/rear/fold brightness and stretched entry/rear exit. Unity was
  returned to Edit Mode without saving test objects, scores or timing overrides.
- Core spawn exclusion follows the full attraction field (currently radius 3.8
  including padding). Existing shared timing, source prefabs and other levels
  are unchanged. Future arbitrary object types still require a surface adapter.
- No standalone build or physical gamepad/haptic test was run for this change.

### Amplifier / Resonance integration, 2026-09-25

- Unity compiled without Console warnings/errors. Passed 51 spawn-placement,
  51 spawn-rule, 79 wrapping-Core and 24 front Resonance grid-field checks, plus
  the existing 235 border/rendering/goal-effect checks. Standard goal/HUD layout
  validation still passes. Scene contains zero missing scripts.
- All 100 deterministic placement batches found a safe neutral position with the
  scene's actual pattern, black-hole exclusion and original clearance settings.
- Passed 19 live checks: paused formation, collision gating, safe placement, one
  owned pair, Core motion into a fold, rear depth/physics separation, seam momentum,
  actual captures and multiplier awards at both goals, dissolution, respawn and
  cleanup. Shorter waits used by the test were runtime-only and restored afterward.
- Native camera renders inspected the front pattern and its grid attraction,
  the rear Core, players, black hole, UI and goal treatments. Unity returned to
  Edit Mode with shared timing and the scene preview restored. No runtime awards
  or test objects were saved; source prefabs and other levels were not edited.
- No standalone build, performance benchmark, physical controller session or
  exhaustive seam-contact test was run. The inherited chart-seam limitation above
  also applies to the wrapping Core.

### Fold styling refinement, 2026-09-25

- Side-border solid/dashed ownership now changes at each actual projected crest,
  so only the front-facing arc is solid. Rear styling includes the rear arc, not
  just the flat rear face, and its color/weight toggle never fills dash gaps.
- Both crest rows are explicit, unique front-style strokes. Sampling splits each
  bend at its crest, retaining centered face rows, rear cell scaling, symmetric
  turn spacing and the existing 200,000-vertex budget. Surface geometry is unchanged.
- Existing scene colors, line widths, rear-border toggle and shared Amplifier
  settings are preserved. No scene rewrite or Play Mode session is needed for
  this rendering-only refinement.
- Unity compiled without Console warnings/errors. Passed 92 lattice/border GPU
  checks (including isolated front/rear fold halves and continuous crest rows),
  23 rendering checks, 91 Amplifier checks and 29 goal-extension checks. Native
  scene camera render inspected; standard goal/HUD layout still matches DYNAMO.

### Hard borders and Amplifier feedback, 2026-09-24

- Unity compiled the scene-local border/extension components and folded feedback
  shader. Focused validation passed 565 checks: the prior 406, 39 centered-lattice
  and border GPU checks, 29 goal-extension checks, and 91 Amplifier checks.
- Native camera renders inspected the four joins, full solid white bends, smaller
  rear lattice, and rear styling on/off with gaps preserved. The four short joins
  use separate Shapes draw commands to avoid a verified instanced-line rendering
  failure in this project's D3D11 configuration; shared Shapes settings are unchanged.
- Play Mode instantiated two temporary physical Core prefabs at the real goals.
  Their normal spawn/capture lifecycle advanced both teams from x1 to x2 and
  triggered the same treatment clock used by each goal. Measured nonzero feedback
  on front, top bend, rear and bottom bend for both teams; held tint was visible.
- Shared treatment presets were not edited. In the current shared preset,
  Allow Effects Over Grid is off, so boundary packets/held effects remain active
  but optional interior discharge is suppressed. Isolated tests cover discharge
  when enabled, including continuity where both fronts meet on the rear.
- Standard-layout validation still matches all 135 transforms and 28 shape
  dimensions to DYNAMO, with full grid footprint 28x12 and unchanged camera/goals.
- Play Mode ended without saving runtime objects, multiplier awards or toggle
  changes. Rear hard-edge styling remains off by default. No Console errors or
  warnings were present after the test. No standalone build was produced.

### Standard layout update, 2026-09-24

- DYNAMO and ORBITAL agree on 28x12 grid bounds, goal/HUD layout and camera framing.
- Matched 135 transforms, 28 shape dimensions and 40 UI text-size/font settings.
  Inactive reference players' parking positions are deliberately not copied;
  their authored match respawn anchors are the positions used for the four players.
- Independent 32,768-step sampling measured X=-14..14 and Z approximately
  -6.000001..6.000001 (float precision), with unit root scale and unchanged depth.
- Layout Undo/Redo restored and revalidated correctly. Edit Mode camera render
  inspected with the standard goals left deliberately unmatched to rounded corners.
- Black-hole field remains radius3.6 and exit distance4.2, inside the newly fitted
  flat-face half-height4.687713; no portal tuning was changed.

### Animated black-hole update, 2026-09-24

- Unity compiled the new sequence, feedback, shader mapping and outline depth pass.
- 368 focused checks passed: the existing 254 checks, 58 transit checks,
  18 presentation checks and 38 GPU outline/deformation checks.
- Scene references and authored field/capture/exit values verified after saving.
- Console contained zero warnings/errors after the final validation run.
- Fresh Play Mode visual/physics review is pending user approval. Haptic strength
  has not been physically verified on a gamepad.

### Integration verification, 2026-09-24

- Unity 6000.0.28f1 compiled scripts and affected shaders.
- All 254 focused checks passed: 121 surface, 48 original probe, 32 real-player
  adapter, 30 portal mapping and 23 rendering checks (including GPU occlusion).
- Real controller traversed all four regions: 32.95 chart units in 6.19 seconds;
  maximum sampled attraction lag 0.151.
- Live black-hole transfers passed front-to-rear and rear-to-front. Staying inside
  did not re-trigger. Player mass and life state were unchanged.
- Native renders inspected player/nugget appearance, front/rear overlap, front
  stroke occlusion, equal dashes and HUD/goal placement.
- Test awards updated both HUDs (25 eV / 15 eV). Real temporary Cores were captured
  at both goal mouths, advancing both multiplier displays to x2.
- Play Mode Console: zero warnings/errors. Runtime test objects cleaned up and
  Unity returned to Edit Mode.
- Movement tests used scripted input through the real controller, not physical
  gamepads. No standalone build, full match or exhaustive combat test was run.
