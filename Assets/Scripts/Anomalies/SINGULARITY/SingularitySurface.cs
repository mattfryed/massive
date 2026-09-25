using System;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>
    /// An unrolled periodic strip mapped onto a flattened sleeve. Across is in
    /// front-face units; loop distance is centerline arc length, not a curve parameter.
    /// Local XYZ uses the game's XZ plane, with the rear below it on negative Y.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SingularitySurface : MonoBehaviour
    {
        [Header("Front face")]
        [SerializeField, Min(4f)] private float frontWidth = 28f;
        [Tooltip("Height of the flat front face only. The top and bottom turns extend beyond it; ProjectedHeight includes both turns.")]
        [SerializeField, Min(4f)] private float frontHeight = 12f;
        [Header("Folded rear face")]
        [Tooltip("Explicit projected scale: orthographic cameras do not shrink with depth.")]
        [SerializeField, Range(.5f, 1f)] private float rearScale = .875f;
        [SerializeField, Min(.25f)] private float depth = 3f;
        [Tooltip("Control-point reach beyond each face end. Larger values expose more of the turn in the top-down camera.")]
        [SerializeField, Min(.25f)] private float curlReach = 2.1f;
        [Header("Backside object readability")]
        [Tooltip("Brightness of surface-bound objects on the rear face. 1 keeps their original colors; 0 is black. The folds smoothly blend between front and rear brightness without changing opacity or depth.")]
        [SerializeField, Range(0f, 1f)] private float backsideBrightness = .4f;
        [Header("Authoring aids")]
        [SerializeField] private bool showSurfaceFrame = true;

        private const int ArcSamples = 512;
        private readonly float[] arc = new float[ArcSamples + 1];
        private Vector4 cachedShape;
        private float cachedReach, topLength, projectedHeight, crestArcLength;
        private bool cached;
        private int revision;

        public float Width { get { Ensure(); return cachedShape.x; } }
        public float FrontHeight { get { Ensure(); return cachedShape.y; } }
        /// <summary>Exact local Z extent of the entire folded surface, including both bends.</summary>
        public float ProjectedHeight { get { Ensure(); return projectedHeight; } }
        /// <summary>Loop coordinates of the exact top/bottom projected extrema.
        /// Useful for joining decorative goal outlines to the folded side edges.</summary>
        public float TopCrestDistance { get { Ensure(); return cachedShape.y + crestArcLength; } }
        public float BottomCrestDistance { get { Ensure(); return LoopLength - crestArcLength; } }
        public float RearScale { get { Ensure(); return cachedShape.z; } }
        public float Depth { get { Ensure(); return cachedShape.w; } }
        public float CurlReach { get { Ensure(); return cachedReach; } }
        public int Revision { get { Ensure(); return revision; } }
        public float BacksideBrightness => Safe(backsideBrightness, 0f, 1f);
        public float EvaluateBrightness(float loopDistance)
        { return Mathf.Lerp(1f, BacksideBrightness, RearWeight(loopDistance)); }
        public float TopStart { get { Ensure(); return cachedShape.y; } }
        public float RearStart { get { Ensure(); return cachedShape.y + topLength; } }
        public float BottomStart { get { Ensure(); return cachedShape.y * (1f + cachedShape.z) + topLength; } }
        public float LoopLength { get { Ensure(); return cachedShape.y * (1f + cachedShape.z) + 2f * topLength; } }

        public void Configure(float width, float height, float backScale, float separation, float reach)
        {
            frontWidth = width; frontHeight = height; rearScale = backScale;
            depth = separation; curlReach = reach; Ensure();
        }

        /// <summary>Fit the full local XZ footprint without scaling players or the root.
        /// Existing Configure still interprets height as the flat-front height.
        /// A requested extent that cannot fit the supported 4..200-unit front face
        /// is rejected before changing the surface, rather than silently clamped.</summary>
        public void ConfigureProjectedBounds(float width, float totalHeight, float backScale, float separation, float reach)
        {
            float resolvedScale = Safe(backScale, .5f, 1f), resolvedReach = Safe(reach, .25f, 30f);
            double lower = 4d, upper = 200d;
            double minimum = FullProjectedHeight(lower, resolvedScale, resolvedReach);
            double maximum = FullProjectedHeight(upper, resolvedScale, resolvedReach);
            // Permit only float-rounding error at exact supported endpoints.
            double tolerance = Math.Max(1d, Math.Abs(totalHeight)) * .0000002d;
            if (float.IsNaN(totalHeight) || float.IsInfinity(totalHeight)
                || totalHeight < minimum - tolerance || totalHeight > maximum + tolerance)
                throw new ArgumentOutOfRangeException(nameof(totalHeight), totalHeight,
                    "Full projected height must be between " + minimum.ToString("0.######") + " and "
                    + maximum.ToString("0.######") + " for this rear scale and curl reach.");
            double target = Math.Max(minimum, Math.Min(maximum, totalHeight));
            for (int i = 0; i < 48; i++)
            {
                double middle = (lower + upper) * .5d;
                if (FullProjectedHeight(middle, resolvedScale, resolvedReach) < target) lower = middle;
                else upper = middle;
            }
            Configure(width, (float)((lower + upper) * .5d), resolvedScale, separation, resolvedReach);
        }

        private void OnEnable() { Ensure(); }
        private void OnValidate() { cached = false; }

        private void Ensure()
        {
            Vector4 shape = new Vector4(Safe(frontWidth, 4f, 200f), Safe(frontHeight, 4f, 200f),
                Safe(rearScale, .5f, 1f), Safe(depth, .25f, 50f));
            float reach = Safe(curlReach, .25f, 30f);
            if (cached && shape == cachedShape && reach == cachedReach) return;
            cached = true; cachedShape = shape; cachedReach = reach;
            projectedHeight = (float)FullProjectedHeight(shape.y, shape.z, reach);
            Vector2 previous = TopCurve(0f); arc[0] = 0f;
            for (int i = 1; i <= ArcSamples; i++)
            {
                Vector2 next = TopCurve(i / (float)ArcSamples);
                arc[i] = arc[i - 1] + Vector2.Distance(previous, next); previous = next;
            }
            topLength = arc[ArcSamples];
            float crestSample = (float)CrestParameter(shape.y, shape.z, reach) * ArcSamples;
            int crestIndex = Mathf.Clamp(Mathf.FloorToInt(crestSample), 0, ArcSamples - 1);
            crestArcLength = Mathf.Lerp(arc[crestIndex], arc[crestIndex + 1], crestSample - crestIndex);
            revision++;
        }

        private static float Safe(float value, float minimum, float maximum)
        { return float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Clamp(value, minimum, maximum); }

        // The upper bend's Z coordinate is h/2 - d*smoothstep(t) + 3*r*t*(1-t).
        // Its single maximum solves d*t*t - (d+r)*t + r/2 = 0. The rationalized
        // root remains stable for equal faces (d=0), where the maximum is at .5.
        private static double FullProjectedHeight(double height, double backScale, double reach)
        {
            double d = height * (1d - backScale) * .5d;
            double t = CrestParameter(height, backScale, reach);
            double smooth = t * t * (3d - 2d * t);
            return height - 2d * d * smooth + 6d * reach * t * (1d - t);
        }
        private static double CrestParameter(double height, double backScale, double reach)
        {
            double d = height * (1d - backScale) * .5d;
            return reach / (d + reach + Math.Sqrt(d * d + reach * reach));
        }

        // YZ cross section of the upper connector. The two flat-face endpoints
        // have horizontal tangents, so the normal does not snap at either join.
        private Vector2 TopCurve(float t)
        {
            float a = 1f - t;
            Vector2 p0 = new Vector2(0f, cachedShape.y * .5f);
            Vector2 p1 = p0 + new Vector2(0f, cachedReach);
            Vector2 p3 = new Vector2(-cachedShape.w, cachedShape.y * cachedShape.z * .5f);
            Vector2 p2 = p3 + new Vector2(0f, cachedReach);
            return a * a * a * p0 + 3f * a * a * t * p1 + 3f * a * t * t * p2 + t * t * t * p3;
        }

        private float CurveParameter(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, topLength);
            int lo = 0, hi = ArcSamples;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (arc[mid] < distance) lo = mid; else hi = mid;
            }
            float t = Mathf.InverseLerp(arc[lo], arc[hi], distance);
            return (lo + t) / ArcSamples;
        }

        public float Wrap(float distance)
        {
            float length = LoopLength;
            return float.IsNaN(distance) || float.IsInfinity(distance) ? 0f : Mathf.Repeat(distance, length);
        }

        /// <summary>Signed shortest displacement from 'from' to 'to' along this loop.</summary>
        public float LoopDelta(float from, float to)
        {
            float length = LoopLength;
            return Mathf.Repeat(to - from + length * .5f, length) - length * .5f;
        }

        public Vector3 Evaluate(float across, float loopDistance)
        {
            Sample(loopDistance, out Vector2 yz, out float scale, out _);
            return new Vector3(across * scale, yz.x, yz.y);
        }

        public Vector3 WorldPosition(float across, float loopDistance)
        { return transform.TransformPoint(Evaluate(across, loopDistance)); }

        public float WidthScale(float loopDistance)
        { Sample(loopDistance, out _, out float scale, out _); return scale; }

        public float RearWeight(float loopDistance)
        { Sample(loopDistance, out _, out _, out float rear); return rear; }

        // RGBA LUT contract shared by the grid: width scale, local Y, local Z,
        // rear styling weight. Render data derives only from this geometry owner.
        public Vector4 SamplePacked(float loopDistance)
        {
            Sample(loopDistance, out Vector2 yz, out float scale, out float rear);
            return new Vector4(scale, yz.x, yz.y, rear);
        }

        private void Sample(float distance, out Vector2 yz, out float scale, out float rear)
        {
            Ensure(); float h = cachedShape.y, k = cachedShape.z;
            float rearStart = h + topLength, bottomStart = rearStart + h * k;
            float s = Wrap(distance);
            if (s < h)
            {
                yz = new Vector2(0f, -h * .5f + s); scale = 1f; rear = 0f;
            }
            else if (s < rearStart)
            {
                float t = CurveParameter(s - h); yz = TopCurve(t);
                rear = t * t * (3f - 2f * t); scale = Mathf.Lerp(1f, k, rear);
            }
            else if (s < bottomStart)
            {
                yz = new Vector2(-cachedShape.w, h * k * .5f - (s - rearStart)); scale = k; rear = 1f;
            }
            else
            {
                float t = CurveParameter(topLength - (s - bottomStart));
                yz = TopCurve(t); yz.y = -yz.y;
                rear = t * t * (3f - 2f * t); scale = Mathf.Lerp(1f, k, rear);
            }
        }

        /// <summary>Local differential basis. Also captures the horizontal drift
        /// where the sleeve narrows, rather than assuming the two axes are orthogonal.</summary>
        public void Frame(float across, float distance, out Vector3 acrossTangent, out Vector3 alongTangent)
        {
            acrossTangent = Vector3.right * WidthScale(distance);
            const float epsilon = .002f;
            alongTangent = (Evaluate(across, distance + epsilon) - Evaluate(across, distance - epsilon)) / (2f * epsilon);
        }

        public Vector3 Normal(float across, float distance)
        {
            Frame(across, distance, out Vector3 du, out Vector3 ds);
            Vector3 normal = Vector3.Cross(ds, du);
            return normal.sqrMagnitude > .0000001f ? normal.normalized : Vector3.up;
        }

        public string Region(float loopDistance)
        {
            float s = Wrap(loopDistance);
            if (s < TopStart) return "FRONT";
            if (s < RearStart) return "TOP TURN";
            return s < BottomStart ? "REAR" : "BOTTOM TURN";
        }

        private void OnDrawGizmosSelected()
        {
            if (!showSurfaceFrame) return;
            Gizmos.color = new Color(.3f, .8f, 1f, .65f);
            const int count = 128;
            for (int side = -1; side <= 1; side++)
            {
                float x = Width * .5f * side; Vector3 previous = WorldPosition(x, 0f);
                for (int i = 1; i <= count; i++)
                {
                    Vector3 next = WorldPosition(x, LoopLength * i / count);
                    Gizmos.DrawLine(previous, next); previous = next;
                }
            }
        }
    }
}
