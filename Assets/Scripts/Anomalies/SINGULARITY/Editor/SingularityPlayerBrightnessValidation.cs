#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated RGB/alpha regression for the real procedural player,
    /// both nugget passes, attack trails, and the shared CPU presentation cue.</summary>
    public static class SingularityPlayerBrightnessValidation
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct TrailParticle
        {
            public Vector3 position;
            public Vector2 velocity;
            public float u, life, maxLife, size;
            public uint state;
        }

        [MenuItem("MASSIVE/SINGULARITY/Validate Player Backside Brightness")]
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run brightness validation in Edit Mode.");
            int checks = 0;
            Action<bool, string> check = (condition, message) => {
                if (!condition) throw new InvalidOperationException("SINGULARITY player brightness: " + message);
                checks++;
            };
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture previous = RenderTexture.active;
            Material bodyMaterial = null, nuggetMaterial = null, trailMaterial = null;
            RenderTexture target = null;
            Texture2D readback = null;
            Mesh quad = null;
            ComputeBuffer nuggets = null, trails = null;
            var commands = new CommandBuffer { name = "Isolated folded player brightness" };
            try
            {
                var owner = new GameObject("brightness test surface");
                SceneManager.MoveGameObjectToScene(owner, scene);
                var surface = owner.AddComponent<SingularitySurface>();
                var grid = owner.AddComponent<SingularityGridRenderer>();
                grid.surface = surface; grid.showPlayerAttraction = false; grid.RefreshPresentation();
                check(grid.SurfaceLookup != null, "surface lookup is available");
                var actor = new GameObject("brightness test actor");
                SceneManager.MoveGameObjectToScene(actor, scene);
                actor.AddComponent<Rigidbody>().useGravity = false;
                actor.AddComponent<PlayerControllerScript>();
                var adapter = actor.AddComponent<SingularityPlayerAdapter>();
                adapter.Configure(surface, grid);
                var properties = new MaterialPropertyBlock();
                adapter.ApplyRenderProperties(properties, Matrix4x4.identity, Matrix4x4.identity);
                check(Mathf.Approximately(properties.GetFloat("_SingularityBackBrightness"), surface.BacksideBrightness),
                    "shared authored brightness reaches each draw property block");

                float[] distances = { surface.FrontHeight * .5f, (surface.RearStart + surface.BottomStart) * .5f,
                    Mathf.Lerp(surface.TopStart, surface.RearStart, .35f),
                    Mathf.Lerp(surface.BottomStart, surface.LoopLength, .65f) };
                foreach (float distance in distances)
                {
                    Vector3 chart = new Vector3(0f, 0f, distance - surface.FrontHeight * .5f);
                    check(Mathf.Abs(adapter.EvaluateChartBrightness(chart) - surface.EvaluateBrightness(distance)) < .00001f,
                        "CPU shield gradient matches the shared surface at distance " + distance);
                }
                adapter.enabled = false;
                adapter.ApplyRenderProperties(properties, Matrix4x4.identity, Matrix4x4.identity);
                check(properties.GetFloat("_SingularityBackBrightness") == 1f && adapter.EvaluateChartBrightness(actor.transform.position) == 1f,
                    "disabled adapters restore full brightness without stale dimming");
                adapter.enabled = true;

                bodyMaterial = CreateMaterial("MASSIVE/PlayerBlobVector", check);
                nuggetMaterial = CreateMaterial("MASSIVE/NuggetInstanced", check);
                trailMaterial = CreateMaterial("MASSIVE/AttackTrailInstanced", check);
                bodyMaterial.SetFloat("_Radius", .4f);
                bodyMaterial.SetFloat("_OutlineHalf", .07f);
                bodyMaterial.SetFloat("_Stretch", 1f);
                bodyMaterial.SetColor("_FillColor", new Color(.8f, .5f, .2f, .8f));
                bodyMaterial.SetColor("_OutlineColor", new Color(.8f, .5f, .2f, .8f));
                nuggetMaterial.SetColor("_Color", new Color(.8f, .5f, .2f, .8f));
                nuggetMaterial.SetColor("_OutlineColor", new Color(.8f, .5f, .2f, .8f));
                nuggetMaterial.SetFloat("_DotRadius", .4f);
                nuggetMaterial.SetFloat("_OutlineWidth", .06f);
                nuggetMaterial.SetFloat("_DrawOutline", 1f);
                trailMaterial.SetColor("_Team1Color", new Color(.8f, .5f, .2f, 1f));
                trails = new ComputeBuffer(1, Marshal.SizeOf(typeof(TrailParticle)));
                nuggets = new ComputeBuffer(1, 12);
                nuggets.SetData(new[] { Vector3.zero });
                quad = new Mesh { name = "brightness fixture quad" };
                quad.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f),
                    new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f) };
                quad.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create();
                readback = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);

                for (int face = 0; face < distances.Length; face++)
                {
                    float distance = distances[face];
                    Vector3 chart = new Vector3(0f, 0f, distance - surface.FrontHeight * .5f);
                    Vector3 displayed = surface.Evaluate(0f, distance);
                    Vector3 normal = surface.Normal(0f, distance);
                    Vector3 up = Vector3.Cross(Vector3.right, normal).normalized;
                    Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(
                        displayed + normal * 20f, Quaternion.LookRotation(-normal, up), Vector3.one).inverse;
                    Matrix4x4 projection = Matrix4x4.Ortho(-1f, 1f, -1f, 1f, .1f, 100f);
                    adapter.ApplyRenderProperties(properties, Matrix4x4.Translate(chart),
                        GL.GetGPUProjectionMatrix(projection, true) * view);
                    properties.SetMatrix("_MVP", GL.GetGPUProjectionMatrix(projection, true) * view * Matrix4x4.Translate(chart));
                    properties.SetVector("_CenterWS", chart);
                    properties.SetVector("_CamRightWS", Vector3.right);
                    properties.SetVector("_CamUpWS", up);
                    properties.SetBuffer("_NuggetPos", nuggets);
                    properties.SetBuffer("_Particles", trails);
                    trails.SetData(new[] { new TrailParticle { position = chart, size = .4f, life = 1f, maxLife = 1f, state = 1 } });

                    Material[] materials = { bodyMaterial, bodyMaterial, nuggetMaterial, nuggetMaterial, trailMaterial };
                    string[] labels = { "body fill", "body outline", "nugget fill", "nugget outline", "attack trail" };
                    int[] passes = { 0, 1, 0, 1, 0 };
                    for (int path = 0; path < materials.Length; path++)
                    {
                        properties.SetFloat("_SingularityBackBrightness", 1f);
                        Color[] baseline = Render(commands, target, readback, materials[path], passes[path], properties,
                            path < 2 ? null : quad, view, projection);
                        properties.SetFloat("_SingularityBackBrightness", surface.BacksideBrightness);
                        Color[] dimmed = Render(commands, target, readback, materials[path], passes[path], properties,
                            path < 2 ? null : quad, view, projection);
                        double original = 0, dim = 0; float alphaError = 0;
                        for (int i = 0; i < baseline.Length; i++)
                        {
                            original += baseline[i].r; dim += dimmed[i].r;
                            alphaError = Mathf.Max(alphaError, Mathf.Abs(baseline[i].a - dimmed[i].a));
                        }
                        string label = labels[path] + " at surface sample " + face;
                        check(original > 1, label + " renders representative pixels");
                        check(alphaError < .00001f, label + " preserves alpha and silhouette");
                        double ratio = dim / Math.Max(original, .000001);
                        check(Math.Abs(ratio - surface.EvaluateBrightness(distance)) < .05,
                            label + " dims RGB locally (ratio " + ratio.ToString("F4") + ")");
                    }
                }
                foreach (Material material in new[] { bodyMaterial, nuggetMaterial, trailMaterial })
                    check(!ShaderUtil.ShaderHasError(material.shader), material.shader.name + " compiles after GPU execution");
            }
            finally
            {
                RenderTexture.active = previous; commands.Release();
                if (nuggets != null) nuggets.Release(); if (trails != null) trails.Release();
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (quad != null) UnityEngine.Object.DestroyImmediate(quad);
                if (bodyMaterial != null) UnityEngine.Object.DestroyImmediate(bodyMaterial);
                if (nuggetMaterial != null) UnityEngine.Object.DestroyImmediate(nuggetMaterial);
                if (trailMaterial != null) UnityEngine.Object.DestroyImmediate(trailMaterial);
            }
            string report = "SINGULARITY player brightness: " + checks + " checks passed (front, rear, both bends; body/outline, nuggets, trails; alpha and CPU shield cue).";
            Debug.Log(report); return report;
        }

        private static Material CreateMaterial(string name, Action<bool, string> check)
        {
            Shader shader = Shader.Find(name);
            check(shader != null && !ShaderUtil.ShaderHasError(shader), name + " compiles");
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static Color[] Render(CommandBuffer commands, RenderTexture target, Texture2D readback,
            Material material, int pass, MaterialPropertyBlock properties, Mesh quad, Matrix4x4 view, Matrix4x4 projection)
        {
            commands.Clear(); commands.SetRenderTarget(target);
            commands.SetViewProjectionMatrices(view, projection);
            commands.ClearRenderTarget(true, true, Color.clear);
            if (quad == null) commands.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 128 * 3, 1, properties);
            else commands.DrawMesh(quad, Matrix4x4.identity, material, 0, pass, properties);
            Graphics.ExecuteCommandBuffer(commands); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
            return readback.GetPixels();
        }
    }
}
#endif
