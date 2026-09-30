using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Regular rhombicuboctahedron, with a threefold axis upright for six planar docks.</summary>
    public static class CarrierHullGeometry
    {
        public const float Radius = .9f;
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
            // These six square faces have normals perpendicular to the original (1,1,1) axis.
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

            vertices = new Vector3[24];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Orientation * raw[i] * (Radius / VertexRadius);
            faces = polygons.ToArray();
        }

        private static int[] Face(Vector3[] points, Vector3 normal, float distance)
        {
            var indices = new List<int>(4);
            for (int i = 0; i < points.Length; i++)
                if (Mathf.Abs(Vector3.Dot(points[i], normal) - distance) < .0001f) indices.Add(i);
            normal.Normalize();
            Vector3 center = normal * (Vector3.Dot(points[indices[0]], normal));
            Vector3 u = (points[indices[0]] - center).normalized, v = Vector3.Cross(normal, u);
            indices.Sort((a, b) => Mathf.Atan2(Vector3.Dot(points[a] - center, v), Vector3.Dot(points[a] - center, u))
                .CompareTo(Mathf.Atan2(Vector3.Dot(points[b] - center, v), Vector3.Dot(points[b] - center, u))));
            return indices.ToArray();
        }

        public static bool IsCrown(Vector3[] vertices, int[] face)
        {
            Vector3 normal = Vector3.Cross(vertices[face[1]] - vertices[face[0]], vertices[face[2]] - vertices[face[0]]).normalized;
            return Vector3.Dot(normal, Vector3.up) > .999f;
        }
    }
}
