#if UNITY_EDITOR
using System;
using Massive.Resonance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    public static class SingularityResonanceGridValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Front Resonance Grid Field")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, label) => {
                if (!ok) throw new InvalidOperationException("SINGULARITY Resonance grid: " + label);
                checks++;
            };
            Scene scene = EditorSceneManager.NewPreviewScene();
            ResonancePatternDefinition definition = null;
            try
            {
                Vector4[] samples = { new Vector4(0f, 5f, 2f, .8f) };
                Func<Vector2, Vector2> field = p => SingularityGridRenderer.EvaluateResonanceOffset(p,
                    samples, samples.Length, 10f, 28f, .8f, .8f, .5f, .65f);
                Vector2 a = field(new Vector2(1f, 5f)), b = field(new Vector2(-1f, 5f));
                check(a.x < 0f && Mathf.Abs(a.y) < .00001f, "front field attracts toward the arc sample");
                check((a + b).sqrMagnitude < .00000001f, "opposite sides of a sample are symmetric");
                check(field(new Vector2(0f, 5f)) == Vector2.zero, "sample center stays finite and still");
                check(field(new Vector2(2f, 5f)) == Vector2.zero, "radius edge is exactly zero");
                check(field(new Vector2(1.9999f, 5f)).magnitude < .00001f, "outer feather reaches zero smoothly");
                check(field(new Vector2(.0001f, 5f)).magnitude < .00001f, "inner softening removes the direction reversal spike");
                check(field(new Vector2(0f, 0f)) == Vector2.zero && field(new Vector2(0f, 10f)) == Vector2.zero,
                    "flat-to-fold seams are pinned");
                check(field(new Vector2(1f, 15f)) == Vector2.zero && field(new Vector2(1f, -.5f)) == Vector2.zero,
                    "rear face and either fold receive no front-plane field");
                samples[0] = new Vector4(0f, .2f, 2f, .8f);
                check(field(new Vector2(.1f, .0001f)).magnitude < .00001f, "field fades continuously before the fold");
                samples[0] = new Vector4(13.5f, 5f, 2f, .8f);
                check(field(new Vector2(14f, 5f)) == Vector2.zero, "hard side boundary stays pinned");
                check(field(new Vector2(13.9999f, 5f)).magnitude < .00001f, "hard boundary feather is continuous");
                samples[0] = new Vector4(0f, 5f, 2f, 1000000f);
                check(field(new Vector2(1f, 5f)).magnitude <= .65f, "extreme force remains softly bounded");
                Vector4[] symmetric = { new Vector4(-.8f, 5f, 2f, 2f), new Vector4(.8f, 5f, 2f, 2f) };
                check(SingularityGridRenderer.EvaluateResonanceOffset(new Vector2(0f, 5f), symmetric, 2,
                    10f, 28f, .8f, .8f, .5f, .65f) == Vector2.zero, "overlapping arc pulls are order-independent and cancel symmetrically");

                var root = new GameObject("Resonance field test"); SceneManager.MoveGameObjectToScene(root, scene);
                var surface = root.AddComponent<SingularitySurface>(); surface.Configure(28f, 10f, .875f, 3f, 2.1f);
                var grid = root.AddComponent<SingularityGridRenderer>(); grid.surface = surface; grid.showPlayerAttraction = false;
                var patternObject = new GameObject("Test pattern"); SceneManager.MoveGameObjectToScene(patternObject, scene);
                var pattern = patternObject.AddComponent<ResonancePatternController>();
                definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>(); definition.Set345HzDefaults();
                pattern.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
                pattern.definition = definition; pattern.attractGrid = true; pattern.gridEnergyPulse = 0f; pattern.Rebuild();
                var destination = new ResonancePatternController.GridAttractionSample[128];
                int count = pattern.CopyGridAttractionSamples(destination, 0f);
                check(count == pattern.GridSampleCount && count > 0 && count <= 128, "copy uses the controller's bounded exposed-arc population");
                check(pattern.CopyGridAttractionSamples(new ResonancePatternController.GridAttractionSample[1], 0f) == 1,
                    "small destination never overflows");
                check(pattern.CopyGridAttractionSamples(null, 0f) == 0, "null storage is safe");
                var sample = destination[0]; pattern.gridAttractionStrength *= 2f;
                pattern.CopyGridAttractionSamples(destination, 0f);
                check(Mathf.Abs(destination[0].Strength - sample.Strength * 2f) < .00001f && destination[0].Radius == sample.Radius,
                    "live pattern strength controls the copied field without rebuilding");
                pattern.SetInteractionEnabled(false);
                check(pattern.CopyGridAttractionSamples(destination, 0f) == 0, "formation/dissolve interaction gate removes attraction");
                pattern.SetInteractionEnabled(true); grid.SetResonancePattern(pattern); grid.Rebuild();
                check(grid.ActiveResonancePattern == pattern && grid.ActiveResonanceSampleCount == count,
                    "explicit same-scene binding uploads the live field");
                var block = new MaterialPropertyBlock(); grid.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                check(block.GetInt("_SurfaceResonanceCount") == count, "shader receives exactly the active sample count");
                Vector4 profile = block.GetVector("_SurfaceResonanceShape");
                check(profile.x == surface.FrontHeight && profile.z == pattern.gridOuterFeather && profile.w == pattern.gridInnerSoftening,
                    "shader reuses authored feather and softening controls");
                pattern.transform.position = new Vector3(0f, -surface.Depth, 0f); grid.RefreshPresentation();
                check(grid.ActiveResonanceSampleCount == 0, "rear-height pattern is rejected despite identical projected XZ");
                pattern.transform.position = Vector3.zero; pattern.SetInteractionEnabled(false); grid.RefreshPresentation();
                check(grid.ActiveResonanceSampleCount == 0, "renderer clears samples during dissolve");
                pattern.SetInteractionEnabled(true); grid.SetResonancePattern(null); grid.RefreshPresentation();
                check(grid.ActiveResonanceSampleCount == 0 && grid.ActiveResonancePattern == null, "retired explicit binding leaves no stale field");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            }
            return "SINGULARITY Resonance grid: " + checks + " sample, front-face isolation, gate and shader-property checks passed.";
        }
    }
}
#endif
