using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>The complete triangular band of J23's octagonal antiprism,
    /// plus a frequency-two icosahedral hemisphere behind the firing core.</summary>
    public static class ParticleBeamTurretGeometry
    {
        public const int BaseFaces = 16, RingFaces = 8, DomeFaces = 40, FaceCount = BaseFaces + RingFaces + DomeFaces;
        public const float DefaultBaseRadius = .68f, DefaultBaseHeight = .44774037f;
        private static readonly Vector3[] dome = BuildDome();
        public static float Height(float radius)
        {
            float edge = 2f * radius * Mathf.Sin(Mathf.PI / 8f);
            float diagonal = 2f * radius * Mathf.Sin(Mathf.PI / 16f);
            return Mathf.Sqrt(edge * edge - diagonal * diagonal);
        }
        public static Vector3 Octagon(float angle, float radius, float z) =>
            new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
        public static void BaseTriangle(int index, float radius, Vector3[] output, int offset)
            => BaseTriangle(index, radius, Height(radius), output, offset);
        public static void BaseTriangle(int index, float radius, float height, Vector3[] output, int offset)
        {
            float a = (index % 8) * Mathf.PI / 4f;
            if (index < 8)
            {
                output[offset] = Octagon(a, radius, 0f);
                output[offset + 1] = Octagon(a + Mathf.PI / 4f, radius, 0f);
                output[offset + 2] = Octagon(a + Mathf.PI / 8f, radius, height);
            }
            else
            {
                output[offset] = Octagon(a + Mathf.PI / 4f, radius, 0f);
                output[offset + 1] = Octagon(a + 3f * Mathf.PI / 8f, radius, height);
                output[offset + 2] = Octagon(a + Mathf.PI / 8f, radius, height);
            }
        }
        public static void DomeTriangle(int index, float radius, Vector3[] output, int offset)
        { for (int j = 0; j < 3; j++) output[offset + j] = dome[index * 3 + j] * radius; }
        public static void BuildOutline(Mesh mesh, Vector3[] corners)
        {
            // Four vertices per edge, matching the shared spawn warning shader and join weights.
            var vertices = new Vector3[corners.Length * 4];
            var tangents = new Vector4[vertices.Length]; var uv = new Vector2[vertices.Length];
            var indices = new int[corners.Length * 6];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 a = corners[i], b = corners[i / 3 * 3 + (i + 1) % 3], d = b - a;
                int n = i * 4, t = i * 6;
                vertices[n] = vertices[n + 1] = a; vertices[n + 2] = vertices[n + 3] = b;
                for (int j = 0; j < 4; j++) tangents[n + j] = new Vector4(d.x, d.y, d.z, 1f);
                uv[n] = new Vector2(0, -1); uv[n + 1] = new Vector2(0, 1);
                uv[n + 2] = new Vector2(1, -1); uv[n + 3] = new Vector2(1, 1);
                indices[t] = n; indices[t + 1] = n + 1; indices[t + 2] = n + 2;
                indices[t + 3] = n + 2; indices[t + 4] = n + 1; indices[t + 5] = n + 3;
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.tangents = tangents; mesh.uv = uv; mesh.triangles = indices;
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(.5f); mesh.bounds = bounds;
        }
        private static Vector3[] BuildDome()
        {
            var result = new System.Collections.Generic.List<Vector3>(DomeFaces * 3);
            float z = 1f / Mathf.Sqrt(5f), r = 2f / Mathf.Sqrt(5f);
            Vector3 Upper(int i) => Octagon(i * 2f * Mathf.PI / 5f, r, z);
            Vector3 Lower(int i) => Octagon((i + .5f) * 2f * Mathf.PI / 5f, r, -z);
            void Add(Vector3 a, Vector3 b, Vector3 c)
            {
                if (a.z < -.00001f || b.z < -.00001f || c.z < -.00001f) return;
                // Fold the upper hemisphere behind the core; the rim stays on local Z=0.
                a.z = -Mathf.Max(0f, a.z); b.z = -Mathf.Max(0f, b.z); c.z = -Mathf.Max(0f, c.z);
                result.Add(a); result.Add(b); result.Add(c);
            }
            void Subdivide(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                Add(a, ab, ca); Add(ab, b, bc); Add(ca, bc, c); Add(ab, bc, ca);
            }
            for (int i = 0; i < 5; i++)
            {
                Subdivide(Vector3.forward, Upper(i), Upper(i + 1));
                Subdivide(Upper(i), Lower(i), Upper(i + 1));
                Subdivide(Upper(i), Lower(i - 1), Lower(i));
                Subdivide(Vector3.back, Lower(i + 1), Lower(i));
            }
            return result.ToArray();
        }
    }
}
