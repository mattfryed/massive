# Drone prototype

Common, one-hit contact enemy. The long black crystal half is the forward nose (+Z);
the short wireframe half is the exhaust end (-Z). Each half has six triangular
facets. The tail length defaults to half the nose length. The shell rolls around
its long axis while moving or idle; gameplay pause also freezes its animation.

## Assets and tuning

- `Enemy_Drone.prefab`: Drone Controller (detection, target retention, acceleration,
  separation, idle movement, shield recoil, spawn grace); Drone Visuals (shape,
  axial roll, outlines, core, exhaust, spawn and breakup).
- `Drone Score Toast.prefab`: compact world-space energy label. Font size, lifetime
  and rise distance are editable here; Enemy Score Reward references it on the Drone.
- `ED_Drone.asset`: one health / one sword damage, movement speed 2.4, turn speed
  220, contact mass loss 0.04, and the `ENEMY_DRONE_DEFEAT` reward identity.
- `ESP_DronePrototype.asset`: scheduled batches begin at 2 seconds. Batch size
  rises from 3 to 7; the wait after a batch decreases from 10 to 3 seconds over
  120 active gameplay seconds. Members arrive 0.22 seconds apart within a 1.8-unit
  cluster. Maximum 24 Drones / 26 total enemies. Dyson's weighted single-spawn
  rule becomes eligible at 25 seconds, capped at two.
- `Drone Swarm Spawner.prefab`: reusable EnemyDirector plus its own full-width
  placement region. Assign scene Arena Bounds and optional Amplifier/Resonance
  spawner references. The existing shared exclusion code protects no-go zones,
  solid obstacles, players, goals and active Resonance footprints.
- `Assets/Scripts/Scoring/ScoreEconomyProfile.asset`: **100 meV** and **0.25 personal
  charge** per attributed defeat. Both personal and team Amplifier multipliers
  apply. With the current charge settings, 16 unassisted Drone kills advance a
  fresh player from x1 to x2. Values are an initial prototype balance.

The 120-second spawn ramp is configurable separately from the master score
asset's existing 1,200-second testing duration. The ramp saturates; it does not
continue increasing forever. Existing stage profiles are preserved.

## Interaction contract

- Drones acquire nearby active, living players, retain a target through small
  distance changes, and release invalid or distant targets. Pseudo-players and
  friendly ownership are excluded. Perception is staggered across Drones.
- Movement uses acceleration, a turn limit, neighboring-Drone separation,
  feeler-based obstacle avoidance, a capsule sweep with surface sliding and
  recovery from initial/moving-obstacle overlaps, plus arena containment.
  The Drone includes Default, Enemy, PowerUps, NoSpawnZone and Obstacle layers.
  Its shared query filter excludes its own body, players/weapons, other Drones
  (handled by separation), floor and ordinary pickup triggers. No-spawn/obstacle
  triggers remain barriers. This is local steering, not a general maze pathfinder.
- Resonance surfaces with **Enemies Use Sludge** enabled are traversable. Drones
  use the same pattern-level sludge multiplier, drag and feather reach as players;
  normal speed returns after leaving or switching off the field. Dyson pursuit
  and lunges use this shared enemy movement influence too. Ordinary obstacle
  avoidance and Resonance-safe spawn placement remain in effect.
- A short spawn grace blocks movement and contact damage while the crystal
  appears. An active sword hurtbox can still defeat a spawning Drone.
- Shield contact repels a Drone without granting score. Active attacks and
  invulnerability prevent its contact damage. A successful contact hit removes
  the configured mass once and consumes the Drone without defeat score.
- Sword hits use EnemyHurtbox and EnemyScoreReward; attributed supported combat
  sources can call EnemyBase.TakeDamage. Duplicate callbacks cannot award again.
  The latest persistent-charge and team-Amplifier rules are used unchanged.
- Accepted, attributed defeats display their actual final credited energy including
  personal and team multipliers; e.g. +100 meV or +1.6 eV. Contact suicide and
  administrative/duplicate/rejected awards produce no toast. Up to 24 lightweight
  text labels are reused; a full pool replaces the oldest label. They live in the
  gameplay scene, independently of the dying enemy, and retire after 0.8 seconds.
