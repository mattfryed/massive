using UnityEngine;
using Massive.Lattice;

public partial class PlayerControllerScript
{
    // Assigned by the optional scene-local component; canonical prefabs need no override.
    internal LatticePlayerMotor LatticeMotor { get; set; }
    private float latticeMomentumUntil;

    internal bool CanUseLatticeAttack => !_worldGameplaySuppressed && !_matchInputLocked && !_matchSpawning &&
        !temporarilyEliminated && !isStunned && !IsExternallyStunned && !IsSingularityTransitControlled &&
        controlMode != PlayerControlMode.Disabled && rb && !rb.isKinematic &&
        !(powerUps && powerUps.HasMovementAction) && _repulsorRemaining <= 0 &&
        Time.time >= _enemyHitKickTime + enemyHitRecoilSeconds;

    internal void EndLatticeAttack(bool completed)
    {
        // Successful attacks already end on their node. Interruptions must leave
        // room for incoming knockback rather than immediately snapping it away.
        latticeMomentumUntil = completed ? Time.time : Mathf.Max(latticeMomentumUntil, Time.time + .18f);
    }

    private bool TickLatticeMovement(float movementMultiplier)
    {
        if (!LatticeMotor) return false;
        if (attackController && attackController.IsLatticeAttack)
        {
            if (CanUseLatticeAttack) return true; // Attack travel owns the body; no joystick drift.
            attackController.CancelAttack();
        }
        bool available = CanUseLatticeAttack && !(attackController && attackController.IsAttacking);
        if (!available)
        { latticeMomentumUntil = Time.time + .18f; LatticeMotor.Release(); return false; }
        if (Time.time < latticeMomentumUntil) { LatticeMotor.Release(); return false; }
        return LatticeMotor.Tick(movement, movementMultiplier,
            Effective_moveDeadzone, Time.fixedDeltaTime);
    }
}
