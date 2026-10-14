# EMERGENCE validation — 2026-10-13

Validated in Unity 6000.0.28f1, Built-in Render Pipeline, Windows/DX11.

## Local particle circulation and eddies

The saved icon now enables shader-based differential circulation, counterflowing layers and shared local eddies. These currents move the existing baked particle centers before breathing, whole-body pivot/strain and rotation. Radius-preserving twists retain the full volume and existing conservative bounds; the prefab's particle count, size, chemistry and baked formation are unchanged. Flow controls require no rebake and create no extra meshes, colliders or simulation buffers.

An isolated Play Mode render probe disabled breathing, internal ripple, rocking and pivots. Frames at 0, 2 and 4 seconds still showed visible internal motion (11,523 and 11,361 changed pixels between successive 512-pixel frames). Pixel-exact comparisons verified that zero Flow Speed freezes circulation in place, a speed edit at zero elapsed time does not jump, Animate stops flow, and disabling Local Flow restores the baked formation. Twelve maximum-setting snapshots spanning six simulated minutes stayed inside x=51–460 / y=56–459. The same 378-particle mesh, transform and unchanged gameplay profile persisted throughout; disable/re-enable passed. Runtime/editor compilation and shader compilation passed, with zero final Console errors or warnings. The user's active Play Mode session was preserved.

Evidence: `Library/EmergenceValidation/icon-local-flow.txt` and `icon-local-flow.png` (whole-body motion disabled throughout the three-frame comparison).

## Full-volume, force-shaped icon and intermittent pivots

The icon now settles a complete 3D mixture using the current ecology's asymmetric pair attractions, separation/soft-core repulsion, relative sizes and drag. The saved 378-particle budget is redistributed into 147 structure, 124 motor, 92 catalyst and 15 nutrient particles; the authored base radius remains `0.0052`. The previous missing cap was explicit open-membrane geometry, not camera clipping. The replacement has particles in every octant and full depth on all three axes. Distant food is brought near the body for presentation framing; no production ecology data is modified.

The settled formation is saved in the prefab. With the in-memory formation cache cleared, first mesh preparation took **10.96 ms** and did not run relaxation; rebuilding took **5.35 ms**. A 2,400-call / 40-second animation probe took **5.90 ms total** on this Editor machine (CPU calls only, not overall rendering time). Front, back and side captures showed the entire formation with clear frame margins. The conservative local envelope radius is `0.7096435`, before selection scaling. Mesh ownership, zero colliders, disable/re-enable and unchanged source-profile JSON passed.

A temporary attraction-matrix edit changed the settled geometry (mean normalized displacement `0.5214`), confirming that formation responds to the profile rather than fixed color rings. A 60-second pivot probe verified turns, long pose holds, recoil limited to `0.028`, an unchanged transform anchor and the Animate toggle. The actual carousel selected EMERGENCE at index 3 and confirmed into its instructions normally. There the updated icon continued for 24 seconds with `Time.timeScale = 0`; the separate gameplay demonstration completed four loops and ten releases. Runtime disable/re-enable passed. Final Console: zero errors or warnings; shader compiled and was supported. The original clean level-select scene and background setting were restored after Play Mode.

Evidence in `Library/EmergenceValidation`: `icon-full-volume.txt`, `icon-full-volume.png`, `icon-behavior.txt`, `icon-runtime.txt`, `icon-carousel-final.png`, and `icon-instructions-final.png`. These checks supersede the original synthetic icon's particle counts and formation description below. No gameplay tuning, catalog order, instructions layout or shared scene was changed for this revision.

## Instructions icon and cropped ecology demonstration

The nested icon now matches ORBITAL and COSMOS: local position `(0, 5, 0)` and uniform scale `5.8`, centered above the instructions grid. Its existing prefab link and animation are preserved. The middle-right frame contains an isolated, orthographic close-up using the production CPU ecology model, opaque particle material and all four canonical reward prefabs. The seeded mature colonies retain the gameplay chemistry and produce their own validated buds/releases. Only the demo population and starting state differ; a JSON comparison confirmed the gameplay profile was unchanged.

