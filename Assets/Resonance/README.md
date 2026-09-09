# MASSIVE resonance obstacles

## Current model

The pattern has three independent roles:

1. **Contact profile:** the solid or soft-interaction portion of an exposed arc.
2. **Resting energy:** a bright filament, broader ribbon, and diffuse noisy haze.
3. **Full-pattern ghost:** the underlying complete shape, revealed briefly by a Core impact or magnetic turnaround. It never creates colliders or grid forces.

The 345 Hz reference is an art-directed layout, not an acoustic simulation. The outer exposed sections are the **concave** portions at 0/90/180/270 degrees; the inner circle's arc exposure is independent. Score, goals, capture, attack impulses and multiplier authority are unchanged.

## Start and layout

Select **Resonance 345 Hz Prototype** in `S-8_DYNAMO-PROTOTYPE`. The portable prefab is `Assets/Resonance/Resonance 345 Hz.prefab`. **MASSIVE > Resonance > Create 345 Hz Prototype** creates another instance without overwriting the definition.

- Keep Transform scale at one and the plane parallel to world XZ. Use **Pattern Scale** and **Rotation Degrees** for placement.
- **Layout Fit / Rounded Rectangle** uses fitted cubic segments. **Curvature Preservation** controls how much local rounding is preserved instead of stretching tangents; zero matches stretched curvature. Existing scale, shape, exposure and per-arc edits are retained.
- **Fit to Arena Bounds** is an explicit optional operation, not an automatic layout reset.
- **Select Pattern Definition** opens curves and arcs. Arc start/sweep angles expose portions of a curve. Closed Polyline supports traced future patterns. Turning Exposed off, or setting arc opacity/intensity/thickness/sweep to zero, removes its contacts and attraction samples.

## Global and per-arc taper

The controller has **Global Visual Profile** and **Global Collision Profile**, each with its own **Enabled** override. They supersede corresponding fields across arcs using runtime copies; the shared definition is never overwritten. Turning an override off restores the authored arc settings.

Both profiles expose thickness, taper fraction, taper length, falloff power, and falloff shape (Smooth, Linear, Smoother). Positive **Taper Length** is the distance per end in fitted pattern-local units; it supersedes the fraction and is capped at half the relevant path. Zero uses **Taper Fraction**. Higher power produces a narrower taper.

**Override Collision Extent** separately applies global Collision Start/End fractions within each exposed arc. Per-arc Independent Collision Profile remains available, including its own start/end, thickness, length/fraction and falloff.

Collision thickness is capped by the visible filament/ribbon footprint. The diffuse haze does not enlarge the hard contact area. **Show Collision Gizmos** draws generated contact prisms in orange when selected.

## Option A / Option B rendering comparison

The scene's **Resonance — A-B Comparison** object switches between the original **Option A / Continuous Plasma** and the separate **Option B / Particle Grains** prototype. Use its buttons, then **Select Active Tuning**. Only one complete prototype is active, including its interaction driver and grid attraction. Do not run separate unpaired duplicates over the same arena.

Option A retains its original controller settings, pattern asset and prefab. Option B starts with a copy of that scene's current tuning and an **independent** `345 Hz Pattern - Option B.asset`, so experiments do not edit A. The portable B prefab is `Assets/Resonance/Resonance 345 Hz - Option B.prefab`. The creation menu is **MASSIVE > Resonance > Create Particle Option B from Selected**; it does not overwrite an existing B prefab. Save the scene to persist the comparison objects and selection. Play Mode changes are temporary.

