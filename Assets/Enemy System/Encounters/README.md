# Authored enemy encounters

The Player Actions scene now has two Enemy Lab modes. Select **Enemy Lab** in the Hierarchy and choose **Columns** for the existing six-column showcase or **Encounter Timeline** for the new shared-arena preview. The scene is saved with Encounter Timeline selected.

Click **Select timeline preview controls** on Enemy Lab. In Play Mode the preview offers:

- Full timeline or a single selected cue, with restart and pause/resume.
- Open, Blocked Center, and Side Wall Mounts placement fixtures. Changing the fixture, cue, timeline, or seed restarts the preview.
- Two scripted player actors that move, aim and attack using the existing player input system. They restore lost mass for continuous testing. Movement, attacks, mass restoration and looping can be switched off independently.
- A compact status line above the arena showing the active cue, clock, live/pending population and pressure. The Inspector shows every cue's result and any blocking reason.

These are test layouts in Player Actions, not replicas of NOVA, SINGULARITY, DYNAMO, PULSAR, HIGGS or ORBITAL. Actual level wiring and encounter balancing are still to be authored.

## Assets and authoring

Open **MASSIVE > Enemies > Encounter Composer**, double-click a timeline asset, or use **Open Encounter Composer** on an Enemy Director or Enemy Lab inspector. The dockable window edits the existing timeline asset and loads the reusable formation library. Its **Director** field accepts both a Lab preview and a playable level's Director.

- Drag a formation from the library onto its enemy row. The drop time is its first arrival; the lighter segment before it is the warning, and the blue segment is the staggered spawn sequence. Mixed formations have a separate row. Overlapping cues stack vertically.
- Drag a block to change its time. Snap supports .25s, 1s, or Off; hold Alt during a block drag to bypass it. Click the ruler to set the insertion cursor, then use **Add at cursor**, or double-click the matching row.
- Select a block to edit its name, first arrival, lateness, variants, fallback and permission for seeded mirroring. **Duplicate** / Ctrl+D adds a copy five seconds later; Delete removes the selection. Ctrl+Z / Ctrl+Y support Undo/Redo. Edits save automatically; **Save** / Ctrl+S also saves the asset.
- **Override spawn policy** on a selected block controls Strict/Flexible integrity, maximum position adjustment and blocked-slot grace for that encounter. Otherwise it inherits the chosen formation's settings. Click a library formation to edit **Shared Spawn Settings** for every encounter using it. The six Drone/Ranged batches default to Flexible, a 1-world-unit adjustment limit and 3 seconds of grace.
- **Fit** frames the whole timeline; Zoom and scroll inspect tighter timings. The arena preview uses the chosen layout and seed, shows simultaneous placements in gray, and flags blocked footprints in red. Count, spacing and stagger belong to the shared formation asset, accessible through **Inspect shared formation asset**.
- **Play all** or **Play cue** starts the assigned Director in Play Mode. In the Lab, Restart resets the preview actors. In a playable level, the normal match players remain in control and Restart clears only that Director's enemies and schedule; it does not restart the match or environmental systems. Pause/Resume controls the Director's gameplay clock. Live blocks show cue results and the selected block shows its blocking reason; population, reservations and pressure appear in the toolbar. A single-cue preview rebases the playhead to that cue's authored position. Stop through Unity's normal Play control. Authoring is locked during playback.
- Live encounter details show spawned, waiting and skipped counts plus individual blocking reasons. The live arena diagram shows actual reserved positions: orange for adjustments with dotted lines from the original positions, red for blocked/skipped slots and green for spawned slots. Edit Mode shows authored positions; it cannot predict future enemy movement.

The cursor is for insertion, not simulation scrubbing. Blocks represent spawn sequences, not enemy lifetimes. Authoring checks cover the selected seed's references, timing, batch budgets, mount availability, boundaries, exclusions and possible reservation overlaps. Surviving enemies and moving players still require the live preview. Dragging cues preserves their list indices and seeded choices; removing a cue shifts later indices and may change those choices. Enemy Lab layout fixtures are configured on its Inspector.

The starter sequence in `Enemy Lab/Enemy Lab Timeline.asset` has arrivals at 3, 15, 27, 39, 51 and 63 seconds (Drone rows, Ranged Drone rows, Seekers, Dyson, turrets, Carrier), rings at 87/111 seconds and phalanxes at 135/159 seconds. The current user-edited asset adds six groupings, for 16 cues, and retains its 181-second duration. Use single-cue Preview to inspect a batch immediately. The original two-enemy Ranged Flanks asset remains available for authoring.