The isolated rendered sequence passed two eight-second loops: 296 particles, four released rewards and 66 consumed nutrients. Samples showed two bodies, feeding before the first bud, an attached bud at 1.52 seconds, one release by 2.52 seconds and both releases by 3.52 seconds. Disabling/restarting released and rebuilt the mesh, profile copy, simulation and rewards. Explicit material ownership was added because never-enabled pickup behaviours cannot be relied on for destruction callbacks. The final probe left zero additional temporary reward materials.

The actual instructions scene then passed **eight completed loops, 18 releases and 204 consumed nutrients**. Rewards remained disabled, kinematic and noncolliding and did not enter the shared active-pickup registry. Normal runtime `OnDisable` removed the simulation, mesh, rewards and camera; after deferred destruction there were zero temporary reward materials. Re-enabling produced both rewards again. The final live run had zero Console errors or warnings. The complete layout and cropped reward release were visually reviewed, including the text fit and pane boundaries.

Evidence in `Library/EmergenceValidation`: `instructions-demo.txt`, `instructions-runtime.txt`, `instructions-demo-stages.png` and `instructions-final.png`. Earlier capture probes produced Editor-only callback/recursive-step assertions; the final probes use direct owner cleanup in Edit Mode and ordinary runtime callbacks with end-of-frame screenshot capture, and passed. No gameplay scene, match state, ecology tuning, level order or build settings were changed for these instructions.

## Carousel registration and living organism icon

EMERGENCE is registered once in the nine-entry catalog. Its scale of 10⁻⁶ m places it between ORBITAL (10⁻¹⁰ m) and PULSAR (10⁴ m), correcting the initial append-after-COSMOS placement. Registration inserts or relocates it by characteristic scale while preserving the order of every other level. Its enabled gameplay build entry and existing instructions reference remain intact. Missing-level setup now registers it instead of removing it. The existing icon prefab GUID and root/visual transforms were preserved, so the instructions panel inherits the new icon.

The replacement is a presentation-only 3D colony: 242 opaque, unlit spheres, 18,634 vertices, one mesh/material, zero colliders and no ecology simulation. Bounded breathing, ripples and rocking use unscaled time and leave the transform origin fixed. Its conservative default local envelope radius is 0.690468, before the existing carousel selection scale.

Runtime and Editor assemblies compiled successfully; the icon shader is supported with no compiler errors. Isolated rendered frames at 0, 4 and 8 seconds were reviewed. With an orthographic half-height of 0.85, all visible pixels remained within x=67–446 and y=99–400 of the 512-pixel frame. Disable released the generated mesh, and re-enable recreated it. Reports: `Library/EmergenceValidation/organism-icon.txt` and `organism-icon-stages.png`.

The actual carousel scene loaded in Play Mode, but the automated selection/confirmation/gameplay transition check did not execute: automatic approval review timed out, then reported model capacity on its permitted retry. This is incomplete validation, not a Unity failure. The final Console contained zero errors. Play Mode was stopped, the temporary start-scene override was restored, and the original EMERGENCE scene retained its unsaved edits. The previous gameplay checks below predate this icon change; no gameplay simulation settings were changed here.

## Pre-regulation ecology

The gameplay field now uses an explicit scene match reference to run during Preparing and Countdown. The existing simulation advances motion, recognition, maturation and feeding; it carries its particles and material into regulation without rebuilding. Reward budding, export, physical pickups and player disturbances remain gated by the active match clock. Time pause, anomalies, blocked startup, post-start closed scoring/clock, and gameplay shutdown retain their stop conditions. The scene reference and missing-level setup are wired; no profile tuning or countdown duration was changed in the saved gameplay assets.

**1v1 and 2v2 each passed 31 assertions plus one timing measurement**, with zero runtime Console errors. Both observed advancement before regulation, pause/resume during countdown, no pre-match buds/pickups/score/player disturbances, and preservation of the same simulation across the regulation transition. The existing natural feeding/reward, collection, combat, pause and shutdown checks also passed. First natural rewards in these runs occurred at simulation ages 18.22 s and 16.63 s. Reports: `one-v-one.txt`, `two-v-two.txt`.

