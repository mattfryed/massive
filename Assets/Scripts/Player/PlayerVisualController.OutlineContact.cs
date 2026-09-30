using UnityEngine;

public partial class PlayerVisualController
{
    /// <summary>Visible edge for contact VFX, including the damage dent. Mirrors PlayerBlobVector's
    /// expectedRadius/fanPos; gameplay colliders remain independent of visual deformation.</summary>
    public Vector3 OutlinePointTowards(Vector3 worldPoint)
    {
        Transform frame = visuals ? visuals : transform;
        Vector3 jitter = Vector3.zero;
        if (_chargeJitter01 > .001f)
        {
            float t = Time.time * (16f + 20f * _chargeJitter01);
            jitter = new Vector3(Mathf.Sin(t * 1.13f), 0f, Mathf.Sin(t * .97f + 1.7f))
                * (.06f * _chargeJitter01 * Massive.Player.PlayerScaleAdjuster.SizeOf(this));
        }
        Vector3 center = frame.position + jitter + _decoPreJitterWS + RepulsorVisualOffsetWS;
        Vector3 local = frame.InverseTransformVector(worldPoint - center);
        float stretch = blobMat ? Mathf.Max(.0001f, blobMat.GetFloat("_Stretch")) : 1f;
        float angle = Mathf.Atan2(local.z / stretch, local.x);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float radius = baseRadius;
        if (blobMat && vectorBlobMode)
        {
            Vector4 forward = blobMat.GetVector("_DeformDir");
            Vector2 axis = new Vector2(forward.x, forward.y).normalized;
            if (axis.sqrMagnitude < .001f) axis = Vector2.right;
            float d = Mathf.Min(Mathf.Clamp01(blobMat.GetFloat("_DeformLerp")) * teardropK1, .6f);
            radius *= (1f + d * Vector2.Dot(direction, axis)) / Mathf.Sqrt(1f + .5f * d * d);
            float noise = blobMat.GetFloat("_NoisePhase");
            float shape = 1f + idleWobble * .08f * (Mathf.Sin(noise * 2.3f + angle * 5f) * .6f
                + Mathf.Sin(noise * 1.3f + angle * 9f) * .4f);
            shape += OutlineRipple(angle, blobMat.GetFloat("_HitImpulse"), blobMat.GetFloat("_HitAngle"), blobMat.GetFloat("_HitTime"));
            shape += OutlineRipple(angle, blobMat.GetFloat("_DamageImpulse"), blobMat.GetFloat("_DamageAngle"), blobMat.GetFloat("_DamageTime"));
            float contactAngle = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, blobMat.GetFloat("_ContactAngle") * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            shape -= blobMat.GetFloat("_ContactStrength") * Mathf.Clamp01(1f - Mathf.Abs(contactAngle) / .7f) * .4f;
            radius = Mathf.Max(.02f, radius * shape);
        }
        radius /= Mathf.Sqrt(stretch);
        return center + frame.TransformVector(new Vector3(direction.x * radius, 0f, direction.y * radius * stretch) * RepulsorVisualScale);
    }

    private static float OutlineRipple(float angle, float impulse, float hitAngle, float time)
    {
        if (impulse <= .00001f) return 0f;
        float delta = Mathf.DeltaAngle(hitAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float indent = -Mathf.Exp(-delta * delta / (2f * .45f * .45f)) * Mathf.Exp(-time * 2f);
        float a = Mathf.DeltaAngle((hitAngle + 3f * time) * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float b = Mathf.DeltaAngle((hitAngle - 3f * time) * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float waves = (Mathf.Cos(a * 8f) * Mathf.Exp(-Mathf.Abs(a) * 2f)
            + Mathf.Cos(b * 8f) * Mathf.Exp(-Mathf.Abs(b) * 2f)) * Mathf.Exp(-time);
        float span = Mathf.Clamp01(Mathf.Abs(delta) / Mathf.PI);
        return impulse * (indent + .7f * waves * 4f * span * (1f - span)) * .25f;
    }
}
