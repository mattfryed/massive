using Massive.Player;
using UnityEngine;

public partial class PlayerControllerScript
{
    private PlayerMovementReversal movementReversal;
    private bool movementActionInput;

    public void ProtectActionMomentum(float seconds)
    {
        if (!movementReversal) TryGetComponent(out movementReversal);
        if (movementReversal) movementReversal.BlockFor(seconds);
    }

    private bool IsPlainJoystickMovement()
    {
        return !_worldGameplaySuppressed && !_matchInputLocked && !_matchSpawning &&
            !temporarilyEliminated && !isStunned && !IsExternallyStunned &&
            !isPseudoPlayer && controlMode != PlayerControlMode.Disabled && rb && !rb.isKinematic &&
            !movementActionInput && !shieldOn && !(shieldAbility && shieldAbility.IsActive) &&
            !(attackController && attackController.IsAttacking) &&
            !(powerUps && powerUps.HasMovementAction) &&
            Time.time >= _enemyHitKickTime + enemyHitRecoilSeconds;
    }

    private void ApplyJoystickReversal(ref Vector3 velocity, ref Vector3 planarVelocity)
    {
        if (!movementReversal) TryGetComponent(out movementReversal);
        if (!movementReversal || !movementReversal.isActiveAndEnabled) return;
        bool ordinaryMovement = IsPlainJoystickMovement();
        // Brief recovery also protects the first released frame after an action/stun.
        if (!ordinaryMovement) movementReversal.BlockFor(.2f);
        if (!movementReversal.TryApply(velocity, movement, moveDeadzone, ordinaryMovement, Time.time, out var adjusted)) return;
        rb.linearVelocity = velocity = adjusted;
        planarVelocity = new Vector3(adjusted.x, 0f, adjusted.z);
    }
}
