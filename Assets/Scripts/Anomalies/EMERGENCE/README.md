# EMERGENCE

Select **EMERGENCE** between ORBITAL and PULSAR in the Level Select Carousel, or open `Assets/Scenes/S-10_EMERGENCE.unity` directly. Its characteristic scale is 10⁻⁶ m, so it follows ORBITAL (10⁻¹⁰ m) and precedes PULSAR (10⁴ m). It uses the standard instructions and gameplay flow; existing stages and build indices are unchanged.

The carousel and instructions use a complete 3D organism icon with opaque, unlit particles. A small 3D relaxation uses the ecology's attraction matrix, separation band, soft core, drag and relative species sizes to form the colony; there is no cutaway or hand-arranged membrane/motor belt. **Match Ecology Composition** uses the combined structure/motor/catalyst counts as a budget and distributes it according to the gameplay profile's body-species proportions. The nutrient count remains separate. Distant nutrients are brought near the edge for framing. This is a 3D interpretation of the 2D chemistry, with fed motors and no metabolism, rather than an exact gameplay recording.

The settled formation is baked into the prefab so opening the menu does not run the relaxation. Profile force edits regenerate the preview; use **Bake Formation From Ecology** in the icon Inspector, or the existing **Enable Level and Update Organism Icon** menu command, to save the updated shape. Breathing, internal ripples, slow rocking and intermittent pivots/recoil/settling run inexpensively in unscaled menu time and stay inside a fixed envelope. Tune their strength and timing on **LS_EMERGENCE Icon > Living Colony > Emergence Level Icon**. The root stays anchored, there are no pickups or colliders, and `Animate In Editor` previews the motion while editing the prefab. The authored particle count and base size remain independent of the gameplay population/scale.

**Local Particle Flow** adds continuous internal circulation with layers moving at different rates/directions and smaller shared eddies. Particles move relative to their neighbors even with rocking, breathing and pivots disabled. Tune **Flow Speed**, **Flow Shear**, **Eddy Degrees**, **Eddy Scale** and **Surface Flow** on the icon. The currents preserve each particle's distance from the center and the force-shaped radial color arrangement, so they stay inside the existing icon envelope. These are lightweight shader currents, independent of gameplay physics. Changing their settings needs no rebake; zero Flow Speed freezes circulation in place and Animate stops all icon motion.

The instructions icon sits at the standard central position above the grid, using the same `(0, 5, 0)` placement and `5.8` scale as ORBITAL and COSMOS. The middle-right pane shows a camera close-up of two seeded, mature organisms using the production CPU ecology model and particle material. They consume nearby blue nutrients, pulse, grow attached buds and release the canonical reward visuals. The eight-second demonstration cycles through matter/amplifier nugglets and nuggets. It starts with existing food reserves/waste to show secretion promptly; the saved gameplay ecology and its chemistry are unchanged. At each repeat, particles and rewards ease to zero size before the new organisms grow in.

Tune the loop, lead-in, transition and reward hold times on **InstructionsPanel-EMERGENCE > Organism reward demonstration**; tune the crop on its **Cropped gameplay camera**. The remote demonstration stage uses its own profile copy and 296 particles. Its rewards remain noncollectible, with disabled gameplay scripts and colliders, and never register with the pickup system. It explicitly releases reward materials, meshes and simulation state when disabled.

The scene uses the saved PULSAR level as its template. It retains the universal players, Rewired bootstrap, match clock, score economy, goals, HUD, power-ups, enemy timeline, Amplifier/Resonance encounter and match-end effects. PULSAR's body, attraction, rhythm prototype and central exclusion are removed. PULSAR itself is unchanged.

## Particle soup

Population and appearance are configured in the shared **Primordial Ecology** profile. Every species starts randomly dispersed across the field, without seeded organisms, prescribed paths or organism steering. A matrix controls each species' response to itself and every other species. Ordered pairs can differ, producing active-matter motion. Short-range repulsion, drag, agitation, boundaries and soft spacing determine the resulting structures.

Particles are solid, unlit circular discs, using the same binary coverage and full-opacity approach as COSMOS's opaque particles. There is no blending, soft edge, sphere lighting, glow or brightness falloff in the particle shader. Each species has a color, diameter and stable random size variation. Colors are used directly; alpha is always one. Optional waste/disruption feedback changes a flat color only and is disabled by default.

