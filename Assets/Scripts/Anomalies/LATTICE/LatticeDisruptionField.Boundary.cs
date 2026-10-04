using UnityEngine;

namespace Massive.Lattice
{
    public sealed partial class LatticeDisruptionField
    {
        public struct ConnectionBoundary
        {
            public bool cut, disruptedA, disruptedB;
            // Fractions of the complete edge, measured inward from each intact end.
            public float fromA, fromB, split;
        }

        public ConnectionBoundary ConnectionAt(Vector2 a, Vector2 b, float time, float thresholdOffset = 0)
        {
            float level = threshold + thresholdOffset;
            if (mode == FieldMode.Connected) return new ConnectionBoundary { fromA = .5f, fromB = .5f, split = .5f };
            if (mode == FieldMode.Disconnected) return new ConnectionBoundary { cut = true, disruptedA = true, disruptedB = true, split = .5f };
            const int samples = 8;
            float previous = NoiseAt(a, time) - level;
            var result = new ConnectionBoundary { disruptedA = previous > 0, fromA = 1, fromB = 1 };
            float first = result.disruptedA ? 0 : 1, last = result.disruptedA ? 0 : -1;
            for (int j = 1; j <= samples; j++)
            {
                float t = j / (float)samples;
                float value = NoiseAt(Vector2.Lerp(a, b, t), time) - level;
                if ((previous > 0) != (value > 0))
                {
                    float crossing = Crossing(a, b, (j - 1f) / samples, t, previous > 0, time, level);
                    first = Mathf.Min(first, crossing); last = crossing;
                }
                if (value > 0) { first = Mathf.Min(first, t); last = t; }
                previous = value;
            }
            result.disruptedB = previous > 0;
            result.cut = last >= 0;
            result.fromA = result.disruptedA ? 0 : first;
            result.fromB = result.disruptedB ? 0 : 1 - last;
            result.split = result.disruptedA ? 0 : result.disruptedB ? 1 : (first + last) * .5f;
            if (!result.cut) { result.fromA = result.fromB = result.split = .5f; }
            return result;
        }

        float Crossing(Vector2 a, Vector2 b, float low, float high, bool lowInside, float time, float level)
        {
            for (int i = 0; i < 9; i++)
            {
                float mid = (low + high) * .5f;
                if ((NoiseAt(Vector2.Lerp(a, b, mid), time) > level) == lowInside) low = mid;
                else high = mid;
            }
            return (low + high) * .5f;
        }

        public void GetConnection(int index, out Vector2 a, out Vector2 b, out Vector2 strandLengths)
        {
            Edge edge = edges[index]; a = edge.a; b = edge.b;
            strandLengths = new Vector2(states[index * 2].x, states[index * 2 + 1].x);
        }

        // Shared sampled contour: the dots measure geometric distance to these
        // segments, and the optional editor guide displays the same boundary.
        readonly System.Collections.Generic.List<Vector3> boundaryLines = new();
        readonly Vector2[] contourCorners = new Vector2[4], contourCuts = new Vector2[4];
        readonly float[] contourValues = new float[4];
        float boundaryClock = float.NaN;
        int boundarySettings;
        float[] contourSamples;
        float[] dotDepths;
        int contourRevision, dotDepthRevision = -1;
#if UNITY_EDITOR
        Mesh boundaryMesh;
        Material boundaryMaterial;
        readonly System.Collections.Generic.List<Vector3> boundaryDashes = new();
        readonly System.Collections.Generic.List<int> boundaryIndices = new();
        int guideRevision = -1;

        void DrawEditorBoundary()
        {
            if (!showNoiseBoundary || !isActiveAndEnabled || !Grid || mode != FieldMode.DriftingNoise) return;
            if (!boundaryMaterial)
            {
                var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Anomalies/LATTICE/Editor/LatticeBoundaryGuide.shader");
                if (!shader || !shader.isSupported) return;
                boundaryMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = "LATTICE editor contour" };
            }
            if (!boundaryMesh)
            { boundaryMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "LATTICE editor contour" }; guideRevision = -1; }
            RefreshBoundary();
            boundaryMaterial.SetVector("_LatticeGuideSize", Grid.size);
            Graphics.DrawMesh(boundaryMesh, transform.localToWorldMatrix, boundaryMaterial, gameObject.layer,
                null, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false, null, UnityEngine.Rendering.LightProbeUsage.Off);
        }

        void RefreshBoundary()
        {
            RefreshContour();
            if (guideRevision == contourRevision) return;
            guideRevision = contourRevision;
            boundaryDashes.Clear(); boundaryIndices.Clear();
            for (int i = 0; i < boundaryLines.Count; i += 2)
            {
                Vector3 a = boundaryLines[i], b = boundaryLines[i + 1];
                boundaryDashes.Add(Vector3.Lerp(a, b, .16f)); boundaryDashes.Add(Vector3.Lerp(a, b, .78f));
                boundaryIndices.Add(i); boundaryIndices.Add(i + 1);
            }
            boundaryMesh.Clear(); boundaryMesh.SetVertices(boundaryDashes);
            boundaryMesh.SetIndices(boundaryIndices, MeshTopology.Lines, 0);
        }
