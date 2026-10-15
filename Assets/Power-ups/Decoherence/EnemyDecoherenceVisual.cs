using System.Collections.Generic;
using Massive.Enemies;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.PowerUps
{
    // Offset wire fragments use the live body meshes, including animated panels.
    // They never move physics, hurtboxes, docking points, or the original meshes.
    [DisallowMultipleComponent]
    public sealed class EnemyDecoherenceVisual : ImmediateModeShapeDrawer
    {
        private sealed class Surface
        {
            public MeshFilter filter;
            public Mesh mesh;
            public readonly List<Vector3> vertices = new();
            public readonly List<int> indices = new();
        }
        private readonly List<Surface> surfaces = new();
        private EnemyBase enemy;
        private float lastExposure = float.NegativeInfinity, exposure, split, opacity, envelope;
        public float VisibleAmount => envelope;
        public void Expose(float separation, float alpha, float dt)
        {
            lastExposure = Time.time;
            exposure = Mathf.Min(1f, exposure + dt);
            split = Mathf.Max(0f, separation); opacity = Mathf.Clamp01(alpha);
        }
        private void Start()
        {
            enemy = GetComponent<EnemyBase>();
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                if (!renderer || !renderer.sharedMaterial || !renderer.sharedMaterial.shader) continue;
                // Metaball bounding boxes and plasma volumes are not body geometry.
                string shader = renderer.sharedMaterial.shader.name;
                if (shader.Contains("Metaball") || shader.Contains("Plasma") || shader.Contains("Particle")) continue;
                surfaces.Add(new Surface { filter = filter });
            }
            useCullingMasks = true;
        }
        private void LateUpdate()
        {
            if (!enemy || enemy.IsPaused) return;
            bool active = !enemy.IsDead && Time.time - lastExposure < .06f;
            envelope = Mathf.MoveTowards(envelope, active ? 1f : 0f, Time.deltaTime / (active ? .08f : .2f));
            if (!active) exposure = Mathf.MoveTowards(exposure, 0f, Time.deltaTime * 3f);
            if (envelope <= 0f) return;
            foreach (var surface in surfaces)
            {
                var mesh = surface.filter ? surface.filter.sharedMesh : null;
                if (!mesh || !mesh.isReadable) { surface.mesh = null; continue; }
                if (surface.mesh != mesh)
                {
                    surface.mesh = mesh; surface.indices.Clear();
                    if (mesh.subMeshCount > 0 && mesh.GetTopology(0) == MeshTopology.Triangles) mesh.GetTriangles(surface.indices, 0);
                }
                mesh.GetVertices(surface.vertices);
            }
        }
        public override void DrawShapes(Camera camera)
        {
            if (!enemy || enemy.IsDead || envelope <= .001f) return;
            using (Draw.Command(camera))
            {
                Draw.Matrix = Matrix4x4.identity; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.Thickness = .012f * envelope;
                Draw.Color = new Color(1f, 1f, 1f, opacity * envelope * .7f);
                float separation = split * envelope * Mathf.Lerp(.35f, 1f, exposure);
                foreach (var surface in surfaces)
                {
                    if (!surface.filter || !surface.filter.gameObject.activeInHierarchy || !surface.mesh) continue;
                    var matrix = surface.filter.transform.localToWorldMatrix;
                    int limit = Mathf.Min(surface.indices.Count, 768);
                    for (int i = 0; i + 2 < limit; i += 3)
                    {
                        int ia = surface.indices[i], ib = surface.indices[i + 1], ic = surface.indices[i + 2];
                        if (ia >= surface.vertices.Count || ib >= surface.vertices.Count || ic >= surface.vertices.Count) continue;
                        Vector3 a = matrix.MultiplyPoint3x4(surface.vertices[ia]), b = matrix.MultiplyPoint3x4(surface.vertices[ib]), c = matrix.MultiplyPoint3x4(surface.vertices[ic]);
                        Vector3 outward = (a + b + c) / 3f - enemy.transform.position; outward.y = 0f;
                        if (outward.sqrMagnitude < .00001f) outward = Vector3.right;
                        Vector3 offset = outward.normalized * separation * (1f + .2f * Mathf.Sin(Time.time * 17f + i));
                        offset.y = .015f;
                        Draw.Line(a + offset, b + offset); Draw.Line(b + offset, c + offset); Draw.Line(c + offset, a + offset);
                    }
                }
            }
        }
    }
}
