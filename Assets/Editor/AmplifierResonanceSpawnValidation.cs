using System;
using System.Collections.Generic;
using System.Reflection;
using Massive.Resonance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier.Editor
{
    /// <summary>Isolated editor checks for pair timing, ordering, footprint and cleanup.
    /// Runtime capture/match timing must additionally be exercised in Play Mode.</summary>
    public static class AmplifierResonanceSpawnValidation
    {
        [MenuItem("MASSIVE/Amplifier/Validate Pair Spawn Rules")]
        public static void ValidateMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run pair rule validation outside Play Mode.");
            int passed = 0;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(-2, 2), 0), 6), "delay lower endpoint", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(-2, 2), 1), 10), "delay upper endpoint", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(-2, 2), .5), 8), "delay midpoint", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(2, -2), .25), 7), "reversed variation endpoints normalized", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(1, new Vector2(-4, -2), .5), 0), "negative final delay clamped", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(-2, 2), -100), 6), "random sample clamped below zero", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(-2, 2), 100), 10), "random sample clamped above one", ref passed);
                Check(Near(AmplifierResonanceSpawner.SampleDelay(8, Vector2.zero, .37), 8), "zero variation constant delay", ref passed);
                Check(Finite(AmplifierResonanceSpawner.SampleDelay(float.NaN, Vector2.zero, .5)), "NaN delay sanitized", ref passed);
                Check(Finite(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(float.PositiveInfinity, float.NaN), .5)), "non-finite variation sanitized", ref passed);
                Check(Finite(AmplifierResonanceSpawner.SampleDelay(8, new Vector2(-2, 2), double.NaN)), "NaN random input sanitized", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(24, 30, 5), 0), "surface stays calm before final five seconds", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(25, 30, 5), 0), "warning begins at twenty-five seconds", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(27.5f, 30, 5), .5f), "surface instability builds during warning", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(35, 30, 5), 1), "late warning saturates", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(99, 0, 5), 0), "disabled time-out has no warning", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(29, 30, 0), 0), "zero warning window disables cue", ref passed);
                Check(Near(AmplifierResonanceSpawner.EvaluateTimeoutWarning(1, 2, 5), .5f), "warning window never exceeds lifetime", ref passed);

                var root = new GameObject("Amplifier pair rules validation");
                SceneManager.MoveGameObjectToScene(root, scene);
                var spawner = root.AddComponent<AmplifierResonanceSpawner>();
                Check(Near(spawner.maximumActiveSeconds, 30) && Near(spawner.timeoutWarningSeconds, 5), "new sequence defaults to thirty seconds with five-second warning", ref passed);
                spawner.startAutomatically = false; spawner.spawnRegion = root.GetComponent<AmplifierSpawnRegion>();
                spawner.spawnRegion.drawZones = false;
                Check(!spawner.StartCycle() && spawner.Phase == AmplifierEncounterPhase.Stopped,
                    "edit-mode Start Cycle refuses runtime side effects", ref passed);

                var warningObject = Child(root, "Surface warning fixture"); warningObject.SetActive(false);
                var warningRenderer = warningObject.AddComponent<MeshRenderer>();
                var warningVisual = warningObject.AddComponent<AmplifierCoreVisual>();
                var warningShader = Shader.Find("MASSIVE/Amplifier Core/Neutral Shell");
                Check(warningShader != null, "Core shell shader available for warning", ref passed);
                var warningMaterial = new Material(warningShader) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    warningRenderer.sharedMaterial = warningMaterial;
                    var properties = new SerializedObject(warningVisual);
                    properties.FindProperty("shellRenderer").objectReferenceValue = warningRenderer;
                    properties.ApplyModifiedPropertiesWithoutUndo();
                    warningVisual.SetTimeoutWarning(.75f);
                    warningVisual.SetExcitement(1f); warningVisual.SetExcitement(0f);
                    warningVisual.SetLifecycleReveal(1f, 1f);
                    var block = new MaterialPropertyBlock(); warningRenderer.GetPropertyBlock(block);
                    Check(Near(warningVisual.TimeoutWarning01, .75f) && Near(block.GetFloat("_TimeoutWarning"), .75f),
                        "attraction excitement and reveal cannot erase surface warning", ref passed);
                    Check(Near(warningMaterial.GetFloat("_TimeoutWarning"), 0f), "warning does not mutate shared material", ref passed);
                    warningVisual.RestorePresentation(); warningRenderer.GetPropertyBlock(block);
                    Check(Near(warningVisual.TimeoutWarning01, 0) && Near(block.GetFloat("_TimeoutWarning"), 0),
                        "presentation reset clears warning state and shader property", ref passed);
                }
                finally { UnityEngine.Object.DestroyImmediate(warningMaterial); }

                var actualCore = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab");
                Check(actualCore != null && actualCore.GetComponent<AmplifierCoreGameplay>() != null, "actual gameplay Core prefab available", ref passed);
                var corePrefab = actualCore.GetComponent<AmplifierCoreGameplay>();
                string originalCore = EditorJsonUtility.ToJson(corePrefab);
                float expectedCoreRadius = .75f * corePrefab.PendingScaleRatio;
                Check(Near(AmplifierResonanceSpawner.EstimateCoreRadius(corePrefab), expectedCoreRadius), "actual Core sphere includes effective global scale without AABB inflation", ref passed);
                spawner.corePrefab = corePrefab; spawner.minimumCoreRadius = .9f;
                Check(Near(spawner.CorePlacementRadius, Mathf.Max(.9f, expectedCoreRadius)), "minimum radius is a floor", ref passed);
                spawner.minimumCoreRadius = .1f;
                Check(Near(spawner.CorePlacementRadius, Mathf.Max(.1f, expectedCoreRadius)), "effective collider prevents unsafe radius override", ref passed);
                Check(AmplifierResonanceSpawner.EstimateCoreRadius(null) == 0f, "null Core radius is zero", ref passed);

                var footprintObject = Child(root, "Footprint fixture"); footprintObject.SetActive(false);
                footprintObject.AddComponent<Rigidbody>();
                var sphere = footprintObject.AddComponent<SphereCollider>(); sphere.radius = .75f;
                var footprint = footprintObject.AddComponent<AmplifierCoreGameplay>();
                footprintObject.transform.localScale = new Vector3(1, 4, 1);
                Check(Near(AmplifierResonanceSpawner.EstimateCoreRadius(footprint), 3f), "sphere largest Y scale retained in planar radius", ref passed);
                footprintObject.transform.localScale = Vector3.one; sphere.center = new Vector3(1, 0, 0);
                Check(Near(AmplifierResonanceSpawner.EstimateCoreRadius(footprint), 1.75f), "off-center sphere footprint", ref passed);
                sphere.enabled = false;
                Check(Near(AmplifierResonanceSpawner.EstimateCoreRadius(footprint), 0), "disabled colliders excluded", ref passed);
                sphere.enabled = true; sphere.isTrigger = true;
                Check(Near(AmplifierResonanceSpawner.EstimateCoreRadius(footprint), 0), "trigger-only decorative collider excluded", ref passed);
                sphere.enabled = false;
                var capsule = footprintObject.AddComponent<CapsuleCollider>(); capsule.direction = 0; capsule.radius = .5f; capsule.height = 1f;
                footprintObject.transform.localScale = new Vector3(1, 3, 1);
                Check(AmplifierResonanceSpawner.EstimateCoreRadius(footprint) >= 1.5f - .001f,
                    "short capsule radial scaling cannot underestimate physics sphere", ref passed);
                capsule.enabled = false; footprintObject.transform.localScale = Vector3.one;
                var box = footprintObject.AddComponent<BoxCollider>(); box.center = new Vector3(2, 0, 0); box.size = new Vector3(2, 1, 2);
                Check(AmplifierResonanceSpawner.EstimateCoreRadius(footprint) >= Mathf.Sqrt(10f) - .001f, "box offset and corners covered", ref passed);

                var assetA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Resonance 345 Hz.prefab");
                var assetB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Resonance 345 Hz - Option B.prefab");
                var optionA = assetA != null ? assetA.GetComponent<ResonancePatternController>() : null;
                var optionB = assetB != null ? assetB.GetComponent<ResonancePatternController>() : null;
                Check(optionA != null && optionB != null && optionA.definition != null && optionB.definition != null,
                    "both authored pattern assets are configured", ref passed);
                string originalA = EditorJsonUtility.ToJson(optionA), originalB = EditorJsonUtility.ToJson(optionB);
                spawner.patternOrder = new List<ResonanceSpawnEntry> {
                    null, new ResonanceSpawnEntry(), new ResonanceSpawnEntry { label = "B first", patternPrefab = optionB },
                    new ResonanceSpawnEntry { label = "A second", patternPrefab = optionA }
                };
                SetField(spawner, "nextIndex", 0); spawner.loopPatternOrder = false;
                ResonancePatternController selected;
                Check(Select(spawner, out selected) && selected == optionB && spawner.CurrentPatternIndex == 2,
                    "ordered selection skips null entry and missing prefab", ref passed);
                Check(Select(spawner, out selected) && selected == optionA && spawner.CurrentPatternIndex == 3,
                    "ordered selection advances once", ref passed);
                Check(!Select(spawner, out selected), "non-looping order ends", ref passed);
                spawner.loopPatternOrder = true;
                Check(Select(spawner, out selected) && selected == optionB && spawner.CurrentPatternIndex == 2,
                    "looping order wraps and skips blanks", ref passed);
                SetField(spawner, "nextIndex", 3);
                Check(Select(spawner, out selected) && selected == optionA, "configured starting index honored", ref passed);
                spawner.patternOrder = new List<ResonanceSpawnEntry> { null, new ResonanceSpawnEntry() };
                SetField(spawner, "nextIndex", 0);
                Check(!Select(spawner, out selected), "all-invalid looping order terminates bounded search", ref passed);
                spawner.patternOrder = null;
                Check(!Select(spawner, out selected), "missing order is safe", ref passed);
                var invalidPatternObject = Child(root, "Unconfigured pattern"); invalidPatternObject.SetActive(false);
                var invalidPattern = invalidPatternObject.AddComponent<ResonancePatternController>();
                spawner.patternOrder = new List<ResonanceSpawnEntry> {
                    new ResonanceSpawnEntry { patternPrefab = invalidPattern },
                    new ResonanceSpawnEntry { patternPrefab = optionB }
                };
                SetField(spawner, "nextIndex", 0);
                Check(Select(spawner, out selected) && selected == optionB && spawner.CurrentPatternIndex == 1,
                    "missing pattern definition skipped", ref passed);

                // Build no rendering assets or actual managed pair. Arrange ownership then
                // exercise the same public cleanup path used by Stop/disable.
                var demoA = Child(root, "Standalone A"); var demoB = Child(root, "Standalone B"); demoB.SetActive(false);
                spawner.standaloneExamples = new[] { demoA, demoB, demoA, root };
                Invoke(spawner, "SuppressExamples");
                Check(!demoA.activeSelf && !demoB.activeSelf && root.activeSelf, "only explicitly assigned examples suppressed; self protected", ref passed);
                Invoke(spawner, "SuppressExamples");
                spawner.StopCycle();
                Check(demoA.activeSelf && !demoB.activeSelf, "example active states restored after repeated suppression", ref passed);
                var owned = Child(root, "Owned pair to clean");
                var ownedCoreObject = Child(owned, "Owned Core"); ownedCoreObject.SetActive(false);
                ownedCoreObject.AddComponent<Rigidbody>(); ownedCoreObject.AddComponent<SphereCollider>();
                var ownedCore = ownedCoreObject.AddComponent<AmplifierCoreGameplay>();
                SetField(spawner, "ownedRoot", owned);
                SetProperty(spawner, "ActiveCore", ownedCore);
                SetField(spawner, "running", true);
                spawner.StopCycle();
                Check(owned == null && spawner.ActiveCore == null && spawner.ActivePattern == null && spawner.ActiveManifestation == null,
                    "Stop removes only owned fixture and clears references", ref passed);
                Check(demoA != null && demoB != null && spawner.Phase == AmplifierEncounterPhase.Stopped,
                    "Stop retains standalone examples", ref passed);
                Invoke(spawner, "SuppressExamples");
                owned = Child(root, "Disabled owner cleanup"); SetField(spawner, "ownedRoot", owned);
                Invoke(spawner, "OnDisable");
                Check(owned == null && demoA.activeSelf && !demoB.activeSelf && spawner.Phase == AmplifierEncounterPhase.Stopped,
                    "disable path cleans ownership and restores examples", ref passed);
                Invoke(spawner, "OnDisable");
                Check(demoA.activeSelf && !demoB.activeSelf, "repeated disable cleanup idempotent", ref passed);

                Check(EditorJsonUtility.ToJson(corePrefab) == originalCore && EditorJsonUtility.ToJson(optionA) == originalA && EditorJsonUtility.ToJson(optionB) == originalB,
                    "original Core and pattern assets untouched", ref passed);
                return passed + " Amplifier/Resonance spawn-rule checks passed (editor fixtures; runtime capture validation separate).";
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject Child(GameObject parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent.transform, false); return go; }
        private static bool Select(AmplifierResonanceSpawner spawner, out ResonancePatternController selected)
        {
            object[] arguments = { null };
            bool result = (bool)Method("SelectNextPattern").Invoke(spawner, arguments);
            selected = arguments[0] as ResonancePatternController; return result;
        }
        private static MethodInfo Method(string name)
        { return typeof(AmplifierResonanceSpawner).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(name); }
        private static void Invoke(AmplifierResonanceSpawner value, string name) { Method(name).Invoke(value, null); }
        private static void SetField(AmplifierResonanceSpawner value, string name, object setting)
        { typeof(AmplifierResonanceSpawner).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, setting); }
        private static void SetProperty(AmplifierResonanceSpawner value, string name, object setting)
        { typeof(AmplifierResonanceSpawner).GetProperty(name).GetSetMethod(true).Invoke(value, new[] { setting }); }
        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .001f; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void Check(bool condition, string label, ref int passed)
        { if (!condition) throw new InvalidOperationException("Amplifier pair validation failed: " + label); passed++; }
    }
}
