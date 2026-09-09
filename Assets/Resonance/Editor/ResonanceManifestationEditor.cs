using UnityEditor;
using UnityEngine;

namespace Massive.Resonance.Editor
{
    [CustomEditor(typeof(ResonanceManifestation)), CanEditMultipleObjects]
    public sealed class ResonanceManifestationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var distribution = serializedObject.FindProperty("birthDistribution");
            DrawPropertiesExcluding(serializedObject, "m_Script", "localSpawnSpread", "alongArcSpread", "fieldCoherence", "fieldWavelength", "birthPulse",
                "dispersalCenter", "dispersalHalfExtents", "showDispersalArea", "spawnPrefabTuningTarget");
            if (distribution.hasMultipleDifferentValues || distribution.enumValueIndex == (int)ResonanceBirthDistribution.LocalBand)
            {
                EditorGUILayout.LabelField("Local birth / field vibration", EditorStyles.boldLabel);
                var spread = serializedObject.FindProperty("localSpawnSpread");
                EditorGUILayout.PropertyField(spread, new GUIContent("Spawn Spread", spread.tooltip));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("alongArcSpread"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fieldCoherence"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fieldWavelength"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("birthPulse"));
            }
            if (distribution.hasMultipleDifferentValues || distribution.enumValueIndex == (int)ResonanceBirthDistribution.FullField)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dispersalCenter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dispersalHalfExtents"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showDispersalArea"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnPrefabTuningTarget"));
            if (serializedObject.ApplyModifiedProperties()) foreach (ResonanceManifestation item in targets) item.RefreshPreview();
            EditorGUILayout.Space();
            var manifestation = (ResonanceManifestation)target;
            bool sceneObject = !EditorUtility.IsPersistent(manifestation) && manifestation.gameObject.scene.IsValid();
            if (!sceneObject)
                EditorGUILayout.HelpBox("This is a prefab asset, not the visible scene object. Use the scene's Option B to preview in the Game view, or open Prefab Mode to preview in its Scene view.", MessageType.Info);
            EditorGUILayout.HelpBox("Form In / Dissolve Out and the scrubber work in Edit Mode, in both Scene and Game views. Preview poses are temporary and do not change the idle pattern settings. " +
                "Collisions, magnetic/sludge effects and grid attraction switch off while forming, dissolving or hidden. " +
                "The complete ghost pattern remains reserved for Core impacts.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!sceneObject || !manifestation.isActiveAndEnabled))
            {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Form In")) foreach (ResonanceManifestation item in targets)
                { if (item.IsIdle) item.SetImmediate(false); item.BeginSpawn(); }
                if (GUILayout.Button("Dissolve Out")) foreach (ResonanceManifestation item in targets) item.BeginDespawn();
                if (GUILayout.Button("Reset to Idle")) foreach (ResonanceManifestation item in targets) item.SetImmediate(true);
            }
            if (targets.Length != 1) return;
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider("Formation Preview", manifestation.NormalizedProgress, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) manifestation.SetNormalizedProgress(value);
            }
            if (targets.Length != 1) return;
            var destination = manifestation.spawnPrefabTuningTarget;
            bool validDestination = destination != null && EditorUtility.IsPersistent(destination)
                && PrefabUtility.IsPartOfPrefabAsset(destination);
            if (sceneObject)
            {
                EditorGUILayout.HelpBox("Scene changes are retained when you save the scene. To use the same animation for future spawns, save only its animation tuning to the linked spawn prefab below. Pattern shape, particles, materials and physics are not copied.", MessageType.Info);
                using (new EditorGUI.DisabledScope(Application.isPlaying || !validDestination))
                {
                    if (GUILayout.Button("Save Animation Tuning to Spawn Prefab")) SaveAnimationTuning(manifestation);
                    if (GUILayout.Button("Load Animation Tuning from Spawn Prefab"))
                    {
                        Undo.RecordObject(manifestation, "Load Resonance animation tuning");
                        manifestation.CopyAnimationSettingsFrom(destination); EditorUtility.SetDirty(manifestation);
                    }
                }
            }
            if (manifestation.IsAnimating) Repaint();
        }
        public static void SaveAnimationTuning(ResonanceManifestation source)
        {
            var destination = source != null ? source.spawnPrefabTuningTarget : null;
            if (Application.isPlaying || destination == null || !EditorUtility.IsPersistent(destination)
                || !PrefabUtility.IsPartOfPrefabAsset(destination))
                throw new System.InvalidOperationException("Choose a prefab Manifestation as the tuning destination in Edit Mode.");
            Undo.RecordObject(destination, "Save Resonance animation tuning to spawn prefab");
            destination.CopyAnimationSettingsFrom(source);
            EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssetIfDirty(destination);
        }
    }
}
