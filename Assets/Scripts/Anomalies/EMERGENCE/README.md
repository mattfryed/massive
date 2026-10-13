# EMERGENCE (development level)

Open `Assets/Scenes/S-10_EMERGENCE.unity` directly and enter Play Mode. **EMERGENCE is deliberately absent from the level-selection catalog.** Its build entry remains available for direct development loading; existing stages and build indices are unchanged.

The scene uses the saved PULSAR level as its template. It retains the universal players, Rewired bootstrap, match clock, score economy, goals, HUD, power-ups, enemy timeline, Amplifier/Resonance encounter and match-end effects. PULSAR's body, attraction, rhythm prototype and central exclusion are removed. PULSAR itself is unchanged.

## Particle soup

Population and appearance are configured in the shared **Primordial Ecology** profile. Every species starts randomly dispersed across the field, without seeded organisms, prescribed paths or organism steering. A matrix controls each species' response to itself and every other species. Ordered pairs can differ, producing active-matter motion. Short-range repulsion, drag, agitation, boundaries and soft spacing determine the resulting structures.

Particles are solid, unlit circular discs, using the same binary coverage and full-opacity approach as COSMOS's opaque particles. There is no blending, soft edge, sphere lighting, glow or brightness falloff in the particle shader. Each species has a color, diameter and stable random size variation. Colors are used directly; alpha is always one. Optional waste/disruption feedback changes a flat color only and is disabled by default.

The fifth species, **Inert**, is a persistent physical background with no biological role. Inert particles cannot feed, be eaten, join organisms, mature or produce nuggets. They still move through the normal force rules, soft repulsion, agitation and player pushes. Their default pale-gray appearance, population, size/variation and motion multipliers are independently editable. Their row starts with zero attraction to the four functional species and weak self-repulsion; the other species mildly repel from them. These are starting settings for experimenting with passive backgrounds, separated patches and channels; no pattern is prescribed or guaranteed.

The attraction matrix is **5 × 5**. The **Inert row** controls how inert particles respond to each species, and the **Inert column** controls how each species responds to inert particles. A zero matrix entry removes that species attraction/repulsion band, not the shared short-range repulsion or other motion. Inert is a biological role, so you may still give it attractive physics through the matrix. Existing four-species assets expand safely, preserving their original 16 interaction coefficients and all original species settings; migration alone adds zero inert particles.

Visible radii plus the preferred edge gap define a universal repulsive core. Repulsion rises quadratically inside its configurable range; additional damping slows closing motion. These forces are independent of the attraction matrix and run in the neighbour pass. Particles then move once, with wall containment and no swept contacts, sliding, or overlap correction. Spacing is approximate: dense clusters and strong impulses can briefly compress or overlap particles. Initial placement requires empty space; GPU nutrient returns check the previous spatial snapshot, so simultaneous returns can briefly overlap and separate through repulsion. Oversized or overcrowded starting configurations report a configuration error. Rendering uses fixed-step positions at the configured simulation rate (currently 60 Hz).

## Metabolism and play

Nearby body particles are recognized as groups periodically. Links are limited both by Connection Radius and by Connection Size Multiplier times the two particles' radii (default 5). Each member also needs at least Minimum Local Neighbours nearby body particles (default 3) to participate. This prevents broad proximity and sparse chains alone from qualifying as organisms. A connected group then needs the configured size and proportions of structure/motor/catalyst particles, followed by uninterrupted maturation, before its catalysts process food.

A nutrient entering a mature catalyst's Feeding Radius is claimed once, travels visibly to the catalyst's surface at full size, then eases its scale to exactly zero. The approach follows the moving catalyst. Defaults are 0.45 seconds for approach and 0.25 seconds for shrinking. Captured nutrients leave the free-particle simulation and cannot be claimed again; their material is credited at capture, so their visual transition cannot duplicate food. Stored material becomes waste, and enough waste produces a standard Mass Nugget. A field gate and group cooldown bound emission.

Reserves, waste, disruption and cooldowns belong to individual particles. Splits, merges and reclassification do not reset their budgets. After shrinking finishes, a consumed nutrient waits the configured return delay, finds room in the uniform external supply, and grows from zero to full size over 0.45 seconds by default. It cannot be captured during growth. Shrink and growth use smoothstep easing, affect visible scale rather than authored physical radius, and keep full opacity. Consumed material is accounted for as stored reserves/waste plus exported nugget material. Movement and disruption alone create no rewards.

Players push particles aside. Thrust and Sweep use the active canonical melee collider; the expanding Repulsor uses its live radius. Each attack/pulse affects a particle once. Body contact is non-damaging. Enemies and Amplifier cores do not feed or harvest the ecosystem.

Pickups use shared body-only collection, mass restoration, scoring, multipliers, feedback and once-per-life reward tokens. A level-owned pool caps active nuggets; production waits when it is full. Expiration and cleanup do not award score. Disabling the ecology retires its pickups while preserving its simulation and material ledger.