- Each facet traces its perimeter from a vertex with independently varied start
  time and duration. Black fill grows from that vertex behind the tracing. Death
  reverses the paths while separating the facets, preserving partial progress
  when a Drone dies during its spawn. All parts finish within the lifecycle duration.
- Death disables colliders immediately, stops the body and animates retracting,
  separating facets for 0.32 seconds. Existing enemies retain instant removal
  unless their new optional despawn delay is configured.
- Batch members reserve space independently. Blocked/capped members retry briefly
  and then expire; no backlog is released as a sudden burst. Match/bonus gating
  freezes batch progression. Paused enemies remain rendered.

The core/exhaust reuse MASSIVE/MetaballSDF with a white shared material, a per-instance
property block, seven small core lobes, four connecting nozzle lobes and at most
28 exhaust droplets (39 total, within the shader's 48-ball limit). Lobes vary in
radius and orbit. Exhaust starts densely connected, accelerates and shrinks toward
its tip, with variation in size, speed, lifetime and trajectory. Emission catches
up across slower frames without stacking newborn particles. Visual randomness is
independent of gameplay randomness. No GameObjects are spawned per droplet.
Shell meshes are generated as nonpersistent data and released on disable.
Enemy pooling and large-swarm GPU profiling remain future work;
the prototype's population cap is intentional.

## Editor workflow

### Batch spawn warning

The Drone batch profile now references **Drone Spawn Telegraph**, a 2.8x outline
of the same six-sided crystal. Its 18 edges use a diffuse white glow, a slow axial
roll, and a small breathing motion. A subtle dark underlay helps it read on bright
surfaces. Four faint ghost copies drift outward in independently randomized
directions on the gameplay plane, retaining the crystal orientation from their
birth and fading as they spread. Lifetimes and travel distances vary by 25%.
Each batch reuses the same four ghost renderers (up to eight when configured),
mesh and glow material. The effect does not require post-processing bloom and
has no collision, damage, or score. Its private random stream leaves spawning
and other gameplay randomness untouched.

The director chooses a safe cluster center, shows the marker for **3 seconds**,
and then spawns individuals around that fixed center. It stays visible throughout
the batch and fades away over **0.45 seconds** after the final unit appears.
Placement is checked again for every unit. An obstructed or capped batch times out
at its announced location rather than spawning elsewhere without a warning.

Open **MASSIVE > Drone Spawn Indicator** for the combined settings panel. It
finds the scene's Drone spawn profile, falling back to `ESP_DronePrototype`, and
edits its referenced assets directly with Undo support. Controls are editable
outside Play Mode and persist for future batches in every level using those
assets. The Locate buttons select the timing asset, indicator prefab or material
in the Project window. The same panel is also under Enemies > Drone > Spawn
Indicator Settings.

All three assets are in `Assets/Enemy System/Enemy Types/Melee/Drone`:
- `ESP_DronePrototype.asset`: Drone batch **Telegraph Seconds** (warning lead),
  default 3 seconds, and **Telegraph Prefab** reference.
- `Drone Spawn Telegraph.prefab`: size, roll, breathing, fades and ghost controls.
  Ghost defaults: enabled, four copies, 0.2 opacity, 0.9-second lifetime,
  0.75 world-unit travel and 25% lifetime/distance variation. Shorter lifetimes
  increase drift speed; more copies increase density.
- `Drone Spawn Glow.mat`: glow color, diffuse width, brightness and contrast
  underlay. Ghosts inherit the glow but omit the dark underlay.

A missing telegraph prefab retains immediate spawning for other rules.

The initial delay and completion-to-next-batch delay still target the first unit.
The warning starts that many seconds earlier, with a minimum full warning period:
the current first delay of 2 seconds therefore becomes a warning at gameplay start
and the first Drone at approximately 3 seconds. The 10-to-3-second later batch
cadence is preserved. The warning clock, roll, and fading pause with the spawning
clock, including every ghost; ending the match, disabling the director/profile,
or removing the owner cleans up pending warnings and their copies.

`MASSIVE > Enemies > Drone > Create Spawn Telegraph` creates and wires missing
assets without resetting existing tuning. `Run Spawn Telegraph Validation` checks
the production warning, timing, batch completion, fixed placement, pause, caps,
obstruction, and cleanup in a temporary Play Mode scene.

`MASSIVE > Enemies > Drone > Add Swarm To Active Scene` adds a wired spawner when
the scene has none; it leaves the scene unsaved. Existing directors should instead
receive the desired batch rule/profile. Do not run two directors for the same
population. The Drone is included in the gameplay gallery catalog; the existing
gallery rebuild command can refresh its display when desired.

`MASSIVE > Enemies > Drone > Run Prototype Validation` uses a temporary Play Mode
scene and cloned tuning assets, then restores the prior play-start scene and
removes the fixture. It does not save the open gameplay scene.

## Validation — 2026-09-08

The outward-ghost update passed 39 focused spawn-indicator Play Mode checks,
including bounded renderer reuse, randomized directions, gameplay-random-stream
isolation, pause, disable/re-enable and completion cleanup. Dark- and light-surface
captures were inspected. The combined settings panel resolved the production
profile; prefab edits persisted through save, Undo and Redo on an isolated asset
copy. No Console errors or warnings were reported during these checks.

143 targeted Play Mode checks passed: actual sword/hurtbox collision, one award per
life, charge contribution, personal/team multiplier combination, cleanup, target
acquisition/retention/loss, idle roll, emitted exhaust, pause, spawn grace, single-use
contact damage, shielding, active attacks, separation, thin-wall avoidance,
staggered batches, caps, scoring-open gating and bonus pause. Refinement coverage
includes partial/staggered tracing, completed/retracting outlines, a bounded denser
engine, actual final-score toasts, fractional energy units, toast expiry/reuse,
and routes around the actual Amplifier and Dyson prefabs without penetration,
including recovery when obstacles overlap the Drone. Captured runtime frames
were visually inspected for spawning, breakup, sustained exhaust and score text.

The Resonance extension adds actual Drone/Dyson traversal through generated hard
contact geometry with sludge, recovery on exit, Dyson lunge slowdown, collider-pair
ignore/restore, normal walls behind Resonance, overlapping patterns, pause and
interaction-gate cleanup, and unchanged player sludge. The complete run finished
without Console errors. The gameplay scene was not saved by validation.

The player feedback extension adds 26 checks for directional indentation, outline
recovery, damage scaling, both team colors, bounded repeated hits, recoil direction
and distance, steering, real wall collision, rejected hits, Drone shielding/contact,
generic touch, Dyson lunge, projectile source destruction, suppression and death.
Runtime impact/ripple/recovery frames were visually inspected. Tuning is documented
in `Assets/Scripts/Player/Enemy Hit Feedback.md`.

A subsequent dark-team palette regression was reproduced with the actual player
prefab. Restoring the visual rig's original local palette lookup keeps its black
body and white outline. All 31 focused feedback checks passed after the correction,
including new production-prefab checks before, during, and after impact. This fixes
an incorrect white-palette expectation in the original synthetic feedback test.

The spawn-warning extension adds 27 checks for the production crystal outline,
three-second lead time, fixed center, staggered arrivals, completion fade, shared
pause clock, safe placement revalidation, blocked-batch cancellation, population
caps, match-end/disable cleanup, and compatibility with rules without warnings.
The complete 143-check run passed with no Console errors. Captures of the warning
and surrounding spawned Drones were visually inspected after reducing the initial
glow so its individual edges and open center remain readable.

The original avoidance mask excluded both prefabs' Default-layer solid colliders.
A follow-up route test exposed an inverted side choice and a sweep that discarded
all tangential motion at contact. The corrected side selection is opt-in for Drones;
other enemies retain their existing steering configuration. The movement guard
now slides and rechecks clearance, including during shield recoil.

The original prototype live check ran for approximately 74 seconds. The refinement
check ran for approximately 51 seconds in the actual Dynamo scene and spawned 25
enemies; ten Drones (nine moving) and two Dyson Spheres were alive at the sample.
A live attributed defeat credited 100 meV and displayed the reduced-size toast;
its captured image was inspected. No Console errors appeared during this live run.
The scene's existing duplicate AudioListener warning remains.
This is prototype validation, not a target-platform build or a measured large-swarm
performance budget. Hands-on tuning of damage, speed and cadence remains useful.

Optional names: **Shard**, **Splinter**, or **Dart**. Drone remains the working name.
