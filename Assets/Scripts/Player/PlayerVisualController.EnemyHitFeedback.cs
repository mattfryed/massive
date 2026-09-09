using UnityEngine;

public partial class PlayerVisualController
{
    [Header("Enemy Damage Pulse")]
    [Range(.1f, .7f)] public float enemyDamagePulseSeconds = .35f;
    [Range(0f, 1.5f)] public float enemyDamageDentStrength = 1f;
    [Tooltip("Additional outline width at peak, as a fraction of the normal width. Team colors stay intact.")]
    [Range(0f, 3f)] public float enemyDamageOutlineBoost = 1.5f;

    private float _enemyDamageStarted = float.NegativeInfinity;
    private float _enemyDamageStrength;
    private Vector3 _enemyDamageTowardSource;
    private static readonly int DamageImpulseId = Shader.PropertyToID("_DamageImpulse");
    private static readonly int DamageAngleId = Shader.PropertyToID("_DamageAngle");
    private static readonly int DamageTimeId = Shader.PropertyToID("_DamageTime");

    public bool EnemyDamageFeedbackActive => _enemyDamageStrength > 0f && Time.time - _enemyDamageStarted < enemyDamagePulseSeconds;

    public void PlayEnemyDamageFeedback(float strength01, Vector3 towardSourceWorld)
    {
        if (strength01 <= 0f) return;
        float age = Mathf.Clamp01((Time.time - _enemyDamageStarted) / Mathf.Max(.01f, enemyDamagePulseSeconds));
        float remaining = _enemyDamageStrength * (1f - Mathf.SmoothStep(0f, 1f, age));
        // Refresh one bounded pulse. A swarm never adds deformation indefinitely.
        _enemyDamageStrength = Mathf.Clamp01(Mathf.Max(strength01, remaining));
        _enemyDamageTowardSource = towardSourceWorld;
        _enemyDamageStarted = Time.time;
        ApplyEnemyDamageUniforms();
    }

    public void ClearEnemyDamageFeedback()
    {
        _enemyDamageStrength = 0f;
        _enemyDamageStarted = float.NegativeInfinity;
        if (!blobMat || !_blobMatInstance) return;
        blobMat.SetFloat(DamageImpulseId, 0f);
        blobMat.SetFloat("_OutlineHalf", outlineHalf);
    }

    private void ApplyEnemyDamageUniforms()
    {
        if (!blobMat) return;
        float age = Mathf.Clamp01((Time.time - _enemyDamageStarted) / Mathf.Max(.01f, enemyDamagePulseSeconds));
        float envelope = _enemyDamageStrength * (1f - Mathf.SmoothStep(0f, 1f, age));
        Transform frame = visuals ? visuals : transform;
        // The vector shader draws in local XZ. Keep the impact on its world-facing side as the player turns.
        Vector3 local = frame.InverseTransformDirection(_enemyDamageTowardSource);
        blobMat.SetFloat(DamageAngleId, Mathf.Atan2(local.z, local.x));
        blobMat.SetFloat(DamageTimeId, age * 1.05f);
        blobMat.SetFloat(DamageImpulseId, envelope * enemyDamageDentStrength);
        float outlinePulse = _enemyDamageStrength * (1f - Mathf.SmoothStep(0f, 1f, age / .55f));
        blobMat.SetFloat("_OutlineHalf", outlineHalf * (1f + enemyDamageOutlineBoost * outlinePulse));
    }
}
