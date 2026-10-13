using UnityEngine;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreVisual
    {
        static readonly int RippleId = Shader.PropertyToID("_CoreRipple");
        static readonly int Ripple2Id = Shader.PropertyToID("_CoreRipple2");
        static readonly int RippleOriginId = Shader.PropertyToID("_CoreRippleOrigin");
        static readonly int RippleOrigin2Id = Shader.PropertyToID("_CoreRippleOrigin2");
        Vector3 energyRestPosition, shellRestPosition;
        MaterialPropertyBlock interactionProperties;
        AmplifierPossessionSettings feedbackSettings;
        float visualCharge;
        bool chargingVisual;
        struct Ripple { public float age; public Vector3 direction; public bool active; }
        Ripple ripple, secondRipple;

        public bool EjectionRippleActive => ripple.active || secondRipple.active;
        public float ChargeVibration01 => feedbackSettings != null && feedbackSettings.chargeVibration && chargingVisual
            ? Mathf.Pow(Mathf.InverseLerp(feedbackSettings.chargeVibrationStart, 1f, visualCharge), Mathf.Max(.25f, feedbackSettings.chargeVibrationRamp)) : 0f;

        public void SetChargeFeedback(float charge, bool charging, AmplifierPossessionSettings settings)
        { feedbackSettings = settings; visualCharge = Mathf.Clamp01(charge); chargingVisual = charging; }

        public void PlayEjectionRipple(Vector3 direction, AmplifierPossessionSettings settings)
        {
            feedbackSettings = settings;
            if (!settings.ejectionRipple) return;
            secondRipple = ripple; // Both players can be ejected on a tied contest.
            ripple = new Ripple { active = true, direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right };
            ApplyInteractionFeedback(0f, 0f);
        }

        public void ResetInteractionFeedback()
        {
            chargingVisual = false; visualCharge = 0f;
            ripple = secondRipple = default;
            if (energyCore) energyCore.localPosition = energyRestPosition;
            if (neutralShell) neutralShell.localPosition = shellRestPosition;
            WriteRipples(energyRenderer); WriteRipples(shellRenderer);
        }

        void ApplyInteractionFeedback(float step, float clock)
        {
            float amount = feedbackSettings == null ? 0f : ChargeVibration01 * Mathf.Max(0f, feedbackSettings.chargeVibrationAmount);
            float phase = clock * (feedbackSettings == null ? 0f : Mathf.Max(0f, feedbackSettings.chargeVibrationFrequency)) * Mathf.PI * 2f;
            Vector3 jitter = new Vector3(Mathf.Sin(phase) + .35f * Mathf.Sin(phase * 1.73f + 1.7f),
                .22f * Mathf.Sin(phase * 1.21f), Mathf.Sin(phase * 1.37f + .8f) + .3f * Mathf.Sin(phase * 2.13f));
            // Displace the two rendered layers together in their shared parent space.
            // The Rigidbody, collider, aim line and capture point remain untouched.
            float radius = Mathf.Max(_shellBaseScale.x, _shellBaseScale.z) * .5f;
            Vector3 offset = jitter * (amount * radius / 1.35f);
            if (energyCore) energyCore.localPosition = energyRestPosition + offset;
            if (neutralShell) neutralShell.localPosition = shellRestPosition + offset;
            AdvanceRipple(ref ripple, step); AdvanceRipple(ref secondRipple, step);
            WriteRipples(energyRenderer); WriteRipples(shellRenderer);
        }

        void AdvanceRipple(ref Ripple pulse, float step)
        {
            if (!pulse.active) return;
            pulse.age += Mathf.Max(0f, step);
            if (feedbackSettings == null || !feedbackSettings.ejectionRipple || pulse.age >= Mathf.Max(.05f, feedbackSettings.ejectionRippleSeconds)) pulse.active = false;
        }

        Vector4 RippleData(Ripple pulse)
        {
            if (!pulse.active || feedbackSettings == null) return Vector4.zero;
            return new Vector4(Mathf.Clamp01(pulse.age / Mathf.Max(.05f, feedbackSettings.ejectionRippleSeconds)),
                Mathf.Clamp(feedbackSettings.ejectionRippleAmplitude, 0f, .2f), Mathf.Max(.1f, feedbackSettings.ejectionRippleWidth),
                Mathf.Max(1f, feedbackSettings.ejectionRippleFrequency));
        }

        void WriteRipples(Renderer target)
        {
            if (!target) return;
            interactionProperties ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(interactionProperties);
            interactionProperties.SetVector(RippleId, RippleData(ripple));
            interactionProperties.SetVector(Ripple2Id, RippleData(secondRipple));
            interactionProperties.SetVector(RippleOriginId, target.transform.InverseTransformDirection(ripple.direction));
            interactionProperties.SetVector(RippleOrigin2Id, target.transform.InverseTransformDirection(secondRipple.direction));
            target.SetPropertyBlock(interactionProperties);
        }
    }
}
