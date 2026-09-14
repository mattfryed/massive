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
- Idle movement uses a loose local swarm: nearby idle Drones gather, partially
  align, circle, and follow distinct smooth wandering paths. A soft roaming
  radius keeps them near their spawn point (or the place pursuit ended).
  The prefab's **Idle Speed** is now 0.85. **Idle Swarm** exposes neighbor radius,
  cohesion, alignment, circling, wander, and roam radius. Player pursuit takes
  priority; losing the target resumes swarming near the current position.
  Neighbor decisions share the existing staggered perception update, with no
  extra scene scans or per-frame neighbor-list allocations.
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

The Drone batch profile references **Drone Spawn Telegraph**, a 1x outline
of the same six-sided crystal. Its 18 edges use red lines with orange-red glow,
a slow axial roll, and a small breathing motion. A subtle dark underlay helps it read on bright
surfaces. Faint ghost copies drift outward in independently randomized
directions on the gameplay plane, retaining the crystal orientation from their
birth and shrinking to 30% scale as they fade and spread. Lifetimes and travel distances vary by 25%.
Each marker reuses its configured ghost renderers (up to eight),
mesh and glow material. The effect does not require post-processing bloom and
has no collision, damage, or score. Its private random stream leaves spawning
and other gameplay randomness untouched.

At shared vertices, the focused wire stays at full brightness to keep each join
continuous. Only the surrounding glow receives gentle compensation: a six-edge
join keeps 62.5% of each edge's broad glow, rather than dividing both layers down
to one sixth. The contrast underlay remains compensated; as the wire defocuses,
it gradually adopts the gentler glow weighting. Outer caps keep their weighting
to avoid rings during defocus. A derived
mesh carrying these join counts is shared across concurrent markers and ghosts,
then released with its last owner; the original outline asset is untouched.

The director chooses a safe cluster center, shows the marker for **3 seconds**,
and then spawns individuals around that fixed center. A broad, soft orange-red glow
appears first and gains strength through the warning; the wireframe begins appearing
after a **0.28-second** head start. When the **first** unit begins spawning, the
indicator blurs out over **0.9 seconds**, overlapping the enemy's arrival animation.
The blur spreads every outline and ghost while the broad glow expands; the enemy
itself stays sharp. Remaining batch members retain their original staggered cadence.
Placement is checked again for every unit. An obstructed or capped batch times out
at its announced location rather than spawning elsewhere without a warning.

Open **MASSIVE > Enemy Spawn Indicators** for the combined settings panel. It
finds the scene's Drone spawn profile, falling back to `ESP_DronePrototype`, and
edits its referenced assets directly with Undo support. Controls are editable
outside Play Mode and persist for future batches in every level using those
assets. The Locate buttons select the timing asset, indicator prefab or material
in the Project window. The same panel is also under Enemies > Drone > Spawn
Indicator Settings. The original **MASSIVE > Drone Spawn Indicator** shortcut
still opens the Drone tab. The **Dyson Sphere** tab controls that enemy's warning.

The Drone tab's **Indicator mode** switches between **Per cluster** (default)
and **Per Drone**. Per Drone uses the same outline, glow and ghosts at each unit's
exact reserved arrival position. Announcements are staggered by the existing
within-batch interval, and every unit gets the complete warning lead. Its marker
blurs away when that unit begins spawning. Locations and population slots are reserved while
the warning runs; blocked positions retry briefly and cancel without relocating.
Overdue arrivals remain spaced, including after an obstruction clears. Pause,
match-end and director-disable behavior applies to all pending warnings.

All three assets are in `Assets/Enemy System/Enemy Types/Melee/Drone`:
- `ESP_DronePrototype.asset`: Drone batch **Telegraph Seconds** (warning lead),
  default 3 seconds, and **Telegraph Prefab** reference.
