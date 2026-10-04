using UnityEngine;
using Massive.Lattice;

public partial class PlayerControllerScript
{
    // Assigned by the optional scene-local component; canonical prefabs need no override.
    internal LatticePlayerMotor LatticeMotor { get; set; }
    private float latticeMomentumUntil;

    private bool TickLatticeMovement(float movementMultiplier)
    {
        if (!LatticeMotor) return false;
        bool available = !_worldGameplaySuppressed && !_matchInputLocked && !_matchSpawning &&
            !temporarilyEliminated && !isStunned && !IsExternallyStunned && !IsSingularityTransitControlled &&
            controlMode != PlayerControlMode.Disabled && rb && !rb.isKinematic &&
            !(attackController && attackController.IsAttacking) && !(powerUps && powerUps.HasMovementAction) &&
            _repulsorRemaining <= 0 && Time.time >= _enemyHitKickTime + enemyHitRecoilSeconds;
        if (!available)
        { latticeMomentumUntil = Time.time + .18f; LatticeMotor.Release(); return false; }
        if (Time.time < latticeMomentumUntil) { LatticeMotor.Release(); return false; }
        return LatticeMotor.Tick(movement, movementMultiplier,
            Effective_moveDeadzone, Time.fixedDeltaTime);
    }
}
