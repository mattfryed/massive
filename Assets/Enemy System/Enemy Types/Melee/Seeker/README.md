# Seeker

`Enemy_Seeker.prefab` is a mobile melee enemy. `Seeker Test` in
`S-T_PLAYER-ACTIONS` is the authored test encounter; prior enemy encounters are
retained, inactive. Its empty spawn profile prevents additional scheduled spawns.
The gallery pauses this encounter during player demonstrations.

## Combat tuning

`Seeker Controller` owns seeking, charging, firing (the lunge), glide, hit recovery,
cooldown and stalking. `ED_Seeker` supplies shared health, contact damage, movement speed and
score reward. Initial health is 1, contact damage is .04 player mass, and the
reward uses the existing `ENEMY_DEFEAT` economy entry.

| Control | Initial value |
| --- | --- |
| Detection / disengage range | 7 / 9 units |
| Seek speed / turn speed (definition) | 3.8 units/s / 280 degrees/s |
| Attack range / maximum lunge travel | 4.5 / 4.5 units |
| Charge / final compression | 1.25 / .12 seconds |
| Aim lock fraction | .8 of charge |
| Lunge speed | 22 units/s |
| Hit recovery / cooldown | .24 / 1.75 seconds |
| Successful player-hit cooldown | .65 seconds |
| Contact bounce distance | .18 units |
| Miss glide duration / initial speed fraction | .1 seconds / 1 |
| Stalk duration / preferred distance | 3 seconds / 3–4.25 units |
| Stalk radial / orbit / strafe speed | 2.6 / 1.1 / .55 units/s |
| Avoidance chance / reaction time | 55% / .16 seconds |
| Avoidance range / half cone | 3.5 units / 24 degrees |
| Strafe speed / duration / cooldown | 4 units/s / .3 / .8 seconds |
| Arrival warning / face reveal | 1.5 / .8 seconds |

Avoidance responds to the targeted player's actual directed attack, with one
chance roll per attack activation. It chooses a lateral direction away from the
attack line, checking obstacles before committing. Seeking and idle yaw ease
smoothly. Charge and cooldown have no movement or evasion. Charge tracks until
the aim lock, then the lunge stays on that heading so a late sidestep can evade it.

A capsule sweep and initial-overlap checks prevent fast lunges from tunnelling
through players or thin obstacles. Contact ends the lunge and applies at most one
hit; a held shield blocks it. Contact produces a short backward bounce. Only
accepted player damage selects the shorter cooldown; shields, obstacles and
misses use the regular cooldown. The powered lunge ends at its maximum distance,
then a miss carries forward momentum into a short glide with a squared ease-out
velocity (about .73 extra units at the defaults). The glide is swept too and can
resolve contact, so it cannot coast through a wall or player. Arena
bounds keep the hull inside. The sword hurtbox expands with the exposed rear
assembly during charge. Shared movement influences (including resonance
sludge) slow both seeking and lunging. Shared EnemyBase, EnemyHurtbox,
EnemyScoreReward and EnemyDirector handle damage, defeat credit and pause.

After either cooldown, stalking maintains a preferred distance band while
circling the tracked player with varying lateral strafe speed. It may avoid
attacks during stalking but cannot charge until the complete stalk duration has
elapsed. Radial, orbit and strafe speeds, strafe frequency and distance range are
independent Inspector controls. Lost targets are reacquired using the same
detection/team rules; without a target it slows to rest. Stalking, recoil and
glide all share the paused gameplay clock and movement influences.

## Visual tuning

`Seeker Visuals` exposes separate dimensions, axial core offsets and signed spin
speeds for the hexagonal pyramid nose, large forward-facing rear ring and smaller
backward-facing ring. Offsets translate whole groups along local Z without
changing ring radii or panel dimensions. Default spin directions alternate.

Charge Stretch pulls both rear rings back while a chain of metaballs stretches
the energy core. Charge Inward Tilt, Vibration Amplitude and Vibration Frequency
control the large ring. Compression Travel and Release Seconds control the snap
and settling at launch. Core Radius and Core Charge Growth tune the core.

Charge Nose Spin Multiplier progressively accelerates the nose (8x maximum by
default); Nose Spin Ease Out Seconds returns it to its normal rate after launch.
The angular phase is integrated so changing speed does not jump the rotation.
Thrust Nose Stretch lengthens only the pyramid along its axis, then Nose Bounce
Seconds / Cycles / Damping control its spring-like compression and settling.

Thrust Outline Ghosts stores up to 16 world-space shell snapshots in fixed arrays
(12 by default). Ghost Spacing controls distance between snapshots, Ghost
Lifetime controls retention, and Ghost Line Width Multiplier controls weight.
Ghosts use opaque white outlines that thin to nothing, without grey fills or
transparent faces. They freeze during pause, retire after thrust, and clear on
death/disable. No per-snapshot GameObjects or materials are created.

The rear launch exhaust uses the turret's opaque black/white plasma pool through
its one-shot `EmitBurst` API. Launch Drops and Launch Plasma Back Offset are on the
controller; drop size, lifetime, speed and white fraction are on the child
`Launch plasma exhaust`. At runtime this owned effect detaches into world space
so its drops stay behind during the lunge; it clears on disable and is destroyed
with the enemy. Faces have black fill and white outlines; death sends
individual triangles outward and shrinks them. Arrival uses the shared spawn
telegraph and all 18 faces trace in from randomized vertices and timing.

## Editor workflow

`MASSIVE > Enemies > Seeker` contains Create Prefab, Place In Player Actions and
Validate In Player Actions. Setup creates missing assets and preserves authored
tuning on existing assets. Runtime geometry stays unsaved; the warning has a
saved neutral outline and uses instance dimensions at runtime.

Validation runs real Player Actions combat with temporary scripted player input,
captures isolated renders and returns to Edit Mode. Report and images are saved
under `Library/SeekerValidation`. It does not save runtime fixture changes.
