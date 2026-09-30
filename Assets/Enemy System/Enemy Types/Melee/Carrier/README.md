# Carrier prototype

`Enemy_Carrier.prefab` is a stationary Drone mothership using EnemyBase, EnemyDefinition,
EnemyHurtbox, EnemyScoreReward and EnemyDirector. Its compact rhombicuboctahedron hull
uses black filled faces and white Shapes outlines. The regular solid has 24 vertices,
18 squares and eight equilateral triangles; only its upward triangle is left open to
expose a seven-lobe core using the Drone's MetaballSDF material. Its 0.9-unit hull radius
is approximately half the previous hull's radius. Geometry uses signed permutations of
(1, 1, 1 + sqrt(2)), following [Paul Bourke's construction](https://www.paulbourke.net/geometry/Rhombicuboctahedron/).

A threefold axis points upward, placing six square faces in a horizontal ring. The
full-size Drones sit with their widest cross-section on those face centers: tails inset
inside the body, noses projecting outward. There are no carved notches. Docked and
launched Drone scales match. The damage trigger has radius 0.775; spawn clearance is 1.25.
EnemyHurtbox lives on that dedicated child trigger so the Rigidbody root's additional
shell contacts cannot apply duplicate sword damage.
A separate solid convex MeshCollider uses the hull's exact 24 vertices and rotates with
the shell. It uses a persistent closed collision mesh, independent of the animated face
mesh. Both colliders are disabled during arrival and death. Drone steering and swept
movement detect the solid hull. A newly launched Drone ignores only its owning shell
until its inset tail clears (or the launch window expires), then resumes normal avoidance.

Arrival begins with a 3-second red wireframe warning using the shared EnemySpawnTelegraph
glow, outward ghosts and defocus fade. The hull and six visual-only Drones then assemble
over 1.2 seconds. Each face traces from its own random vertex with varied timing; black
fills follow the outlines. Collision stays off until assembly completes. After a further
1-second launch delay, it releases slots 1–6 clockwise, 0.5 seconds apart. Five seconds after the last launch,
it reforms the six docked visuals in the same order, 0.5 seconds apart. The next volley
begins 0.5 seconds after the final bay refills. All timing is editable on CarrierController.

Each launch creates the existing Enemy_Drone prefab at its bay and gives it 0.45 seconds
of outward flight at 3.5 units/second before ordinary Drone steering takes over. The
already-formed shell stays visible on release. Spawn and death rewards remain owned by
the existing enemy/score systems. The Carrier has 6 health, takes the normal 1-point
sword hit, awards the existing ENEMY_DEFEAT reward, and has no contact damage. These are
initial prototype values, not final balance.

Launched Drones are independent of Carrier death. Docked visuals disappear with the
Carrier and never collide, register as live enemies, or award score. Pausing the shared
Director freezes the Carrier clock, spawn indicator, face reveal, spin, fuel and released
enemies. Docked Drone visuals inherit the Carrier's pause state. Disable/re-enable
restarts the full arrival sequence. No new package, rendering setting or scoring rule is needed.

The presentation assembly rotates around local Y at 8 degrees/second, carrying all six
docks with the hull while leaving the physics root fixed. Each successful launch emits
three core metaballs, 0.055 seconds apart, along a straight 0.38-second line from the
core to the departing Drone. The flow crosses the shell interior and is naturally
occluded by its opaque faces; it never arcs above the hull.
The packets and core share one dynamically fitted SDF volume and fixed arrays; there are
no per-packet GameObjects or materials. Blocked launches emit nothing.

Death immediately stops launches, removes docked Drones/fuel and disables collision.
The individual square and triangular panels then explode outward, tumble, shrink and
fade over 0.8 seconds, using the Dyson Sphere's breakup motion. The core shrinks away
with them. EnemyBase still resolves damage, score and Director release exactly once.

Tune spawn warning and launch timing on CarrierController; face timing, Y spin, shatter
distance/spin, core and fuel settings on CarrierVisuals; death duration on EnemyBase's
Despawn Delay Seconds. The Carrier Spawn Telegraph prefab owns warning colors/glow.
The Carrier owns this warning so hand-placed and ordinary spawns both show it. If a
future Director rule also supplies a pre-spawn warning, set Spawn Warning Seconds to zero
on that Carrier variant to avoid two warnings.

The default safety limit is 24 active Drones in the scene (also bounded by ED_Drone and
Director caps). A blocked bay or full cap keeps the next Drone docked and retries in
order. Bays outside the arena or overlapping Obstacle/NoSpawnZone volumes do not launch.

## Player Actions

The saved S-T_PLAYER-ACTIONS scene contains `Carrier Test/Enemy_Carrier` at the arena
center. Its dedicated Director uses ESP_CarrierTest, an empty spawn schedule with caps
of 25, and waits for the normal match countdown. Player Action Lab defaults to Manual
for fighting the Carrier. Its existing All Demos mode suppresses the Carrier encounter
along with the other encounters and restores it when leaving the gallery.

Open the prefab or select the scene instance to tune launch timing. The scene's optional
`Scene Director` binding registers a hand-placed Carrier with match pause and enemy
bookkeeping; ordinary Director spawns are initialized automatically. A standalone prefab
without a Director starts its own cycle and uses the scene-wide Drone cap.

## Authoring and validation

- MASSIVE > Enemies > Carrier > Create Prefab creates missing assets and preserves tuning.
- Place In Player Actions adds the test once and records Undo.
- Apply Compact Rhombicuboctahedron updates the existing prefab's presentation, dock
  positions, hitbox and clearance while retaining its GUID and launch tuning.
- Set Up Arrival and Launch Effects creates the warning assets, solid hull, child hurtbox and rotating assembly,
  retaining existing tuning and the Carrier prefab GUID.
- Validate In Player Actions enters Play Mode, runs real cycle/pause/cap/damage/cleanup
  and hull topology/docking/collision checks, captures warning/reveal/fueling/breakup views, and returns to Edit Mode. It temporarily hides
  players to isolate timing and discards those runtime changes on exit.
- The latest run passed 53 checks, including six incoming Drones blocked by the rotating
  hull at 8 units/second, launch clearance, single sword contact damage, straight fuel,
  pause during warning/reveal/fueling, warning cancellation, re-enable reset, blocked
  launches, two volleys and death cleanup. No runtime Console errors occurred.
- Evidence is written to `Library/CarrierValidation/report.txt` and PNGs beside it.

Current scope is the planar Player Actions scene. Folded SINGULARITY integration,
Carrier movement, production encounter scheduling and final balance are separate work.