## Tuning

Select **GameplayObjects / LevelContent / EMERGENCE Particle Ecology**. Its Inspector embeds the **Primordial Ecology** settings, also available as a standalone asset.

- **Species:** counts, colors, world-space diameters, size variation, force/drag/speed/agitation multipliers.
- **Starting conditions:** seed, coverage and initial speed. Placement is always dispersed.
- **Forces:** interaction range, short-range repulsion distance/strength, attraction strength, drag, speed/force limits and agitation.
- **Attraction / repulsion matrix:** labeled rows respond to labeled columns; diagonal values set self-attraction. Positive attracts; negative repels. The universal repulsive core acts independently of the matrix.
- **Soft spacing:** preferred edge gap, core range multiplier, core repulsion strength, closing-motion damping and wall bounce. Larger range starts slowing particles earlier. Strength controls how firmly they resist compression, within Maximum Force. Very strong forces, small cores or low update rates can cause jitter; soft spacing never guarantees zero overlaps.
- **Simulation:** update rate and maximum catch-up steps per frame.
- **Backend:** GPU for normal use (up to 100,000 particles); CPU Reference for comparisons or unsupported hardware (up to 4,000).
- **Recognition/metabolism:** group size, composition thresholds, detection radius/cadence, size-relative link limit, local-neighbour requirement, maturation, feeding, approach duration, shrink/grow durations, storage, processing, nugget cost, cooldowns, replenishment and motor response to fuel.
- **Interaction/feedback/pickups:** wake, melee and Repulsor strength, disruption duration, optional flat feedback colors, pickup capacity/lifetime/ejection speed.

Colors and force strengths update live. During Play Mode, restart after changing population, size/variation, gap, core range, seed, starting coverage or distance ranges. This preserves stable repulsive-core sizes and the reward ledger during a match.

The GPU solver keeps particle state, spatial bins, organism membership, feeding and material budgets on the graphics card. Bins use prefix offsets and contiguous particle storage, with no fixed per-bin population cap. Organism membership lists replace repeated full-population scans for each organism. Atomic nutrient claims prevent duplicate consumption. Only a small status/event mailbox returns asynchronously to the CPU; ordinary pooled Mass Nuggets still use the shared score economy. Particle drawing reads the GPU buffer directly, preserving opaque discs without CPU mesh updates.

There is no Rigidbody, collider or GameObject per soup particle. Player body, capsule/box/sphere melee and Repulsor shapes are sent to the GPU; attack sequence IDs retain once-per-attack particle contact. Match gating pauses it during countdown, closed scoring/clock, anomalies and time pause. The gameplay root stops it at match end. Hidden menu/instructions assets use a separate presentation-only profile with no scoring authority.

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

The preview uses the actual ecology model, including feeding and waste production. Produced nuggets are counted in the status line without creating physical pickups, running player interactions, starting a match or changing scores. Preview positions and accumulated material are temporary. Playback stops on entering Play Mode, compilation, scene removal or disabling the field; a real match starts with fresh simulation state. Only one field previews at a time, and it keeps running when selecting the profile or another object.

## Validation tools

- **MASSIVE > EMERGENCE > Create Missing Level from PULSAR** creates missing development assets, preserves an existing level scene and keeps EMERGENCE out of the selection catalog.
- **MASSIVE > EMERGENCE > Validate Model and Scene** exercises multiple seeds, dispersed placement, soft-spacing stability through formation/combat/replenishment, size stability, starvation, material accounting, repeatability, shader import, universal modules and serialized references.
- **MASSIVE > EMERGENCE > Validate GPU Model** checks GPU/CPU force agreement, compressed particles, connected organisms, dense bins, unique nutrient consumption, material accounting and bounded, nonduplicated nugget events.
- **MASSIVE > EMERGENCE > Validate Visible Feeding** checks sparse-chain rejection, size-limited connections, visible approach, eased shrinking/growth, newborn protection and once-only food accounting on CPU and GPU at 30/60/120 Hz.
- **MASSIVE > EMERGENCE > Validate Inert Species** checks legacy-profile migration, 5×5 matrix directions and GPU packing, population scaling, inert-only motion and exclusion from all biological functions on both backends.
- **MASSIVE > EMERGENCE > Benchmark Soft Spacing** measures isolated solver ticks using the same 60 Hz, seed, arena and 5-second warmup/5-second measurement as the hard-contact baseline. Diagnostics are outside the timing. This is not whole-frame performance.
- **MASSIVE > EMERGENCE > Validate Gameplay (1v1 and 2v2)** runs disposable live fixtures for countdown, roster, enemies, secretion, collection/scoring, pooled reuse, pause, real melee/Repulsor contacts and shutdown. It restores previously loaded scene roots without saving runtime state.

Reports and Game-view captures are in `Library/EmergenceValidation`. See `Validation.md` for observed results and remaining coverage. Balance and cabinet performance require playtesting.
