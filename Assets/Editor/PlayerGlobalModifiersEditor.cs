using Massive.Player;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerGlobalModifiersEditing
    {
        public const string AssetPath = "Assets/Scripts/Player/Scaling/Resources/MassivePlayerModifiers.asset";
        static PlayerGlobalModifiersEditing() { Undo.undoRedoPerformed += RefreshAfterUndo; }

        public static PlayerGlobalModifiers GetOrCreate()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PlayerGlobalModifiers>(AssetPath);
            if (profile) return profile;
            const string folder = "Assets/Scripts/Player/Scaling/Resources";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scripts/Player/Scaling", "Resources");
            profile = ScriptableObject.CreateInstance<PlayerGlobalModifiers>();
            // Migrate the open gameplay roster once. Do not copy gallery instances or
            // overwrite another scene's authoring values. IDs map P1-P4 to 0-3.
            foreach (var p in Object.FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (p.IsPseudoPlayer || !p.transform.parent || p.transform.parent.name != "Players") continue;
                var slot = profile.ForPlayer(p.playerID);
                if (slot == null) continue;
                var scale = p.GetComponent<PlayerScaleAdjuster>();
                if (scale)
                {
                    slot.size = scale.LocalSize;
                    slot.scaleActionReach = scale.ScaleActionReach;
                    slot.scaleMovement = scale.ScaleMovement;
                    slot.scaleProjectileRange = scale.ScaleProjectileRange;
                }
                var reversal = p.GetComponent<PlayerMovementReversal>();
                if (reversal)
                {
                    slot.reversalEnabled = reversal.assistEnabled;
                    slot.backwardConeDegrees = reversal.backwardConeDegrees;
                    slot.retainedMomentum = reversal.retainedMomentum;
                }
            }
            AssetDatabase.CreateAsset(profile, AssetPath);
            AssetDatabase.SaveAssetIfDirty(profile);
            PlayerGlobalModifiers.Reload();
            RefreshPlayers();
            return profile;
        }

        private static void RefreshAfterUndo()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PlayerGlobalModifiers>(AssetPath);
            if (!profile) return;
            AssetDatabase.SaveAssetIfDirty(profile);
            RefreshPlayers();
        }

        public static void RefreshPlayers()
        {
            foreach (var scale in Object.FindObjectsByType<PlayerScaleAdjuster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (scale.gameObject.scene.IsValid()) scale.ApplyScale();
            foreach (var reversal in Object.FindObjectsByType<PlayerMovementReversal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (reversal.gameObject.scene.IsValid()) reversal.ResetHistory();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        public static void Draw(bool sizeControls)
        {
            var profile = GetOrCreate();
            EditorGUILayout.LabelField(sizeControls ? "Global Player Size" : "Global Joystick Reversal", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("PROJECT SETTINGS — shared across all scenes and builds by player slot (P1–P4). Changes save to the shared asset, including edits made in Play Mode. Undo is supported. Player components can opt out for local exceptions.", MessageType.Info);
            using (var data = new SerializedObject(profile))
            {
                data.Update();
                var enabled = data.FindProperty(sizeControls ? "globalSizeEnabled" : "globalReversalEnabled");
                EditorGUILayout.PropertyField(enabled, new GUIContent(sizeControls ? "Use Global Player Size" : "Use Global Joystick Reversal"));
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    var slots = data.FindProperty("players");
                    for (int i = 0; i < slots.arraySize; i++)
                    {
                        var slot = slots.GetArrayElementAtIndex(i);
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            EditorGUILayout.LabelField("P" + (i + 1) + " — every scene", EditorStyles.boldLabel);
                            if (sizeControls) DrawSize(slot);
                            else DrawReversal(slot);
                        }
                    }
                }
                if (data.ApplyModifiedProperties())
                {
                    AssetDatabase.SaveAssetIfDirty(profile);
                    RefreshPlayers();
                }
            }
            if (GUILayout.Button("Select Shared Player Settings Asset")) Selection.activeObject = profile;
            EditorGUILayout.HelpBox(sizeControls
                ? "Size includes the body, colliders and connected effects. Movement speed, attack travel and projectile range use the switches above. Local size values remain intact when globals are disabled. Older player prefabs receive the required components automatically when gameplay starts."
                : "The cone is centered behind current travel. 0% momentum drops the old drift; 100% keeps normal movement. Attacks, recoil and other protected actions keep their existing momentum.", MessageType.None);
        }

        private static void Percent(SerializedProperty property, string label, float min, float max)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(label, property.floatValue * 100f, min, max);
            if (EditorGUI.EndChangeCheck()) property.floatValue = value / 100f;
        }

        private static void DrawSize(SerializedProperty slot)
        {
            var size = slot.FindPropertyRelative("size");
            Percent(size, "Player Size (%)", 10f, 300f);
            using (new EditorGUILayout.HorizontalScope())
                foreach (float value in new[] { .5f, .66f, .75f, 1f, 1.25f, 1.5f })
                    if (GUILayout.Button($"{value * 100f:0}%")) size.floatValue = value;
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("scaleActionReach"), new GUIContent("Scale Attack Travel & Targeting"));
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("scaleMovement"), new GUIContent("Scale Movement Speed"));
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("scaleProjectileRange"), new GUIContent("Scale Projectile Range"));
        }

        private static void DrawReversal(SerializedProperty slot)
        {
            var enabled = slot.FindPropertyRelative("reversalEnabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent("Enable Joystick Reversal"));
            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("backwardConeDegrees"), new GUIContent("Backward Cone (degrees)", "Full width: 100 degrees is ±50 degrees from directly backward."));
                Percent(slot.FindPropertyRelative("retainedMomentum"), "Momentum Retained (%)", 0f, 100f);
            }
        }
    }

    [CustomEditor(typeof(PlayerGlobalModifiers))]
    public sealed class PlayerGlobalModifiersEditor : UnityEditor.Editor
    {
        private int tab;
        public override void OnInspectorGUI()
        {
            tab = GUILayout.Toolbar(tab, new[] { "Player Size", "Joystick Reversal" });
            PlayerGlobalModifiersEditing.Draw(tab == 0);
        }
    }
}
