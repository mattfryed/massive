using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Singularity
{
    /// <summary>Presentation-only folded lattice. No gameplay grid, fields,
    /// colliders, scene searches or simulation. Derived mesh is never serialized.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SingularityLevelIcon : MonoBehaviour
    {
        [Header("Miniature folded space")]
        [Range(2f, 8f)] public float flatWidth = 3.4f;
        [Range(1f, 6f)] public float depth = 2.7f;
        [Range(.6f, 3f)] public float layerSeparation = 1.5f;
        [Range(.1f, 1.5f)] public float foldReach = .75f;
        [Header("Central gravity well")]
        [Range(.1f, 2f)] public float wellRadius = 1.15f;
        [Range(0f, .98f)] public float wellDepth = .92f;
        [Range(0f, .8f)] public float radialPull = .18f;
        [Header("Wire lattice")]
        [Range(6, 24)] public int widthCells = 12;
        [Range(4, 20)] public int depthCells = 8;
        [Range(.003f, .025f)] public float wireRadius = .0065f;
        public Color upperColor = new Color(.92f, .96f, 1f, 1f);
        public Color lowerColor = new Color(.38f, .47f, .58f, 1f);
        // Retain old serialized values for compatibility. Accretion now belongs
        // to the authored particle children, never to the generated lattice.
        [HideInInspector] public float diskInnerRadius = .40f;
        [HideInInspector] public float diskOuterRadius = .94f;
        [HideInInspector] public Vector3 diskTilt = new Vector3(0f, 0f, -12f);

        private Mesh ownedMesh, previousMesh;
        private MeshFilter meshFilter;
        private bool dirty = true;
        public int VertexCount => ownedMesh != null ? ownedMesh.vertexCount : 0;

        private void OnEnable() { dirty = true; }
        private void OnValidate() { dirty = true; }
        private void LateUpdate() { if (dirty) Rebuild(); }
        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }

        public void Rebuild()
        {
            Release();
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            meshFilter = GetComponent<MeshFilter>();
            previousMesh = meshFilter.sharedMesh;
            var vertices = new List<Vector3>(32000);
            var colors = new List<Color>(32000);
            var uv = new List<Vector2>(32000);
            var wires = new List<int>(160000);
            var path = new List<Vector3>(256);
            var shades = new List<Color>(256);
            int across = Mathf.Clamp(widthCells, 6, 24), rows = Mathf.Clamp(depthCells, 4, 20);
            const int faceSteps = 80, turnSteps = 40;
            float width = Safe(flatWidth, 2f, 8f), halfHeight = Safe(layerSeparation, .6f, 3f) * .5f;
            float halfDepth = Safe(depth, 1f, 6f) * .5f, reach = Safe(foldReach, .1f, 1.5f);

            // Each longitudinal line is one closed loop: upper face -> right
            // fold -> lower face -> left fold. Joins share their exact endpoints.
            for (int row = 0; row <= rows; row++)
            {
                float z = Mathf.Lerp(-halfDepth, halfDepth, row / (float)rows);
                path.Clear(); shades.Clear();
                for (int i = 0; i < faceSteps; i++)
                    AddPath(EvaluateFace(Mathf.Lerp(-width * .5f, width * .5f, i / (float)faceSteps), z, true), upperColor);
                for (int i = 0; i < turnSteps; i++)
                {
                    float t = i / (float)turnSteps, angle = t * Mathf.PI;
                    AddPath(new Vector3(width * .5f + reach * Mathf.Sin(angle), halfHeight * Mathf.Cos(angle), z), Color.Lerp(upperColor, lowerColor, t));
                }
                for (int i = 0; i < faceSteps; i++)
                    AddPath(EvaluateFace(Mathf.Lerp(width * .5f, -width * .5f, i / (float)faceSteps), z, false), lowerColor);
                for (int i = 0; i <= turnSteps; i++)
                {
                    float t = i / (float)turnSteps, angle = t * Mathf.PI;
                    AddPath(new Vector3(-width * .5f - reach * Mathf.Sin(angle), -halfHeight * Mathf.Cos(angle), z), Color.Lerp(lowerColor, upperColor, t));
                }
                AddTube(path, shades, true, vertices, colors, uv, wires);
            }
            // Cross-bars follow both wells; sampling is independent of cell count.
            for (int face = 0; face < 2; face++)
                for (int col = 0; col <= across; col++)
                {
                    float x = Mathf.Lerp(-width * .5f, width * .5f, col / (float)across);
                    path.Clear(); shades.Clear();
                    for (int i = 0; i <= 64; i++)
                        AddPath(EvaluateFace(x, Mathf.Lerp(-halfDepth, halfDepth, i / 64f), face == 0), face == 0 ? upperColor : lowerColor);
                    AddTube(path, shades, false, vertices, colors, uv, wires);
                }
            for (int side = -1; side <= 1; side += 2)
                for (int row = 1; row < 8; row++)
                {
                    float t = row / 8f, angle = t * Mathf.PI;
                    path.Clear(); shades.Clear();
                    for (int i = 0; i <= 16; i++)
                        AddPath(new Vector3(side * (width * .5f + reach * Mathf.Sin(angle)), halfHeight * Mathf.Cos(angle), Mathf.Lerp(-halfDepth, halfDepth, i / 16f)), Color.Lerp(upperColor, lowerColor, t));
                    AddTube(path, shades, false, vertices, colors, uv, wires);
                }

            ownedMesh = new Mesh { name = "SINGULARITY icon lattice (derived)", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            ownedMesh.SetVertices(vertices); ownedMesh.SetColors(colors); ownedMesh.SetUVs(0, uv);
            ownedMesh.SetTriangles(wires, 0); ownedMesh.RecalculateBounds();
            meshFilter.sharedMesh = ownedMesh;
            dirty = false;

            void AddPath(Vector3 point, Color color) { path.Add(point); shades.Add(color); }
        }

        /// <summary>Symmetric opposing wells meet inside the black event horizon.
        /// Feathering reaches zero with zero slope at all four authored edges.</summary>
        public Vector3 EvaluateFace(float x, float z, bool upper)
        {
            float w = Safe(flatWidth, 2f, 8f), d = Safe(depth, 1f, 6f), radius = Safe(wellRadius, .1f, 2f);
            float edgeX = Mathf.Clamp01(1f - Mathf.Abs(x) / (w * .5f));
            float edgeZ = Mathf.Clamp01(1f - Mathf.Abs(z) / (d * .5f));
            float feather = Mathf.SmoothStep(0, 1, edgeX * 3f) * Mathf.SmoothStep(0, 1, edgeZ * 3f);
            float bell = Mathf.Exp(-(x * x + z * z) / (radius * radius)) * feather;
            float pull = 1f - Safe(radialPull, 0, .8f) * bell;
            float y = Safe(layerSeparation, .6f, 3f) * .5f * (1f - Safe(wellDepth, 0, .98f) * bell);
            return new Vector3(x * pull, upper ? y : -y, z * pull);
        }

        private void AddTube(List<Vector3> path, List<Color> shades, bool closed,
            List<Vector3> vertices, List<Color> colors, List<Vector2> uv, List<int> indices)
        {
            const int sides = 6;
            int start = vertices.Count, last = path.Count - 1;
            float radius = Safe(wireRadius, .003f, .025f);
            Vector3 previousNormal = Vector3.zero;
            for (int i = 0; i <= last; i++)
            {
                Vector3 before = path[i > 0 ? i - 1 : closed ? last - 1 : 0];
                Vector3 after = path[i < last ? i + 1 : closed ? 1 : last];
                Vector3 tangent = (after - before).normalized;
                Vector3 reference = Mathf.Abs(tangent.z) > .9f ? Vector3.up : Vector3.forward;
                Vector3 n = previousNormal - tangent * Vector3.Dot(previousNormal, tangent);
                if (n.sqrMagnitude < .00001f) n = Vector3.Cross(tangent, reference);
                n.Normalize();
                Vector3 b = Vector3.Cross(tangent, n);
                previousNormal = n;
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    vertices.Add(path[i] + radius * (n * Mathf.Cos(angle) + b * Mathf.Sin(angle)));
                    colors.Add(shades[i]); uv.Add(Vector2.zero);
                    if (i > 0)
                    {
                        int prev = start + (i - 1) * sides, current = start + i * sides, next = (side + 1) % sides;
                        Quad(prev + side, prev + next, current + side, current + next, indices);
                    }
                }
            }
        }
        private static void Quad(int a, int b, int c, int d, List<int> indices)
        { indices.Add(a); indices.Add(c); indices.Add(b); indices.Add(b); indices.Add(c); indices.Add(d); }
        private static float Safe(float value, float min, float max)
        { return float.IsNaN(value) || float.IsInfinity(value) ? min : Mathf.Clamp(value, min, max); }
        private void Release()
        {
            if (ownedMesh == null) return;
            if (meshFilter != null && meshFilter.sharedMesh == ownedMesh) meshFilter.sharedMesh = previousMesh;
            if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
            ownedMesh = null; previousMesh = null; dirty = true;
        }
    }
}