The startup fixture uses an unsaved 10-second countdown to leave room for pause/resume observations in the shared Editor; the level retains its authored 3 seconds. An initial shorter-window check failed after pause/resume (`pre-regulation-first-attempt.txt`). A subsequent run passed startup but exceeded the previous 25-second real-time wait for a natural pickup (`pre-regulation-emission-attempt.txt`). The live fixture now allows 60 seconds for that event, logs simulation age, and bounds both modes to five minutes. This accommodates capped ecology catch-up when the unscaled match clock outruns simulation; no production timing was changed. The final runs passed both modes, and runtime/editor compilation succeeded.

## Reward budding telegraph

Enabled the 0.8-second local pulse and attached reward growth in the gameplay profile, preserving its 11,700 particles, Minimum Body 40, Initial Coverage 1 and existing ecology tuning. Visuals stay opaque; attached rewards cannot collide or collect. Release retains the same selected pickup and starts its collectible lifetime at full scale. Disruption, lost eligibility and competing merged windups retract without consuming stored waste. Particle forces and radii are unchanged by the pulse. Editor preview shows temporary noncollectible buds and removes them at release.

- **85 budding model checks passed** across CPU/GPU and 30/60/120 Hz, covering reserved capacity, no early export, moving anchors, disruption of a non-anchor organism member, gradual retraction, zero-scale cancellation, retained material, recovery, once-only release, concurrent organisms, merge conflicts, owner cancellation and disabling the telegraph during an active windup without overrunning capacity. Report: `budding.txt`.
- **17 GPU model checks passed**, including force agreement, dense bins, unique consumption, accounting and bounded event delivery. The immediate-emission mailbox test explicitly disables the separately tested telegraph. Report: `gpu-model.txt`.
- **98 shared pickup Play Mode checks passed**, including all previous 86 checks plus three attached/release checks for each of the four types. Real player contacts cannot collect a bud; opaque materials and whole-silhouette scaling are verified; detachment restores normal physics and full size without restarting the spawn animation. Report: `pickup-rewards.txt`.
- **1v1 and 2v2 each passed 24 assertions plus one timing measurement**, with zero runtime Console errors. Natural feeding/budding/release, collection, pool reuse, match gates, player attacks, shutdown and bounded populations passed. Reports: `one-v-one.txt`, `two-v-two.txt`.
- Close-up GPU/field captures show all four reward types at initial pulse, partial growth, full growth and retraction. Review exposed excessive separation for nugglets; attachment distance now scales with their authored size. Each of the four preview fixtures then completed four conversions without creating collectible pickups. Evidence: `budding-stages.png`, `budding-preview.txt`.

GPU particle/group layouts and the 18 compute passes are retained. New bud state is bounded at 32 slots; the asynchronous status/event/bud mailbox is 3,136 bytes rather than the previous 1,088. No soup-particle readback, per-particle GameObjects or colliders were added. The preceding validation sections are historical snapshots; the current reports supersede their prior copies. A packaged build and target-cabinet frame time were not measured for this addition.

## Weighted pickup types

Defaults are 40% matter nugglet, 40% amplifier nugglet, 10% matter nugget and 10% amplifier nugget. Inspector weights normalize automatically and do not alter material cost, feeding, processing or secretion gates. The scene has four assigned gameplay prefabs; the authored 11,700-particle profile, Minimum Body 40 and Initial Coverage 1 were preserved.

**Shared pickup Play Mode validation: 86 checks passed**, including 49 additional EMERGENCE checks. It uses real player-body physics contacts in an isolated scene and covers all four pickup types, the 10:1 mass ratio, one-third nugglet size, exact +1×/+0.1× personal gains, full mass, duplicate collection, pooled lives, receipts, phase gates, default/custom caps and continued ordinary charge growth. Probability bins cover defaults, proportional weights, excluded types and invalid weights. Mixed pools were recycled for 40 rounds with rotating slots, retaining four distinct live references and only four allocated fixture pickups. Existing matter pickup scoring checks also passed. Evidence: `Library/EmergenceValidation/pickup-rewards.txt`.

Runtime and editor assemblies compile without errors. Saved-scene reference checks passed. **Live 1v1 and 2v2 each passed 24 assertions plus one timing measurement**, with zero runtime Console errors. Natural feeding/conversion, emitted pickups, shared collection, pause, combat, bounded populations and shutdown passed. The fixture now explicitly creates a matter pickup for its fixed-score assertion because a random harvest may contain only amplifiers; it waits through the existing 0.3-second collection lock before moving the player onto that new pickup. An initial run that omitted this wait failed at collection; the corrected fixture passed both modes. The Editor returned to Edit Mode and released its fixture holds. Reports: `one-v-one.txt`, `two-v-two.txt`. A rendered four-type comparison was reviewed in `pickup-types.png`.