The fifth species, **Inert**, is a persistent physical background with no biological role. Inert particles cannot feed, be eaten, join organisms, mature or produce nuggets. They still move through the normal force rules, soft repulsion, agitation and player pushes. Their default pale-gray appearance, population, size/variation and motion multipliers are independently editable. Their row starts with zero attraction to the four functional species and weak self-repulsion; the other species mildly repel from them. These are starting settings for experimenting with passive backgrounds, separated patches and channels; no pattern is prescribed or guaranteed.

The attraction matrix is **5 × 5**. The **Inert row** controls how inert particles respond to each species, and the **Inert column** controls how each species responds to inert particles. A zero matrix entry removes that species attraction/repulsion band, not the shared short-range repulsion or other motion. Inert is a biological role, so you may still give it attractive physics through the matrix. Existing four-species assets expand safely, preserving their original 16 interaction coefficients and all original species settings; migration alone adds zero inert particles.

Visible radii plus the preferred edge gap define a universal repulsive core. Repulsion rises quadratically inside its configurable range; additional damping slows closing motion. These forces are independent of the attraction matrix and run in the neighbour pass. Particles then move once, with wall containment and no swept contacts, sliding, or overlap correction. Spacing is approximate: dense clusters and strong impulses can briefly compress or overlap particles. Initial placement requires empty space; GPU nutrient returns check the previous spatial snapshot, so simultaneous returns can briefly overlap and separate through repulsion. Oversized or overcrowded starting configurations report a configuration error. Rendering uses fixed-step positions at the configured simulation rate (currently 60 Hz).

## Metabolism and play

Nearby body particles are recognized as groups periodically. Links are limited both by Connection Radius and by Connection Size Multiplier times the two particles' radii (default 5). Each member also needs at least Minimum Local Neighbours nearby body particles (default 3) to participate. This prevents broad proximity and sparse chains alone from qualifying as organisms. A connected group then needs the configured size and proportions of structure/motor/catalyst particles, followed by uninterrupted maturation, before its catalysts process food.

A nutrient entering a mature catalyst's Feeding Radius is claimed once, travels visibly to the catalyst's surface at full size, then eases its scale to exactly zero. The approach follows the moving catalyst. Defaults are 0.45 seconds for approach and 0.25 seconds for shrinking. Captured nutrients leave the free-particle simulation and cannot be claimed again; their material is credited at capture, so their visual transition cannot duplicate food. Stored material becomes waste, and enough waste produces one weighted pickup. A field gate and group cooldown bound emission.

Reserves, waste, disruption and cooldowns belong to individual particles. Splits, merges and reclassification do not reset their budgets. After shrinking finishes, a consumed nutrient waits the configured return delay, finds room in the uniform external supply, and grows from zero to full size over 0.45 seconds by default. It cannot be captured during growth. Shrink and growth use smoothstep easing, affect visible scale rather than authored physical radius, and keep full opacity. Consumed material is accounted for as stored reserves/waste plus exported nugget material. Movement and disruption alone create no rewards.

Players push particles aside. Thrust and Sweep use the active canonical melee collider; the expanding Repulsor uses its live radius. Each attack/pulse affects a particle once. Body contact is non-damaging. Enemies and Amplifier cores do not feed or harvest the ecosystem.

Pickups use shared body-only collection, mass restoration, scoring, multipliers, feedback and once-per-life reward tokens. A level-owned pool caps attached buds plus released pickups; production waits when it is full. Expiration and cleanup do not award score. Disabling the ecology cancels windups and retires its pickups while preserving its simulation and material ledger.

### Reward budding telegraph

An organism ready to convert waste begins a **0.8-second windup** at an outer mature catalyst. Nearby body particles make a small visual size pulse while the selected reward grows smoothly from zero and moves outward as an attached bud. The bud follows its catalyst, shows the chosen matter/amplifier appearance and nugget/nugglet size, and stays fully opaque. Attachment distance scales with reward size so small nugglets remain close to the catalyst. It detaches at full size with **Nugget Ejection Speed** as its outward velocity contribution. Colliders and collection are disabled until release; release does not restart the spawn animation.

The profile's **Reward budding telegraph** section exposes Enable Reward Telegraph, Bud Windup Seconds, Bud Pulse Strength, Bud Pulse Radius, Bud Growth Start/End (fractions of windup), Bud Attachment Distance and Bud Retract Seconds. Defaults are 0.8 s, 0.22, 0.5 units, 0.2/0.9, 0.4 units and 0.25 s. Ejection speed remains under **Pickups**. Pulse strength/radius and other visual controls can be tuned live; the duration and material cost of an already-started bud stay fixed. Disabling the feature skips windup for subsequent rewards.

