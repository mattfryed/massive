using UnityEngine;

/// <summary>
/// A narrowly owned movement lease. It does not reuse a match lock, stun timer,
/// input mode, life state, or serialized movement setting for portal animation.
/// </summary>
public partial class PlayerControllerScript
{
    private Object singularityTransitOwner;
    private bool singularityOriginalDetectCollisions;
    private bool singularityOriginalKinematic;
    private int singularityTransitLife;

    public bool IsSingularityTransitControlled => singularityTransitOwner != null;
    public bool CanStartSingularityTransit => isActiveAndEnabled && !temporarilyEliminated &&
        !_matchSpawning && !_matchInputLocked && !_worldGameplaySuppressed && !IsSingularityTransitControlled;
    public bool CanContinueSingularityTransit => isActiveAndEnabled && !temporarilyEliminated &&
        !_matchSpawning && !_matchInputLocked && !_worldGameplaySuppressed &&
        LifeSequence == singularityTransitLife;

    public bool TryAcquireSingularityTransit(Object owner)
    {
        if (owner == null || !CanStartSingularityTransit) return false;
        if (rb == null) rb = GetComponent<Rigidbody>();
        // A different system already controlling physics must retain authority.
        if (rb == null || rb.isKinematic) return false;
        singularityOriginalDetectCollisions = rb.detectCollisions;
        singularityOriginalKinematic = rb.isKinematic;
        singularityTransitLife = LifeSequence;
        singularityTransitOwner = owner;
        movement = Vector3.zero;
        moveHorizontal = moveVertical = 0f;
        didPlayerTapActionThisFrame = false;
        shieldOn = false;
        if (attackController != null) attackController.CancelAttack(false);
        if (shieldAbility != null) shieldAbility.ForceStopShield();
        if (shield != null) shield.SetActive(false);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.detectCollisions = false;
        rb.isKinematic = true;
        return true;
    }

    public bool IsSingularityTransitOwnedBy(Object owner)
    {
        return owner != null && singularityTransitOwner == owner;
    }

    public void ReleaseSingularityTransit(Object owner, Vector3 releaseVelocity)
    {
        if (!ReferenceEquals(singularityTransitOwner, owner)) return;
        singularityTransitOwner = null;
        if (rb == null) return;
        // Death/respawn owns isKinematic once it starts. Its collider disabling
        // remains intact, while our detectCollisions lease must not leak into
        // the player's next life.
        rb.detectCollisions = singularityOriginalDetectCollisions;
        bool lifeStillOwned = LifeSequence == singularityTransitLife &&
            !temporarilyEliminated && !_matchSpawning;
        if (!lifeStillOwned) return;
        rb.isKinematic = singularityOriginalKinematic;
        if (!rb.isKinematic)
        {
            // World suppression owns disabled gameplay/colliders, not isKinematic.
            // Return our kinematic lease even while suppressed, without motion.
            rb.linearVelocity = _matchInputLocked || _worldGameplaySuppressed ? Vector3.zero : releaseVelocity;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
    }

    private void ReleaseSingularityTransitOnDisable()
    {
        if (!ReferenceEquals(singularityTransitOwner, null))
            ReleaseSingularityTransit(singularityTransitOwner, Vector3.zero);
    }
}
