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

        private bool playing;
        private float startTime;
        private float duration;
        private float strength01;
        private float startRadius;

        public bool IsPlaying => playing;

        private void Reset()
        {
            followTarget = transform;
            visuals = GetComponentInParent<PlayerVisualController>();
        }

        private void Awake()
        {
            if (!followTarget) followTarget = transform;
            if (!visuals) visuals = GetComponentInParent<PlayerVisualController>();
        }

        public void Play(float strength01, float durationSeconds)
        {
            this.strength01 = Mathf.Clamp01(strength01);
            duration = Mathf.Max(0.05f, durationSeconds);
            startTime = Time.time;
            playing = true;

            BuildRings();
        }

        public void Stop(bool immediate)
        {
            if (!playing) return;

            if (immediate)
            {
                playing = false;
                rings.Clear();
                return;
            }

            // If we're already in (or past) the release window, don't rewind time.
            // Rewinding here causes the "second pulse" at the end.
            float elapsed = Time.time - startTime;
            float t01 = (duration <= 0.0001f) ? 1f : Mathf.Clamp01(elapsed / duration);
            float releaseStart01 = Mathf.Clamp01(1f - releaseFraction);

            if (t01 >= releaseStart01)
                return;

            // Cancel early: warp to release start so we fade out now
            startTime = Time.time - duration * releaseStart01;
        }


        private void BuildRings()
        {
            rings.Clear();
            startRadius = GetPlayerRadiusWorld();

            int count = Mathf.RoundToInt(Mathf.Lerp(minRings, maxRings, strength01));
            count = Mathf.Clamp(count, 0, maxRings);
            if (count <= 0) return;

            float thicknessBase = Mathf.Lerp(minThickness, maxThickness, strength01);

            // Use System.Random to avoid disturbing UnityEngine.Random global state
            int seedInt = unchecked((int)(Time.time * 1000f) ^ GetInstanceID());
            var rng = new System.Random(seedInt);

            for (int i = 0; i < count; i++)
            {
                float ringT01 = (count <= 1) ? 0f : (float)i / (count - 1);

                // Seed for perlin noise offsets
                float seed = (float)rng.NextDouble() * 1000f + i * 13.37f;

                // Concentric expansion
                float targetRadius = startRadius + radiusGrow + ringSpacing * i;

                // Outer ring thickness falloff
                float falloff = Mathf.Lerp(1f, 1f - thicknessFalloff, ringT01);
                float targetThickness = thicknessBase * falloff;

                // Static offset (random direction + random magnitude)
                float ang = (float)rng.NextDouble() * (Mathf.PI * 2f);
                float mag = (float)rng.NextDouble();
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 staticOffset = dir * (staticOffsetMax * mag * strength01 * falloff);

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

            return 0.5f;
        }

        public override void DrawShapes(Camera cam)
        {
            if (!playing || rings.Count == 0 || followTarget == null)
                return;

            float elapsed = Time.time - startTime;
            float t01 = (duration <= 0.0001f) ? 1f : Mathf.Clamp01(elapsed / duration);

            if (elapsed >= duration + 0.05f)
            {
                playing = false;
                rings.Clear();
                return;
            }

            float atk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t01 / Mathf.Max(0.0001f, attackFraction)));
            float rel = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t01 - (1f - releaseFraction)) / Mathf.Max(0.0001f, releaseFraction)));
            float env = atk * rel;

            if (env <= 0.0001f)
                return;

            float mult = unstableJitter ? unstableMultiplier : 1f;
            float time = Time.time;

            using (Draw.Command(cam))
            {
                // Reset all static state so we don't inherit styling from other drawers
                Draw.ResetAllDrawStates();
                Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Flat2D;
                Draw.ThicknessSpace = ThicknessSpace.Meters;

                // Draw in a horizontal plane (XZ) around the target
                Vector3 posWS = followTarget.position + Vector3.up * heightOffset;
                Draw.Matrix = Matrix4x4.TRS(posWS, Quaternion.Euler(-90f, 0f, 0f), Vector3.one);

                for (int i = 0; i < rings.Count; i++)
                {
                    Ring r = rings[i];

                    float nX = Mathf.PerlinNoise(r.seed, time * jitterFrequency) * 2f - 1f;
                    float nY = Mathf.PerlinNoise(r.seed + 10.123f, time * jitterFrequency) * 2f - 1f;
                    float nR = Mathf.PerlinNoise(r.seed + 20.456f, time * jitterFrequency) * 2f - 1f;
                    float nT = Mathf.PerlinNoise(r.seed + 30.789f, time * jitterFrequency) * 2f - 1f;

                    Vector2 dynOffset = new Vector2(nX, nY) * (dynamicOffsetMax * strength01 * mult * env);
                    Vector2 totalOffset = (r.staticOffset + dynOffset) * env;

                    float radius = Mathf.Lerp(startRadius, r.targetRadius, atk);
                    radius += nR * radiusJitter * strength01 * mult * env;

                    float thickness = r.targetThickness * env;
                    thickness += nT * thicknessJitter * strength01 * mult * env;
                    thickness = Mathf.Max(0f, thickness);

                    if (thickness <= 0.0001f)
                        continue;

                    Draw.Color = new Color(color.r, color.g, color.b, alpha * env);
                    Draw.Ring(new Vector3(totalOffset.x, totalOffset.y, 0f), radius, thickness: thickness);
                }
            }
        }
    }
}
