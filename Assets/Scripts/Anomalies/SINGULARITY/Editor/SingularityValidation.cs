using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Geometry-contract checks in an isolated preview scene. No production scene,
    /// prefab, material, physics, grid or player state is changed.</summary>
    public static class SingularityValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Surface Geometry")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var passed = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("SINGULARITY geometry validation") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var surface = root.AddComponent<SingularitySurface>();
                ValidateShape(surface, "reference", 28f, 12f, .875f, 3f, 2.1f, true, passed);
                ValidateShape(surface, "square half-size rear", 12f, 12f, .5f, 4f, 3f, true, passed);
                ValidateShape(surface, "equal faces", 28f, 12f, 1f, 3f, 2.1f, true, passed);
                ValidateShape(surface, "minimum dimensions", 4f, 4f, .5f, .25f, .25f, true, passed);
                ValidateShape(surface, "deep tight turn extreme", 200f, 200f, .5f, 50f, .25f, false, passed);
                ValidateShape(surface, "wide shallow extreme", 200f, 4f, .5f, .25f, 30f, false, passed);

                surface.Configure(28f, 12f, .875f, 3f, 2.1f);
                int revision = surface.Revision;
                for (int i = 0; i < 10; i++)
                { surface.Evaluate(i, i * .7f); surface.SamplePacked(i); surface.Normal(i, i); }
                Check(surface.Revision == revision, "sampling does not invalidate the cached surface", passed);
                surface.Configure(28f, 12f, .875f, 3f, 2.1f);
                Check(surface.Revision == revision, "equivalent configuration preserves the revision", passed);
                surface.Configure(30f, 12f, .875f, 3f, 2.1f);
                Check(surface.Revision > revision && Near(surface.Width, 30f, .0001f), "shape change publishes a fresh revision and width", passed);
                revision = surface.Revision;
                surface.Configure(30f, 12f, .875f, 3f, 3f);
                Check(surface.Revision > revision && Near(surface.CurlReach, 3f, .0001f), "curl reach participates in geometry cache invalidation", passed);
                surface.Configure(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f);
                Check(surface.Width == 4f && surface.FrontHeight == 4f && surface.RearScale == .5f
                    && surface.Depth == .25f && surface.CurlReach == .25f, "invalid dimensions sanitize to finite nondegenerate minima", passed);
                Check(Finite(surface.LoopLength) && surface.LoopLength > 0f && Finite(surface.Normal(2f, surface.TopStart)),
                    "sanitized configuration still produces a usable loop and normal", passed);
                Check(surface.Wrap(float.NaN) == 0f && surface.Wrap(float.PositiveInfinity) == 0f
                    && surface.Wrap(float.NegativeInfinity) == 0f, "invalid loop positions wrap safely to the origin", passed);
                surface.Configure(1000f, 1000f, 2f, 100f, 100f);
                Check(surface.Width == 200f && surface.FrontHeight == 200f && surface.RearScale == 1f
                    && surface.Depth == 50f && surface.CurlReach == 30f, "oversized authoring inputs respect the geometry limits", passed);
                surface.Configure(28f, 12f, .875f, 3f, 2.1f);
                root.transform.SetPositionAndRotation(new Vector3(3f, 5f, -7f), Quaternion.Euler(13f, 37f, -8f));
                root.transform.localScale = new Vector3(1.25f, .8f, 1.5f);
                Vector3 expected = root.transform.TransformPoint(surface.Evaluate(4f, surface.RearStart + 2f));
                Check(Vector3.Distance(surface.WorldPosition(4f, surface.RearStart + 2f), expected) < .0001f,
                    "world-space adapter respects position, rotation and nonuniform scale", passed);
                return passed.Count + " SINGULARITY surface checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ValidateShape(SingularitySurface surface, string label, float width, float height,
            float rearScale, float depth, float reach, bool checkJoinSpeeds, List<string> passed)
        {
            surface.Configure(width, height, rearScale, depth, reach);
            float length = surface.LoopLength;
            float shapeScale = Mathf.Max(width, height, depth, reach);
            float positionTolerance = Mathf.Max(.0002f, shapeScale * .00002f);
            float halfWidth = surface.Width * .5f;
            float rearMiddle = (surface.RearStart + surface.BottomStart) * .5f;
            float frontMiddle = surface.FrontHeight * .5f;
            Check(surface.TopStart > 0f && surface.RearStart > surface.TopStart
                && surface.BottomStart > surface.RearStart && length > surface.BottomStart,
                label + ": four regions have positive ordered lengths", passed);
            Check(Near(surface.TopStart, height, positionTolerance)
                && Near(surface.BottomStart - surface.RearStart, height * rearScale, positionTolerance)
                && Near(length - surface.BottomStart, surface.RearStart - surface.TopStart, positionTolerance),
                label + ": front/rear dimensions and mirrored bend lengths agree", passed);
            Check(Vector3.Distance(surface.Evaluate(0f, 0f), new Vector3(0f, 0f, -height * .5f)) < positionTolerance
                && Vector3.Distance(surface.Evaluate(0f, surface.TopStart), new Vector3(0f, 0f, height * .5f)) < positionTolerance
                && Vector3.Distance(surface.Evaluate(0f, surface.RearStart), new Vector3(0f, -depth, height * rearScale * .5f)) < positionTolerance
                && Vector3.Distance(surface.Evaluate(0f, surface.BottomStart), new Vector3(0f, -depth, -height * rearScale * .5f)) < positionTolerance,
                label + ": all four face/bend endpoints occupy their intended positions", passed);
            float frontSpan = surface.Evaluate(halfWidth, frontMiddle).x - surface.Evaluate(-halfWidth, frontMiddle).x;
            float rearSpan = surface.Evaluate(halfWidth, rearMiddle).x - surface.Evaluate(-halfWidth, rearMiddle).x;
            Check(Near(frontSpan, width, positionTolerance) && Near(rearSpan, width * rearScale, positionTolerance)
                && Near(surface.WidthScale(frontMiddle), 1f, .0001f) && Near(surface.WidthScale(rearMiddle), rearScale, .0001f),
                label + ": across uses front-face distance and rear width shrinks explicitly", passed);
            Check(surface.RearWeight(frontMiddle) == 0f && surface.RearWeight(rearMiddle) == 1f
                && surface.Region(frontMiddle) == "FRONT" && surface.Region(rearMiddle) == "REAR"
                && surface.Region((surface.TopStart + surface.RearStart) * .5f) == "TOP TURN"
                && surface.Region((surface.BottomStart + length) * .5f) == "BOTTOM TURN",
                label + ": region labels and rear styling agree with geometry", passed);
            surface.Frame(halfWidth * .7f, frontMiddle, out Vector3 frontAcross, out Vector3 frontAlong);
            surface.Frame(halfWidth * .7f, rearMiddle, out Vector3 rearAcross, out Vector3 rearAlong);
            Check(frontAlong.z > .98f && rearAlong.z < -.98f && Mathf.Abs(frontAlong.y) < .001f
                && Mathf.Abs(rearAlong.y) < .001f && frontAcross.x > 0f && rearAcross.x > 0f,
                label + ": positive loop travel advances front Z and reverses rear Z", passed);
            Check(Vector3.Dot(surface.Normal(0, frontMiddle), Vector3.up) > .9999f
                && Vector3.Dot(surface.Normal(0, rearMiddle), Vector3.down) > .9999f,
                label + ": front/rear normals consistently face opposite sides", passed);

            bool periodic = true, mirrors = true, normals = true, metric = true, packed = true, ranges = true;
            float minimumMetric = float.PositiveInfinity;
            for (int i = 0; i <= 192; i++)
            {
                float s = length * i / 192f;
                Vector4 data = surface.SamplePacked(s);
                ranges &= data.x >= rearScale - .0001f && data.x <= 1.0001f && data.w >= -.0001f && data.w <= 1.0001f;
                for (int side = -2; side <= 2; side++)
                {
                    float across = halfWidth * side * .5f;
                    Vector3 point = surface.Evaluate(across, s);
                    Vector3 left = surface.Evaluate(-across, s);
                    Vector3 bottom = surface.Evaluate(across, height - s);
                    mirrors &= Vector3.Distance(left, new Vector3(-point.x, point.y, point.z)) < positionTolerance
                        && Vector3.Distance(bottom, new Vector3(point.x, point.y, -point.z)) < positionTolerance;
                    periodic &= Vector3.Distance(point, surface.Evaluate(across, s + length * 2f)) < positionTolerance
                        && Vector3.Distance(point, surface.Evaluate(across, s - length * 3f)) < positionTolerance;
                    packed &= Vector3.Distance(point, new Vector3(across * data.x, data.y, data.z)) < positionTolerance;
                    surface.Frame(across, s, out Vector3 du, out Vector3 ds);
                    Vector3 normal = surface.Normal(across, s);
                    float area = Vector3.Cross(du, ds).magnitude;
                    minimumMetric = Mathf.Min(minimumMetric, area);
                    metric &= Finite(du) && Finite(ds) && Finite(area) && area > .05f;
                    normals &= Finite(point) && Finite(normal) && Mathf.Abs(normal.magnitude - 1f) < .0002f
                        && Mathf.Abs(Vector3.Dot(normal, du.normalized)) < .001f
                        && Mathf.Abs(Vector3.Dot(normal, ds.normalized)) < .001f;
                }
            }
            Check(periodic, label + ": negative and multiple loop wraps preserve geometry", passed);
            Check(mirrors, label + ": left/right and top/bottom symmetry hold across faces and bends", passed);
            Check(packed, label + ": renderer LUT matches the CPU surface exactly", passed);
            Check(ranges, label + ": width and rear blend remain within their authored ranges", passed);
            Check(normals, label + ": all dense samples have finite unit normals orthogonal to the frame", passed);
            Check(metric, label + ": nondegenerate surface metric including turns (minimum area " + minimumMetric.ToString("F4") + ")", passed);

            float[] joins = { 0f, surface.TopStart, surface.RearStart, surface.BottomStart };
            bool positions = true, directions = true, speeds = true;
            float epsilon = Mathf.Max(.00005f, Mathf.Min(depth, reach) * .0002f);
            float minimumCosine = 1f, maximumSpeedError = 0f;
            foreach (float join in joins)
                for (int side = -1; side <= 1; side++)
                {
                    float across = halfWidth * side;
                    Vector3 center = surface.Evaluate(across, join);
                    Vector3 before = surface.Evaluate(across, join - epsilon), after = surface.Evaluate(across, join + epsilon);
                    Vector3 incoming = (center - before) / epsilon, outgoing = (after - center) / epsilon;
                    float cosine = Vector3.Dot(incoming.normalized, outgoing.normalized);
                    float speedError = Mathf.Abs(incoming.magnitude - outgoing.magnitude) / Mathf.Max(.001f, incoming.magnitude);
                    minimumCosine = Mathf.Min(minimumCosine, cosine); maximumSpeedError = Mathf.Max(maximumSpeedError, speedError);
                    positions &= Vector3.Distance(center, before) < epsilon * 2f + positionTolerance
                        && Vector3.Distance(center, after) < epsilon * 2f + positionTolerance;
                    directions &= cosine > .99f;
                    speeds &= speedError < .03f;
                }
            Check(positions, label + ": position continuity at all joins including periodic seam", passed);
            Check(directions, label + ": tangent direction continuity at every join (minimum cosine " + minimumCosine.ToString("F5") + ")", passed);
            if (checkJoinSpeeds)
                Check(speeds, label + ": no material join-speed kink within arc-table tolerance (maximum error " + (maximumSpeedError * 100f).ToString("F2") + "%)", passed);

            float edge = .025f;
            Check(Near(surface.Wrap(-edge), length - edge, positionTolerance)
                && Near(surface.LoopDelta(length - edge, edge), edge * 2f, positionTolerance)
                && Near(surface.LoopDelta(edge, length - edge), -edge * 2f, positionTolerance),
                label + ": shortest periodic distance crosses the seam locally in both directions", passed);
            Vector3 front = surface.Evaluate(0, frontMiddle), rear = surface.Evaluate(0, rearMiddle);
            float projectedDistance = new Vector2(front.x - rear.x, front.z - rear.z).magnitude;
            float chartDistance = Mathf.Abs(surface.LoopDelta(frontMiddle, rearMiddle));
            Check(projectedDistance < positionTolerance && chartDistance > height * .5f
                && Near(chartDistance, length * .5f, positionTolerance),
                label + ": coincident front/rear projection stays separated in force-chart distance", passed);
            float a = .23f * length, b = .63f * length;
            Check(Near(surface.LoopDelta(a, b), -surface.LoopDelta(b, a), positionTolerance)
                && Near(surface.LoopDelta(a, b), surface.LoopDelta(a - length * 2f, b + length), positionTolerance)
                && Mathf.Abs(surface.LoopDelta(a, b)) <= length * .5f,
                label + ": periodic distance is signed, bounded and invariant to repeated wraps", passed);
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
        private static bool Near(float a, float b, float tolerance) { return Mathf.Abs(a - b) <= tolerance; }
        private static void Check(bool condition, string name, List<string> passed)
        { if (!condition) throw new InvalidOperationException("SINGULARITY surface check failed: " + name); passed.Add(name); }
    }
}
