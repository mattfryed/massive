using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Cosmos
{
    /// <summary>Seeded filament morphology; an illustrative matter model, not a galaxy catalogue.</summary>
    public static class CosmicWebTopology
    {
        // Kept in sync with CosmicWebMotion.hlsl. The persistent GPU buffer needs no frame uploads.
        public struct Particle { public Vector4 initial, filament, cluster, flow, bend; }
        public struct Statistics { public int filaments, boundaryEnds, reconnectedEnds; public float meanBend; }
        sealed class Edge
        {
            public Vector3 a, b;
            public bool shell;
            public Vector3[][] routes;
        }
        const int Samples = 32;
        public static readonly Vector3 ModelHalfSize = new Vector3(18, 6.5f, 4);

        public static Particle[] Build(int count, int seed, out Statistics statistics, float organicStrength = 1)
        {
            var random = new System.Random(seed);
            var offset = new Vector3(Range(random, 10, 90), Range(random, 10, 90), Range(random, 10, 90));
            // Spatially varying site density produces a range of void sizes, without a seed lattice.
            var sites = new List<Vector3>();
            for (int attempts = 0; sites.Count < 190 && attempts < 10000; attempts++)
            {
                var p = new Vector3(Range(random, -21, 21), Range(random, -9, 9), Range(random, -7, 7));
                if (random.NextDouble() > .62f + .34f * Noise(p * .19f + offset)) continue;
                bool crowded = false;
                foreach (var other in sites) if ((other - p).sqrMagnitude < 3.2f) { crowded = true; break; }
                if (!crowded) sites.Add(p);
            }
            var edges = Edges(sites.ToArray(), out var ends);
            statistics = new Statistics { boundaryEnds = ends.Count };
            // Reconnect clipped ends through the enclosing ellipsoid. Projected near/far surface
            // paths turn back into the web instead of terminating at the visible oval mask.
            while (ends.Count > 1)
            {
                Vector3 a = ends[0]; ends.RemoveAt(0);
                int nearest = 0; float best = float.PositiveInfinity;
                for (int i = 0; i < ends.Count; i++)
                {
                    float distance = (Normalize(a) - Normalize(ends[i])).sqrMagnitude;
                    if (distance < best) { best = distance; nearest = i; }
                }
                edges.Add(new Edge { a = a, b = ends[nearest], shell = true });
                ends.RemoveAt(nearest); statistics.reconnectedEnds += 2;
            }
            if (ends.Count == 1)
            {
                Vector3 a = ends[0], b = edges[0].a; float best = float.PositiveInfinity;
                foreach (var edge in edges)
                    foreach (var candidate in new[] { edge.a, edge.b })
                        if ((candidate - a).sqrMagnitude > .001f && (candidate - a).sqrMagnitude < best)
                        { best = (candidate - a).sqrMagnitude; b = candidate; }
                edges.Add(new Edge { a = a, b = b, shell = true }); statistics.reconnectedEnds++;
            }
            if (edges.Count == 0) throw new InvalidOperationException("Cosmic web topology has no filaments.");
            float totalBend = 0;
            foreach (var edge in edges)
            {
                edge.routes = new Vector3[4][];
                Vector3 direction = (edge.b - edge.a).normalized;
                Vector3 tangent = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right).normalized;
                Vector3 bitangent = Vector3.Cross(direction, tangent);
                float fork = Range(random, .25f, .75f);
                for (int route = 0; route < 4; route++)
                {
                    var curve = edge.routes[route] = new Vector3[Samples + 1];
                    float angle = Range(random, 0, Mathf.PI * 2), width = Range(random, .22f, .95f);
                    for (int sample = 0; sample <= Samples; sample++)
                    {
                        float t = (float)sample / Samples;
                        Vector3 p = edge.shell ? Denormalize(Vector3.Slerp(Normalize(edge.a), Normalize(edge.b), t)) : Vector3.Lerp(edge.a, edge.b, t);
                        // Fine branches leave and rejoin their parent filament at shared endpoints.
                        float envelope = Mathf.Sin(Mathf.PI * t);
                        if (route > 1) envelope *= Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / fork));
                        if (route > 0) p += (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * envelope * width * organicStrength;
                        curve[sample] = Warp(p, offset, organicStrength);
                    }
                }
                totalBend += Vector3.Distance(edge.routes[0][Samples / 2], (edge.routes[0][0] + edge.routes[0][Samples]) * .5f);
            }
            statistics.filaments = edges.Count; statistics.meanBend = totalBend / edges.Count;
            var particles = new Particle[count];
            for (int i = 0; i < count; i++)
            {
                float angle = Range(random, 0, Mathf.PI * 2), radius = Mathf.Sqrt(Range(random, 0, .999f));
                Vector3 source = new Vector3(Mathf.Cos(angle) * radius * 18, Mathf.Sin(angle) * radius * 6.5f,
                    Range(random, -4, 4) * Mathf.Sqrt(1 - radius * radius));
                float best = float.PositiveInfinity, along = 0; Edge edge = null;
                foreach (var candidate in edges)
                {
                    Vector3 ab = candidate.b - candidate.a;
                    float t = Mathf.Clamp01(Vector3.Dot(source - candidate.a, ab) / Mathf.Max(.00001f, ab.sqrMagnitude));
                    float distance = (source - Vector3.Lerp(candidate.a, candidate.b, t)).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance; edge = candidate; along = t;
                }
                int route = random.NextDouble() < .52 ? 0 : random.Next(1, 4);
                Vector3 projected = Sample(edge, along, route);
                float end = along < .5f ? 0 : 1;
                Vector3 knot = Sample(edge, end, 0), scatter = Gaussian(random);
                float population = Range(random, 0, 1);
                float nearKnot = Mathf.Exp(-Vector3.Distance(projected, knot) * 1.4f);
                float width = Mathf.Lerp(.023f, .1f, Noise(projected * 1.2f + offset) * .5f + .5f);
                Vector3 mid = projected + scatter * width * (route == 0 ? 1 : .42f);
                if (population < .13f) { mid = knot + scatter * .105f; nearKnot = 1; }
                Vector3 late = knot + scatter * Range(random, .018f, .055f);
                float lateKnot = 1;
                if (population > .98f) { late = projected + scatter * .022f; lateKnot = nearKnot; }
                Vector3 halfPath = Sample(edge, (along + end) * .5f, route);
                Vector3 bend = population < .13f || population > .98f ? Vector3.zero : halfPath - (mid + late) * .5f;
                Vector3 flow = VectorNoise(source * .35f + offset) * .9f;
                float formationBias = Noise(source * .28f + offset) * 1.6f + Range(random, -.18f, .18f);
                float evacuationBias = Noise(projected * .47f + offset + Vector3.one * 19) * 1.8f + Range(random, -.12f, .12f);
                // Inverse of the default lens gives an approximately uniform initial projected disk.
                float rawRadius = radius / Mathf.Pow(Mathf.Max(.00001f, 1 - Mathf.Pow(radius, 10)), .1f);
                var initial = new Vector4(Mathf.Cos(angle) * rawRadius, Mathf.Sin(angle) * rawRadius, source.z / 4, Range(random, .25f, 1));
                particles[i] = new Particle { initial = initial, filament = Pack(mid * 1.15f, nearKnot),
                    cluster = Pack(late * 1.15f, lateKnot), flow = Pack(flow, formationBias), bend = Pack(bend * 1.15f, evacuationBias) };
            }
            return particles;
        }

        static Vector3 Sample(Edge edge, float t, int route)
        {
            float f = Mathf.Clamp01(t) * Samples; int index = Mathf.Min(Samples - 1, (int)f);
            return Vector3.Lerp(edge.routes[route][index], edge.routes[route][index + 1], f - index);
        }
        static List<Edge> Edges(Vector3[] sites, out List<Vector3> ends)
        {
            var edges = new List<Edge>(512); ends = new List<Vector3>();
            for (int i = 0; i < sites.Length - 2; i++)
                for (int j = i + 1; j < sites.Length - 1; j++)
                    for (int k = j + 1; k < sites.Length; k++)
                    {
                        Vector3 a = sites[j] - sites[i], b = sites[k] - sites[i];
                        float aa = a.sqrMagnitude, bb = b.sqrMagnitude, ab = Vector3.Dot(a, b);
                        float determinant = aa * bb - ab * ab;
                        if (determinant < .0001f) continue;
                        float da = (sites[j].sqrMagnitude - sites[i].sqrMagnitude) * .5f;
                        float db = (sites[k].sqrMagnitude - sites[i].sqrMagnitude) * .5f;
                        Vector3 origin = (a * (da * bb - db * ab) + b * (db * aa - da * ab)) / determinant;
                        Vector3 direction = Vector3.Cross(a, b).normalized;
                        float min = -100, max = 100; bool valid = true;
                        for (int s = 0; s < sites.Length && valid; s++)
                        {
                            if (s == i || s == j || s == k) continue;
                            Vector3 normal = sites[s] - sites[i];
                            valid = Clip(Vector3.Dot(origin, normal), Vector3.Dot(direction, normal),
                                (sites[s].sqrMagnitude - sites[i].sqrMagnitude) * .5f, ref min, ref max);
                        }
                        if (!valid) continue;
                        Vector3 o = Normalize(origin), d = Normalize(direction);
                        float od = Vector3.Dot(o, d), dd = d.sqrMagnitude;
                        float discriminant = od * od - dd * (o.sqrMagnitude - 1);
                        if (discriminant <= 0) continue;
                        float low = (-od - Mathf.Sqrt(discriminant)) / dd, high = (-od + Mathf.Sqrt(discriminant)) / dd;
                        bool boundaryA = min < low, boundaryB = max > high;
                        min = Mathf.Max(min, low); max = Mathf.Min(max, high);
                        if (max - min <= .04f) continue;
                        var edge = new Edge { a = origin + direction * min, b = origin + direction * max };
                        edges.Add(edge);
                        if (boundaryA) ends.Add(edge.a);
                        if (boundaryB) ends.Add(edge.b);
                    }
            return edges;
        }
        static bool Clip(float origin, float direction, float limit, ref float min, ref float max)
        {
            if (Mathf.Abs(direction) < .00001f) return origin <= limit + .0001f;
            float t = (limit - origin) / direction;
            if (direction > 0) max = Mathf.Min(max, t); else min = Mathf.Max(min, t);
            return min <= max;
        }
        static Vector3 Warp(Vector3 p, Vector3 offset, float amount) => p + amount *
            (VectorNoise(p * .34f + offset) * 1.65f + VectorNoise(p * .83f + offset * 2) * .48f + VectorNoise(p * 2.2f + offset * 3) * .13f);
        static Vector3 VectorNoise(Vector3 p) => new Vector3(Noise(p), Noise(p + Vector3.one * 31.7f), Noise(p + Vector3.one * 67.3f));
        static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
            float u = Fade(p.x - x), v = Fade(p.y - y), w = Fade(p.z - z);
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Hash(x,y,z), Hash(x+1,y,z),u), Mathf.Lerp(Hash(x,y+1,z), Hash(x+1,y+1,z),u),v),
                Mathf.Lerp(Mathf.Lerp(Hash(x,y,z+1), Hash(x+1,y,z+1),u), Mathf.Lerp(Hash(x,y+1,z+1), Hash(x+1,y+1,z+1),u),v),w);
        }
        static float Hash(int x, int y, int z)
        {
            unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + z * 1442695041); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffffff) / 8388607.5f - 1; }
        }
        static Vector3 Normalize(Vector3 p) => new Vector3(p.x / 18, p.y / 6.5f, p.z / 4);
        static Vector3 Denormalize(Vector3 p) => Vector3.Scale(p, ModelHalfSize);
        static Vector4 Pack(Vector3 p, float w) { var n = Normalize(p); return new Vector4(n.x, n.y, n.z, w); }
        static float Range(System.Random r, float a, float b) => Mathf.Lerp(a, b, (float)r.NextDouble());
        static Vector3 Gaussian(System.Random r)
        {
            float Normal() => Mathf.Sqrt(-2 * Mathf.Log(Mathf.Max(.00001f, (float)r.NextDouble()))) * Mathf.Cos(2 * Mathf.PI * (float)r.NextDouble());
            return new Vector3(Normal(), Normal(), Normal());
        }
        // CPU references for validation; production runs the matching functions in CosmicWebMotion.hlsl.
        public static float Phase(float t, float bias, float variation) => Mathf.SmoothStep(0, 1,
            Mathf.Pow(Mathf.Clamp01(t), Mathf.Pow(2, bias * variation)));
        public static Vector3 Position(Particle p, float age, float variation = 1)
        {
            float f = Phase(age * 2, p.flow.w, variation), e = Phase(age * 2 - 1, p.bend.w, variation);
            Vector3 infall = Vector3.Lerp(p.initial, p.filament, f) + (Vector3)p.flow * (Mathf.Sin(Mathf.PI * f) * variation);
            return Vector3.Lerp(infall, p.cluster, e) + (Vector3)p.bend * (4 * e * (1 - e));
        }
        public static Vector2 Project(Vector3 p, Vector4 oval, Vector2 scale, float padding, float condensation)
        {
            Vector2 q = new Vector2(p.x, p.y); float r = q.magnitude;
            Vector2 ab = Vector2.Scale(new Vector2(oval.z, oval.y), scale);
            float power = Mathf.Lerp(16, 6, Mathf.Clamp01(condensation));
            float lens = r / Mathf.Pow(1 + Mathf.Pow(r, power), 1 / power);
            Vector2 direction = q / Mathf.Max(.00001f, r);
            Vector2 boundary = Vector2.Scale(direction, ab);
            Vector2 normal = new Vector2(direction.x / ab.x, direction.y / ab.y).normalized;
            Vector2 result = (boundary - normal * padding) * lens;
            float cutoff = Mathf.Max(.01f, oval.x * scale.x - padding);
            float start = Mathf.Max(0, cutoff - Mathf.Lerp(1.4f, 4.5f, condensation));
            if (Mathf.Abs(result.x) > start)
            {
                float span = Mathf.Max(.01f, ab.x - padding - start), room = Mathf.Max(.01f, cutoff - start);
                float t = Mathf.Clamp01((Mathf.Abs(result.x) - start) / span);
                result.x = Mathf.Sign(result.x) * (start + room * (1 - Mathf.Pow(1 - t, span / room)));
            }
            return new Vector2(result.x / scale.x, result.y / scale.y);
        }
    }
}
