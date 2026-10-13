#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Lattice.EditorTools
{
    // Compare the optimized search with the original exhaustive distance oracle.
    public static class LatticeFieldOptimizationValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Output = "Library/LatticeFieldOptimization";
        static readonly Type FieldType = typeof(LatticeDisruptionField);
        static readonly FieldInfo Clock = FieldType.GetField("clock", Private);
        static readonly FieldInfo Revision = FieldType.GetField("contourRevision", Private);
        static readonly FieldInfo Lines = FieldType.GetField("boundaryLines", Private);
        static int nodeChecks, cases;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/S-1_LATTICE.unity");
            Run();
        }

        [MenuItem("MASSIVE/LATTICE/Validate field optimization")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run field validation in Edit Mode.");
            var installed = Object.FindFirstObjectByType<LatticeDisruptionField>();
            if (!installed) throw new InvalidOperationException("Open the LATTICE scene first.");
            var sourceGrid = installed.GetComponent<VectorGridGPU>();
            var previousDefault = VectorGridGPU.Instance;
            Directory.CreateDirectory(Output);
            nodeChecks = cases = 0;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var layout in new[] { new Vector3(28, 12, 1), new Vector3(9.1f, 5.3f, .37f), new Vector3(20, 8, 1.25f) })
                {
                    var go = new GameObject("LATTICE bounded distance validation");
                    go.SetActive(false); SceneManager.MoveGameObjectToScene(go, scene);
                    try
                    {
                        var grid = go.AddComponent<VectorGridGPU>();
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceGrid), grid);
                        grid.registerAsDefault = false; grid.size = new Vector2(layout.x, layout.y); grid.gridScale = layout.z;
                        var f = go.AddComponent<LatticeDisruptionField>();
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(installed), f);
                        f.animateInEditor = false; f.showNoiseBoundary = false;
                        go.SetActive(true);
                        var ensure = (Func<bool>)FieldType.GetMethod("EnsureResources", Private).CreateDelegate(typeof(Func<bool>), f);
                        if (!ensure()) throw new Exception("Fixture failed to initialize");
                        float[] expected = null; int expectedRevision = -1;
                        foreach (float frequency in new[] { .03f, .19f, 1f })
                        foreach (float depth in new[] { .1f, .75f, 3f, 12f })
                        {
                            f.mode = LatticeDisruptionField.FieldMode.DriftingNoise;
                            f.noiseFrequency = frequency; f.dotJitterDepth = depth; f.threshold = .51f;
                            f.seed = cases % 2 == 0 ? 173 : -29; f.rgbDotJitter = true;
                            f.drift = new Vector2(.09f, .035f); f.evolutionSpeed = .075f;
                            foreach (float time in new[] { 0f, .02f, .061f, 25f, 25f, .1f })
                            {
                                Clock.SetValue(f, time);
                                Verify(f, 0, ref expected, ref expectedRevision);
                            }
                        }
                        foreach (var mode in new[] { LatticeDisruptionField.FieldMode.Connected, LatticeDisruptionField.FieldMode.Disconnected, LatticeDisruptionField.FieldMode.DriftingNoise })
                        foreach (float threshold in new[] { 0f, 1f, .51f })
                        {
                            f.mode = mode; f.threshold = threshold;
                            f.noiseFrequency = .19f; f.dotJitterDepth = 3;
                            Verify(f, .06f, ref expected, ref expectedRevision);
                        }
                        f.mode = LatticeDisruptionField.FieldMode.DriftingNoise;
                        f.threshold = .51f; f.drift = Vector2.zero; f.evolutionSpeed = 0;
                        Verify(f, .06f, ref expected, ref expectedRevision);
                        Verify(f, .1f, ref expected, ref expectedRevision); // stationary contour cache
                        f.rgbDotJitter = false;
                        Verify(f, 0, ref expected, ref expectedRevision);
                        f.rgbDotJitter = true;
                        Verify(f, 0, ref expected, ref expectedRevision);
                        f.enabled = false; f.enabled = true;
                        if (!ensure()) throw new Exception("Disable/enable failed to rebuild");
                        expected = null; expectedRevision = -1;
                        Verify(f, .06f, ref expected, ref expectedRevision);
                        // Resize the real grid and force the same resource path used in editing.
                        grid.size += new Vector2(2, 2); grid.RefreshLayout();
                        if (!ensure()) throw new Exception("Resize failed to rebuild");
                        expected = null; expectedRevision = -1;
                        Verify(f, .06f, ref expected, ref expectedRevision);
                    }
                    finally { Object.DestroyImmediate(go); }
                }
                if (VectorGridGPU.Instance != previousDefault) throw new Exception("Fixture changed the default gameplay grid");
                File.WriteAllText(Output + "/report.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { result = "Passed", cases, nodeChecks, comparison = "Exact float bits against exhaustive pre-optimization distance calculation" }, Newtonsoft.Json.Formatting.Indented));
                Debug.Log($"LATTICE field optimization validation PASSED: {cases} cases, {nodeChecks} exact node comparisons.");
            }
            catch (Exception e)
            {
                File.WriteAllText(Output + "/report.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { result = "Failed", cases, nodeChecks, error = e.ToString() }, Newtonsoft.Json.Formatting.Indented));
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void Verify(LatticeDisruptionField f, float dt, ref float[] expected, ref int expectedRevision)
        {
            f.Advance(dt); cases++;
            int revision = (int)Revision.GetValue(f);
            if (f.rgbDotJitter && (expected == null || expectedRevision != revision))
            {
                expected = ExhaustiveDepths(f); expectedRevision = revision;
            }
            for (int i = 0; i < f.NodeCount; i++)
            {
                f.GetNode(i, out _, out float disruption, out float depth);
                float reference = f.rgbDotJitter ? expected[i] : 0;
                if (BitConverter.SingleToInt32Bits(depth) != BitConverter.SingleToInt32Bits(reference))
                    throw new Exception($"Depth mismatch in case {cases}, node {i}: {depth:R} vs {reference:R}");
                if (!float.IsFinite(disruption) || disruption < 0 || disruption > 1.00001f)
                    throw new Exception("Invalid locomotion state");
                nodeChecks++;
            }
        }

        static float[] ExhaustiveDepths(LatticeDisruptionField f)
        {
            var segments = (List<Vector3>)Lines.GetValue(f);
            var result = new float[f.NodeCount];
            float maximum = Mathf.Max(.1f, f.dotJitterDepth) * f.Spacing;
            for (int i = 0; i < result.Length; i++)
            {
                f.GetNode(i, out Vector2 p, out _, out _);
                if (f.mode == LatticeDisruptionField.FieldMode.Connected ||
                    f.mode == LatticeDisruptionField.FieldMode.DriftingNoise && f.NoiseAt(p, f.FieldTime) <= f.threshold) continue;
                float squared = maximum * maximum;
                for (int j = 0; j < segments.Count; j += 2)
                {
                    Vector2 a = segments[j], span = segments[j + 1] - segments[j];
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, span) / Mathf.Max(.000001f, span.sqrMagnitude));
                    squared = Mathf.Min(squared, (p - a - span * t).sqrMagnitude);
                }
                result[i] = Mathf.Sqrt(squared) / f.Spacing;
            }
            return result;
        }
    }
}
#endif
