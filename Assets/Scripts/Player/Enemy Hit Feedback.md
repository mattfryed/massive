# Enemy hit feedback

Confirmed, nonlethal enemy damage now dents the struck side of the player's vector
body, sends a ripple in both directions around its perimeter, and briefly thickens
the contrasting outline. The pulse settles in 0.35 seconds. Team fill and outline
colors remain intact. Death clears this pulse before the existing dissolve runs.

The player recoils away from the attacker in the XZ plane. Drone-sized damage
currently produces a 1.45 units/second initial recoil, with softer braking along
that axis for 0.16 seconds. The usual movement input stays active, and Rigidbody
collisions stop the player at walls. Resonance movement influence also scales the
recoil. Actual travel depends on steering, drag, and obstacles.

## Tuning

On **PlayerControllerScript > Enemy Hit Feedback**:

- Toggle visual feedback and recoil independently.
- **Full Strength Damage** sets the actual mass loss for maximum feedback (0.08).
- **Recoil Speed** sets the weakest/strongest initial speed (0.9 / 2.0).
- **Recoil Seconds** sets the braking recovery window (0.16).
- **Recoil Lockout** prevents multiple impulses in rapid succession (0.12).

On **BlobQuad > PlayerVisualController > Enemy Damage Pulse**:

- **Pulse Seconds** controls recovery (0.35).
- **Dent Strength** scales indentation and travelling waves (1.0).
- **Outline Boost** controls additional outline width at impact (1.5).

These fields are part of the existing components, so current player prefabs and
scene instances receive defaults without new components or scene migration.

## Integration

Drone, Dyson lunge, generic enemy touch, and enemy projectile damage pass their
source through `ApplyExternalMassDelta`. Feedback runs only after positive mass
loss is accepted. Existing shielding, active-attack, and invulnerability gates
remain owned by each damage path; this effect does not grant invulnerability or
change damage/scoring rules. Dyson's shield collider parries its lunge; its body
contact path retains the existing behavior for attacks that get around the shield.

Impact direction is captured synchronously, before a projectile or Drone despawns.
Projectiles use their travel direction, including when their center overlaps the
player. Contact enemies use their position with a velocity fallback for coincident
centers. New enemy damage should supply its source GameObject in the same way.

Repeated damage refreshes one bounded pulse. Recoil replaces the impact-axis
velocity and observes a lockout instead of adding unbounded impulses. The effect
uses the player's owned material, excludes lunge ghosts, and keeps the existing
death ripple and charging jitter separate. No per-hit objects are allocated.

`MASSIVE > Enemies > Drone > Run Prototype Validation` includes feedback, real
contact/projectile routes, movement, collision, protection, cleanup, and shader
capture checks in a disposable Play Mode scene. It does not save the gameplay scene.

For just this feature, use `MASSIVE > Players > Run Enemy Hit Feedback Validation`.
The initial 26 focused checks and 111-check Drone/Resonance run passed, but their
synthetic palette check incorrectly expected a white dark-team body. A subsequent
production-prefab regression reproduced that error: looking up the parent player
in the child visual's Awake switched the previously used black/white primary
palette to the alternate white/black palette. The local palette lookup is restored;
enemy feedback does not change how the existing player rigs select their colors.

On 2026-09-08, all 31 focused checks passed after this correction, including the
actual dark-team prefab before, during, and after enemy impact. Captured production
prefab frames were visually checked, with no Console errors. These are Editor Play
Mode checks; a target-platform build was not run.