This verifies drop selection and reward behavior; the split's effect on match balance still requires playtesting. No packaged-build or target-cabinet performance claim is made.

## Arena edge coverage

Initial Coverage 1 still left a uniform border because `EmergenceField.Initialize` subtracted an additional 0.4 world units from both playable half-extents. The live scene confirmed grid/playable half-size (14, 6), shared inset 0, but simulation half-size (13.6, 5.6). Removed that extra subtraction; per-particle radius containment remains in both solvers.

After compilation and rebuilding the paused preview, the live GPU field used (14, 6). The user's current 11,700 particles included 175/169/390/363 particles in the previously excluded left/right/bottom/top strips; no particle disc crossed the bounds. Closest disc-edge gaps were 0.00732/0.00026/0.00261/0.00189 world units. An isolated CPU profile at coverage 0.5 retained the smaller initial cloud within the full movement bounds. No profile, arena or scene settings were changed. Report: `arena-padding.txt`. To reproduce, reset the EMERGENCE preview at coverage 1 and compare `Simulation.HalfSize` with the arena's scaled playable half-size.

## Organism swimming

Added autonomous cruising after the existing per-organism viability and maturation gates. Swimming continues without food or reserves, retains soft particle forces, wanders with smooth turns and speed pulses, biases its course toward free nutrients, and turns from walls. Per-particle swimming memory survives membership changes; disruption or lost eligibility fades propulsion. New profile controls are enabled with a 0.65 speed fraction, 0.6-second response, 90-degrees/second turn limit, 0.55 wandering strength and 0.3 food bias. The user's current 11,000-particle population, Minimum Body 40, colors and attraction matrix were preserved.

- **75 swimming checks passed** on CPU and GPU, including 30/60/120 Hz activation, food-independent motion, coherent shape, passive nutrient/inert species, CPU/GPU trajectory agreement, enable/disable, size gating, moving consumption/material accounting, wandering, food seeking, newborn exclusion, wall avoidance, split/merge continuity and disruption recovery. Report: `swimming.txt`.
- **17 GPU model, 64 visible-feeding and 25 inert-species checks passed.** The existing feeding lifecycle fixture explicitly disables swimming because it measures approach to a stationary catalyst; moving consumption is covered by the new suite.
- **11,000 particles ran for 120 simulated seconds:** 1,489 consumed, 1,123 replenished and 88 nuggets secreted. All positions stayed finite and bounded. Material-accounting error was **0.00000800 units**. The test drains nugget events immediately; this is not an in-match reward-rate target. Report: `gpu-long-11000.txt`.
- **50,000 particles, RTX 4090:** median **1.153 ms/tick**, p95 **1.379 ms/tick**, after 30 simulated seconds of warmup, measured over 30 batches of six 60 Hz ticks. The final sample had 38 eligible organisms, 36 productive. This includes command submission and waiting for completed GPU work, excludes rendering and the rest of the game, and is not a comparison against the old solver. Report: `swimming-benchmark.txt`.
- **1v1 and 2v2 each passed 24 gameplay assertions plus a timing measurement**, including real feeding/secretion, pickup scoring, pause, player body/melee/Repulsor contacts, post-combat spacing and shutdown. Both reported zero runtime Console errors. The original ten-second first-feeding deadline failed with the user's larger Minimum Body 40; the fixture now allows 45 seconds for formation while retaining the same feeding assertion. Both modes passed with that window, and the Editor returned to Edit Mode with preview paused and all fixture refresh/reload holds released. Reports and inspected Game-view captures: `one-v-one.txt/.png`, `two-v-two.txt/.png`.

The first trajectory comparison exposed a small cumulative GPU heading drift from repeatedly converting a direction through atan2/cos/sin. Applying incremental vector rotation removed it; the unchanged 0.001-unit CPU/GPU trajectory tolerance passes. No whole-project build or target-cabinet performance claim is made. Swimming changes encounters and food throughput, so visual character and reward balance still need playtesting. The older sections below describe prior profiles and validation snapshots.

