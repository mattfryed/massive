using Massive.Enemies;
using Massive.Player;
using UnityEngine;

public partial class PlayerControllerScript
{
    [Header("Enemy Hit Feedback")]
    [Tooltip("Directional body dent, travelling ripple and outline pulse after confirmed enemy damage.")]
    public bool enemyHitVisualFeedback = true;
    public bool enemyHitKnockback = true;
    [Tooltip("Mass loss that produces the strongest reaction. Drone damage is currently 0.04.")]
    [Min(.001f)] public float enemyHitFullStrengthDamage = .08f;
    [Tooltip("Minimum and maximum recoil speed, scaled by actual damage. World units per second.")]
    public Vector2 enemyHitRecoilSpeed = new Vector2(.9f, 2f);
    [Tooltip("Briefly soften braking along the impact direction; steering remains available.")]
    [Range(.02f, .4f)] public float enemyHitRecoilSeconds = .16f;
    [Tooltip("Minimum interval between recoil impulses. Visuals can still refresh on every hit.")]
    [Min(.02f)] public float enemyHitRecoilLockout = .12f;

    private Vector3 _enemyHitAway;
    private float _enemyHitKickTime = float.NegativeInfinity;
    private float _nextEnemyHitKickTime = float.NegativeInfinity;

    private void ApplyEnemyHitFeedback(PlayerHitResult hit)
    {
        if (!hit.accepted || hit.causedDeath || hit.massLost01 <= 0f || !hit.source ||
            temporarilyEliminated || _worldGameplaySuppressed || _matchSpawning || _matchInputLocked || isPseudoPlayer)
            return;

        var projectile = hit.source.GetComponentInParent<EnemyProjectileBase>();
        var enemy = hit.source.GetComponentInParent<EnemyBase>();
        if (!projectile && !enemy) return;

        // Capture direction now: projectiles and kamikaze enemies may disappear this frame.
        Vector3 away = projectile ? projectile.TravelDirection : transform.position - hit.source.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < .0001f)
        {
            var sourceBody = hit.source.GetComponentInParent<Rigidbody>();
            away = sourceBody ? sourceBody.linearVelocity : hit.source.transform.forward;
            away.y = 0f;
        }
        if (away.sqrMagnitude < .0001f) away = hit.source.transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < .0001f) away = Vector3.right;
        away.Normalize();

        float strength = Mathf.Clamp01(hit.massLost01 / Mathf.Max(.001f, enemyHitFullStrengthDamage));
        if (!visualsController) visualsController = GetComponentInChildren<PlayerVisualController>(true);
        if (enemyHitVisualFeedback && visualsController && visualsController.isActiveAndEnabled)
            visualsController.PlayEnemyDamageFeedback(strength, -away);

        if (!enemyHitKnockback || !rb || rb.isKinematic || isStunned || IsExternallyStunned || Time.time < _nextEnemyHitKickTime)
            return;

        _enemyHitAway = away;
        _enemyHitKickTime = Time.time;
        _nextEnemyHitKickTime = Time.time + Mathf.Max(.02f, enemyHitRecoilLockout);
        float speed = Mathf.Lerp(Mathf.Max(0f, enemyHitRecoilSpeed.x), Mathf.Max(0f, enemyHitRecoilSpeed.y), strength);
        float movementMultiplier = ExternalMovementMultiplier * PlayerScaleAdjuster.MovementOf(this);
        speed *= movementMultiplier;
        Vector3 velocity = rb.linearVelocity;
        // Replace only the impact-axis component, so swarm hits cannot accumulate speed.
        // Preserve tangential steering and vertical motion; physics still resolves walls.
        Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
        planar += away * (speed - Vector3.Dot(planar, away));
        if (clampSpeed) planar = Vector3.ClampMagnitude(planar, Mathf.Max(speed, maxMoveSpeed * movementMultiplier));
        rb.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
    }

    private Vector3 EnemyHitBraking(Vector3 braking)
    {
        float age = Time.time - _enemyHitKickTime;
        if (age >= enemyHitRecoilSeconds || Vector3.Dot(braking, _enemyHitAway) >= 0f) return braking;
        float recovery = Mathf.SmoothStep(.25f, 1f, age / Mathf.Max(.02f, enemyHitRecoilSeconds));
        return braking - Vector3.Project(braking, _enemyHitAway) * (1f - recovery);
    }

    private void ResetEnemyHitFeedback()
    {
        _enemyHitKickTime = _nextEnemyHitKickTime = float.NegativeInfinity;
        _enemyHitAway = Vector3.zero;
        if (visualsController) visualsController.ClearEnemyDamageFeedback();
    }
}
