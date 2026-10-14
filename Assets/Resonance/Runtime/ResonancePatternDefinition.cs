using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Resonance
{
    // Append values to preserve existing serialized curve kinds.
    public enum ResonanceCurveKind { Circle, PolarLobes, ClosedPolyline, OpenPolyline }
    public enum ResonanceBehavior { VisualOnly, HardWall, LensDeflector, DampingMembrane }
    public enum ResonanceTaperShape { Smooth, Linear, Smoother }

    [Serializable]
    public sealed class ResonanceGlobalProfile
    {
        [Tooltip("Overrides this profile on every arc. Turning it off restores the unmodified definition values.")]
        public bool enabled;
        [Min(0f)] public float thickness = .3f;
        [Range(0f, .5f)] public float taperFraction = .22f;
        [Tooltip("Local units per end. Zero uses Taper Fraction; positive length supersedes the fraction.")]
        [Min(0f)] public float taperLength;
        [Min(.1f)] public float falloffPower = 1f;
        public ResonanceTaperShape falloffShape;
    }

    [Serializable]
    public sealed class ResonanceCurve
    {
        public string name = "Boundary";
        public ResonanceCurveKind kind;
        [Min(0.001f)] public float radius = 2f;
        [Range(0f, 0.8f)] public float lobeDepth = 0.3f;
        [Range(2, 16)] public int lobes = 4;
        public float rotationDegrees = 45f;
        public Vector2 offset;
        [Tooltip("Ordered XZ points. Closed Polyline wraps; Open Polyline ends at its last point. Arc degrees map to normalized path length.")]
        public List<Vector2> points = new List<Vector2>();

        public Vector3 Evaluate(float degrees)
        {
            float a = (degrees - rotationDegrees) * Mathf.Deg2Rad;
            Vector2 p;
            if ((kind == ResonanceCurveKind.ClosedPolyline || kind == ResonanceCurveKind.OpenPolyline) && points != null && points.Count > 1)
            {
                bool open = kind == ResonanceCurveKind.OpenPolyline;
                int edges = open ? points.Count - 1 : points.Count;
                float length = 0f;
                for (int i = 0; i < edges; i++) length += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
                float target = (open ? Mathf.Clamp01(degrees / 360f) : Mathf.Repeat(degrees / 360f, 1f)) * length;
                p = open ? points[points.Count - 1] : points[0];
                for (int i = 0; i < edges; i++)
                {
                    Vector2 next = points[(i + 1) % points.Count];
                    float d = Vector2.Distance(points[i], next);
                    if (target <= d) { p = Vector2.Lerp(points[i], next, d > 0f ? target / d : 0f); break; }
                    target -= d;
                }
            }
            else
            {
                float r = Mathf.Max(0.001f, radius) * (kind == ResonanceCurveKind.PolarLobes
                    ? 1f + Mathf.Clamp(lobeDepth, 0f, 0.8f) * Mathf.Cos(Mathf.Clamp(lobes, 2, 16) * a) : 1f);
                float theta = degrees * Mathf.Deg2Rad;
                p = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
            }
            p += offset;
            return new Vector3(p.x, 0f, p.y);
        }
    }

    [Serializable]
    public sealed class ResonanceArc
    {
        public string name = "Arc";
        public bool exposed = true;
        [Min(0)] public int curveIndex;
        [Tooltip("Degrees from +X toward +Z. Negative starts wrap naturally.")]
        public float startDegrees = 30f;
        [Range(0f, 360f)] public float sweepDegrees = 30f;
        public ResonanceBehavior behavior = ResonanceBehavior.HardWall;
        [Min(0.001f)] public float thickness = 0.16f;
        [Min(0f)] public float colliderThickness = 0.16f;
        [Header("Collision profile (independent of the resting ribbon)")]
        public bool independentCollisionProfile;
        [Range(0f, 1f)] public float collisionStart = 0.08f;
        [Range(0f, 1f)] public float collisionEnd = 0.92f;
        [Range(0f, 0.5f)] public float collisionTaperFraction = 0.18f;
        [Min(0.1f)] public float collisionFalloffPower = 1f;
        [Min(0f)] public float collisionTaperLength;
        public ResonanceTaperShape collisionFalloffShape;
        [Header("Resting visual profile")]
        [Range(0f, 0.5f)] public float endTaperFraction = 0.22f;
        [Min(0.1f)] public float falloffPower = 1f;
        [Tooltip("Local units per end; zero uses the fraction. Length is resolved after the pattern is fitted.")]
        [Min(0f)] public float taperLength;
        public ResonanceTaperShape falloffShape;
        [Tooltip("Keep the start full-width where this arc meets an arena wall. Applies to visuals and collision taper.")]
        public bool untaperedStart;
        [Tooltip("Keep the end full-width where this arc meets an arena wall. Applies to visuals and collision taper.")]
        public bool untaperedEnd;
        // Resolved from the curve during rebuild, never serialized into the definition.
        [NonSerialized] public bool openPath;
        [Range(0f, 1f)] public float opacity = 0.8f;
        [Min(0f)] public float intensity = 1.5f;
        public Color color = new Color(0.65f, 0.9f, 1f, 1f);
        [Tooltip("Lens turning speed in degrees/second. Positive aligns along the nearest curve tangent; negative aligns perpendicular.")]
        public float deflectionDegreesPerSecond = 160f;
        [Min(0f)] public float dampingPerSecond = 3f;
        [Header("Grid attraction (multiplies the controller settings)")]
        [Min(0f)] public float gridAttractionMultiplier = 1f;
        [Min(0f)] public float gridRadiusMultiplier = 1f;

        public ResonanceArc Copy() { return (ResonanceArc)MemberwiseClone(); }

        public float Envelope(float t)
        {
            if ((!openPath && sweepDegrees >= 359.999f) || endTaperFraction <= 0f) return 1f;
            return Taper(t, endTaperFraction, falloffPower, falloffShape);
        }
        public float CollisionEnvelope(float t)
        {
            if (!independentCollisionProfile) return Envelope(t);
            if (t < collisionStart || t > collisionEnd || collisionEnd <= collisionStart) return 0f;
            if (!openPath && sweepDegrees >= 359.999f && collisionStart <= 0f && collisionEnd >= 1f) return 1f;
            return Taper(Mathf.InverseLerp(collisionStart, collisionEnd, t), collisionTaperFraction, collisionFalloffPower, collisionFalloffShape);
        }
        private float Taper(float t, float fraction, float power, ResonanceTaperShape shape)
        {
            if (fraction <= 0f) return 1f;
            float x = Mathf.Clamp01(Mathf.Min(untaperedStart ? 1f : t, untaperedEnd ? 1f : 1f - t) / Mathf.Clamp(fraction, 0.0001f, 0.5f));
            if (shape == ResonanceTaperShape.Smooth) x = x * x * (3f - 2f * x);
            else if (shape == ResonanceTaperShape.Smoother) x = x*x*x*(x*(x*6f-15f)+10f);
            return Mathf.Pow(x, Mathf.Max(0.1f, power));
        }
        public bool IsVisible => exposed && sweepDegrees > 0f && thickness > 0f && opacity > 0f && intensity > 0f && color.a > 0f;
    }

    [CreateAssetMenu(menuName = "MASSIVE/Resonance/Pattern Definition", fileName = "Resonance Pattern")]
    public sealed class ResonancePatternDefinition : ScriptableObject
    {
        [Tooltip("Reference label, not a physical frequency simulation.")]
        public string referenceLabel = "345 Hz — geometric study";
        [Tooltip("Optional one-shot on a real Amplifier Core impact/turnaround. Uses the shared SFX pool, mixer, mute policy and cue cooldown. Player passage stays silent.")]
        public AudioCue coreImpactSound;
        public bool showCenter = true;
        [Min(0.001f)] public float centerRadius = 0.055f;
        public Color centerColor = Color.white;
        public List<ResonanceCurve> curves = new List<ResonanceCurve>();
        public List<ResonanceArc> arcs = new List<ResonanceArc>();
        [NonSerialized] public int revision;
        private void OnValidate() { revision++; }

        public void Set345HzDefaults()
        {
            curves = new List<ResonanceCurve> {
                new ResonanceCurve { name = "Inner circle", radius = 1.3f },
                new ResonanceCurve { name = "Outer four-lobe boundary", kind = ResonanceCurveKind.PolarLobes, radius = 3.7f, lobeDepth = 0.31f, rotationDegrees = 45f }
            };
            arcs = new List<ResonanceArc>();
            for (int i = 0; i < 4; i++)
            {
                arcs.Add(new ResonanceArc { name = "Inner arc " + (i + 1), curveIndex = 0, startDegrees = i * 90f + 28f, sweepDegrees = 34f });
                arcs.Add(new ResonanceArc { name = "Concave boundary " + (i + 1), curveIndex = 1, startDegrees = i * 90f - 22f, sweepDegrees = 44f, thickness = 0.3f, colliderThickness = 0.2f, independentCollisionProfile = true });
            }
            revision++;
        }
    }
}