Drone and Ranged Drone each have three reusable batches:

| Batch | Count | Placement and sequence |
| --- | --- | --- |
| Paired Rows | 20 | Ten top, ten bottom. Each row starts nearest its middle, then alternates outward at .25s per member; matching top/bottom members appear together. |
| Center Ring | 10 | Radius 3.8 world units in the 28×12 lab arena. Opposite pairs start at left/right, then alternate clockwise/counterclockwise at .25s per pair. |
| Goal Phalanxes | 20 | Two 1–2–3–4 triangular ranks, with horizontal centroids at ±5 world units, facing the goals. All members arrive together. |

Matching Ranged batches are offset: rows interleave by 1.25 units horizontally, the ring rotates 18 degrees, and phalanxes shift 1.15 units vertically (opposite directions for left/right). Minimum cross-type clearance is 1.15 units, above the combined 1.10 spawn footprint. These offsets permit matching batches at the same arrival time; arbitrary combinations still use the director's collision and budget checks. All slots use a short coordinated entrance before normal AI.

Positions are normalized to the lab's arena, so these world distances scale with a different arena size. `Enemy Lab Limits.asset` allows 40 total and 24 Ranged enemies; the current timeline's user-edited pressure limit is 120. A matching 20-Drone/20-Ranged pair costs 60 pressure in an otherwise empty arena. Existing enemies still consume population and pressure capacity. Enemy definition caps and gameplay stats are unchanged. Preview actors start farther toward the goals to leave the phalanx footprints clear; **Actor Horizontal Position** controls that distance.

Create assets through **Create > MASSIVE > Enemies > Formation / Encounter Timeline**.

### ORBITAL integration

`S-6_ORBITAL` contains **ORBITAL Enemy Timeline**, using the same `Enemy Lab Timeline.asset` by reference. Composer edits therefore affect both scenes. The level's separate `ORBITAL Encounter Profile.asset` controls enabled/anomaly behavior; population and pressure still come from the timeline. The schedule begins when match scoring opens, follows the match's active clock and stops when scoring closes. It runs once per match rather than looping the Lab's demonstration.

ORBITAL's layout supplies the shared formation regions, Y=0 placement and four top/bottom turret mounts. **Allow Resonance And Amplifier Overlap** and **Spawn Overlap Roots** permit arrivals over the paired power-up system and the Orbital Cloud hierarchy. These options only filter spawn-clearance queries: they do not disable gameplay collisions, enemy sludge, Cloud impulses or environmental timers. The Director does not borrow the Amplifier spawn region. Ordinary obstacle/no-spawn layers, authored layout exclusions, player occupancy, living enemies and reservations remain enforced.

**MASSIVE > ORBITAL > Set Up Enemy Lab Timeline** installs this wiring on ORBITAL's existing Director. **Validate Encounter Integration** exercises the real match startup, generated environmental colliders, overlap arrivals, normal-roster targeting and Composer controls. Reports are in `Library/OrbitalEncounterValidation`.

A formation lists explicit slots. Each slot has an enemy definition, warning prefab, region ID, position within the region (0–1), inward facing, release delay, and optional coordinated entrance. Use a wall socket ID for turrets. Regions and socket positions are authored on **Enemy Arena Layout** in normalized arena coordinates (-1–1). Socket roots lie on the wall; their clearance footprint is offset into the arena, and their aim arc is applied to the turret. A support collider can be explicitly excluded from that socket's placement check.

Select the preview object in Scene view to see formation footprints, headings and arrival times. Select the layout to see region borders, socket aim arcs and exclusion bounds. To inspect one formation without clutter, choose its single-cue preview.

Each cue specifies first arrival time, a formation, optional seeded variants/horizontal mirroring, an approved fallback, and maximum lateness. Slot release delays are relative to that first arrival. Arrival time means the enemy object appears after a complete warning; its existing shell assembly animation follows normally. Single-cue preview moves the first arrival to one second plus the warning duration.

## Runtime behavior

The existing EnemyDirector owns this mode. Assigning a timeline replaces its random and batch scheduling; leaving the timeline empty retains the legacy spawning behavior. All spawned enemies still use their canonical definitions, controllers, damage, rewards and death bookkeeping.