- `Drone Spawn Telegraph.prefab`: size range, roll, breathing, timing, defocus,
  broad glow strength/size, independent wireframe/glow/ghost colors and ghost controls.
  Size range defaults to (1, 1), matching the enemy's scale even under a scaled root.
  Set different minimum and maximum values for per-marker variation; breathing is separate.
  Ghost defaults: enabled, four copies, 0.2 opacity, 0.9-second lifetime,
  0.75 world-unit travel and 25% lifetime/distance variation. Shorter lifetimes
  increase drift speed; more copies increase density.
- `Drone Spawn Glow.mat`: edge glow width, brightness and contrast underlay.
  Ghosts omit the dark underlay. Runtime colors come from the indicator prefab.

The shared `EnemySpawnTelegraph` component and outline shader provide this sequence
for both current enemies and future indicator prefabs. Shared halo assets live in
`Assets/Enemy System/Resources`; the effect adds one reused mesh/material renderer
per active indicator and needs no scene-wide blur or bloom. New enemy indicator
meshes should describe the enemy's wireframe at its native prefab scale.

A missing telegraph prefab retains immediate spawning for other rules.

Dyson Spheres use the same warning renderer and timing lifecycle through their
weighted spawn rules, with a separate 80-face spherical outline and glow asset
in `Assets/Enemy System/Enemy Types/Melee/DysonSphere`. Both existing profiles
that include Dyson now use a 3-second warning and a 2-second blocked-location
timeout. Rule eligibility starts the warning; the enemy appears after the lead.
Dyson's prefab now references the shared `Drone Score Toast` through
`EnemyScoreReward`, displaying the actual credited energy, including multipliers.
Its authored reward remains 1 eV in the master score economy; administrative
removal and rejected/duplicate awards produce no toast.

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

## Validation — 2026-09-09

The gentler glow correction passed **61 indicator/spawn checks**. The rendering
test now separates wire and glow into color channels: every vertex must retain
the original wire brightness, tip glow must remain present with a modest
reduction, and mid-edge glow must retain its brightness. This replaces the prior
test that rewarded a large drop in combined tip brightness and missed the broken
appearance of the joins. The Dyson first-frame fix remains covered.

### Earlier vertex-glow and Dyson first-frame fixes

The vertex-glow and Dyson first-frame fixes passed **60 indicator/spawn checks**.
A rendered comparison verifies reduced tip brightness with unchanged mid-edge
brightness, plus shared-mesh retention/release. Fresh and re-enabled Dyson spawns
start with no full black faces, then reveal normally. The original flash was
reproduced as 80 visible full panels alongside 80 collapsed spawn panels at time
zero: both Awake and OnEnable built a shell, leaving the first one visible until
deferred destruction. OnEnable now owns the build, and retired panels are hidden
immediately. A refreshed 30 fps capture checks the exact transition frame.

### Earlier shared glow/defocus update

The shared glow/defocus update passed **50 focused indicator checks** and **36
swarm and individual/weighted spawn checks**, with no Console errors or warnings.
Coverage includes glow-first buildup, default enemy-relative size under a scaled
parent, optional size ranges, shrinking ghosts, independent colors, shared blur,
overlapping cluster arrivals, pause, reservations and complete cleanup. Both
shaders compiled without errors; dark- and light-surface captures were inspected.
The preview uses the production prefabs in an isolated scene.

### Earlier swarm and Dyson update

The complete updated suite passed **191 Play Mode checks**, including 36 new
checks for pipelined individual warnings, exact reserved positions, capacity
reservations, blocked-location cancellation, Dyson warning/pause/cleanup,
amplified Dyson toasts and duplicate rejection, idle swarm motion, pause, and
transitions into and out of pursuit. The existing combat, obstacle avoidance,
Resonance sludge, player feedback, cluster warning and ghost checks also passed.
Dyson warning captures were inspected on dark and light surfaces. An older test
that required a warning larger than 2x was corrected to validate a positive,
finite authored scale; that update preserved the then-saved 1.25x scale and six ghosts.
The subsequent shared appearance update intentionally defaults both enemies to 1x.
Validation used temporary scenes and restored the gameplay scene without saving it.

### Previous checks — 2026-09-08

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