Option B takes inspiration from the fine appearing/disappearing grains in the [Chladni plate reference](https://www.youtube.com/watch?v=IvOawAKX9Is). It uses real Unity Particle Systems for each filament, ribbon and diffuse population, the full-pattern ghost, and the optional center node. Camera-facing **individual circular grains**, rather than continuous billboard strips, sample the same authored paths and shared energy/contact equations as A. It is an art-directed particle rendering, not a simulated plate, sand solver or volumetric cloud. Tiny gaps between dots do not open gaps in the underlying collision profile.

Expand **Particles** on the B controller:

- **Filament / Ribbon / Diffuse / Full Pattern / Center:** particles per unit, dot size, size variation and extra brightness. Existing global layer width, color, opacity, softness, taper and energy controls still apply. Density is measured along the fitted curve; center density uses an equivalent extent of eight times its radius.
- **Particle Budget:** combined hard pool cap, including hidden full-pattern grains. Current first-pass cap is 48,000, with 46,625 grains for the copied scene layout. If requested density exceeds the budget, all populations scale down proportionally. Zero density hides that population without changing collisions or grid attraction.
- **Lifetime / Lifetime Variation:** how often individual grains recycle. **Presence** sets the visible fraction of each cycle. **Birth Death Feather** chooses quick popping versus gentler fade-in/out.
- **Grain Wander / Grain Motion Speed:** small smooth motion around the curve. **Dot Edge Softness** softens each circular dot; **Seed** changes the distribution reproducibly.

The controller owns the generated systems: tune it, not the transient Particle System modules. Pools are allocated only on rebuild, then held paused; the shader handles logical lifetimes, random repositioning at rebirth, wandering and visibility, without per-frame particle upload or object spawning. Paused native systems are intentional. Core compression/flare/traveling pulses and player diffusion/wake/recovery use the same event buffers and parameters as A. Full-pattern grains remain hidden until a Core reveal (or the existing preview toggle), and never create contacts or attraction.

More grains and larger dots increase rendering cost and can merge into solid-looking bands. Keep dots small at the gameplay camera distance and raise density before making them large. Alpha, additive, premultiplied, screen and multiply modes remain available; multiply is generally unsuitable against black. Target-device profiling is still required.

**Validation:** 22 dedicated particle/lifecycle checks, the existing 105 Resonance checks, 17 grid-smoothing checks, and ten before/after Option A GPU comparisons (five blend modes, resting and ghost) passed. The A shader comparisons had zero channel differences. The B prefab contains only authored settings, no generated geometry or missing scripts. Play Mode rendering has been checked at gameplay distance and close-up, including the local Core pulse, full-pattern reveal and player wake. An isolated B fixture using the actual Core prefab recorded one magnetic turnaround, one full reveal and one local contact. A registered player produced a wake, reached a 0.5 sludge multiplier and recovered to 1.0 with zero lingering player contacts after exit. Temporary fixtures were removed; Unity was returned to Edit Mode with B selected and the existing dirty scene left unsaved. No standalone build or target-device performance certification was performed.

## Resting energy and blending

The controls below are shared by both options; the continuous-rendering description refers specifically to Option A. Option B samples the same field with the grain populations described above.

**Energy Flow** offers From Pattern Center, Along Arc (the older traveling effect), and Still. From Pattern Center uses distance from the origin to make equal-radius wavefronts expand outward. Equal-radius sections may breathe together; they should not appear to chase around the arc. **Energy Origin** can use another transform such as the grid; unassigned means this controller. Tune **Energy Wavelength**, **Energy Flow Speed** and **Organic Motion**.

**Filament**, **Ribbon**, and **Diffuse** each expose width relative to arc thickness, opacity, brightness, softness, and an **HDR Color** picker. Colors are global per layer. **Arc Color Influence** is zero by default (use the layer colors directly); increase it to multiply in the definition's arc colors. The ghost retains its separate Reveal Color.

All three layers use **continuous curve-distance rendering** on one flat coverage quad per arc. The shader measures distance to the curve instead of interpolating a thick segmented strip. This removes triangle-diagonal zigzags and folded/overlapping inner edges at tight bends without camera-facing billboards. It is still a planar effect, not volumetric plasma. **Plasma Curve Samples** bounds curve evaluation to at most 128 points per draw; increase only if the actual contour looks faceted. Wide coverage and held full-pattern ghosts cost more pixel work than thin strips, so this still needs target-hardware profiling.

**Plasma Displacement**, **Noise Scale**, **Noise Speed**, **Fine Detail**, and **Tip Boost** control smooth, domain-warped electrical motion. The filament moves most; the broader ribbon and haze move less. Tip Boost raises relative activity toward the tapered ends without moving collisions. Organic Motion controls the overall animation strength. The third layer remains procedural shader haze, not emitted particles.

**Blending** applies to this controller's transient material, not the shared material asset:

- Alpha: conventional transparency.
- Additive: luminous accumulation.
- Premultiplied: soft compositing with premultiplied color.
- Screen: brightening without conventional alpha darkening.
- Multiply: darkens existing pixels; usually invisible on black backgrounds.

The impact ghost has its own thickness, opacity, color and duration, but shares the selected blend. **Preview Core Impact — Full Pattern Flash** tests a one-shot reveal. **Preview Full Pattern** holds it visible; turn it off for collision-only behavior. A held ghost never changes physics or attraction.

## Grid attraction

Enable **Attract Grid**. Assign **Grid** explicitly, or leave it unassigned to use the current VectorGridGPU instance. Attraction runs in Play Mode via the existing additive grid-force queue; it does not replace player, Core or environmental forces.

- **Grid Attraction Strength:** pull toward exposed arc samples.
- **Grid Attraction Radius:** influence distance in world units.
- **Override Grid Falloff** (on by default): gives only this pattern its own attraction profile. Turn off to compare the previous shared `VectorGridGPU / Force Falloff` behavior; the two local settings below are then ignored.
- **Outer Feather** (default **0.8**): fraction of the attraction radius used for the outer fade. At 0.8, full outer weight extends through the first 20% of the radius, then smoothly fades across the remaining 80%. At 1 the fade spans the whole radius; 0 deliberately gives a hard cutoff. The new profile always reaches zero at the radius, independently of the global falloff mode.
- **Inner Softening** (default **0.5**): fraction of the attraction radius over which pull diminishes toward zero at each sample center. Higher values spread the reversal over a broader region and reduce concentrated pinching; 0 disables it. This is not a full-strength inner plateau. It scales with each arc's Grid Radius Multiplier. With radius 1.5 and multiplier 1, the default softens the inner 0.75 world units.
- **Grid Sample Spacing / Budget:** approximation quality, capped at 128 samples per controller. The Inspector displays the actual count. Forces are length-weighted so changing density does not simply multiply their strength.
- **Grid Energy Pulse:** subtle modulation of attraction, outward from the energy origin.
- Per-arc **Grid Attraction Multiplier / Grid Radius Multiplier:** tune individual portions; zero disables that arc's attraction.

Hidden/unexposed portions and the full-pattern impact ghost emit no forces. This is a bounded sampled attraction field, not a continuous curve constraint. Tune radius and strength against the grid's spring/damping; high strength can distort nearby cells considerably. Transform scale should stay at one.

Resonance samples use **balanced radial accumulation**: overlapping sample forces are summed before normalization, so their list order cannot steer the grid sideways. This is an opt-in force flag; existing player/impact force behavior is preserved. Other actors, boundaries, unequal arc settings and grid sampling can still make the full gameplay field asymmetric.

The local profile uses smooth, zero-slope transitions at the feather/softening boundaries. Inner softening attenuates the force separately from overlap weight, so the grid's force cap cannot normalize the softening away. Exact sample centers keep their overlap contribution while exerting zero radial force. The existing force-buffer layout is unchanged. The profile does not modify global Force Falloff, arena Boundary Feather, player/Core responses, collision geometry, visual taper, or the full-pattern ghost.

Feathering shapes the simulated attraction; **VectorGridGPU / Curve Smoothing** shapes its rendering. They complement each other. Strong attraction, narrow radii, sparse arc samples and other actors can still deform the grid; these controls are not a guarantee against every fold. Neither control increases grid resolution.

## Player, enemy and Amplifier Core responses

**Player Response** and **Core Response** are independent global actor policies. **Pattern Default** preserves each arc's original behavior. Visual Only arcs always remain non-interactive.

Player options:

- **Hard Wall:** solid player contact on interactive arcs.
- **Pass Through:** no obstacle contact or legacy arc field for players.
- **Sludge:** traversable contact with a feathered slowing band. **Sludge Movement Multiplier** reduces propulsion and the normal speed cap; **Sludge Drag** damps existing planar momentum. **Interaction Reach** feathers the slowing region beyond the contact surface. Overlapping patterns choose the strongest movement slow, and exit/disable removes only that pattern's influence.

**Enemies Use Sludge** is enabled by default. Drones and Dyson Spheres pass through
interactive Resonance surfaces using the same **Sludge Movement Multiplier**,
**Sludge Drag**, and **Interaction Reach** as players. This includes Drone pursuit
and idle movement, Dyson pursuit and lunges, and drag on existing recoil momentum.
It is independent of Player Response; disabling it restores the enemy's normal
obstacle/collision behavior. Normal walls, Amplifiers and other physical obstacles
retain their collision/avoidance behavior. Visual Only arcs never slow actors.

Enemy speed influences use the same strongest-source rule as player influences.
They clear on exit, pause, disable, removal, or interaction gating during
formation/dissolution. The shared `EnemyBase.ExternalMovementMultiplier` is the
speed input for future enemy movement controllers. The driver discovers enabled
EnemyBase actors from a registry and handles both solid Dyson bodies and the
Drone's trigger-only body; it does not perform a scene search every physics step.

Enemy sludge is covered by `MASSIVE > Enemies > Drone > Run Prototype Validation`.
The 85-check run includes generated Resonance geometry, both enemy prefabs,
slowdown/recovery, Dyson lunges, overlapping fields, collision-filter cleanup,
ordinary walls beyond the field, and the existing player sludge response.

Core options:

- **Pattern Default:** the arc's original hard bounce, lens or damping response.
- **Magnetic Repulsion:** removes the hard collision for the Core only and uses the global **Magnetic Path** described below. Its turnaround triggers the same full-pattern reveal.
- **Pass Through:** ignores obstacle contacts and legacy arc fields for the Core.

The **Magnetic Core — global path** controls apply to every arc, independently of legacy per-arc Lens Deflector settings:

- **Curved Glide** (new default) retains tangential movement while reversing the inward normal velocity. The approach is parabolic in a fixed surface frame, producing a rounded turn rather than a V-shaped full stop. **Tangential Carry** controls retained sideways momentum; **Surface Follow** bends the trajectory with the wall's changing normal. A perfectly head-on hit has no tangential momentum and can still return straight back.
- **Stop And Reflect** retains the previous full-stop response for comparison.
- **Magnetic Reach** controls only the Core's soft-field width (default 0.8 world units). It is separate from the existing player Sludge Interaction Reach.
- **Magnetic Brake Seconds / Release Seconds / Speed Retention** tune both paths. Fast approaches shorten braking to stay on the entry side. Wider reach provides more room for a readable bend. A predictive normal-velocity limit prevents retained tangent motion from driving into the curved contact surface; positions are not teleported.

The runtime driver uses the existing active Core list and lightweight player/enemy registries. It applies/restores collider-pair filtering locally; it never edits the project's collision matrix. Presentation-only/spawning/captured Cores and pseudo players do not receive the new soft response. Soft response respects the field layer mask, collision matrix and wall height.

Legacy per-arc **Lens Deflector** turns planar velocity toward the curve tangent (negative deflection selects the normal). **Damping Membrane** exponentially damps planar velocity. Their **Fields Affect Only Amplifier Cores** toggle remains specific to these legacy fields; explicitly selected Sludge/Magnetic policies are independent of that toggle. Legacy thin trigger fields can still be skipped at extreme speeds.

Magnetic motion is an art-directed response, not a physical electromagnetism solver. It preserves incoming/outgoing directional readability without altering scoring or goal acceptance. Test corner cases, multiple overlapping patterns and competitive balance during gameplay.

## Localized contact effects

The controller's **Localized Contact Effects — visual only** section adds two independent, enabled-by-default effect sets to either renderer. They do not move collision geometry, modify magnetic/sludge response, or add grid forces.

**Core Contact Visuals**:

- Magnetic braking gathers haze and tightens/brightens the filament around the nearest point on the contacted arc.
- The actual magnetic turnaround releases a brief white-hot flare and two broadening pulses traveling in opposite directions **along that arc**. Hard-wall Core impacts use the same release. The existing full-pattern ghost is triggered separately; disabling local effects does not disable it.
- Incoming speed scales intensity. Direct approaches are stronger than glancing ones; tangential motion biases pulse brightness and travel speed toward that direction.
- **Strength, Reach, Compression, Flare, Pulse Strength, Pulse Speed, Pulse Width, Recovery Seconds, Reference Speed** are independent controls. Reach is the contact envelope along the arc; pulse travel is Pulse Speed × elapsed time, fading during Recovery Seconds. Partial arcs retain their authored taper; full-circle arcs wrap pulses across the seam.

**Player Passage Visuals**:

- Passing through locally diffuses the filament, stretches the haze in the direction of travel, and adds a short noisy wake. The filament reconnects smoothly as the disturbance recovers.
- Sludge sustains one disturbance while the player remains in the band, including at low speed; it does not repeatedly flash. Leaving or removing the actor lets that disturbance decay. Player passage never reveals the full pattern.
- **Strength, Reach, Filament Diffusion, Wake Stretch, Wake Noise, Attack Seconds, Recovery Seconds, Reference Speed** control the response. Hard-wall players do not get a passage effect. Pass Through, Sludge and traversable Pattern Default arcs do. Visual Only arcs may react visually but remain physically non-interactive.

Reach and pulse distances use **local pattern units**; retain Transform scale at one. Wake Stretch uses multiples of arc half-width. Reference Speed uses world units per second. Each arc retains at most four Core events and four player wakes; oldest entries yield at capacity. Transient state is discarded on rebuild/disable. A swept player query detects fast crossings; large teleports are excluded. Gameplay contact clocks freeze when Play Mode is paused.

Below the Inspector, choose **Local Preview Arc**, then use **Preview Local Core — Compression / Flare / Pulses** or **Preview Player — Passage / Wake / Recovery**. Both work in Edit and Play Mode, without spawning actors or changing physics. The separate full-pattern-only preview remains available. The player preview holds a localized passage briefly, then releases it; real passage follows the player's location and direction.

## Ownership and persistence

- `ResonancePatternDefinition`: shared curves, arc exposure and local tuning.
- `ResonancePatternController`: layout, global overrides, presentation, geometry and grid samples.
- `ResonanceSegment`: sampled arc geometry, nearest-contact queries and legacy fields.
- `ResonanceInteractionDriver`: transient player/Core policy, pair-filter cleanup and magnetic/sludge state.
- `ResonanceContactVisuals`: bounded per-arc contact presentation and nearest/swept queries against the rendered curve; no physics authority.
- `PlayerControllerScript`: reusable source-owned movement influence hook; existing movement logic applies the multiplier.
- `ResonanceRibbon.shader`: Built-in pipeline three-layer procedural energy.
- `VectorGridGPU` / `GridUnlitLines.compute`: opt-in order-independent accumulation for continuous-field samples; legacy force policy remains unchanged.

Generated children, meshes and blend material are DontSave and cleaned up on rebuild/disable. Save the controller and definition settings, never generated geometry. No packages, render pipeline, global physics layers, or project settings are required.

## Validation

**MASSIVE > Resonance > Validate Local Contact Effects** runs the 31 targeted contact checks; these are also included in **Run Prototype Checks**. The local-effects pass passed **105 Resonance checks** and **17 grid-smoothing checks**, with no Console errors or Resonance shader messages. Tests cover finite/swept curve queries, direct/glancing intensity, per-body deduplication, bounded concurrent events, sustained sludge/recovery, disabled effects, ghost isolation, and unchanged contact topology/grid sampling.

A temporary off-arena Play Mode fixture used the actual Amplifier Core prefab and a registered player: one magnetic turnaround produced one local impact and one full-pattern reveal; approach compression and player passage were observed. Sludge reached a 0.50 movement multiplier and returned to 1.00 after exit, with zero remaining player visual contacts. Close-up camera captures inspected resting energy, compression, flare, separated traveling pulses, and the directional player wake. This is targeted runtime/render validation, not a full-match performance or standalone-build certification.

The hard-wall fallback was also exercised with the actual Core prefab: one local impact and one global reveal, with the body remaining on the entry side. Temporary fixtures/camera were removed, capture images were moved outside the project, and Unity was returned to Edit Mode. The user's 3.24 × 1.50 pattern layout, 264 colliders, strength 55, and 0.8/0.5 attraction feathering were retained; the scene was not saved automatically.

**MASSIVE > Resonance > Run Prototype Checks** runs isolated editor checks for geometry, taper overrides, definition preservation, lifecycle, bounded grid samples, all five blend modes, shader material ownership, legacy fields, both magnetic paths, movement-source cleanup, and a rigidbody hitting a wall at 20 units/second. It also tests bounded distance-field data, independent layer colors, and actual compute-shader force probes. The suite includes 20 new falloff checks for inner/outer continuity, compact support, overlap normalization, legacy-force compatibility, radius scaling and mirrored cancellation. Run just those checks with **MASSIVE > Resonance > Validate Grid Attraction Falloff**.

The falloff pass passed **74 Resonance checks** plus the **17 grid curve-smoothing checks** in Unity 6000.0.28f1 on Windows Editor. Play Mode confirmed 58 locally profiled attraction samples alongside legacy actor forces; disabling the override restored 58 inherited balanced samples. Collider count remained 264 in the prototype, and existing strength (55), radius (1.5), layout and 8×/8× grid resolutions were retained. Local 0.8 outer / 0.5 inner and a broader 1.0 inner setting were inspected visually. Broader softening reduced concentrated pull, but strong-field folds were not completely eliminated. Unity was returned to Edit Mode with the initial local defaults enabled and the user's scene left unsaved.

The prior Stop And Reflect acceptance pass decelerated an actual Core below 0.001 units/second and recorded one turnaround/reveal. A player in Sludge reported a 0.30 multiplier and returned to 1.00 outside the field.

The September 7 Curved Glide pass launched an actual Core at a curved wall. It retained about 2.8 units/second through the normal-velocity reversal, followed a rounded measured path, stayed on the entry side, and recorded exactly one turnaround/reveal. Temporary fixtures and the close-up QA camera were removed; Play Mode was stopped.

Confirmed defect evidence: the original GPU cap test produced -0.016 or +0.016 lateral velocity solely from reversing two identical opposing forces; the balanced version produces zero in both orders. The old wide east/west strips each contained eight inverted triangles and up to 0.143 local units of triangle-diagonal centerline error. Their replacements use non-overlapping coverage quads and shader-evaluated curve distance; the original close-up stepping/fan artifacts were retested visually.

The exposed-arc attraction and three-layer rendering were inspected in Game View. No new Console or shader errors were present. No standalone player build or full-match balance/performance certification was performed.