## Fifth inert species

Added a fifth, pale-gray **Inert** species and a 5×5 attraction matrix on CPU and GPU. It participates in physical forces and player pushes, but never organism recognition, food claims, nutrient returns, maturation, material storage or nugget production. Defaults: no response to the four functional species in its matrix row, weak self-repulsion (-0.08), and mild repulsion from the existing species toward inert particles (-0.12 in the new column). Shared core/short-range repulsion still applies. These are tunable initial conditions for passive-background and patch/channel experiments, not a prescribed reaction-diffusion pattern.

The gameplay profile now contains **600 inert + the unchanged 3,400 functional particles = 4,000 total**. The presentation profile adds 48 inert particles to its existing 304. Both saved profiles were checked against pre-change snapshots: all four original species settings and all 16 original attraction coefficients were preserved exactly. Automatic legacy migration adds the fifth species with zero population; the populated defaults above were explicitly applied to these two development profiles.

- **25 focused checks passed** for legacy migration/idempotence/serialization, all original matrix coefficients retaining their meaning, population scaling, purely inert populations, exclusion from organism size and food, and independent fifth-row/fifth-column force responses. CPU/GPU trajectories with all 25 asymmetric coefficients agreed within **0.00000012 world units**. Report: `inert-species.txt`.
- **17 GPU model checks and 64 visible-feeding checks passed** after expanding the matrix. Nutrient approach/shrink/growth remains intact.
- **50,000 mixed particles ran for 60 simulated seconds**, consuming 5,496 nutrients, replenishing 4,295 and producing 59 nuggets. All particle states remained finite/bounded; independent material-accounting error was **0.00000927 units**. Report: `gpu-long-50000.txt`.
- **1v1 and 2v2 each passed 24 gameplay assertions plus a timing measurement** with 600 inert particles present, including natural secretion, shared pickup scoring, player disturbances and shutdown. Both had zero runtime Console errors. Game-view captures were inspected and show the fifth opaque gray species alongside the existing four colors. Reports: `one-v-one.txt`, `two-v-two.txt`; previous reports were preserved with the `before-inert-` prefix. Editor preview was restored after the fixtures finished.

## Visible feeding and particle lifecycle

Organism links now require local support (default 3 neighbours) and a size-relative distance limit (default 5 times the sum of the visible radii), in addition to the existing absolute radius, body size, composition and maturity rules. Nutrients visibly approach their owning catalyst at full size, ease down to zero at its surface, wait the replenishment delay, and grow from zero at their return position. Newborns are protected from capture until growth completes. Visual scale is separate from physical radius and opacity remains one. Approach/shrink/grow durations are tunable, defaulting to 0.45/0.25/0.45 seconds. Material is credited once at capture and remains owned by the catalyst during presentation.

- **64 focused checks passed** across CPU/GPU and 30/60/120 Hz: sparse chains and oversized links are rejected, compact groups feed, approach has no initial teleport or disappearance, shrinking is monotonic and reaches exactly zero, the return delay follows shrinking, regrowth starts at zero, newborns cannot be recaptured, physical radii stay stable and material is counted once. Report: `visible-feeding.txt`.
- **17 existing GPU model checks passed**, including force agreement, spatial-bin overflow, unique claims, secretion capacity and accounting. The 512-particle union-connectivity test explicitly opts out of the new recognition gates; the focused tests separately enforce those gates.
- **60-second GPU runs passed** at the user's current 3,400 particles (434 consumed, 366 replenished, 17 nuggets; material error 0.00000654) and 50,000 with proportional distance scaling (6,978 consumed, 5,394 replenished, 60 nuggets; error 0.0000112). Both remained finite and bounded. Reports: `gpu-long-3400.txt`, `gpu-long-50000.txt`.
- A five-stage capture from the actual GPU renderer was inspected: capture, approach, shrink, fully hidden, regrowth. All visible stages retain opaque flat colors. Image: `visible-feeding-stages.png`.
- Updated 50,000-particle scaled-distance benchmark: median **0.287 ms/tick**, p95 **3.136 ms/tick**, including completed GPU work and submission/synchronization, excluding rendering and the rest of the game. Recognition adds a local-support pass. This short Editor measurement is not whole-game FPS.
- **1v1 and 2v2 each passed 24 gameplay assertions plus a submission-time measurement**, including natural feeding/secretion, pickup scoring, pause, melee, Repulsor, disable/re-enable and shutdown. Both had zero runtime Console errors and no overlapping free particles in the post-combat sample. The fixture returned to Edit Mode and released its refresh/reload holds. Reports: `one-v-one.txt`, `two-v-two.txt`; preceding results were backed up as `before-visible-feeding-one-v-one.txt` and `before-visible-feeding-two-v-two.txt`.

