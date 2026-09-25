using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    public static class SingularityCoreBrightnessValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Core Backside Brightness")]
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run the isolated brightness checks in Edit Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, why) => { if (!ok) throw new InvalidOperationException(why); checks++; };
            var scene = EditorSceneManager.NewPreviewScene();
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var previous = RenderTexture.active;
            try
            {
                var root = new GameObject("Brightness test surface");
                SceneManager.MoveGameObjectToScene(root, scene);
                var surface = root.AddComponent<SingularitySurface>();
                var so = new SerializedObject(surface);
                so.FindProperty("backsideBrightness").floatValue = .4f;
                so.ApplyModifiedPropertiesWithoutUndo();
                check(Mathf.Abs(surface.EvaluateBrightness(surface.FrontHeight * .5f) - 1f) < .0001f, "Front is unchanged");
                check(Mathf.Abs(surface.EvaluateBrightness((surface.RearStart + surface.BottomStart) * .5f) - .4f) < .0001f, "Rear reaches the shared brightness");
                float upper = 1f, lower = .4f;
                for (int i = 0; i <= 64; i++)
                {
                    float a = surface.EvaluateBrightness(Mathf.Lerp(surface.TopStart, surface.RearStart, i / 64f));
                    float b = surface.EvaluateBrightness(Mathf.Lerp(surface.BottomStart, surface.LoopLength, i / 64f));
                    check(a <= upper + .0001f && b >= lower - .0001f && a >= .3999f && b <= 1.0001f,
                        "Fold brightness is monotonic and bounded " + i);
                    upper = a; lower = b;
                }
                check(Mathf.Abs(surface.EvaluateBrightness(.001f) - surface.EvaluateBrightness(surface.LoopLength - .001f)) < .001f,
                    "Brightness is continuous at the loop seam");
                var actor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SceneManager.MoveGameObjectToScene(actor, scene);
                actor.transform.localScale = Vector3.one * 2f;
                var renderer = actor.GetComponent<Renderer>();
                var brightness = actor.AddComponent<SingularityRendererBrightness>();
                brightness.Configure(surface);
                var block = new MaterialPropertyBlock();
                block.SetFloat("_BandWidth", .72f); block.SetFloat("_TimeoutWarning", .3f);
                renderer.SetPropertyBlock(block);
                actor.transform.position = new Vector3(0f, 0f, (surface.RearStart + surface.BottomStart) * .5f - surface.FrontHeight * .5f);
                brightness.RefreshPresentation(); renderer.GetPropertyBlock(block);
                check(Mathf.Abs(block.GetFloat("_SingularityBrightness") - .4f) < .0001f, "Renderer receives rear brightness");
                check(block.GetFloat("_BandWidth") == .72f && block.GetFloat("_TimeoutWarning") == .3f,
                    "Brightness preserves shell coverage and timeout warning");
                brightness.enabled = false; brightness.RefreshPresentation(); renderer.GetPropertyBlock(block);
                check(block.GetFloat("_SingularityBrightness") == 1f, "Disabled dimmer restores neutral brightness");

                var cameraGo = new GameObject("Brightness test camera");
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 1.25f; camera.aspect = 1f;
                camera.transform.SetPositionAndRotation(actor.transform.position + Vector3.up * 10f,
                    Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0);
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create(); camera.targetTexture = target;
                readback = new Texture2D(96, 96, TextureFormat.RGBAFloat, false, true);
                foreach (string shader in new[] { "MASSIVE/Amplifier Core/Energy", "MASSIVE/Amplifier Core/Neutral Shell" })
                {
                    material = new Material(Shader.Find(shader)); renderer.sharedMaterial = material;
                    material.SetFloat("_FlowSpeed", 0f);
                    block.Clear(); block.SetFloat("_SingularityBrightness", 1f); renderer.SetPropertyBlock(block);
                    var full = Render(camera, target, readback);
                    block.SetFloat("_SingularityBrightness", .4f); renderer.SetPropertyBlock(block);
                    var dim = Render(camera, target, readback);
                    check(full.x > 10f && Mathf.Abs(dim.x / full.x - .4f) < .015f, shader + " multiplies rendered RGB");
                    check(Mathf.Abs(full.y - dim.y) < .01f, shader + " preserves opacity/coverage");
                    block.Clear(); renderer.SetPropertyBlock(block);
                    var normal = Render(camera, target, readback);
                    check(Mathf.Abs(normal.x / full.x - 1f) < .015f, shader + " stays unchanged without adapter");
                    UnityEngine.Object.DestroyImmediate(material); material = null;
                }
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
            return "SINGULARITY Core brightness: " + checks + " gradient, property preservation and GPU color/opacity checks passed.";
        }
        private static Vector2 Render(Camera camera, RenderTexture target, Texture2D texture)
        {
            camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            Vector2 sum = Vector2.zero;
            foreach (var color in texture.GetPixels()) { sum.x += color.r + color.g + color.b; sum.y += color.a; }
            return sum;
        }
    }
}
