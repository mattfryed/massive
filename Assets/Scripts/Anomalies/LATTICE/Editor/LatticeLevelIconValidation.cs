using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.Editor
{
    public static class LatticeLevelIconValidation
    {
        public const string Output = "Library/LatticeIconValidation";
        [MenuItem("MASSIVE/LATTICE/Validate Level Icon %#&j")]
        public static void RunMenu()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                LatticeLevelIconAssets.CreateMissing();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LatticeLevelIconAssets.PrefabPath);
                Check(prefab && prefab.GetComponent<LevelIcon>(), "Prefab uses the existing LevelIcon selection contract");
                Check(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentsInChildren<VectorGridGPU>(true).Length == 0,
                    "Icon has no colliders or gameplay grid");
                Check(prefab.GetComponentInChildren<MeshFilter>().sharedMesh == null, "Generated geometry is not serialized into the prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var icon = root.GetComponentInChildren<LatticeLevelIcon>();
                // Keep the original dense-volume stress fixture independent of authored icon tuning.
                icon.cellsPerAxis = 10; icon.noiseFrequency = .19f; icon.threshold = .51f;
                icon.transform.localScale = Vector3.one * .085f;
                icon.animateInEditor = false; icon.Rebuild(); icon.Advance(0);
                Check(icon.NodeCount == 1331 && icon.EdgeCount == 3630, "Ten cells per axis form 1,331 nodes and 3,630 unique axis connections");
                int interior = 0; var coordinates = new HashSet<Vector3>();
                for (int i = 0; i < icon.NodeCount; i++)
                {
                    icon.GetNode(i, out var p, out _, out _); coordinates.Add(p);
                    if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) < 5) interior++;
                }
                Check(coordinates.Count == 1331 && interior == 729 && coordinates.Contains(Vector3.zero), "Lattice fills the interior with distinct 3D intersections");
                var connections = new HashSet<string>(); bool unitEdges = true;
                for (int i = 0; i < icon.EdgeCount; i++)
                {
                    icon.GetEdge(i, out var a, out var b, out _, out _);
                    unitEdges &= Mathf.Abs(Vector3.Distance(a, b) - 1) < .0001f;
                    connections.Add(a.ToString("F0") + b.ToString("F0"));
                }
                Check(unitEdges && connections.Count == 3630, "Every connection spans one cell with no duplicate edges");
                var shader = root.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader;
                Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Shader is supported and compiles on the active graphics device");
                Check(icon.DisconnectedEdgeCount > 100 && icon.DisconnectedEdgeCount < icon.EdgeCount - 100, "Noise creates both connected and disrupted regions throughout the cube");
                float depthVariation = 0;
                for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++)
                    depthVariation += Mathf.Abs(icon.NoiseAt(new Vector3(x, y, 3), 12) - icon.NoiseAt(new Vector3(x, y, -3), 12));
                Check(depthVariation > .5f, "Noise varies through depth instead of repeating a flat slice");
                for (int i = 0; i < 12; i++) icon.Advance(.1f);
                bool single = false, held = true;
                for (int i = 0; i < icon.EdgeCount; i++)
                {
                    icon.GetEdge(i, out var a, out var b, out var sa, out var sb);
                    if (sa.w < .5f) continue;
                    bool ia = icon.NoiseAt(a, icon.FieldTime) > icon.threshold, ib = icon.NoiseAt(b, icon.FieldTime) > icon.threshold;
                    if (ia != ib) { single = true; held &= (ia ? sa.x : sb.x) == 0; }
                    if (sa.x > .001f) held &= Mathf.Abs(icon.NoiseAt(Vector3.Lerp(a, b, sa.x), icon.FieldTime) - icon.threshold) < .003f;
                    if (sb.x > .001f) held &= Mathf.Abs(icon.NoiseAt(Vector3.Lerp(b, a, sb.x), icon.FieldTime) - icon.threshold) < .003f;
                }
                Check(single && held, "Endpoint cuts retain one tether and held tips land on the 3D noise boundary");
                float minDepth = float.MaxValue, maxDepth = 0;
                for (int i = 0; i < icon.NodeCount; i++) { icon.GetNode(i, out _, out var missing, out var depth); if (missing > .9f) { minDepth = Mathf.Min(minDepth, depth); maxDepth = Mathf.Max(maxDepth, depth); } }
                Check(minDepth < .5f && maxDepth > 1, "Interior dots have more jitter depth than boundary dots");
                var camera = MakeCamera(scene);
                var first = Capture(camera, "cube-a.png");
                Check(first.Count(c => c.r + c.g + c.b > 35) > 2000, "Actual volume render contains visible threads and nodes");
                for (int i = 0; i < 20; i++) icon.Advance(.1f);
                var second = Capture(camera, "cube-b.png");
                Check(!first.SequenceEqual(second), "Noise, loose threads and RGB dots visibly animate");
                icon.Advance(0); var frozen = Capture(camera, "cube-frozen.png");
                Check(second.SequenceEqual(frozen), "A held clock freezes all rendering including ghost trails");
                root.transform.rotation = Quaternion.Euler(0, 0, 180);
                var carousel = Capture(camera, "cube-carousel.png");
                Check(carousel.Count(c => c.r + c.g + c.b > 35) > 2000, "Icon remains visible in the carousel's selected orientation");
                root.transform.rotation = Quaternion.identity;
                camera.orthographicSize = 4;
                Capture(camera, "cube-menu-size.png"); camera.orthographicSize = .8f;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 20; i++) icon.Advance(.05f);
                watch.Stop(); File.WriteAllText(Output + "/cpu-sampling.txt", $"Mean full field/state refresh: {watch.Elapsed.TotalMilliseconds / 20:F2} ms (20 samples, Unity Editor; excludes rendering).\n");
                icon.mode = LatticeDisruptionField.FieldMode.Disconnected; icon.Rebuild();
                for (int i = 0; i < 20; i++) icon.Advance(.1f);
                Check(icon.DisconnectedEdgeCount == icon.EdgeCount, "Disconnected control removes all volume connections");
                icon.dotJitterRadius = 5; icon.dotDiameterPixels = 3; icon.Advance(0);
                var trails = Capture(camera, "dots-trails.png");
                Check(trails.Count(c => c.r > 60 && c.g < 20 && c.b < 20) > 50 &&
                    trails.Count(c => c.g > 60 && c.r < 20 && c.b < 20) > 50 &&
                    trails.Count(c => c.b > 60 && c.r < 20 && c.g > 12 && c.g < c.b * .7f) > 50,
                    "Separated red, green and cyan-tinted blue dots render in the volume");
                icon.dotGhostTrails = false; icon.Advance(0); var heads = Capture(camera, "dots-heads.png");
                Check(trails.Where((c, i) => c.r + c.g + c.b > heads[i].r + heads[i].g + heads[i].b + 10).Count() > 100,
                    "Ghost trails add fading pixels beyond the current dot heads");
                icon.rgbDotJitter = false; icon.Advance(0); var white = Capture(camera, "dots-white.png");
                Check(white.All(c => Mathf.Abs(c.r - c.g) < 2 && Mathf.Abs(c.g - c.b) < 2), "Disabling jitter restores white nodes throughout the cube");
                icon.mode = LatticeDisruptionField.FieldMode.Connected; icon.Advance(.1f);
                Check(icon.TransitioningEdgeCount > 0, "Reconnect grows threads over time instead of popping them on");
                for (int i = 0; i < 20; i++) icon.Advance(.1f);
                Check(icon.DisconnectedEdgeCount == 0 && icon.TransitioningEdgeCount == 0, "Reconnect completes the entire cube");
                Capture(camera, "cube-connected.png");
                var filter = icon.GetComponent<MeshFilter>(); Mesh old = filter.sharedMesh;
                icon.enabled = false;
                Check(!icon.IsReady && old == null && filter.sharedMesh == null, "Disable releases owned mesh and state buffers");
                icon.enabled = true; icon.Rebuild(); icon.Advance(0);
                Check(icon.IsReady && icon.EdgeCount == 3630, "Re-enable recreates the volume without stale resources");
                Check(!ShaderUtil.ShaderHasError(shader), "Rendered shader has no compilation errors");
                File.WriteAllLines(Output + "/report.txt", new[] { "PASSED" }.Concat(results));
                Debug.Log($"LATTICE icon validation PASSED: {results.Count} checks.");
            }
            catch (Exception e)
            { File.WriteAllLines(Output + "/report.txt", new[] { "FAILED", e.ToString() }.Concat(results)); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); results.Add("PASS " + label); }
        }

        public static Camera MakeCamera(Scene scene)
        {
            var go = new GameObject("Icon Validation Camera"); SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.transform.position = new Vector3(0, 5, 0); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true; camera.orthographicSize = .8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = false;
            return camera;
        }
        public static Color32[] Capture(Camera camera, string filename)
        {
            var rt = new RenderTexture(1000, 1000, 24) { antiAliasing = 4 };
            var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + "/" + filename, texture.EncodeToPNG()); return texture.GetPixels32();
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }
}