The saved particle counts, sizes, forces and colors were preserved, including the user's current 3,400-particle tuning. The sections below retain historical validation from earlier revisions; current reports can supersede those earlier measurements.

## GPU refactor

The GPU path owns particle motion, spatial sorting, organism membership, metabolism and secretion. It draws directly from a GPU buffer and reads back a 1,088-byte status/event mailbox. CPU Reference remains available. Existing profile tuning was retained (1,400 particles at the start of this refactor); GPU capacity is 100,000. No shared player, scoring, physics, render-pipeline, package or level-selection assets were changed by this refactor.

**Model validation: 17 checks passed.** GPU/CPU positions agreed within 0.00000024 world units over 12 deterministic ticks with noise/feeding disabled. Checks also covered coincident-particle recovery at 30/60/120 Hz, a 512-particle connected chain, 4,096 particles in one spatial bin, nutrient-only populations, competing catalyst claims, material conservation, capacity backpressure and once-only event delivery. See `gpu-model.txt`.

**Gameplay: 1v1 and 2v2 passed 24 assertions plus a CPU submission measurement per mode**, with zero runtime Console errors. Both used GPU simulation and covered fresh countdown, normal roster/input/HUD/enemies, natural secretion, ordinary pickup scoring and pooling, pause, actual capsule melee, Repulsor, disable/re-enable, bounded populations and shutdown. GPU Game-view captures retained the opaque flat-color particle appearance. The initial gameplay report's 0.05–0.07 ms values measure CPU command submission only; the timing label has been corrected in the fixture. They must not be interpreted as GPU execution times.

**GPU editor lifecycle: 10 checks passed.** Covered Run, Step/Pause, Reset releasing the prior GPU buffers, automatic rebuild after scaling to 5,000 particles, Undo/Redo, invalid settings and recovery, CPU Reference switching, and GPU resource disposal on Stop. This used a disposable preview scene and profile; the saved profile remained at 1,400 particles with its authored values and no dirty state. See `gpu-editor-lifecycle.txt`. Runtime/editor scripts and GPU shaders compiled, and the final Console check contained no errors.

### Completed-work benchmark

Hardware: NVIDIA GeForce RTX 4090. Same 13.6 x 5.6 half-size arena, seed 1729, source profile, 60 Hz and 120 warmup/120 measured ticks. CPU baseline timed individual ticks. GPU measurements use 20 batches of six ticks and include CPU submission plus a synchronization after each batch, divided by six. Rendering, the rest of the game and startup allocation/placement are excluded. These are short isolated solver measurements, not whole-game FPS or a guarantee for every evolved cluster.

| Backend / population | Median ms/tick | p95 ms/tick |
| --- | ---: | ---: |
| CPU, original 1,400 | 4.788 | 6.845 |
| GPU, original 1,400 | 0.409 | 0.533 |
| GPU, 5,000 | 0.718 | 0.893 |
| GPU, 10,000 | 1.485 | 1.867 |
| GPU, 25,000 | 3.196 | 4.128 |
| GPU, 50,000 | 6.566 | 9.389 |
| GPU, 50,000 with proportional distances/motion | 0.243 | 0.410 |
| GPU, 100,000 with proportional distances/motion | 0.387 | 0.476 |

Above 1,400, diameters and preferred gap were reduced to fit the larger population. The first five GPU rows retain the 1.6-unit attraction radius and original force/motion settings. The final two rows also scale interaction/recognition/feeding distances and motion with the square root of the population ratio (attraction radii 0.2677 and 0.1893 respectively); they represent different morphology/scale, not faster execution of identical physical settings. All sampled particle states remained finite. Reports: `gpu-refactor-cpu-baseline.txt` and `gpu-benchmark-*.txt`.

