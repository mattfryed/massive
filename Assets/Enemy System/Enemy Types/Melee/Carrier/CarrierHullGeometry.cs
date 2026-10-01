using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Vertex-truncated rhombicuboctahedron, with a threefold axis upright for six planar octagonal docks.</summary>
    public static class CarrierHullGeometry
    {
        public const float Radius = .9f;
        public const int FaceCount = 50, MaxFaceCorners = 8, OpenTriangleCount = 184;
        public const float Truncation = .27f;
        private static readonly float A = 1f + Mathf.Sqrt(2f);
        private static readonly float VertexRadius = Mathf.Sqrt(A * A + 2f);
        private static readonly Quaternion Orientation = GetOrientation();

        private static Quaternion GetOrientation()
        {
            var upright = Quaternion.FromToRotation(Vector3.one.normalized, Vector3.up);
            var first = upright * new Vector3(1f, -1f, 0f).normalized;
            return Quaternion.AngleAxis(-Mathf.Atan2(first.x, first.z) * Mathf.Rad2Deg, Vector3.up) * upright;
        }

        public static void GetDockPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(0f, slot * 60f, 0f);
            // Truncation preserves the planes and centers of the six original docking faces.
            float distance = (A + 1f) / Mathf.Sqrt(2f) * Radius / VertexRadius;
            position = rotation * Vector3.forward * distance;
        }

        public static void Create(out Vector3[] vertices, out int[][] faces)
        {
            var raw = new Vector3[24];
            int index = 0;
            for (int axis = 0; axis < 3; axis++)
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var p = new Vector3(x, y, z); p[axis] *= A;
                            raw[index++] = p;
                        }

            var polygons = new List<int[]>(26);
            // Six axial squares, twelve diagonal squares and eight equilateral triangles.
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var n = Vector3.zero; n[axis] = sign;
                    polygons.Add(Face(raw, n, A));
                }
            for (int zeroAxis = 0; zeroAxis < 3; zeroAxis++)
                for (int s = -1; s <= 1; s += 2)
                    for (int t = -1; t <= 1; t += 2)
                    {
                        var n = Vector3.zero; n[(zeroAxis + 1) % 3] = s; n[(zeroAxis + 2) % 3] = t;
                        polygons.Add(Face(raw, n, A + 1f));
                    }
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        polygons.Add(Face(raw, new Vector3(x, y, z), A + 2f));

            var cut = new List<Vector3>(96);
            var directedEdges = new Dictionary<Vector2Int, int>(96);
            int CutVertex(int a, int b)
            {
                var key = new Vector2Int(a, b);
                if (directedEdges.TryGetValue(key, out int existing)) return existing;
                int id = cut.Count; cut.Add(Vector3.Lerp(raw[a], raw[b], Truncation));
                directedEdges.Add(key, id); return id;
            }
            var truncatedFaces = new List<int[]>(FaceCount);
            foreach (var polygon in polygons)
            {
                var expanded = new int[polygon.Length * 2];
                for (int i = 0; i < polygon.Length; i++)
                {
                    int a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                    expanded[i * 2] = CutVertex(a, b); expanded[i * 2 + 1] = CutVertex(b, a);
                }
                truncatedFaces.Add(expanded);
            }
            var cutPoints = cut.ToArray();
            for (int i = 0; i < raw.Length; i++)
            {
                var corners = new List<int>(4);
                foreach (var edge in directedEdges) if (edge.Key.x == i) corners.Add(edge.Value);
                SortFace(cutPoints, corners, raw[i].normalized);
                truncatedFaces.Add(corners.ToArray());
            }
            vertices = new Vector3[cutPoints.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Orientation * cutPoints[i] * (Radius / VertexRadius);
            faces = truncatedFaces.ToArray();
        }

        private static int[] Face(Vector3[] points, Vector3 normal, float distance)
        {
            var indices = new List<int>(4);
            for (int i = 0; i < points.Length; i++)
                if (Mathf.Abs(Vector3.Dot(points[i], normal) - distance) < .0001f) indices.Add(i);
            SortFace(points, indices, normal.normalized);
            return indices.ToArray();
        }

        private static void SortFace(Vector3[] points, List<int> indices, Vector3 normal)
        {
            Vector3 center = normal * (Vector3.Dot(points[indices[0]], normal));
            Vector3 u = (points[indices[0]] - center).normalized, v = Vector3.Cross(normal, u);
            indices.Sort((a, b) => Mathf.Atan2(Vector3.Dot(points[a] - center, v), Vector3.Dot(points[a] - center, u))
                .CompareTo(Mathf.Atan2(Vector3.Dot(points[b] - center, v), Vector3.Dot(points[b] - center, u))));
        }

        public static bool IsCrown(Vector3[] vertices, int[] face)
        {
            Vector3 normal = Vector3.Cross(vertices[face[1]] - vertices[face[0]], vertices[face[2]] - vertices[face[0]]).normalized;
            return Vector3.Dot(normal, Vector3.up) > .999f;
        }
    }
}