### Population controls

Composer's **Population** section edits the timeline asset: **Total limit**, one limit per enemy type, and **Pressure limit** with expandable **Pressure costs**. A zero limit disables that gate; an enemy without an entry is unlimited. In timeline mode these replace SpawnProfile total/category caps and EnemyDefinition/rule caps. The profile still controls enabled, match/anomaly behavior; legacy random/batch spawning keeps all its existing caps when no timeline is assigned.

The Lab timeline starts at total 40, Drone 24, Ranged Drone 24, Seeker 12, Turret 8, Carrier 2, Dyson unlimited, and pressure 120. These are explicit starting values, not an automatic estimate from the schedule. Composer can check whether a batch fits by itself; the live preview is necessary to see how surviving enemies and Carrier offspring overlap later encounters. Docked Drone visuals are not living enemies; launched Drones consume the Drone and total budgets once, plus their own pressure cost. The Carrier itself retains its own cost. Its standalone maxActiveDrones pre-check also yields to the timeline, so it cannot silently impose another ceiling.

Blocked cues explain the controlling limit with alive, reserved and requested counts. Reservations include warnings and blocked waiting slots, and are released on expiry/cancellation. Full-batch budget admission remains in effect. Increasing a limit does not change spatial clearance requirements.

**Unlimited Lab preview** bypasses total, per-enemy and pressure gates only in an active EnemyEncounterLab's own simulation session. It displays **UNLIMITED PREVIEW** in Composer and the Lab HUD, leaves the timeline asset limits intact, and keeps placement, warning, pause and expiry behavior. Toggle it during Play Mode for a temporary comparison or in Edit Mode to store the Lab preference. Re-enabling limits never kills existing enemies; new arrivals must fit again. This toggle is separate from the gameplay timeline.

The director reserves population capacity and pressure for the full batch. Those limits still gate the whole encounter. Spatial conflicts follow the selected integrity policy:

- **Strict** requires the complete formation to fit. An obstruction after warnings holds all remaining members until the release deadline. This preserves the original behavior.
- **Flexible** releases each clear slot while blocked slots retry independently. Each slot must finish its warning and spawn by its authored arrival plus its release delay plus **Blocked-slot grace**. At expiry, only that slot is skipped and its budget is released. The encounter reports Partial if some members arrived. Waiting slots reserve budget; only successfully placed slots reserve space.

**Max lateness** bounds the initial attempt to reserve the batch with enough warning time, including budget retries and approved fallback selection. Once a Flexible batch is reserved, its individual slot grace controls release deadlines. A slot that clears late still needs the full warning; it can expire before showing a warning if too little grace remains. An approved fallback is considered before any warning, including when every Flexible slot is blocked. Partial formations do not switch fallback after announcing.

Before announcing, the director checks a bounded, deterministic set of nearby poses. It prefers translating an entire row/phalanx or rotating a ring, then tries local corrections where necessary. **Placement group** IDs keep the initial group search coherent; **Adjustment pattern** selects the formation's preference. All displacement stays within **Max position adjustment** of the original authored slot. Wall sockets never slide. Every candidate passes enemy-radius, arena-boundary, explicit-exclusion, obstacle-mask, player, existing-enemy and reservation checks. An optional AmplifierSpawnRegion supplies shared no-go, Resonance and obstacle exclusions, while EnemyArenaLayout owns timeline territory and height. Its power-up neutral stripe, spawn height and goal-attraction radius apply only to Amplifier and legacy random/batch placement. Physical goal colliders still block enemy placement. A bounded search may leave a slot blocked even if a more distant position is clear.

**Enemy Arena Layout > Gameplay Height** defaults to world Y=0 for preview players, enemy arrivals and wall sockets, independently of the decorative grid's height. The Enemy Lab waits for match startup, fully deactivates the normal player roots, then starts the selected preview. This removes hidden match players from active-player and spawn-blocking checks. Leaving the Lab restores the previously active players and their visuals; unused roster slots remain inactive.

Once a telegraph appears its position is locked. An actor entering it makes the slot wait, never teleport or spawn through the actor. Clear slots retain the authored stagger; frame hitches do not dump all overdue releases into one frame. Equal-delay independent slots release together when clear, but do not wait for blocked peers. To require a pair/group to announce, release and expire together, assign its slots the same positive **Release group** and equal delays. Zero means independent. The default Drone batches use independent slots.