An attack disrupting any current member, loss of organism eligibility, or insufficient stored waste cancels the windup. The bud shrinks back over the retraction time; its material stays in the organism for a later attempt. Buds track stable catalyst IDs through movement and regrouping. If two budding organisms merge, only one keeps its pending conversion. Material is debited once at successful release, and the selected pickup is never rerolled during that windup.

The pulse changes rendering only, leaving forces, particle positions and physical radii unchanged. Both solvers support up to 32 concurrent windups within the shared pickup capacity. The GPU adds a fixed 32-slot bud buffer to its existing status readback (3,136 bytes total); it does not read back soup particles or add per-particle scene objects. Only attached/released reward objects use the existing bounded pool. Attached rewards temporarily use an opaque unlit material; detached rewards resume their authored shared pickup materials.

### Pickup type chances

The profile's **Pickup type chances** controls the reward selected when each windup begins (or at immediate conversion when the telegraph is disabled):

| Type | Default chance | Mass reward | Additional reward |
| --- | --- | --- | --- |
| Matter nugglet | 40% | 1/10 nugget | Shared `MASS_NUGGLET` score |
| Amplifier nugglet | 40% | 1/10 nugget | +0.1× personal multiplier |
| Matter nugget | 10% | Full nugget | Shared `MASS_NUGGET` score |
| Amplifier nugget | 10% | Full nugget | +1× personal multiplier |

Values are relative weights, automatically normalized; the Inspector shows effective percentages. Zero excludes a type, and all-zero/negative/non-finite weights are invalid. Each conversion always creates one pickup with the same material cost and timing. Changing the split does change the mix of rewards: nugglets grant one tenth of a nugget's mass and use one third of its linear size. The draw uses its own seeded random stream and never changes particle motion or feeding randomness. CPU and GPU share this selection path. Weights can be edited live.

Amplifiers grant mass plus a literal personal-multiplier increase, not score or team Amplifier growth. They respect the shared personal cap and match/bonus gates; at the cap they still restore mass. Their receipt reports actual gain and actual mass restored. They reuse the canonical prismatic visual asset and the existing smooth pickup lifecycle. The per-type pools grow only when needed, reuse inactive objects, and share one active capacity; up to four times that capacity can remain cached inactive.

The four prefab references are on **EMERGENCE Particle Ecology**. Amplifier reward amounts are editable on their **Emergence Mass Nugget / Personal Multiplier Gain** fields. The source visual-only amplifier prefabs remain reusable presentation assets.

## Tuning

Select **GameplayObjects / LevelContent / EMERGENCE Particle Ecology**. Its Inspector embeds the **Primordial Ecology** settings, also available as a standalone asset.

- **Species:** counts, colors, world-space diameters, size variation, force/drag/speed/agitation multipliers.
- **Starting conditions:** seed, coverage and initial speed. Placement is always dispersed. Initial Coverage of 1 fills the playable arena bounds, with only each particle's own radius kept inside the edge; EMERGENCE adds no extra border inset. Lower coverage shrinks the initial cloud, not its later movement area.
- **Forces:** interaction range, short-range repulsion distance/strength, attraction strength, drag, speed/force limits and agitation.
- **Attraction / repulsion matrix:** labeled rows respond to labeled columns; diagonal values set self-attraction. Positive attracts; negative repels. The universal repulsive core acts independently of the matrix.
- **Soft spacing:** preferred edge gap, core range multiplier, core repulsion strength, closing-motion damping and wall bounce. Larger range starts slowing particles earlier. Strength controls how firmly they resist compression, within Maximum Force. Very strong forces, small cores or low update rates can cause jitter; soft spacing never guarantees zero overlaps.
- **Simulation:** update rate and maximum catch-up steps per frame.
- **Backend:** GPU for normal use (up to 100,000 particles); CPU Reference for comparisons or unsupported hardware (up to 4,000).
- **Recognition/metabolism:** group size, composition thresholds, detection radius/cadence, size-relative link limit, local-neighbour requirement, maturation, feeding, approach duration, shrink/grow durations, storage, processing, nugget cost, cooldowns, replenishment and motor response to fuel.
- **Organism swimming:** enabled by default once a viable body matures. Cruising speed is a fraction of Speed Limit; response time eases propulsion in and out. Turn speed, wandering strength/period and speed pulse amount/period shape its movement. Food Bias gently steers toward nearby free blue particles without controlling propulsion or requiring reserves. Sense Radius Multiple and Wall Range Multiple use Interaction Radius as their scale. All swimming controls update live in the editor preview and Play Mode.
- **Interaction/feedback/pickups:** wake, melee and Repulsor strength, disruption duration, optional flat feedback colors, pickup capacity/lifetime/ejection speed.

