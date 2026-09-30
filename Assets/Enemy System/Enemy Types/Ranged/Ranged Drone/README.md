# Ranged Drone

Enemy_RangedDrone uses the normal Drone's 0.8 root scale, six front facets, short
wireframe tail, point-traced spawn/breakup, axial spin, seven-lobe metaball core,
movement, perception and obstacle handling. Only the front panels hover outward
along their surface normals. The default lift is 0.045 local units, with subtle
independent hovering. Panels remain black with white outlines.

RangedDroneController is an attack companion to DroneController. It detects
eligible players within the shared 6-unit detection range and approaches until
within 4.5 units and a clear line of sight. It holds position while attacking:

1. Charge for 2 seconds, tracking the target. Panels pull inward into tight, fast
   jitter independent of their idle breathing; the core
   grows by up to 35% and its lobes move faster and slightly farther apart.
2. Burst for 1.5 seconds, firing at 0, 0.5 and 1.0 seconds. Each shot pivots the
   front triangles outward around their rear edges, opening the mouth, then closes.
3. Cool down for 3 seconds, then charge again if a valid target is available.

Each shot emerges from the core toward the target's current position and travels
straight after launch. Losing the target, firing range or line of sight cancels
an attack into cooldown. The body never deals contact damage or self-destructs.

Health, sword damage and reward match ED_Drone: one health, one sword hit and the
shared ENEMY_DRONE_DEFEAT reward (100 meV base, with normal score multipliers).
EnemyBase, EnemyHurtbox and EnemyScoreReward retain all damage/death/credit authority.

The projectile prefab uses the shared MetaballSDF material and EnemyProjectileBase
damage/reflection rules. A four-lobe packet travels at 5 units/second for up to
1.5 times the firing/positioning range (6.75 units by default), with a 4-active-second
lifetime backstop, and removes 0.04 player mass per hit. Reflected travel counts
toward the same distance budget. A sphere
sweep and overlap checks prevent thin-wall tunnelling and duplicate contacts.
Shots stop at world obstacles and arena bounds; reflected shots can defeat their
firing Drone with the reflecting player's score credit. Each shot owns one visual
volume with fixed arrays and a MaterialPropertyBlock. Emitted shots retain their
own lifetime after their owner dies. Match/Director pause freezes their travel,
lifetime and animation as well as the Drone's charge and burst.

Resonance colliders are pass-through for these shots and do not obstruct the
Ranged Drone's firing line of sight. The projectile's **Resonance Speed Multiplier**
slider defaults to 0.75 (75% speed) while overlapping Resonance, and full speed
returns on exit or when its colliders are disabled/removed. Multiple overlapping
colliders/patterns do not stack the slowdown. Entry is swept so a fast shot also
detects thin Resonance; ordinary obstacles behind it still block shots and sight.

On impact, collision is disabled immediately while the visual stays for a 0.06s
contact beat. Player contacts follow the actual rendered outline, including its
hit dent, using PlayerVisualController.OutlinePointTowards. Ten randomized
metaball fragments scatter and shrink over 0.4s. Missed shots stop precisely at
the distance cutoff and ease-out shrink over 0.35s. Shader union softness shrinks
with the radii so no residual blob remains. These effects retain pause handling
and cannot deal additional damage.

Both Drone variants now smooth their idle steering and accelerate/decelerate
their heading changes. The orbit force fades near its center instead of switching
axes abruptly. Collision sweeps and boundary guards remain authoritative.

## Authoring

- Tune charge, burst duration, shot spacing/count, cooldown and firing range on
  RangedDroneController. Defaults are 2 / 1.5 / 0.5 / 3 shots / 3 seconds.
- DroneVisuals has separate **Ranged panels — idle breathing**, **charging**, and
  **firing** sections. Front Face Lift / Front Hover Amplitude / Front Hover Speed
  retain the authored 0.075 / 0.1 / 5 settings. Charge Face Lift / Charge Face
  Vibration / Charge Face Speed are absolute, independent values: 0.025 / 0.006 /
  110. Distances are local units; speed is
  radians/second. Charging mixes two bounded frequencies for tighter jitter.
  Charge Panel Blend Seconds (0.18s) eases into that pose on charge entry and back
  to breathing on fire or cancellation; it does not add idle amplitude to charge.
  Charge Face Tilt and Shot Open Angle remain independently adjustable. The floating-face option
  is disabled on the normal Drone and Carrier's docked Drone visuals.
- Tune the range multiplier on RangedDroneController. A muzzle offset of zero
  starts each packet at the core, growing/separating its lobes during departure.
- Tune speed, damage, lifetime, contact hold, fragment count/spread and impact/
  expiry durations on Ranged Drone Metaball Shot.prefab.
- Its **Resonance passage** section contains the **Resonance Speed Multiplier**
  slider (0.05–1, default 0.75). The normal speed remains unchanged outside the field.
- Tune idle steering smooth time, turn speed and turn acceleration under
  DroneController > Idle swarm on either variant (defaults 0.45s, 100°/s, 240°/s²).
- MASSIVE > Enemies > Ranged Drone > Create Prefab creates missing assets and
  preserves authored tuning on subsequent runs.
- Place In Player Actions creates one test encounter, registers its Director with
  gallery pause, and disables the Carrier test encounter so the new enemy can be
  tried alone. Scene edits support Undo.
- Validate In Player Actions runs the actual enemy/projectile/player interactions
  in Play Mode and returns to Edit Mode, discarding runtime fixture changes.
  Evidence is written to Library/RangedDroneValidation/report.txt with idle,
  charge, firing, player contact, impact and expiry PNGs.

Latest validation: 58 Play Mode checks passed in Unity 6000.0.28f1, including
real player damage, shield reflection and credited defeat, pause, exact attack
timings, target loss, thin-wall collision, projectile expiry and normal Drone
contact-damage regression, visible outline contact, single-use damage during
impact animation, fragment spread/shrink, exact distance cutoff, and per-physics-step
idle turn speed/acceleration measurements for both variants. Resonance checks use
real generated colliders to verify firing through fields, adjustable passage speed,
non-stacking overlaps, full-speed recovery on exit/disable, and ordinary walls
behind Resonance. No runtime Console errors occurred.

This prototype targets the planar Player Actions arena. Production encounter
placement and final combat balance are not configured by this feature.
