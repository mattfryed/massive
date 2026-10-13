# EMERGENCE validation — 2026-10-12

Validated in Unity 6000.0.28f1, Built-in Render Pipeline, Windows/DX11.

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
- Scene checks verified six universal roots, shared PULSAR-derived systems, valid shader and 6,900 serialized reference slots. EMERGENCE remains absent from level selection and available for direct scene loading. PULSAR and the level catalog have no Git diff from this change.
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
