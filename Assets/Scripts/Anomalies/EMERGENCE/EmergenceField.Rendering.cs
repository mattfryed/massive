using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Emergence
{
    public sealed partial class EmergenceField
    {
        Mesh mesh;
        Vector3[] vertices;
        Color[] colors;
        void CreateMesh()
        {
            if (mesh || Simulation == null || UsesGpu) return;
            int count = Simulation.Count;
            mesh = new Mesh { name = "EMERGENCE live particles", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic(); vertices = new Vector3[count * 4]; colors = new Color[count * 4];
            var uv = new Vector2[count * 4]; var indices = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1; indices[t + 3] = v; indices[t + 4] = v + 3; indices[t + 5] = v + 2;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = indices;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>(); renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        void RenderParticles()
        {
            if (UsesGpu) return;
            if (!mesh) CreateMesh(); if (!mesh) return;
            for (int i = 0; i < Simulation.Particles.Length; i++)
            {
                // Visible lifecycle scale is independent of whether a nutrient is still edible.
                var p = Simulation.Particles[i]; Vector2 position = p.position;
                float size = p.radius * p.visualScale;
                if (p.species < 3)
                {
                    float pulse = 0;
                    foreach (var bud in Simulation.RewardBuds) if (bud.Attached)
                    {
                        float weight = Mathf.Clamp01(1 - Vector2.Distance(position, bud.position) / profile.budPulseRadius);
                        pulse += bud.Pulse(profile) * weight * weight;
                    }
                    size *= 1 + Mathf.Clamp(pulse, -1, 1) * profile.budPulseStrength;
                }
                Color color = profile.species[p.species].color;
                if (profile.showWasteColor && p.species == 2) color = Color.Lerp(color, profile.wasteColor, Mathf.Clamp01(p.waste / profile.wasteColorAt));
                if (profile.showDisruptionColor && p.disruption > 0) color = profile.disruptionColor;
                color.a = 1;
                int v = i * 4;
                SetVertex(v, position + new Vector2(-size, -size), color);
                SetVertex(v + 1, position + new Vector2(size, -size), color);
                SetVertex(v + 2, position + new Vector2(size, size), color);
                SetVertex(v + 3, position + new Vector2(-size, size), color);
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        }
        void SetVertex(int index, Vector2 p, Color color)
        { vertices[index] = presentationOnly ? new Vector3(p.x, 0, p.y) : transform.InverseTransformPoint(WorldPosition(p)); colors[index] = color; }
        void ReleaseMesh()
        {
            if (!mesh) return;
            var filter = GetComponent<MeshFilter>(); if (filter && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); mesh = null;
        }
    }
}