**Sustained operation:** the actual editor preview advanced a temporary 50,000-particle profile for 54.75 simulated seconds, replenished 17,336 nutrients and produced 58 simulated nuggets without invalid positions or runtime errors. Its Game-view capture was inspected. This exposed small accumulated rounding drift during reserve-to-waste transfers; preserving each catalyst's total material during that transfer corrected it. A subsequent 60-second, 50,000-particle GPU run consumed 25,294 nutrients, replenished 19,408 and secreted 65 nuggets, with independently measured material error of **0.00000163 units** and finite, bounded particle states. Reports: `gpu-live-50000.txt` (before the precision correction), `gpu-long-50000.txt` (after), and `gpu-50000-preview.png`. The saved 1,400-particle profile was restored unchanged after the temporary preview.

GPU startup and steady operation require compute shaders and asynchronous readback. Long-term trajectories need not match the CPU model because noise generation, parallel food claims and floating-point reduction order differ. Concurrent nutrient returns use the previous position snapshot and can briefly overlap. The CPU accounting authority for real pickups and scores remains unchanged. Packaged builds and target-cabinet hardware are not covered by these measurements.

## Editor preview addition

Added Run/Pause, Step, Reset and preview speed controls to the field's embedded profile Inspector and the profile asset Inspector. The editor driver advances the existing ecology model without starting a match, invoking player interactions or creating pickup objects. Colors and forces update live; structural settings rebuild the preview. Entering Play Mode clears its transient state.

- **23 focused checks passed**, recorded in `Library/EmergenceValidation/editor-preview.txt`. Covered Run/Pause/Step/Reset, deterministic reset, live forces/colors, automatic population/size/range/seed rebuilds, invalid settings and recovery, no gameplay side effects, cleanup, compilation stop, serialized Inspector edits, Undo/Redo and fresh match startup.
- The actual 1,800-particle field advanced more than 34 simulated seconds through Editor callbacks outside Play Mode. Pause held its simulation age across subsequent callbacks. The custom Inspector drew without Console errors.
- A match launched from an already advanced, running editor preview started at zero simulation age with zero released nuggets. **1v1 and 2v2 each passed 24 assertions plus one timing measurement**, with zero runtime Console errors. Editor validation returned to Edit Mode and released its temporary refresh/reload holds.
- The first gameplay attempt lost Rewired input initialization during the match and failed its melee assertion. Its evidence is retained in `editor-preview-gameplay-first-attempt.txt`. Repeating the same code passed both modes; the cause of that first shared-Editor interruption was not established. The preview startup/countdown checks passed in both attempts.
- Runtime and editor assemblies compiled successfully. A packaged build and Enter Play Mode configurations with domain/scene reload disabled were not exercised for this addition.

The remaining sections describe the preceding soft-spacing revision and its measurements. The latest `one-v-one.txt` and `two-v-two.txt` reports are the GPU gameplay regression run; earlier CPU reports were preserved as `before-gpu-one-v-one.txt` and `before-gpu-two-v-two.txt`.

## Implementation and defaults

The 1,800-particle gameplay profile uses soft, size-based repulsion instead of swept contacts, sliding and final overlap rejection. Core range is 3 times combined visible radii plus preferred gap, core strength is 80, closing-motion damping is 6, and drag is 4.5 (previously 3.2). Counts, sizes, colors, attraction matrix, speed cap and 60 Hz update rate are retained. Initial placement and nutrient returns still find free space; arena walls contain particles. No per-particle Unity physics components were added.

The solver rebuilds its neighbour grid once per tick and integrates each particle once. Motor response is cached per particle, and the pair-force loop uses scalar arithmetic. Extra grid rebuilds, per-move grid maintenance and the hard contact solver are removed. Explicit overlap diagnostics rebuild their own current grid; they are not part of normal runtime movement.

## Performance

Same PC and Editor, 1,800 particles, seed 1729, arena half-size 13.6 x 5.6, 60 Hz; 5 seconds of warmup followed by 5 seconds of measured solver ticks. Pickup-queue draining and overlap diagnostics are outside the timings.

| Solver | Median tick | p95 tick |
| --- | ---: | ---: |
| Hard contacts, original settings | 25.905 ms | 31.515 ms |
| Soft spacing, final defaults | 10.763 ms | 12.618 ms |

