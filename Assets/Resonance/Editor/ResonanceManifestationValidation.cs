using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Resonance.Editor
{
    /// <summary>Isolated, dependency-free lifecycle and GPU checks. Never advances the gameplay scene.</summary>
    public static class ResonanceManifestationValidation
    {
        [MenuItem("MASSIVE/Resonance/Validate Formation Lifecycle")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var passed = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject root = null;
            try
            {
                definition.Set345HzDefaults();
                definition.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = -20, sweepDegrees = 40, thickness = .3f, colliderThickness = .3f } };
                root = new GameObject("Formation validation"); SceneManager.MoveGameObjectToScene(root, scene);
                var pattern = root.AddComponent<ResonancePatternController>();
                pattern.definition = definition;
                pattern.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Particles.mat");
                pattern.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
                pattern.rendering = ResonanceRendering.OptionBParticles; pattern.particles.particleBudget = 2048;
                pattern.Rebuild(); Physics.SyncTransforms();
                var physics = scene.GetPhysicsScene(); RaycastHit hit;
                var generated = pattern.GeneratedRoot;
                var population = root.GetComponentsInChildren<ParticleSystem>(true);
                int count = pattern.ParticleCount, gridSamples = pattern.GridSampleCount;
                var manifestation = root.AddComponent<ResonanceManifestation>();
                Check(!manifestation.IsControlled && manifestation.IsIdle && pattern.InteractionEnabled, "adding optional component preserves standalone idle authority", passed);
                Check(physics.Raycast(Vector3.zero, Vector3.right, out hit, 3f), "baseline hard arc has real collision", passed);
                string settings = JsonUtility.ToJson(pattern), definitionSettings = JsonUtility.ToJson(definition);
                manifestation.spawnSeconds = 2f; manifestation.despawnSeconds = 1f;
                manifestation.SetArea(new Vector2(1, 2), new Vector2(6, 4));
                manifestation.BeginSpawn();
                Check(manifestation.NormalizedProgress == 0f && manifestation.IsTransitioning && !pattern.InteractionEnabled, "first spawn begins at size zero and gates immediately", passed);
                Check(root.GetComponentsInChildren<Collider>(true).All(c => !c.enabled) && pattern.InteractiveSegments.All(s => !s.enabled), "all generated physical and trigger contacts are disabled", passed);
                Physics.SyncTransforms();
                Check(!physics.Raycast(Vector3.zero, Vector3.right, out hit, 3f), "forming pattern cannot block a physics ray", passed);
                Check(root.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "zero-size start renders no grains or full-pattern ghost", passed);
                Check(generated == pattern.GeneratedRoot && count == pattern.ParticleCount && population.All(s => s != null && s.particleCount > 0), "formation retains original particle pools and hierarchy", passed);
                manifestation.Advance(.5f);
                Check(Near(manifestation.NormalizedProgress, .25f) && !pattern.InteractionEnabled, "scaled elapsed time advances formation without early collisions", passed);
                var block = new MaterialPropertyBlock(); var first = root.GetComponentInChildren<ParticleSystemRenderer>();
                first.GetPropertyBlock(block); Vector4 pose = block.GetVector("_Manifestation");
                Check(pose.x > 0 && pose.x < pose.y && pose.y < 1 && pose.z > 0, "GPU pose grows early, condenses later, and vibrates", passed);
                Vector4 before = pose; manifestation.Advance(0); first.GetPropertyBlock(block);
                Check(block.GetVector("_Manifestation") == before, "zero scaled delta freezes formation and vibration", passed);
                manifestation.BeginDespawn();
                Check(Near(manifestation.NormalizedProgress, .25f), "reverse interruption does not jump to another pose", passed);
                manifestation.Advance(.1f);
                Check(Near(manifestation.NormalizedProgress, .15f), "reverse uses configured despawn rate", passed);
                manifestation.BeginSpawn(); manifestation.Advance(2f);
                Check(manifestation.IsIdle && !manifestation.IsTransitioning && pattern.InteractionEnabled, "completed spawn alone restores interaction", passed);
                Physics.SyncTransforms();
                Check(physics.Raycast(Vector3.zero, Vector3.right, out hit, 3f), "settled arc restores real collisions", passed);
                first.GetPropertyBlock(block); pose = block.GetVector("_Manifestation");
                Check(pose.x == 1 && pose.y == 1 && pose.z == 0, "idle pose exactly bypasses all particle deformation", passed);
                pattern.PreviewLocalContact(false); pattern.PreviewImpact();
                Check(pattern.CurrentReveal > 0 && pattern.InteractiveSegments[0].ContactVisuals.ActivePlayerCount > 0, "idle still supports local effects and Core reveal", passed);
                manifestation.BeginDespawn();
                Check(!pattern.InteractionEnabled && pattern.CurrentReveal == 0, "despawn gates at call time before its first animation frame", passed);
                Check(pattern.InteractiveSegments[0].ContactVisuals.ActivePlayerCount == 0, "leaving play clears transient contact history", passed);
                pattern.PreviewImpact(); pattern.PreviewLocalContact(false);
                Check(pattern.CurrentReveal == 0 && pattern.InteractiveSegments[0].ContactVisuals.ActivePlayerCount == 0, "preview callbacks cannot resurrect hidden gameplay feedback", passed);
                manifestation.Advance(1.1f);
                Check(manifestation.IsHidden && !manifestation.IsTransitioning && !pattern.InteractionEnabled, "despawn reaches stable hidden state", passed);
                manifestation.SetNormalizedProgress(.5f); float held = manifestation.NormalizedProgress; manifestation.Advance(10f);
                Check(manifestation.NormalizedProgress == held && !pattern.InteractionEnabled, "preview scrub holds and remains non-interacting", passed);
                manifestation.SetImmediate(false); pattern.Rebuild();
                Check(!pattern.InteractionEnabled && root.GetComponentsInChildren<Collider>(true).All(c => !c.enabled)
                    && root.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "hidden rebuild retains safety gate and zero-size pose", passed);
                Check(pattern.ParticleCount == count && pattern.GridSampleCount == gridSamples, "rebuild preserves authored budgets and field samples", passed);
                manifestation.spawnSeconds = 0; manifestation.BeginSpawn();
                Check(manifestation.IsIdle && pattern.InteractionEnabled, "zero-duration spawn is deterministic", passed);
                manifestation.despawnSeconds = 0; manifestation.BeginDespawn();
                Check(manifestation.IsHidden && !pattern.InteractionEnabled, "zero-duration despawn is deterministic", passed);
                for (int i = 0; i < 4; i++) { manifestation.BeginSpawn(); manifestation.BeginDespawn(); }
                Check(pattern.ParticleCount == count && root.transform.childCount == 1, "repeated cycles do not duplicate pools or generated roots", passed);
                root.SetActive(false); root.SetActive(true); pattern.Rebuild();
                Check(manifestation.IsHidden && !pattern.InteractionEnabled && root.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "disable and reenable retains hidden authority state", passed);
                manifestation.SetImmediate(true);
                Check(JsonUtility.ToJson(pattern) == settings && JsonUtility.ToJson(definition) == definitionSettings, "lifecycle never mutates authored renderer or pattern settings", passed);
                Check(ResonanceManifestation.EasedProgress(0, .2f) == 0 && ResonanceManifestation.EasedProgress(1, 4f) == 1,
                    "all easing powers preserve exact endpoints", passed);
                pattern.rendering = ResonanceRendering.OptionAContinuous; pattern.Rebuild(); manifestation.SetImmediate(false);
                Check(root.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled) && !pattern.InteractionEnabled, "Option A supports safe hide without a particle conversion", passed);
                manifestation.SetImmediate(true);
                Check(root.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled) && pattern.InteractionEnabled, "Option A returns to its authored idle rendering", passed);
                return passed.Count + " formation lifecycle checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(definition); EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static string RunGpu()
        {
            var passed = new List<string>(); var scene = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject root = null, cameraObject = null;
            try
            {
                definition.Set345HzDefaults(); definition.showCenter = false;
                definition.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = -30, sweepDegrees = 60, thickness = .45f } };
                root = new GameObject("Formation GPU validation"); SceneManager.MoveGameObjectToScene(root, scene);
                var pattern = root.AddComponent<ResonancePatternController>(); pattern.definition = definition;
                pattern.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Particles.mat");
                pattern.rendering = ResonanceRendering.OptionBParticles;
                pattern.particles.particleBudget = 6000;
                pattern.particles.filament.dotSize = pattern.particles.ribbon.dotSize = pattern.particles.diffuse.dotSize = .1f;
                pattern.Rebuild();
                // Equal-depth transparent layers can reorder when property blocks change.
                // Fix only the fixture draw order so this checks shader pixels, not sort ties.
                var sortedLayers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                for (int i = 0; i < sortedLayers.Length; i++) sortedLayers[i].sortingOrder = i;
                cameraObject = new GameObject("Formation test camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 2.5f; camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = camera.allowMSAA = false;
                cameraObject.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
                Color32[] baseline = Render(pattern, camera);
                Check(LitPixels(baseline) > 20, "particle idle GPU image is non-blank", passed);
                var manifestation = root.AddComponent<ResonanceManifestation>();
                manifestation.vibrationStrength = 0; manifestation.SetArea(Vector2.zero, new Vector2(2, 2));
                manifestation.SetImmediate(true);
                Color32[] idle = Render(pattern, camera);
                Check(baseline.SequenceEqual(idle), "explicit idle matches unmanaged GPU pixels exactly", passed);
                manifestation.SetNormalizedProgress(.4f); Color32[] forming = Render(pattern, camera);
                Check(LitPixels(forming, true) > LitPixels(idle, true) + 10, "forming grains disperse beyond the home arc into the field", passed);
                var area = new Vector4(0, 0, 2, 2);
                pattern.SetManifestation(new Vector4(.25f, .25f, 0, 0), area); Color32[] small = Render(pattern, camera);
                pattern.SetManifestation(new Vector4(.25f, 1f, 0, 0), area); Color32[] large = Render(pattern, camera);
                Check(LitPixels(large) > LitPixels(small) * 1.5f, "GPU manifestation scales actual billboard size, not just opacity", passed);
                manifestation.SetImmediate(false);
                Check(LitPixels(Render(pattern, camera)) == 0, "hidden formation produces zero visible pixels", passed);
                Check(ShaderUtil.GetShaderMessages(pattern.particleMaterial.shader).Length == 0, "formation shader compiles without messages", passed);
                return passed.Count + " formation GPU checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(definition); EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static Color32[] Render(ResonancePatternController pattern, Camera camera)
        {
            var originals = new List<ParticleSystemRenderer>();
            var originalProperties = new List<MaterialPropertyBlock>();
            var rt = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = rt;
                foreach (var renderer in pattern.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (!renderer.enabled) continue;
                    var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original);
                    originals.Add(renderer); originalProperties.Add(original);
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    block.SetFloat("_ParticleClock", 1.234f);
                    block.SetFloat("_ResonanceTime", 1.234f); block.SetFloat("_ContactNoiseTime", 1.234f);
                    // Render the real Particle System buffers. A mesh stand-in would hide a
                    // regression in Edit Mode native custom-stream initialization.
                    renderer.SetPropertyBlock(block);
                }
                camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); texture.Apply(); return texture.GetPixels32();
            }
            finally
            {
                for (int i = 0; i < originals.Count; i++)
                    if (originals[i] != null) originals[i].SetPropertyBlock(originalProperties[i]);
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        private static int LitPixels(Color32[] pixels, bool leftOnly = false)
        {
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
                if ((!leftOnly || i % 256 < 110) && Math.Max(pixels[i].r, Math.Max(pixels[i].g, pixels[i].b)) > 12) count++;
            return count;
        }
        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .0001f; }
        private static void Check(bool condition, string name, List<string> passed)
        { if (!condition) throw new InvalidOperationException("Formation check failed: " + name); passed.Add(name); }
    }
}