#endif

        void RefreshContour()
        {
            float padding = rgbDotJitter ? Mathf.Max(.1f, dotJitterDepth) * Spacing : 0;
            float resolution = Mathf.Clamp(Mathf.Min(Spacing * .5f, .2f / noiseFrequency), .1f, .5f);
#if UNITY_EDITOR
            if (showNoiseBoundary) resolution = Mathf.Min(resolution, .2f);
#endif
            int settings = System.HashCode.Combine(Grid.size, noiseFrequency, drift, evolutionSpeed, seed, threshold, mode, padding);
            settings = System.HashCode.Combine(settings, resolution, Spacing);
            // The contour evolves slowly. Cache it at 20 Hz, while RGB jitter and
            // thread motion continue every frame. Settings/time resets update immediately.
            if (settings == boundarySettings && !float.IsNaN(boundaryClock) &&
                (clock == boundaryClock || clock > boundaryClock &&
                    (clock - boundaryClock < .05f || drift == Vector2.zero && evolutionSpeed == 0))) return;
            boundaryClock = clock; boundarySettings = settings; boundaryLines.Clear();
            contourRevision++;
            if (mode != FieldMode.DriftingNoise) return;
            // Include the area outside the arena so an arena edge is never mistaken
            // for the noise boundary. Distances saturate at the requested jitter depth.
            Vector2 size = Grid.size + Vector2.one * (padding * 2);
            int width = Mathf.Clamp(Mathf.CeilToInt(size.x / resolution), 2, 256);
            int height = Mathf.Clamp(Mathf.CeilToInt(size.y / resolution), 2, 256);
            int count = (width + 1) * (height + 1);
            if (contourSamples == null || contourSamples.Length != count) contourSamples = new float[count];
            Vector2 min = -size * .5f, step = new Vector2(size.x / width, size.y / height);
            for (int y = 0; y <= height; y++) for (int x = 0; x <= width; x++)
                contourSamples[x + y * (width + 1)] = NoiseAt(min + new Vector2(x * step.x, y * step.y), clock) - threshold;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                Vector2 p = min + new Vector2(x * step.x, y * step.y);
                contourCorners[0] = p; contourCorners[1] = p + Vector2.right * step.x;
                contourCorners[2] = p + step; contourCorners[3] = p + Vector2.up * step.y;
                int index = x + y * (width + 1);
                contourValues[0] = contourSamples[index]; contourValues[1] = contourSamples[index + 1];
                contourValues[2] = contourSamples[index + width + 2]; contourValues[3] = contourSamples[index + width + 1];
                int cuts = 0;
                for (int side = 0; side < 4; side++)
                {
                    int next = (side + 1) % 4;
                    if ((contourValues[side] > 0) == (contourValues[next] > 0)) continue;
                    float t = Crossing(contourCorners[side], contourCorners[next], 0, 1, contourValues[side] > 0, clock, threshold);
                    contourCuts[cuts++] = Vector2.Lerp(contourCorners[side], contourCorners[next], t);
                }
                if (cuts == 2) AddContour(0, 1);
                else if (cuts == 4)
                {
                    if ((contourValues[0] > 0) == (NoiseAt(p + step * .5f, clock) > threshold))
                    { AddContour(0, 1); AddContour(2, 3); }
                    else { AddContour(3, 0); AddContour(1, 2); }
                }
            }
        }

        void RefreshDotDepths()
        {
            RefreshContour();
            if (dotDepths != null && dotDepths.Length == nodes.Length && dotDepthRevision == contourRevision) return;
            if (dotDepths == null || dotDepths.Length != nodes.Length) dotDepths = new float[nodes.Length];
            dotDepthRevision = contourRevision;
            float maxDistance = Mathf.Max(.1f, dotJitterDepth) * Spacing;
            for (int i = 0; i < nodes.Length; i++)
            {
                Vector2 p = origin + new Vector2(i % nx, i / nx) * Spacing;
                dotDepths[i] = 0;
                if (mode == FieldMode.Connected || mode == FieldMode.DriftingNoise && NoiseAt(p, clock) <= threshold) continue;
                float squared = maxDistance * maxDistance;
                for (int j = 0; j < boundaryLines.Count; j += 2)
                {
                    Vector2 a = boundaryLines[j], span = boundaryLines[j + 1] - boundaryLines[j];
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, span) / Mathf.Max(.000001f, span.sqrMagnitude));
                    squared = Mathf.Min(squared, (p - a - span * t).sqrMagnitude);
                }
                dotDepths[i] = Mathf.Sqrt(squared) / Spacing;
            }
        }
        void AddContour(int a, int b)
        {
            boundaryLines.Add(new Vector3(contourCuts[a].x, contourCuts[a].y, -.02f));
            boundaryLines.Add(new Vector3(contourCuts[b].x, contourCuts[b].y, -.02f));
        }
    }
}