Median solver cost fell about 58%. This is an isolated simulation measurement in the shared Editor, not whole-frame FPS or target-cabinet performance. The final benchmark sample had no visually overlapping particles; this does not guarantee zero overlaps throughout play.

Live gameplay ecology ticks (including player interactions and nugget release) measured median/p95 **13.00/14.57 ms in 1v1** and **12.92/15.39 ms in 2v2**. The full model suite averaged 14.525 ms per tick including its additional geometric audits.

## Passed checks

- **Model and scene: 61 checks.** Seeds 1729, 731 and 4919 each ran for 60 simulated seconds at the configured 60 Hz, including a strong Repulsor-like disturbance halfway through. They remained finite and bounded, retained stable sizes, fed, replenished nutrients, secreted nuggets and conserved material. Each recovered productive activity during the final 15 seconds. Starved particles created no rewards; identical seeds and ticks reproduced positions.
- The three seeds produced **4, 9 and 9 nuggets**. The model drains the secretion queue immediately; these values are functional evidence, not final reward-balance targets.
- Soft spacing was audited once per simulated second. Peak visually overlapping particle counts were **11, 36 and 16 out of 1,800**, with maximum overlap depths **0.0192, 0.0262 and 0.0381 world units**. All samples met the requirement of at least 95% of the full population visually separate. These are sampled observations, not continuous guarantees.
- Focused pair checks covered compressed particles at 30/60/120 Hz, coincident centers, size scaling, core ranges larger than the attraction range, damping of closing motion only, wall containment, invalid settings, and overlap remaining when all repulsion is disabled.
- Scene checks verified six universal roots, shared PULSAR-derived systems, valid shader and 6,900 serialized reference slots. At that stage EMERGENCE was absent from level selection and available for direct scene loading; the carousel registration above supersedes that restriction. PULSAR and the level catalog had no Git diff from the soft-spacing change.
- **1v1 and 2v2: 24 assertions plus one timing measurement per mode**, with zero runtime Console errors. Covered countdown, roster/input, HUD, enemies, feeding/secretion, collection/scoring, pooled expiration/reuse, pause, real melee and Repulsor interactions, temporary disable, bounded populations and gameplay shutdown. The post-combat samples contained 6 and 28 overlapping particles respectively.
- Current Game-view captures were reviewed: flat opaque colored discs form distributed clusters, with readable shared match UI. No rendering/shader changes were needed for soft spacing.
- Unity Editor runtime and editor assemblies compiled without errors. Validation returned to Edit Mode and released its temporary refresh/reload holds.

## Test contract and evidence

Soft spacing intentionally permits brief compression. The previous hard-contact zero-overlap assertion was replaced with sampled visible-spacing checks and explicit force-only recovery tests. Nugget yield changes because organisms split, merge, feed and mature differently. The model requires each seed to complete feeding and secretion and reports exact yields for balance review, rather than encoding the old prototype's reward rate. Recovery is checked over a period after disturbance instead of requiring food reserves at one exact final frame. No metabolism or reward settings were changed.

Reports and screenshots are in `Library/EmergenceValidation`:

- `soft-spacing-before.txt`, `soft-spacing-after.txt`
- `model-and-scene.txt`, `soft-spacing-model-status.txt`
- `one-v-one.txt`, `two-v-two.txt`
- `one-v-one.png`, `two-v-two.png`

Earlier tuning results and the overly specific old yield/final-frame assertion failures are preserved in the other `soft-spacing-*.txt` reports. They are superseded by the final passing model and gameplay reports above. Prior hard-contact results used an older 1,400-particle profile and do not describe current behavior.

Reproduce with **MASSIVE > EMERGENCE > Validate Model and Scene**, **Validate Gameplay (1v1 and 2v2)**, and **Benchmark Soft Spacing**. Runtime fixtures restore loaded roots without saving their temporary changes.

## Remaining coverage

Human playtesting is still needed for morphology, reward pacing and competitive balance. Arbitrary attraction matrices need not form productive organisms. Large populations or dense clusters can still make the attraction calculations expensive; target-cabinet frame time and a packaged Windows build were not measured in this revision. Soft spacing cannot promise a strict minimum distance under every force setting or impact. Restart Play Mode after changing counts, sizes, preferred gap, core range or other distance ranges.
