using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Resonance.Editor
{
    /// <summary>Real Edit Mode Particle System GPU checks in an isolated, disposable scene.</summary>
    public static class ResonanceLocalBirthValidation
    {
        private const int Resolution = 256;

        [MenuItem("MASSIVE/Resonance/Validate Local Grain Birth")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var passed = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject root = null, cameraObject = null, copyObject = null;
            try
            {
                definition.Set345HzDefaults(); definition.showCenter = false;
                definition.arcs = new List<ResonanceArc> {
                    new ResonanceArc { startDegrees = -30, sweepDegrees = 60, thickness = .3f }
                };
                root = new GameObject("Local grain birth validation");
                SceneManager.MoveGameObjectToScene(root, scene);
                var pattern = root.AddComponent<ResonancePatternController>();
                pattern.definition = definition;
                pattern.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Particles.mat");
                pattern.rendering = ResonanceRendering.OptionBParticles;
                pattern.particles.particleBudget = 6000;
                pattern.particles.filament.dotSize = pattern.particles.ribbon.dotSize = pattern.particles.diffuse.dotSize = .09f;
                pattern.Rebuild();
                var generated = pattern.GeneratedRoot;
                var pools = root.GetComponentsInChildren<ParticleSystem>(true);
                int count = pattern.ParticleCount;
                var first = root.GetComponentInChildren<ParticleSystemRenderer>();
                // Coplanar transparent populations otherwise tie in Unity's renderer sort.
                // Updating property blocks can reorder a tie without changing any glyph/grain
                // math. Define a stable fixture draw order for exact pixel comparisons.
                var sortedLayers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                for (int i = 0; i < sortedLayers.Length; i++) sortedLayers[i].sortingOrder = i;

                cameraObject = new GameObject("Local grain test camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 2.5f; camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = camera.allowMSAA = false;
                cameraObject.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));

                var area = new Vector4(0, 0, 2.2f, 2.2f);
                var local = new Vector4(1, .15f, .25f, .9f);
                var field = new Vector4(2, 0, 0, 0);
                Color32[] baseline = Render(pattern, camera);
                Check(LitPixels(baseline) > 30, "native Edit Mode particle streams render a nonblank idle image", passed);
                pattern.SetManifestation(new Vector4(1, 1, 0, .375f), area, local, new Vector4(2, 1, 0, 0));
                CheckSamePixels(baseline, Render(pattern, camera), "local mode and maximum birth pulse preserve exact idle pixels", passed);

                var pose = new Vector4(.15f, .85f, 0, .25f);
                pattern.SetManifestation(pose, area, Vector4.zero, field);
                Color32[] fullField = Render(pattern, camera);
                pattern.SetManifestation(pose, area, local, field);
                Color32[] localBand = Render(pattern, camera);
                Check(LitPixels(localBand) > 20 && LitPixels(fullField, true) > LitPixels(localBand, true) + 20,
                    "local birth stays beside the arc while legacy field birth reaches the opposite half", passed);

                // Both reference and test render a scaled billboard, avoiding the different
                // operation order of the optimized idle branch when comparing translation.
                var noOffset = new Vector4(1, 0, .25f, 1);
                pattern.SetManifestation(new Vector4(1, .7f, 0, .25f), area, noOffset, field);
                Color32[] sizeReference = Render(pattern, camera);
                pattern.SetManifestation(new Vector4(.2f, .7f, 0, .25f), area, noOffset, field);
                CheckSamePixels(sizeReference, Render(pattern, camera),
                    "zero spawn spread and zero vibration introduce no translation at equal billboard size", passed);

                pose = new Vector4(.25f, 1, .18f, .125f);
                local = new Vector4(1, .2f, .25f, 0);
                pattern.SetManifestation(pose, area, local, field);
                Color32[] individual = Render(pattern, camera);
                local.w = 1;
                pattern.SetManifestation(pose, area, local, field);
                Color32[] coherent = Render(pattern, camera);
                Check(DifferentPixels(individual, coherent) > 30, "coherence changes real grain motion from jitter to shared field vibration", passed);
                pose.w = .625f;
                pattern.SetManifestation(pose, area, local, field);
                Check(DifferentPixels(coherent, Render(pattern, camera)) > 30,
                    "coherent standing-wave grains move between opposite vibration phases", passed);

                pose = new Vector4(.25f, 1, 0, 0);
                pattern.SetManifestation(pose, area, local, field);
                int ordinarySize = LitPixels(Render(pattern, camera));
                pattern.SetManifestation(pose, area, local, new Vector4(2, .7f, 0, 0));
                Check(LitPixels(Render(pattern, camera)) < ordinarySize,
                    "birth pulse contracts actual grain footprints below their authored size", passed);

                local.y = .1f;
                pattern.SetManifestation(pose, area, local, field);
                Bounds closeBounds = first.localBounds;
                local.y = 1.2f;
                pattern.SetManifestation(pose, area, local, field);
                Bounds widerBounds = first.localBounds;
                Check(widerBounds.extents.x >= closeBounds.extents.x + 1.09f
                    && widerBounds.extents.z >= closeBounds.extents.z + 1.09f,
                    "changing local spawn radius expands culling bounds without a rebuild", passed);
                local.y = .1f;
                pattern.SetManifestation(pose, area, local, field);
                Check((first.localBounds.size - closeBounds.size).sqrMagnitude < .000001f,
                    "reducing local spawn radius restores the smaller culling envelope", passed);

                var manifestation = root.AddComponent<ResonanceManifestation>();
                manifestation.birthDistribution = ResonanceBirthDistribution.LocalBand;
                manifestation.localSpawnSpread = .42f; manifestation.alongArcSpread = .31f;
                manifestation.fieldCoherence = .76f; manifestation.fieldWavelength = 1.73f;
                manifestation.birthPulse = .19f;
                manifestation.SetNormalizedProgress(.4f);
                Check(!pattern.InteractionEnabled && root.GetComponentsInChildren<Collider>(true).All(c => !c.enabled),
                    "local formation keeps gameplay collision gates off", passed);
                manifestation.SetImmediate(false);
                Check(LitPixels(Render(pattern, camera)) == 0, "hidden local birth produces zero visible GPU pixels", passed);
                manifestation.SetImmediate(true);
                Check(pattern.InteractionEnabled, "returning to idle restores interactions", passed);
                CheckSamePixels(baseline, Render(pattern, camera),
                    "returning to idle restores the unchanged native GPU picture", passed);
                Check(pattern.GeneratedRoot == generated && pattern.ParticleCount == count
                    && pools.SequenceEqual(root.GetComponentsInChildren<ParticleSystem>(true))
                    && pools.All(p => p.particleCount > 0),
                    "mode, phase, and radius changes preserve all native particle pools and population", passed);

                copyObject = new GameObject("Local birth tuning copy");
                SceneManager.MoveGameObjectToScene(copyObject, scene);
                var copy = copyObject.AddComponent<ResonanceManifestation>();
                copy.CopyAnimationSettingsFrom(manifestation);
                Check(copy.birthDistribution == manifestation.birthDistribution
                    && copy.localSpawnSpread == manifestation.localSpawnSpread && copy.alongArcSpread == manifestation.alongArcSpread
                    && copy.fieldCoherence == manifestation.fieldCoherence && copy.fieldWavelength == manifestation.fieldWavelength
                    && copy.birthPulse == manifestation.birthPulse,
                    "animation tuning copy includes all six local birth controls", passed);
                Check(ShaderUtil.GetShaderMessages(pattern.particleMaterial.shader).Length == 0,
                    "local grain shader reports no compilation messages", passed);
                return passed.Count + " local grain birth checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (copyObject != null) UnityEngine.Object.DestroyImmediate(copyObject);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(definition);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Color32[] Render(ResonancePatternController pattern, Camera camera)
        {
            var originals = new List<ParticleSystemRenderer>();
            var properties = new List<MaterialPropertyBlock>();
            var target = RenderTexture.GetTemporary(Resolution, Resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                foreach (var renderer in pattern.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (!renderer.enabled) continue;
                    var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original);
                    originals.Add(renderer); properties.Add(original);
                    var frozen = new MaterialPropertyBlock(); renderer.GetPropertyBlock(frozen);
                    frozen.SetFloat("_ParticleClock", 1.234f);
                    frozen.SetFloat("_ResonanceTime", 1.234f); frozen.SetFloat("_ContactNoiseTime", 1.234f);
                    renderer.SetPropertyBlock(frozen);
                }
                // No mesh substitute: this exercises the real native Particle System stream.
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0); texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                for (int i = 0; i < originals.Count; i++)
                    if (originals[i] != null) originals[i].SetPropertyBlock(properties[i]);
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static int LitPixels(Color32[] pixels, bool oppositeHalf = false)
        {
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
                if ((!oppositeHalf || i % Resolution < 110) && Math.Max(pixels[i].r, Math.Max(pixels[i].g, pixels[i].b)) > 12) count++;
            return count;
        }
        private static int DifferentPixels(Color32[] a, Color32[] b)
        { int count = 0; for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) count++; return count; }
        private static void CheckSamePixels(Color32[] a, Color32[] b, string name, List<string> passed)
        {
            int different = 0, largest = 0; long total = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i])) different++;
                int delta = Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g)
                    + Math.Abs(a[i].b - b[i].b) + Math.Abs(a[i].a - b[i].a);
                largest = Math.Max(largest, delta); total += delta;
            }
            if (different != 0) throw new InvalidOperationException("Local grain birth check failed: " + name
                + " (different pixels=" + different + ", total channel error=" + total + ", max pixel error=" + largest + ")");
            passed.Add(name);
        }
        private static void Check(bool condition, string name, List<string> passed)
        { if (!condition) throw new InvalidOperationException("Local grain birth check failed: " + name); passed.Add(name); }
    }
}
