# Player size testing

Open **MASSIVE → Player → Player Size Controls**, or select a player root and use
**Player Scale Adjuster → Player Size (%)**. The window includes inactive players.
Edit-mode changes are saved with the scene; Play-mode changes are temporary.

In `S-8_DYNAMO-PROTOTYPE`, the four actual roster players have this component.
P1 starts at 50%; P2/P3/P4 start at 100%. P1's saved collider radius was 0.275
and its visual radius 0.35, with a unit root scale. Those two fields were restored
to P3's 0.5 baseline before applying 50%. Other player-specific attack tuning,
colors, particle counts and references are retained. The inactive sample prefab
groups are not part of this setup; the shared Players prefab is unchanged.

Use this size slider instead of separately shrinking the Transform and collider.
Half root scale plus half collider radius produces a quarter-size world collider.
Local collider dimensions remain authored at 100%; the hierarchy scales them once.
The component captures its root's existing local scale as its reference on addition.
Use a unit root when adding it to a new player; it cannot infer previously hand-edited
component dimensions. Uniform positive parent/root scales are required.

| System | What changes with size |
| --- | --- |
| Player body and physical collisions | Hierarchy geometry, visual outline, collider and attached offsets |
| GPU nuggets and body effects | World positions, dot sizes, jitter and decoherence split; simulation remains local |
| Melee, shield, repulsor | Physical hitboxes inherit size; generated effects and repulsor falloff use matching world dimensions |
| Attack travel | Each stage's travel and lunge lock-on search scale by default; can be disabled independently of physical hitbox size |
| Legacy attack particles / GPU trails | Native particles use Hierarchy scaling; GPU widths, spread and velocities scale explicitly |
| New thrust and sweep | Collider-calibrated reach, volume thickness and emission offsets; already emitted trails keep their birth size and world location |
| Particle Accelerator | Charge effects, muzzle offset, cast thickness, beam and impacts; projectile range is independently optional |
| Grid interaction | Player-owned influence radii and wave dimensions; shared profile and force strength stay unchanged |
| Respawn / decoherence clearance | Uses current body size; opposing bodies both contribute to pass-through clearance |
| Player ID label | Offset follows size; lettering remains readable at its original size |
| Hit/score transfer blobs | Visual size snapshots the source player; arrival clearance follows the target player. World pickup grains remain world-sized |

**Scale Movement Speed** is off by default. It scales traversal acceleration,
speed ceilings and player recoil when enabled. Environmental currents, delivered
impact forces, rigidbody mass, drag, damage, cooldowns, attack durations, projectile
speed, arena dimensions, pickups and HUD remain unchanged. This separates a size
comparison from balance changes. Smaller players at the same speed cross their own
diameter faster; enabling proportional speed keeps that relationship closer.

The scaler does not rewrite shared attack/power-up definitions or materials.
During a live resize, an attack already underway keeps its cached travel; a new
attack uses the new size. Emitted world-space effects deliberately keep their
birth scale. Body colliders and attached visuals resize immediately. Stop an
attack before resizing when comparing exact collision/visual extents.

## Integration notes

`PlayerScaleAdjuster.SizeOf(component)` returns the player's current uniform world
scale, or 1 without a scaler. Use it only for authored world-space lengths. Local
geometry, transform-relative positions and collider-derived world extents already
include scale and must not be multiplied again. Optional policy helpers are
`ActionReachOf`, `MovementOf`, and `ProjectileReachOf`; `BodyRadiusOf` also works
while the body's collider is disabled. Generated melee particle roots marked
DontSave are excluded from native particle setup because their adapters already
calibrate their world sizes.

New custom effects need this same integration if they use independent world-space
distances. There is no safe automatic interpretation of arbitrary future scripts.
The dormant legacy `PlayerArc` is not wired to the inspected players and was not
changed. Other scenes behave as before until a scaler is added to their players.

## Validation

Run **MASSIVE → Player → Validate Player Scaling** for isolated editor checks of
repeated resizing, unchanged authored dimensions, parent scaling, policy options,
disabled-body clearance, particle scaling and generated-effect exclusion.
The fixtures live in a temporary preview scene and are removed after the run.

Verified in Dynamo: P1 at 50% has a 0.25-unit collision radius; P3 at 100% has
0.5. A normal-input lunge cached 1 versus 2 world units with the same 0.3-second
duration. P1 retained its size, restored local visual radius and enabled collider
after death/respawn. These are targeted checks, not an exhaustive gameplay balance
test or standalone player build.
