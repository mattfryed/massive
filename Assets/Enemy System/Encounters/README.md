# Authored enemy encounters

The Player Actions scene now has two Enemy Lab modes. Select **Enemy Lab** in the Hierarchy and choose **Columns** for the existing six-column showcase or **Encounter Timeline** for the new shared-arena preview. The scene is saved with Encounter Timeline selected.

Click **Select timeline preview controls** on Enemy Lab. In Play Mode the preview offers:

- Full timeline or a single selected cue, with restart and pause/resume.
- Open, Blocked Center, and Side Wall Mounts placement fixtures. Changing the fixture, cue, timeline, or seed restarts the preview.
- Two scripted player actors that move, aim and attack using the existing player input system. They restore lost mass for continuous testing. Movement, attacks, mass restoration and looping can be switched off independently.
- A compact status line above the arena showing the active cue, clock, live/pending population and pressure. The Inspector shows every cue's result and any blocking reason.

These are test layouts in Player Actions, not replicas of NOVA, SINGULARITY, DYNAMO, PULSAR, HIGGS or ORBITAL. Actual level wiring and encounter balancing are still to be authored.

## Assets and authoring

`Enemy Lab/Enemy Lab Timeline.asset` holds the starter sequence. Its first arrivals are at 3, 15, 27, 39, 51 and 63 seconds; the preview loops at 85 seconds. It covers paired Drone rows, Ranged Drone flanks, staggered Seekers, Dyson flanks, wall turrets and a Carrier. `Enemy Lab Limits.asset` supplies population caps. Pressure costs on the timeline are provisional tuning values, not changes to health or rewards.

Create assets through **Create > MASSIVE > Enemies > Formation / Encounter Timeline**.

A formation lists explicit slots. Each slot has an enemy definition, warning prefab, region ID, position within the region (0–1), inward facing, release delay, and optional coordinated entrance. Use a wall socket ID for turrets. Regions and socket positions are authored on **Enemy Arena Layout** in normalized arena coordinates (-1–1). Socket roots lie on the wall; their clearance footprint is offset into the arena, and their aim arc is applied to the turret. A support collider can be explicitly excluded from that socket's placement check.

Select the preview object in Scene view to see formation footprints, headings and arrival times. Select the layout to see region borders, socket aim arcs and exclusion bounds. To inspect one formation without clutter, choose its single-cue preview.

Each cue specifies first arrival time, a formation, optional seeded variants/horizontal mirroring, an approved fallback, and maximum lateness. Slot release delays are relative to that first arrival. Arrival time means the enemy object appears after a complete warning; its existing shell assembly animation follows normally. Single-cue preview moves the first arrival to one second plus the warning duration.

## Runtime behavior

The existing EnemyDirector owns this mode. Assigning a timeline replaces its random and batch scheduling; leaving the timeline empty retains the legacy spawning behavior. All spawned enemies still use their canonical definitions, controllers, damage, rewards and death bookkeeping.

The director resolves a complete formation before announcing it. Every member reserves its footprint, socket, population capacity and pressure cost. Equal-delay members release together. Placement checks use enemy radius, arena boundaries, explicit exclusion colliders, the director's obstacle mask, players, existing enemies and other reservations. An optional AmplifierSpawnRegion supplies the existing shared exclusion policy for floor placements.

Blocked formations retry briefly. A fallback can be chosen before warnings appear. Announced positions never relocate; if they become blocked, the remaining formation waits only until its release deadline and then expires. Delayed staggered releases retain their spacing rather than releasing overdue members in one frame. Skipped/expired cues remain visible in the Inspector.

Pressure includes living enemies, reserved enemies and every Carrier-launched Drone. Carrier launches go through the same cap and pressure gate. A timeline ending does not kill live enemies; only the lab's explicit restart/loop clears its own actors. New gameplay timers and warnings use the director's active gameplay clock and respect its match/anomaly gates.

The timeline coordinates spawning, not a global attack queue. Existing enemy targeting/attack logic takes over after arrival or optional entrance. The preview shares targets within its own simulation scope and cannot target the live player roster. The column showcase retains one target per column.

## Validation

Run **MASSIVE > Demonstrations > Validate Encounter Timeline** from Edit Mode with Player Actions open. Results are written to `Library/EnemyEncounterValidation/report.txt`. The checks cover atomic reservations, pressure/caps, blocked warnings and expiry, pause clocks, entrance handoff, wall mounting, all six enemies in the shared preview, looping, and return to the original column showcase. Tests modify runtime copies of tuning assets and leave the saved scene settings intact.
