using System;
using Massive.Resonance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Multiplier.Editor
{
    [CustomEditor(typeof(AmplifierResonanceSpawner))]
    public sealed class AmplifierResonanceSpawnerEditor : UnityEditor.Editor
    {
        private ReorderableList order;
        private void OnEnable()
        {
            order = new ReorderableList(serializedObject, serializedObject.FindProperty("patternOrder"), true, true, true, true);
            order.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Pattern order — drag rows to reorder");
            order.elementHeight = EditorGUIUtility.singleLineHeight * 2 + 8;
            order.drawElementCallback = (rect, index, active, focused) =>
            {
                var entry = order.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2; rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, entry.FindPropertyRelative("label"), new GUIContent((index + 1) + ". Label"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(rect, entry.FindPropertyRelative("patternPrefab"), new GUIContent("Pattern prefab"));
            };
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "patternOrder");
            order.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            var s = (AmplifierResonanceSpawner)target;
            EditorGUILayout.HelpBox("Formation → safe neutral Core spawn → capture → dissolution → respawn wait → next pattern. Scoring and goal effects remain unchanged. Expand Spawn Region for placement/zone controls; open each pattern prefab for formation settings.", MessageType.Info);
            if (!Application.isPlaying)
            {
                if (GUILayout.Button("Edit / Preview Scene Option B Animation")) AmplifierResonanceSpawnSetup.SelectSceneAnimationPreview(s);
                return;
            }
            EditorGUILayout.LabelField("Phase", s.Phase.ToString());
            EditorGUILayout.HelpBox(s.Status, MessageType.None);
            EditorGUILayout.LabelField("Wait / pairs / captures", s.SecondsRemaining.ToString("0.0") + "s / " + s.PairsSpawned + " / " + s.CapturesObserved);
            if (GUILayout.Button("Restart Cycle")) s.StartCycle();
            using (new EditorGUI.DisabledScope(s.Phase != AmplifierEncounterPhase.Delay))
                if (GUILayout.Button("Skip Respawn Wait")) s.SpawnNow();
            using (new EditorGUI.DisabledScope(s.ActivePattern == null))
            {
                if (GUILayout.Button("Retire Pair / Next Pattern (no score awarded)")) s.RetireCurrentPair();
                if (GUILayout.Button("Select Live Pattern / Formation Controls")) Selection.activeObject = s.ActivePattern.gameObject;
            }
            if (GUILayout.Button("Stop Cycle / Restore Standalone Examples")) s.StopCycle();
            Repaint();
        }
    }

    public static class AmplifierResonanceSpawnSetup
    {
        private const string Folder = "Assets/Resonance/Spawn Patterns";
        [MenuItem("MASSIVE/Resonance/Install Amplifier Spawn Cycle from Current Option B")]
        public static void Install()
        {
            var pair = Object.FindFirstObjectByType<ResonanceRenderComparison>();
            AmplifierCoreGameplay core = null;
            foreach (var item in Object.FindObjectsByType<AmplifierCoreGameplay>(FindObjectsSortMode.None))
                if (!item.IsPresentationOnly)
                {
                    if (core != null) throw new InvalidOperationException("Multiple gameplay Cores are active. Configure an AmplifierResonanceSpawner explicitly instead.");
                    core = item;
                }
            Create(pair != null ? pair.optionB : null, core, pair);
        }
        public static AmplifierResonanceSpawner Create(ResonancePatternController source, AmplifierCoreGameplay core, ResonanceRenderComparison comparison)
        {
            if (Application.isPlaying || source == null || source.definition == null || core == null || core.IsPresentationOnly)
                throw new InvalidOperationException("Use a configured scene pattern and gameplay Core in Edit Mode.");
            foreach (var existing in Object.FindObjectsByType<AmplifierResonanceSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (existing.gameObject.scene == source.gameObject.scene) { Selection.activeObject = existing.gameObject; return existing; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resonance", "Spawn Patterns");
            string patternPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/345 Hz Particle Encounter.prefab");
            string corePath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Amplifier Core - Spawn Cycle.prefab");
            var definition = Object.Instantiate(source.definition); definition.name = source.definition.name + " - Spawn Cycle";
            AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(Folder + "/345 Hz Spawn Pattern.asset"));
            var temp = new GameObject("345 Hz Particle Encounter"); temp.SetActive(false);
            try
            {
                temp.transform.localScale = source.transform.lossyScale;
                var copy = temp.AddComponent<ResonancePatternController>(); EditorUtility.CopySerialized(source, copy);
                copy.definition = definition; copy.arenaBounds = null; copy.grid = null; copy.energyOrigin = null;
                copy.enabled = false;
                var formation = temp.AddComponent<ResonanceManifestation>();
                var oldFormation = source.GetComponent<ResonanceManifestation>();
                if (oldFormation != null) EditorUtility.CopySerialized(oldFormation, formation);
                formation.enabled = true;
                // Preserve existing authored settings. Only a new component gets arena-sized dispersal defaults.
                if (oldFormation == null && source.arenaBounds != null && source.arenaBounds.IsValid)
                {
                    var bounds = source.arenaBounds;
                    Vector2 half = bounds.GetHalfSizeLocalInset();
                    Vector3 center = source.transform.InverseTransformPoint(bounds.Current.centerWS);
                    Vector3 right = source.transform.InverseTransformVector(bounds.Grid.transform.TransformVector(new Vector3(half.x, 0, 0)));
                    Vector3 up = source.transform.InverseTransformVector(bounds.Grid.transform.TransformVector(new Vector3(0, half.y, 0)));
                    formation.SetArea(new Vector2(center.x, center.z), new Vector2(Mathf.Abs(right.x) + Mathf.Abs(up.x), Mathf.Abs(right.z) + Mathf.Abs(up.z)));
                }
                temp.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(temp, patternPath);
            }
            finally { Object.DestroyImmediate(temp); }
            var contents = PrefabUtility.LoadPrefabContents(patternPath);
            try { contents.GetComponent<ResonancePatternController>().enabled = true; PrefabUtility.SaveAsPrefabAsset(contents, patternPath); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            var staging = new GameObject("Spawn core snapshot staging"); staging.SetActive(false);
            try
            {
                var clone = Object.Instantiate(core.gameObject, staging.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                clone.name = "Amplifier Core - Spawn Cycle";
                clone.transform.localPosition = Vector3.zero; clone.transform.localRotation = core.transform.rotation;
                clone.transform.localScale = core.transform.lossyScale;
                clone.SetActive(true); clone.GetComponent<AmplifierCoreGameplay>().enabled = true;
                PrefabUtility.SaveAsPrefabAsset(clone, corePath);
            }
            finally { Object.DestroyImmediate(staging); }

            var go = new GameObject("Amplifier + Resonance — Spawn Cycle");
            Undo.RegisterCreatedObjectUndo(go, "Install Amplifier Resonance Spawn Cycle");
            var spawner = Undo.AddComponent<AmplifierResonanceSpawner>(go);
            spawner.spawnRegion = go.GetComponent<AmplifierSpawnRegion>();
            spawner.spawnRegion.arenaBounds = source.arenaBounds != null ? source.arenaBounds : Object.FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
            spawner.corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(corePath).GetComponent<AmplifierCoreGameplay>();
            spawner.patternOrder.Add(new ResonanceSpawnEntry { label = "345 Hz — Chladni grains", patternPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(patternPath).GetComponent<ResonancePatternController>() });
            spawner.patternAnchor = source.transform;
            spawner.comparison = comparison;
            spawner.standaloneExamples = new[] { core.gameObject, source.gameObject };
            spawner.spawnRegion.previewPattern = source;
            spawner.spawnRegion.previewObjectRadius = spawner.CorePlacementRadius;
            EditorUtility.SetDirty(spawner); EditorUtility.SetDirty(spawner.spawnRegion);
            EditorSceneManager.MarkSceneDirty(go.scene);
            SelectSceneAnimationPreview(spawner);
            Selection.activeObject = go;
            Debug.Log("Spawn cycle installed using independent snapshots of current Core and Option B tuning. Original demonstrations are preserved. Save the scene to keep the wiring.");
            return spawner;
        }

        public static ResonanceManifestation SelectSceneAnimationPreview(AmplifierResonanceSpawner spawner)
        {
            if (Application.isPlaying || spawner == null) throw new InvalidOperationException("Use the scene preview in Edit Mode.");
            var source = spawner.comparison != null ? spawner.comparison.optionB
                : spawner.patternAnchor != null ? spawner.patternAnchor.GetComponent<ResonancePatternController>() : null;
            if (source == null || !source.gameObject.scene.IsValid()) throw new InvalidOperationException("Assign the scene Option B or a scene Pattern Anchor first.");
            ResonanceManifestation destination = null;
            if (spawner.patternOrder != null && spawner.patternOrder.Count > 0)
            {
                var entry = spawner.patternOrder[Mathf.Clamp(spawner.startingPattern, 0, spawner.patternOrder.Count - 1)];
                if (entry != null && entry.patternPrefab != null) destination = entry.patternPrefab.GetComponent<ResonanceManifestation>();
            }
            var preview = source.GetComponent<ResonanceManifestation>();
            if (preview == null)
            {
                preview = Undo.AddComponent<ResonanceManifestation>(source.gameObject);
                if (destination != null) preview.CopyAnimationSettingsFrom(destination);
            }
            if (preview.spawnPrefabTuningTarget == null && destination != null)
            {
                Undo.RecordObject(preview, "Link Resonance animation spawn tuning");
                preview.spawnPrefabTuningTarget = destination; EditorUtility.SetDirty(preview);
            }
            if (spawner.comparison != null && spawner.comparison.selected != ResonanceRendering.OptionBParticles)
            {
                Undo.RecordObject(spawner.comparison, "Show Option B animation preview");
                Undo.RecordObject(source.gameObject, "Show Option B animation preview");
                if (spawner.comparison.optionA != null) Undo.RecordObject(spawner.comparison.optionA.gameObject, "Show Option B animation preview");
                spawner.comparison.Select(ResonanceRendering.OptionBParticles);
            }
            Selection.activeObject = source.gameObject;
            preview.RefreshPreview();
            return preview;
        }
    }
}