Mobile enemies gently steer away from active telegraph footprints during ordinary movement. The Director's **Announced arrival avoidance** settings control strength, padding and look-ahead. Drone launches/recoil, Ranged charges/bursts, Seeker committed attacks and Dyson attacks retain their movement. Carriers yield using eased, collision-checked movement, with turret-lane clearance taking priority. Players and wall-mounted turrets are never repositioned. Avoidance is a best-effort aid; placement checks and per-slot expiry remain authoritative.

Pressure includes living enemies, reserved enemies and every Carrier-launched Drone. Carrier launches go through the same cap and pressure gate. A timeline ending does not kill live enemies; only the lab's explicit restart/loop clears its own actors. New gameplay timers and warnings use the director's active gameplay clock and respect its match/anomaly gates.

The timeline coordinates spawning, not a global attack queue. Existing enemy targeting/attack logic takes over after arrival or optional entrance. The preview shares targets within its own simulation scope and cannot target the live player roster. The column showcase retains one target per column.

## Validation

Run **MASSIVE > Enemies > Validate Encounter Composer** in Edit Mode for editing/Undo/Redo, saved asset round trips, seed parity with the Director, placement conflicts and authoring diagnostics. It uses a temporary asset and leaves the authored timeline unchanged. Report: `Library/EnemyEncounterComposerValidation/report.txt`.

Run **MASSIVE > Demonstrations > Validate Encounter Timeline** from Edit Mode with Player Actions open. Results are written to `Library/EnemyEncounterValidation/report.txt`. The checks cover atomic reservations, pressure/caps, blocked warnings and expiry, pause clocks, entrance handoff, wall mounting, all six enemies in the shared preview, looping, and return to the original column showcase. They also check Y=0 placement, match-roster suspension/restoration, and shared exclusions without the Amplifier neutral stripe. Tests modify runtime copies of tuning assets and preserve the authored timeline, assigned placement region and saved scene settings.

Run **MASSIVE > Demonstrations > Validate Drone Formations** for formation counts, center-out timing, circle geometry, phalanx centers/headings, simultaneous cross-type clearance, and live paired-batch release/entrance/cap checks. Report: `Library/EnemyDroneFormationValidation/report.txt`. The older encounter regression uses compact runtime fixture copies so its atomic-six-member cases stay independent of authored batch sizes.

Run **MASSIVE > Demonstrations > Validate Flexible Spawning** for independent blocked slots, complete late warnings, immutable telegraphs, Strict overrides, linked groups, bounded row/ring/phalanx corrections, player occupancy, fixed wall sockets, cap/pressure enforcement, pause/restart cleanup and live Drone avoidance. Report: `Library/EnemySpawnFlexValidation/report.txt`. Tests use runtime copies and verify the authored timeline is unchanged.

Run **MASSIVE > Demonstrations > Validate Timeline Population** for timeline authority, legacy fallback, reservation accounting, Carrier launches, detailed budget reasons and Lab-only bypass isolation. Report: `Library/EnemyPopulationValidation/report.txt`.


### Manual player control

At the top of Composer's details panel, enable **Manual control (P1)** under **Preview Player**. The same preference is available on EnemyEncounterLab in the Inspector. Click the Game view to control the left preview actor using the existing Player 1 movement, Sword and Shield bindings, including its assigned controller. The other actor still follows Actors Move / Actors Attack. Those automatic settings do not restrict your manual actor.

Toggle during playback to take over or return to automatic control without recreating players or resetting the timeline. Handoffs clear scripted inputs and cancel the outgoing attack/shield and momentum. Timeline pause locks both preview actors; focus loss locks the manual actor. Resume/focus returns the selected control mode. Restart and timeline loops carry the manual preference to the fresh actor. The actor remains scoped to the Lab, uses shared gameplay tuning, and does not participate in match scoring. Existing Restore Player Mass behavior still applies.

**MASSIVE > Demonstrations > Validate Timeline Manual Control** runs input-source routing, handoff, pause, lifecycle and isolation checks. Its report and current keyboard bindings are in `Library/EnemyManualControlValidation`. Physical keyboard/controller play should be checked in the focused Game view. It uses a runtime timeline copy and does not save the scene.
