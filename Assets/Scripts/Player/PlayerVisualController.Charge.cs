using UnityEngine;

public partial class PlayerVisualController
{
    public Vector3 ThrustChargeVisualOffsetWS
    {
        get
        {
            if (!attackController || !attackController.IsCharging) return Vector3.zero;
            float t = attackController.ChargeElapsed;
            float max = attackController.Effective_thrustChargeMaxSeconds;
            float frequency = attackController.Effective_thrustChargeVibrationFrequency;
            float startFrequency = Mathf.Min(8f, frequency);
            // Integrate the frequency ramp to avoid phase jumps as charge grows.
            float phase = 2f * Mathf.PI * (startFrequency * t + .5f * (frequency - startFrequency) * t * t / max);
            float amplitude = attackController.Effective_thrustChargeVibrationAmplitude *
                Massive.Player.PlayerScaleAdjuster.SizeOf(this) * attackController.Charge01;
            return new Vector3(Mathf.Sin(phase), 0f, Mathf.Sin(phase * 1.31f + .7f)) * amplitude;
        }
    }
}
