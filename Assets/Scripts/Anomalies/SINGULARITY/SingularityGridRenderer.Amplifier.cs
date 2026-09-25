using Massive.Multiplier;
using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityGridRenderer
    {
        [Header("Amplifier scoring feedback")]
        [Tooltip("Scene-owned treatment clock/settings also assigned to both goals. Capture packets, held charge and discharge follow the complete folded border.")]
        public AmplifierGoalTreatments amplifierTreatments;

        private static readonly int AmplifierTimingId = Shader.PropertyToID("_AmpTiming");

        private bool HasAmplifierTreatment => surface != null && amplifierTreatments != null
            && amplifierTreatments.isActiveAndEnabled && amplifierTreatments.gameObject.scene == gameObject.scene;

        /// <summary>Additional local-surface X extent for conservative culling.
        /// Advancing loop distance cannot exceed the existing surface YZ bounds;
        /// the extra across displacement can. Includes either team and active
        /// packet images without allocating or sampling the lattice.</summary>
        public float AmplifierBoundsPadding
        {
            get
            {
                if (!HasAmplifierTreatment) return 0f;
                AmplifierTreatmentSettings settings = amplifierTreatments.Settings;
                float maximum = 0f;
                for (int team = 1; team <= 2; team++)
                {
                    Vector4 origin = amplifierTreatments.GetGridOrigin(team, Vector2.zero);
                    if (settings.capturePackets)
                        for (int i = 0; i < Mathf.Clamp(settings.emissions, 1, 5); i++)
                            maximum += 2f * Mathf.Abs(settings.carrierAmplitude)
                                * PacketMaximumGain(origin.z - i * settings.packetSpacing, settings);
                    if (settings.heldCharge)
                    {
                        if (settings.travellingWave) maximum += Mathf.Abs(settings.heldAmplitude) * origin.w;
                        if (settings.occasionalPackets)
                        {
                            float period = Mathf.Max(.5f, settings.heldPacketInterval);
                            float recent = Mathf.Repeat(amplifierTreatments.Clock, period);
                            for (int i = 0; i < 3; i++)
                                maximum += 4f * Mathf.Abs(settings.heldAmplitude) * origin.w
                                    * PacketMaximumGain(recent + i * period, settings);
                        }
                    }
                    if (settings.gridDischarge && settings.allowEffectsOverGrid && origin.z >= 0f && origin.z < 8f)
                    {
                        float support = Mathf.Max(0f, settings.gridSpeed * origin.z) + 4f * Mathf.Max(.2f, settings.gridWidth);
                        int images = Mathf.Min(49, Mathf.CeilToInt(2f * support / surface.LoopLength) + 1);
                        maximum += images * Mathf.Abs(settings.gridAmplitude) * Mathf.Max(1f, Mathf.Abs(settings.gridCurl))
                            * Mathf.Exp(-origin.z * .65f) * Smooth01(origin.z / .15f);
                    }
                }
                return maximum * Mathf.Abs(settings.gridCoupling) / Mathf.Max(.0001f, surface.RearScale);
            }
        }

        private static float PacketMaximumGain(float age, AmplifierTreatmentSettings settings)
        {
            if (age < 0f || age > 8f) return 0f;
            float w = Mathf.Max(.1f, settings.envelopeWidth);
            float width = Mathf.Sqrt(w * w + settings.dispersion * settings.dispersion * age * age);
            return Mathf.Sqrt(w / width) * Smooth01(age / .12f) * Mathf.Exp(-age * .65f);
        }

        private void BindAmplifierTreatment()
        {
            if (!HasAmplifierTreatment)
            {
                properties.SetVector(AmplifierTimingId, Vector4.zero);
                return;
            }
            amplifierTreatments.WriteGridProperties(properties, AmplifierOrigin(1), AmplifierOrigin(2));
        }

        private Vector2 AmplifierOrigin(int team)
        {
            return new Vector2(surface.Width * .5f * (team == 1 ? -1f : 1f), surface.FrontHeight * .5f);
        }

        /// <summary>Shared reference for attached border extensions and validation.
        /// Result is an offset in (across, periodic loop distance), not world XZ.</summary>
        public Vector2 EvaluateAmplifierBoundary(float loopDistance, int team, out Color color)
        {
            Vector2 point = new Vector2(surface == null ? 0f : surface.Width * .5f * (team == 1 ? -1f : 1f), loopDistance);
            color = EvaluateAmplifierColor(point, Color.white);
            return EvaluateAmplifierOffset(point);
        }

        public Vector3 EvaluateAmplifierPoint(Vector2 logical, out Color color)
        {
            color = EvaluateAmplifierColor(logical, Color.white);
            if (surface == null) return Vector3.zero;
            Vector2 shifted = logical + EvaluateAmplifierOffset(logical);
            return surface.WorldPosition(shifted.x, shifted.y);
        }

        public Vector2 EvaluateAmplifierOffset(Vector2 logical)
        {
            if (!HasAmplifierTreatment) return Vector2.zero;
            AmplifierTreatmentSettings settings = amplifierTreatments.Settings;
            float metric = Mathf.Max(.0001f, surface.WidthScale(logical.y));
            float length = surface.LoopLength;
            Vector2 result = Vector2.zero;
            for (int team = 1; team <= 2; team++)
            {
                Vector4 origin = amplifierTreatments.GetGridOrigin(team, AmplifierOrigin(team));
                float y = PeriodicDistance(logical.y - origin.y, length);
                float distance = (logical.x - origin.x) * metric;
                float side = team == 1 ? 1f : -1f;
                Vector2 offset = Vector2.zero;
                if (settings.capturePackets)
                    for (int i = 0; i < Mathf.Clamp(settings.emissions, 1, 5); i++)
                        offset += AmplifierPacket(y, origin.z - i * settings.packetSpacing,
                            settings.carrierAmplitude, side, length, settings);
                if (settings.heldCharge && origin.w > 0f)
                {
                    if (settings.travellingWave)
                    {
                        float modes = Mathf.Max(1f, Mathf.Floor(length / Mathf.Max(.25f, settings.wavelength) + .5f));
                        offset.x += settings.heldAmplitude * origin.w * Mathf.Sin(y * 2f * Mathf.PI * modes / length
                            - amplifierTreatments.Clock * settings.carrierFrequency * 2f * Mathf.PI);
                    }
                    if (settings.occasionalPackets)
                    {
                        float period = Mathf.Max(.5f, settings.heldPacketInterval);
                        float recent = Mathf.Repeat(amplifierTreatments.Clock, period);
                        for (int i = 0; i < 3; i++)
                            offset += AmplifierPacket(y, recent + i * period, settings.heldAmplitude * origin.w * 2f,
                                side, length, settings);
                    }
                }
                // Unlike a planar grid there is no top/bottom end to pin: the packet
                // traverses each bend and rear face continuously around the loop.
                result += new Vector2(offset.x / metric, offset.y)
                    * Mathf.Exp(-distance * distance / (2f * .65f * .65f));
                if (settings.gridDischarge && settings.allowEffectsOverGrid && origin.z >= 0f && origin.z < 8f)
                {
                    Vector2 discharge = AmplifierDischarge(new Vector2(distance, y), origin.z, length, settings);
                    result += new Vector2(discharge.x / metric, discharge.y);
                }
            }
            return result * settings.gridCoupling;
        }

        public Color EvaluateAmplifierColor(Vector2 logical, Color baseColor)
        {
            if (!HasAmplifierTreatment) return baseColor;
            AmplifierTreatmentSettings settings = amplifierTreatments.Settings;
            float metric = Mathf.Max(.0001f, surface.WidthScale(logical.y));
            for (int team = 1; team <= 2; team++)
            {
                Vector4 origin = amplifierTreatments.GetGridOrigin(team, AmplifierOrigin(team));
                float y = PeriodicDistance(logical.y - origin.y, surface.LoopLength);
                float edge = 1f - Smooth01((Mathf.Abs(logical.x - origin.x) * metric - .005f) / .025f);
                float strength = settings.heldCharge && settings.coloredBoundary ? settings.heldIntensity * origin.w : 0f;
                if (settings.capturePackets && origin.z >= 0f && origin.z < 8f)
                {
                    float width = Mathf.Max(.1f, settings.envelopeWidth) + settings.dispersion * origin.z;
                    float d = PeriodicDistance(Mathf.Abs(y) - settings.propagationSpeed * origin.z, surface.LoopLength);
                    strength += settings.carrierAmplitude * settings.gridCoupling * 3f
                        * Mathf.Exp(-d * d / (2f * width * width)) * Mathf.Exp(-origin.z * .65f);
                }
                // Hue is loop-periodic too; no tint jump at the rear antipode or seam.
                float hue = .55f + .13f * Mathf.Sin(y * 2f * Mathf.PI * 3f / surface.LoopLength - amplifierTreatments.Clock * .4f);
                Color tint = Color.LerpUnclamped(Color.white, Color.HSVToRGB(Mathf.Repeat(hue, 1f), 1f, 1f), .72f);
                float alpha = baseColor.a;
                baseColor = Color.LerpUnclamped(baseColor, tint, Mathf.Clamp01(strength) * edge);
                baseColor.a = alpha;
            }
            return baseColor;
        }

        private static Vector2 AmplifierPacket(float y, float age, float amplitude, float side,
            float length, AmplifierTreatmentSettings settings)
        {
            if (age < 0f || age > 8f || amplitude == 0f) return Vector2.zero;
            float w = Mathf.Max(.1f, settings.envelopeWidth);
            float width = Mathf.Sqrt(w * w + settings.dispersion * settings.dispersion * age * age);
            float gain = Mathf.Sqrt(w / width) * Smooth01(age / .12f) * Mathf.Exp(-age * .65f);
            Vector2 result = Vector2.zero;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                float s = PeriodicDistance(y * direction - settings.propagationSpeed * age, length);
                // Compact C1 antipodal fade makes even deliberately broad packets
                // continuous at their wrap, without fading at the actual top/bottom bends.
                float wrapFade = 1f - Smooth01((Mathf.Abs(s) / length - .35f) / .15f);
                float envelope = Mathf.Exp(-s * s / (2f * width * width)) * gain * wrapFade;
                float phase = s * 2f * Mathf.PI / Mathf.Max(.25f, settings.wavelength)
                    - age * settings.carrierFrequency * 2f * Mathf.PI
                    + settings.dispersion * age * s * s / (width * width);
                result.x += amplitude * envelope * Mathf.Cos(phase) * side;
                result.y += amplitude * envelope * Mathf.Cos(phase - settings.quadraturePhase * Mathf.Deg2Rad)
                    * settings.alongAcrossRatio * direction;
            }
            return result;
        }

        private static Vector2 AmplifierDischarge(Vector2 delta, float age, float length, AmplifierTreatmentSettings settings)
        {
            float width = Mathf.Max(.2f, settings.gridWidth);
            float reach = settings.gridSpeed * age;
            float support = Mathf.Max(0f, reach) + 4f * width;
            // Sum images of the radial field on the unrolled sleeve. Opposed fronts
            // meet smoothly at the rear antipode instead of choosing a shortest-
            // path normal that abruptly reverses. A compact C1 window gives exact
            // finite support; 24 images per side exceeds all authored ranges,
            // while only 1..3 are normally evaluated.
            int first = Mathf.Max(-24, Mathf.CeilToInt((-support - delta.y) / length));
            int last = Mathf.Min(24, Mathf.FloorToInt((support - delta.y) / length));
            Vector2 result = Vector2.zero;
            for (int image = first; image <= last; image++)
            {
                Vector2 imageDelta = new Vector2(delta.x, delta.y + image * length);
                float radius = imageDelta.magnitude;
                float travel = radius - reach;
                float sigma = Mathf.Abs(travel) / width;
                if (sigma >= 4f) continue;
                float window = 1f - Smooth01(sigma - 3f);
                float envelope = Mathf.Exp(-travel * travel / (2f * width * width))
                    * Mathf.Exp(-age * .65f) * Smooth01(age / .15f) * window;
                float phase = travel * 2f * Mathf.PI / Mathf.Max(.3f, settings.gridWavelength) - age * 2f;
                Vector2 normal = imageDelta / Mathf.Max(.001f, radius);
                Vector2 tangent = new Vector2(-normal.y, normal.x);
                result += settings.gridAmplitude * envelope
                    * (normal * Mathf.Cos(phase) + tangent * Mathf.Sin(phase) * settings.gridCurl);
            }
            return result;
        }

        private static float PeriodicDistance(float value, float length)
        { return Mathf.Repeat(value + length * .5f, length) - length * .5f; }
        private static float Smooth01(float value)
        { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
    }
}
