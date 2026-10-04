using UnityEngine;

namespace Massive.Lattice
{
    public sealed partial class LatticeLevelIcon
    {
        bool[] inside;
        float[] depthSamples;
        float[] nodeNoise;
        Vector3[] contourBuckets;
        byte[] contourCounts;

        // A coherent volume, rather than six unrelated face textures or repeated slices.
        public float NoiseAt(Vector3 local, float time)
        {
            if (mode == LatticeDisruptionField.FieldMode.Connected) return 0;
            if (mode == LatticeDisruptionField.FieldMode.Disconnected) return 1;
            float t = time * Mathf.Max(0, evolutionSpeed);
            Vector3 p = (local + drift * time) * Mathf.Max(.03f, noiseFrequency);
            Vector3 warp = new Vector3(
                Noise(p * .63f + new Vector3(t, 8.7f, 0)),
                Noise(p * .63f + new Vector3(29.3f, -t, 0)),
                Noise(p * .63f + new Vector3(0, 19.1f, t * .7f))) - Vector3.one * .5f;
            p += warp * 1.1f;
            return .72f * Noise(p + new Vector3(.23f * Mathf.Sin(t), .31f * Mathf.Cos(t * .83f), 0))
                + .28f * Noise(p * 1.83f + new Vector3(t + 41, -t * .7f, t * .31f));
        }

        float Noise(Vector3 p)
        {
            // Differently oriented native Perlin projections combine into one
            // continuous volume; no axis is a repeated/extruded 2D pattern.
            float seed = (noiseSeed % 10007) * .173f;
            float a = Mathf.PerlinNoise(p.x + p.z * .57735f + seed, p.y - p.z * .57735f + seed * 1.83f);
            float b = Mathf.PerlinNoise(p.z - p.y * .57735f + seed + 31.7f, p.x + p.y * .57735f + seed * 1.37f);
            return Mathf.Clamp01(.5f + (a + b - 1) * .7f);
        }

        Boundary Connection(int ia, int ib)
        {
            if (mode == LatticeDisruptionField.FieldMode.Connected) return new Boundary { fromA = .5f, fromB = .5f, split = .5f };
            if (mode == LatticeDisruptionField.FieldMode.Disconnected) return new Boundary { cut = true, a = true, b = true, split = .5f };
            Vector3 a = positions[ia], b = positions[ib];
            const int samples = 2;
            float previous = nodeNoise[ia];
            var result = new Boundary { a = previous > 0 };
            float first = result.a ? 0 : 1, last = result.a ? 0 : -1;
            for (int j = 1; j <= samples; j++)
            {
                float t = j / (float)samples, value = j == samples ? nodeNoise[ib] : NoiseAt(Vector3.Lerp(a, b, t), clock) - threshold;
                if ((previous > 0) != (value > 0))
                {
                    float low = (j - 1f) / samples, high = t;
                    for (int k = 0; k < 6; k++)
                    {
                        float mid = (low + high) * .5f;
                        if ((NoiseAt(Vector3.Lerp(a, b, mid), clock) > threshold) == (previous > 0)) low = mid;
                        else high = mid;
                    }
                    float crossing = (low + high) * .5f;
                    first = Mathf.Min(first, crossing); last = crossing;
                }
                if (value > 0) { first = Mathf.Min(first, t); last = t; }
                previous = value;
            }
            result.b = previous > 0; result.cut = last >= 0;
            result.fromA = result.a ? 0 : first; result.fromB = result.b ? 0 : 1 - last;
            result.split = result.a ? 0 : result.b ? 1 : (first + last) * .5f;
            if (!result.cut) result.fromA = result.fromB = result.split = .5f;
            return result;
        }

        void RefreshDepth()
        {
            if (inside == null || inside.Length != nodes.Length) inside = new bool[nodes.Length];
            if (nodeNoise == null || nodeNoise.Length != nodes.Length) nodeNoise = new float[nodes.Length];
            // Cache geometric distances to contour crossings on a padded 3D sample
            // grid. Include the surrounding volume so the cube edge isn't a boundary.
            int padding = Mathf.CeilToInt(Mathf.Clamp(dotJitterDepth, .1f, 4)) + 1;
            int n = builtCells + 1 + padding * 2;
            if (depthSamples == null || depthSamples.Length != n * n * n)
            { depthSamples = new float[n * n * n]; contourBuckets = new Vector3[n * n * n * 3]; contourCounts = new byte[n * n * n]; }
            Vector3 origin = Vector3.one * (-builtCells * .5f - padding);
            System.Array.Clear(contourCounts, 0, contourCounts.Length);
            for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                depthSamples[x + n * (y + n * z)] = NoiseAt(origin + new Vector3(x, y, z), clock) - threshold;
            for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int i = x + n * (y + n * z); Vector3 p = origin + new Vector3(x, y, z);
                if (x + 1 < n) Crossing(i, p, Vector3.right, depthSamples[i], depthSamples[i + 1]);
                if (y + 1 < n) Crossing(i, p, Vector3.up, depthSamples[i], depthSamples[i + n]);
                if (z + 1 < n) Crossing(i, p, Vector3.forward, depthSamples[i], depthSamples[i + n * n]);
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                int width = builtCells + 1;
                int x = i % width + padding, y = (i / width) % width + padding, z = i / (width * width) + padding;
                nodeNoise[i] = depthSamples[x + n * (y + n * z)];
                inside[i] = nodeNoise[i] > 0;
                float distance = Mathf.Clamp(dotJitterDepth, .1f, 4); float squared = distance * distance;
                if (!inside[i]) { nodes[i].y = 0; continue; }
                // Bucket crossings by sample cell. Only nearby cells can beat
                // the bounded jitter depth, avoiding an all-pairs distance search.
                Vector3 p = positions[i]; int reach = padding;
                for (int bz = Mathf.Max(0, z - reach); bz <= Mathf.Min(n - 1, z + reach); bz++)
                for (int by = Mathf.Max(0, y - reach); by <= Mathf.Min(n - 1, y + reach); by++)
                for (int bx = Mathf.Max(0, x - reach); bx <= Mathf.Min(n - 1, x + reach); bx++)
                {
                    int bucket = bx + n * (by + n * bz), count = contourCounts[bucket];
                    for (int j = 0; j < count; j++)
                    {
                        Vector3 q = contourBuckets[bucket * 3 + j];
                        float dx = p.x - q.x, dy = p.y - q.y, dz = p.z - q.z;
                        float d = dx * dx + dy * dy + dz * dz; if (d < squared) squared = d;
                    }
                }
                nodes[i].y = Mathf.Sqrt(squared);
            }
            void Crossing(int bucket, Vector3 a, Vector3 direction, float va, float vb)
            {
                if ((va > 0) == (vb > 0)) return;
                contourBuckets[bucket * 3 + contourCounts[bucket]++] = a + direction * Mathf.Clamp01(va / (va - vb));
            }
        }
    }
}