Colors and force strengths update live. During Play Mode, restart after changing population, size/variation, gap, core range, seed, starting coverage or distance ranges. This preserves stable repulsive-core sizes and the reward ledger during a match.

The GPU solver keeps particle state, spatial bins, organism membership, feeding and material budgets on the graphics card. Bins use prefix offsets and contiguous particle storage, with no fixed per-bin population cap. Organism membership lists replace repeated full-population scans for each organism. Atomic nutrient claims prevent duplicate consumption. Only a small status/event mailbox returns asynchronously to the CPU; ordinary pooled Mass Nuggets still use the shared score economy. Particle drawing reads the GPU buffer directly, preserving opaque discs without CPU mesh updates.

There is no Rigidbody, collider or GameObject per soup particle. Player body, capsule/box/sphere melee and Repulsor shapes are sent to the GPU; attack sequence IDs retain once-per-attack particle contact. Ecology starts when the gameplay scene loads and develops throughout preparation and countdown: motion, organism formation, maturation and feeding all run. Reward budding, collectible pickups and player disturbances wait for the active match clock. Regulation continues the same ecosystem without resetting its particles or stored material. The field's **Match** reference identifies the scene match owner, and the level setup assigns it automatically.

Time pause and anomalies still pause the ecology; after preparation, closed scoring or a stopped match clock also pause it. A blocked startup does not run the ecology. The gameplay root stops it at match end. Hidden menu/instructions assets use a separate presentation-only profile with no scoring authority.

Swimming is a stylized active-matter behavior: a mature group shares a persistent cruising velocity, with smooth wandering turns and gentle speed pulses. Soft attraction/repulsion continues to deform the organism. Swimming is applied as shared advection so high drag does not suppress its propulsion or require extreme attraction forces. Final particle speeds retain their normal limits. Organisms turn away from arena walls and sense food once per group through the existing spatial bins; there are no per-particle scene searches or extra runtime readbacks.

Swimming memory belongs to body particles and is averaged over current group membership, so splits inherit motion and merges combine it. Loss of eligibility or disruption fades propulsion; qualifying again requires the existing maturation period. Nutrients and inert particles never self-propel. Food reserves still affect the motor species' attraction response, independently of the new cruising movement. Setting Food Bias to zero preserves free wandering. Disabling Enable Swimming returns to particle-force motion after the response fade. This activates per eligible organism, not after a global number of eaters has formed; swimming does not enforce a population of six to eight.

## Larger populations

In the profile Inspector, enter a **Target particle count** and click **Apply population**. **Scale sizes and distances** preserves species proportions and scales sizes, spacing, interaction/feeding/recognition distances, velocities and force strengths by the square root of the population ratio. This approximately preserves local neighbourhood density in the same arena. Timings, attraction coefficients and nugget rules stay as authored. Undo/redo is supported. The minimum particle diameter is 0.005 world units.

Leaving scaling off keeps sizes/ranges unchanged. More particles in the same area then have more neighbours, increasing work and changing organism morphology. Large populations may also fail initial placement if particle sizes or gaps are too large. The GPU refactor preserves your existing profile values; it does not automatically increase the saved population.

GPU operation requires compute shader and asynchronous readback support. A clear configuration error is shown on unsupported hardware; CPU Reference can be selected explicitly. No package or render-pipeline changes are required. Status and nugget delivery can lag a few frames behind GPU simulation; production uses bounded capacity credits so that lag cannot duplicate pickups or overfill the pool. Random noise, concurrent feeding order and floating-point sums can produce different long-term organisms from the CPU reference.

`Simulation.Particles` and `SynchronizeDiagnostics()` deliberately synchronize for diagnostics. Normal gameplay and rendering never call them on the GPU backend. `LastStepMilliseconds` measures CPU submission work for GPU simulation, not GPU execution time. The benchmark tool waits for completed GPU work and labels its measurements separately.

## Editor particle preview

With the scene open and Unity outside Play Mode, select **GameplayObjects / LevelContent / EMERGENCE Particle Ecology** and expand **Particle settings (shared profile)**. The **Editor particle preview** controls also appear when selecting its **Primordial Ecology** profile asset.

