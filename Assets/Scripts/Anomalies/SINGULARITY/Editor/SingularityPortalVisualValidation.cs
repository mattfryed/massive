#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    public static class SingularityPortalVisualValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Portal Presentation")]
        public static string Run()
        {
            int checks = 0;
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Portal presentation validation");
                SceneManager.MoveGameObjectToScene(root, scene);
                var surface = root.AddComponent<SingularitySurface>();
                var gridObject = new GameObject("Grid"); gridObject.transform.SetParent(root.transform);
                var grid = gridObject.AddComponent<SingularityGridRenderer>();
                grid.surface = surface; grid.RefreshPresentation();
                var actor = new GameObject("Actor"); actor.transform.SetParent(root.transform);
                var body = actor.AddComponent<Rigidbody>(); body.useGravity = false;
                var player = actor.AddComponent<PlayerControllerScript>(); player.massScore = .73f;
                var adapter = actor.AddComponent<SingularityPlayerAdapter>(); adapter.Configure(surface, grid);
                adapter.SetSurfacePosition(new Vector2(2f, surface.FrontHeight * .5f));
                Vector3 scale = actor.transform.localScale;
                float mass = body.mass;
                Vector3 center = adapter.RenderWorldPosition;
                var offset = new Vector2(2f, -3f);
                Check(SingularityPlayerAdapter.DeformPortalOffset(offset, Vector2.right, 1f, 1f) == offset,
                    "Identity leaves positions unchanged", ref checks);
                var along = SingularityPlayerAdapter.DeformPortalOffset(Vector2.right, Vector2.right, .5f, 3f);
                var across = SingularityPlayerAdapter.DeformPortalOffset(Vector2.up, Vector2.right, .5f, 3f);
                Check(Mathf.Abs(along.x - 1.5f) < .00001f && along.y == 0f, "Axial stretch", ref checks);
                Check(Mathf.Abs(across.y - .5f / Mathf.Sqrt(3f)) < .00001f && across.x == 0f, "Perpendicular narrowing", ref checks);
                adapter.SetPortalVisual(Vector2.right, .4f, 3f);
                Check(adapter.HasPortalVisual && adapter.PortalVisualScale == .4f, "Visual state enabled", ref checks);
                Check(Vector3.Distance(adapter.RenderWorldPosition, center) < .00001f, "Body center does not shift", ref checks);
                Vector3 edge = adapter.MapChartPoint(actor.transform.position + Vector3.right);
                Check(Mathf.Abs(edge.x - center.x - 1.2f) < .0001f, "CPU mapped effects share deformation", ref checks);
                var properties = new MaterialPropertyBlock();
                adapter.ApplyRenderProperties(properties, Matrix4x4.identity, Matrix4x4.identity);
                Check(properties.GetFloat("_SingularityEnabled") == 1f, "Surface rendering is active", ref checks);
                Check(properties.GetVector("_SingularityPortalCenter").w == 1f, "GPU visual state enabled", ref checks);
                Check(Vector4.Distance(properties.GetVector("_SingularityPortalShape"), new Vector4(1f, 0f, .4f, 3f)) < .00001f,
                    "GPU body and nuggets receive same shape", ref checks);
                Check(actor.transform.localScale == scale && body.mass == mass && player.massScore == .73f,
                    "No transform, physics mass or scoring mutation", ref checks);
                adapter.SetPortalVisual(new Vector2(float.NaN, 0f), float.PositiveInfinity, float.NaN);
                Check(adapter.PortalVisualScale == 1f && adapter.PortalVisualStretch == 1f, "Invalid values use safe defaults", ref checks);
                adapter.ClearPortalVisual();
                adapter.ApplyRenderProperties(properties, Matrix4x4.identity, Matrix4x4.identity);
                Check(!adapter.HasPortalVisual && properties.GetVector("_SingularityPortalCenter").w == 0f, "GPU state clears", ref checks);
                Check(Vector3.Distance(adapter.MapChartPoint(actor.transform.position + Vector3.right), center + Vector3.right) < .0001f,
                    "CPU shape restores", ref checks);
                adapter.SetPortalVisual(Vector2.up, .2f, 2f); adapter.enabled = false;
                // This runtime-only component has no edit-mode Unity lifecycle.
                // Invoke the callback explicitly in this disposable preview.
                typeof(SingularityPlayerAdapter).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(adapter, null);
                Check(!adapter.HasPortalVisual && adapter.PortalVisualScale == 1f && adapter.PortalVisualStretch == 1f,
                    "Disable clears deformation", ref checks);
                grid.EmitRipple(new Vector2(float.NaN, 0f), .5f, 4f, .1f, .3f);
                grid.RefreshPresentation();
                Check(grid.ActiveRippleCount == 0, "Invalid ripple rejected", ref checks);
                for (int i = 0; i < 12; i++) grid.EmitRipple(new Vector2(i * .01f, 6f), 3f, 4f, .1f, .3f);
                grid.RefreshPresentation();
                Check(grid.ActiveRippleCount == 8, "Ripple capacity bounded", ref checks);
                grid.ClearRipples(); grid.RefreshPresentation();
                Check(grid.ActiveRippleCount == 0, "Ripple clear removes GPU count", ref checks);
                grid.EmitRipple(new Vector2(0f, 6f), 3f, 4f, .1f, .3f);
                grid.RefreshPresentation(); grid.enabled = false;
                Check(grid.ActiveRippleCount == 0, "Grid disable clears ripples", ref checks);
                string report = "SINGULARITY portal presentation: PASS " + checks + " checks (deformation, shared CPU/GPU state, no gameplay mutation and bounded ripple lifecycle).";
                Debug.Log(report); return report;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void Check(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException("Portal presentation: " + label); checks++; }
    }
}
#endif
