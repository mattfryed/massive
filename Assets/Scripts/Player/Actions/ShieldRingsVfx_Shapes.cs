using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes;

namespace Massive.Player
{
    /// <summary>
    /// Shield ring VFX using Shapes Immediate Mode drawing.
    ///
    /// - Draws a set of outline rings (white) around a target
    /// - Rings animate thickness from 0 -> target -> 0
    /// - Rings expand slightly in radius, with per-ring jitter + center offsets
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShieldRingsVfx_Shapes : ImmediateModeShapeDrawer
    {
        [Header("Refs")]
        [Tooltip("If null, defaults to this transform.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("Optional. Used to estimate the player visual radius so rings start at the correct size.")]
        [SerializeField] private PlayerVisualController visuals;

        [Header("Rings")]
        [SerializeField, Min(0)] private int minRings = 2;
        [SerializeField, Min(1)] private int maxRings = 8;
        [SerializeField, Min(0f)] private float ringSpacing = 0.12f;

        [Tooltip("How much rings expand outward from the starting radius.")]
        [SerializeField, Min(0f)] private float radiusGrow = 0.22f;

        [Header("Thickness")]
        [SerializeField, Min(0f)] private float minThickness = 0.02f;
        [SerializeField, Min(0f)] private float maxThickness = 0.08f;

        [Tooltip("Outer rings are thinner if > 0")]
        [SerializeField, Range(0f, 1f)] private float thicknessFalloff = 0.35f;

        [Header("Timing")]
        [SerializeField, Range(0.01f, 0.9f)] private float attackFraction = 0.20f;
        [SerializeField, Range(0.01f, 0.9f)] private float releaseFraction = 0.28f;

        [Header("Parry blast")]
        [SerializeField, Range(1, 6)] private int parryBlastRings = 3;
        [SerializeField, Min(.05f)] private float parryBlastSeconds = .35f;
        [SerializeField, Min(0f)] private float parryBlastSpacingSeconds = .035f;
        [SerializeField, Min(0f)] private float parryBlastExpansion = 1.3f;

        [Header("Jitter")]
        [SerializeField, Min(0f)] private float staticOffsetMax = 0.06f;
        [SerializeField, Min(0f)] private float dynamicOffsetMax = 0.04f;
        [SerializeField, Min(0f)] private float radiusJitter = 0.03f;
        [SerializeField, Min(0f)] private float thicknessJitter = 0.015f;
        [SerializeField, Min(0.01f)] private float jitterFrequency = 6.0f;

        [Header("Unstable Mode")]
        [SerializeField] private bool unstableJitter = true;
        [SerializeField, Min(1f)] private float unstableMultiplier = 1.6f;

        [Header("Render")]
        [SerializeField] private float heightOffset = 0.05f;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField] private Color color = Color.white;

        private struct Ring
        {
            public float seed;
            public float targetRadius;
            public float targetThickness;
            public Vector2 staticOffset;
        }

        private readonly List<Ring> rings = new List<Ring>(16);

        private struct Blast { public float start, strength; }
        private readonly List<Blast> blasts = new List<Blast>(8);
        private bool held, stopping;
        private float holdDuration, stopTime, stopElapsed;
        private bool playing;
        private float startTime;
        private float duration;
        private float strength01;
        private float startRadius;
        private float effectSize = 1f;
        private Massive.Singularity.SingularityPlayerAdapter singularityPresentation;

        public bool IsPlaying => playing || blasts.Count > 0;

        private void Reset()
        {
             var owner = GetComponentInParent<PlayerControllerScript>();
            followTarget = owner != null ? owner.transform : (transform.parent != null ? transform.parent : transform);
            if (!visuals && owner != null) visuals = owner.visualsController;
         
        }

        private void Awake()
        {
            singularityPresentation = GetComponentInParent<Massive.Singularity.SingularityPlayerAdapter>();
            // if (!followTarget) followTarget = transform;
            // if (!visuals) visuals = GetComponentInParent<PlayerVisualController>();
             var owner = GetComponentInParent<PlayerControllerScript>();

            // If left at default (self) or not assigned, bind to owning player root.
            if ((followTarget == null || followTarget == transform) && owner != null)
                followTarget = owner.transform;
            else if (followTarget == null)
                followTarget = (transform.parent != null ? transform.parent : transform);

            // If visuals not explicitly assigned, prefer the one already referenced by the player.
            if (!visuals && owner != null)
                visuals = owner.visualsController;
            if (!visuals)
                visuals = GetComponentInParent<PlayerVisualController>();
         }

        /// <summary>
        /// Optional runtime rebind (useful for spawned/pool players).
        /// </summary>
        public void Bind(Transform follow, PlayerVisualController visualsOverride = null)
        {
            if (follow != null) followTarget = follow;
            if (visualsOverride != null) visuals = visualsOverride;
        }

        public void Play(float strength01, float durationSeconds)
        {
            this.strength01 = Mathf.Clamp01(strength01);
            duration = Mathf.Max(0.05f, durationSeconds);
            startTime = Time.time;
            playing = true;
            held = stopping = false;

            BuildRings();
        }

        public void SetHeld(bool value, float maxSeconds)
        {
            if (!playing || stopping) return;
            if (!value && held && Time.time >= startTime + duration)
            { Stop(false); return; }
            held = value;
            holdDuration = Mathf.Max(.05f, maxSeconds);
        }

        public void PlayParryBlast(float strength)
        {
            if (blasts.Count == 8) blasts.RemoveAt(0);
            blasts.Add(new Blast { start = Time.time, strength = Mathf.Clamp01(strength) });
        }

        public void Stop(bool immediate)
        {
            if (immediate)
            {
                playing = held = stopping = false;
                rings.Clear(); blasts.Clear();
                return;
            }
            if (!playing || stopping) return;
            if (held)
            {
                stopping = true; stopTime = Time.time; stopElapsed = Time.time - startTime;
                return;
            }
            float releaseStart = duration * (1f - releaseFraction);
            if (Time.time - startTime < releaseStart) startTime = Time.time - releaseStart;
        }

        private void BuildRings()
        {
            rings.Clear();
            effectSize = PlayerScaleAdjuster.SizeOf(visuals ? visuals : (Component)followTarget);
            startRadius = GetPlayerRadiusWorld();

            int count = Mathf.RoundToInt(Mathf.Lerp(minRings, maxRings, strength01));
            count = Mathf.Clamp(count, 0, maxRings);
            if (count <= 0) return;

            float thicknessBase = Mathf.Lerp(minThickness, maxThickness, strength01) * effectSize;

            // Use System.Random to avoid disturbing UnityEngine.Random global state
            int seedInt = unchecked((int)(Time.time * 1000f) ^ GetInstanceID());
            var rng = new System.Random(seedInt);

            for (int i = 0; i < count; i++)
            {
                float ringT01 = (count <= 1) ? 0f : (float)i / (count - 1);

                // Seed for perlin noise offsets
                float seed = (float)rng.NextDouble() * 1000f + i * 13.37f;

                // Concentric expansion
                float targetRadius = startRadius + (radiusGrow + ringSpacing * i) * effectSize;

                // Outer ring thickness falloff
                float falloff = Mathf.Lerp(1f, 1f - thicknessFalloff, ringT01);
                float targetThickness = thicknessBase * falloff;

                // Static offset (random direction + random magnitude)
                float ang = (float)rng.NextDouble() * (Mathf.PI * 2f);
                float mag = (float)rng.NextDouble();
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 staticOffset = dir * (staticOffsetMax * mag * strength01 * falloff * effectSize);

                rings.Add(new Ring
                {
                    seed = seed,
                    targetRadius = targetRadius,
                    targetThickness = targetThickness,
                    staticOffset = staticOffset
                });
            }
        }

        private float GetPlayerRadiusWorld()
        {
            // Best guess: use the visual controller if present
            if (visuals != null)
            {
                float scale = 1f;
                if (visuals.visuals != null)
                    scale = visuals.visuals.lossyScale.x;
                else if (followTarget != null)
                    scale = followTarget.lossyScale.x;

                return (visuals.baseRadius + visuals.outlineHalf) * scale;
            }

            // Fallback: try collider extents
            if (followTarget != null)
            {
                var col = followTarget.GetComponentInChildren<Collider>();
                if (col != null)
                {
                    Vector3 e = col.bounds.extents;
                    return Mathf.Max(e.x, e.z);
                }
            }

            return 0.5f * effectSize;
        }

        public override void DrawShapes(Camera cam)
        {
            if (followTarget == null) return;
            float time = Time.time;
            float burstLifetime = parryBlastSeconds + (parryBlastRings - 1) * parryBlastSpacingSeconds;
            for (int i = blasts.Count - 1; i >= 0; i--)
                if (time - blasts[i].start >= burstLifetime) blasts.RemoveAt(i);
            if (!IsPlaying) return;

            float currentSize = PlayerScaleAdjuster.SizeOf(visuals ? visuals : (Component)followTarget);
            if (!Mathf.Approximately(currentSize, effectSize))
            {
                float ratio = currentSize / Mathf.Max(.0001f, effectSize);
                startRadius *= ratio;
                for (int i = 0; i < rings.Count; i++)
                {
                    Ring ring = rings[i];
                    ring.targetRadius *= ratio; ring.targetThickness *= ratio; ring.staticOffset *= ratio;
                    rings[i] = ring;
                }
                effectSize = currentSize;
            }

            float elapsed = stopping ? stopElapsed : time - startTime;
            float attackSeconds = Mathf.Max(.0001f, duration * attackFraction);
            float atk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / attackSeconds));
            float rel = held ? 1f : Mathf.SmoothStep(1f, 0f,
                Mathf.Clamp01((elapsed / duration - (1f - releaseFraction)) / releaseFraction));
            if (stopping) rel *= Mathf.Clamp01(1f - (time - stopTime) / .12f);
            if ((stopping && time >= stopTime + .12f) || (!stopping && elapsed >= (held ? holdDuration : duration)))
            { playing = false; rings.Clear(); }

            float mult = unstableJitter ? unstableMultiplier : 1f;
            Vector3 posWS = followTarget.position + Vector3.up * (heightOffset * effectSize);
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                Draw.ZTest = CompareFunction.LessEqual;
                Draw.ThicknessSpace = ThicknessSpace.Meters;
                for (int i = 0; playing && i < rings.Count; i++)
                {
                    Ring r = rings[i];
                    // Outer rings drain first. Every ring reaches exactly zero width in its own time slot.
                    float slot = Mathf.Max(.0001f, (holdDuration - attackSeconds) / rings.Count);
                    float drain = held ? Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(
                        (elapsed - attackSeconds) / slot - (rings.Count - 1 - i))) : 1f;
                    float env = atk * rel * drain;
                    if (env <= .0001f) continue;
                    float nX = Mathf.PerlinNoise(r.seed, time * jitterFrequency) * 2f - 1f;
                    float nY = Mathf.PerlinNoise(r.seed + 10.123f, time * jitterFrequency) * 2f - 1f;
                    float nR = Mathf.PerlinNoise(r.seed + 20.456f, time * jitterFrequency) * 2f - 1f;
                    float nT = Mathf.PerlinNoise(r.seed + 30.789f, time * jitterFrequency) * 2f - 1f;
                    Vector2 offset = (r.staticOffset + new Vector2(nX, nY) *
                        (dynamicOffsetMax * strength01 * mult * env * effectSize)) * env;
                    float radius = Mathf.Lerp(startRadius, r.targetRadius, atk) + nR * radiusJitter * strength01 * mult * env * effectSize;
                    float thickness = Mathf.Max(0f, (r.targetThickness + nT * thicknessJitter * strength01 * mult * effectSize) * env);
                    DrawRing(posWS, offset, radius, thickness, env);
                }
                // Separate envelopes: a parry never restarts, replaces or extends the ordinary shield rings.
                foreach (var blast in blasts)
                {
                    for (int i = 0; i < parryBlastRings; i++)
                    {
                        float t = (time - blast.start - i * parryBlastSpacingSeconds) / parryBlastSeconds;
                        if (t < 0f || t >= 1f) continue;
                        float expansion = 1f - (1f - t) * (1f - t);
                        float envelope = Mathf.Clamp01(t / .07f) * (1f - t);
                        float radius = GetPlayerRadiusWorld() + parryBlastExpansion * effectSize * expansion;
                        float width = Mathf.Lerp(minThickness, maxThickness, blast.strength) * effectSize * envelope;
                        DrawRing(posWS, Vector2.zero, radius, width, envelope);
                    }
                }
            }
        }

        private void DrawRing(Vector3 posWS, Vector2 offset, float radius, float thickness, float envelope)
        {
            if (thickness <= .0001f) return;
            Draw.Color = new Color(color.r, color.g, color.b, alpha * envelope);
            if (singularityPresentation != null && singularityPresentation.IsRenderingOnSurface)
            {
                Draw.Matrix = Matrix4x4.identity;
                Draw.LineGeometry = LineGeometry.Volumetric3D;
                const int samples = 64;
                Vector3 center = posWS + new Vector3(offset.x, 0f, offset.y);
                Vector3 previousChart = center + Vector3.right * radius;
                Vector3 previous = singularityPresentation.MapChartPoint(previousChart, .015f);
                Color previousColor = SurfaceColor(previousChart, envelope);
                for (int step = 1; step <= samples; step++)
                {
                    float angle = step * (Mathf.PI * 2f / samples);
                    Vector3 nextChart = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    Vector3 next = singularityPresentation.MapChartPoint(nextChart, .015f);
                    Color nextColor = SurfaceColor(nextChart, envelope);
                    Draw.Line(previous, next, thickness, previousColor, nextColor);
                    previous = next; previousColor = nextColor;
                }
            }
            else
            {
                Draw.LineGeometry = LineGeometry.Flat2D;
                Draw.Matrix = Matrix4x4.TRS(posWS, Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
                Draw.Ring(new Vector3(offset.x, offset.y, 0f), radius, thickness: thickness);
            }
        }

        private Color SurfaceColor(Vector3 chartPoint, float envelope)
        {
            float brightness = singularityPresentation.EvaluateChartBrightness(chartPoint);
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, alpha * envelope);
        }
    }
}