- **Run preview / Pause preview** starts or freezes the ecology in Scene view.
- **Step** advances one fixed simulation tick and pauses.
- **Reset** restarts from the configured seed; if running, playback continues from the new start.
- **Preview speed** ranges from 0.25x to 2x. Work per editor update is bounded to keep editing responsive; a busy Editor can run slower than the requested speed.

Colors, attraction/repulsion and other force settings apply to the current ecosystem. Changes to counts, diameters, size variation, seed, starting conditions, preferred gap, core range or interaction/connection/feeding radii rebuild it automatically. Undo/redo of profile edits is supported. Invalid configurations pause the preview with an explanation. **Edits affect the shared profile and persist**, just like normal Inspector edits.

The preview uses the actual ecology model, including feeding, waste production and reward budding. Attached buds appear as temporary, noncollectible visual objects and disappear at release; completed conversions are counted in the status line. Preview never creates collectible pickups, runs player interactions, starts a match or changes scores. Bud objects are hidden from the Hierarchy and never saved. Preview positions and accumulated material are temporary. Playback stops on entering Play Mode, compilation, scene removal or disabling the field; a real match starts with fresh simulation state. Only one field previews at a time, and it keeps running when selecting the profile or another object.

## Validation tools

- **MASSIVE > EMERGENCE > Create Missing Level from PULSAR** creates missing assets, preserves an existing level scene and registers EMERGENCE in the selection catalog.
- **MASSIVE > EMERGENCE > Enable Level and Update Organism Icon** migrates the old field icon while preserving prefab references and existing organism-icon tuning; registers the level once at its characteristic-scale position without reordering other entries.
- **MASSIVE > EMERGENCE > Refresh Instructions Demonstration** restores the standard central icon placement and recreates the cropped organism/reward demo in the existing instructions prefab. This resets that demo's Inspector settings to its authored defaults.
- **MASSIVE > EMERGENCE > Validate Model and Scene** exercises multiple seeds, dispersed placement, soft-spacing stability through formation/combat/replenishment, size stability, starvation, material accounting, repeatability, shader import, universal modules and serialized references.
- **MASSIVE > EMERGENCE > Validate GPU Model** checks GPU/CPU force agreement, compressed particles, connected organisms, dense bins, unique nutrient consumption, material accounting and bounded, nonduplicated nugget events.
- **MASSIVE > EMERGENCE > Validate Visible Feeding** checks sparse-chain rejection, size-limited connections, visible approach, eased shrinking/growth, newborn protection and once-only food accounting on CPU and GPU at 30/60/120 Hz.
- **MASSIVE > EMERGENCE > Validate Inert Species** checks legacy-profile migration, 5×5 matrix directions and GPU packing, population scaling, inert-only motion and exclusion from all biological functions on both backends.
- **MASSIVE > EMERGENCE > Validate Organism Swimming** checks maturation and size gates, coherent food-independent propulsion, CPU/GPU agreement at 30/60/120 Hz, food seeking, moving consumption, passive species, wandering, wall avoidance and stopping/restarting.
- **MASSIVE > EMERGENCE > Validate Reward Budding** checks CPU/GPU windup, following, reserved capacity, gradual cancellation, retained waste, recovery, concurrent buds, merged organisms, changing the telegraph toggle during windup and once-only release at 30/60/120 Hz.
- **MASSIVE > EMERGENCE > Benchmark Soft Spacing** measures isolated solver ticks using the same 60 Hz, seed, arena and 5-second warmup/5-second measurement as the hard-contact baseline. Diagnostics are outside the timing. This is not whole-frame performance.
- **MASSIVE > EMERGENCE > Validate Gameplay (1v1 and 2v2)** runs disposable live fixtures for countdown, roster, enemies, secretion, collection/scoring, pooled reuse, pause, real melee/Repulsor contacts and shutdown. It restores previously loaded scene roots without saving runtime state.
- **MASSIVE > Scoring > Validate Mass Pickup Rewards** also covers all four EMERGENCE drops: attached collision/collection exclusion, opaque growth, full-size release, weighted selection, real body contacts, mass/score/multiplier rewards, duplicate prevention, custom multiplier curves and caps, phase gates, and mixed pool reuse.

Reports and Game-view captures are in `Library/EmergenceValidation`. See `Validation.md` for observed results and remaining coverage. Balance and cabinet performance require playtesting.
