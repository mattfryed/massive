using System;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerTuningEditing
    {
        public const string AssetPath = "Assets/Scripts/Settings/Resources/PlayerTuningProfile.asset";
        static PlayerTuningEditing() { Undo.undoRedoPerformed += SaveAndRefresh; }

        public static PlayerTuningProfile GetOrCreate()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuningProfile>(AssetPath);
            if (tuning) return tuning;
            SharedSettingsEditing.EnsureProfiles();
            tuning = ScriptableObject.CreateInstance<PlayerTuningProfile>();
            // Seed from the reusable roster, not whichever level happens to be open.
            var roster = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players.prefab");
            if (roster)
                foreach (var player in roster.GetComponentsInChildren<PlayerControllerScript>(true))
                {
                    if (player.playerID != 0 || player.IsPseudoPlayer) continue;
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(player), tuning.movement);
                    var impact = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
                    if (impact) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(impact), tuning.repulsor);
                    var attack = player.GetComponent<PlayerAttackController>();
                    if (attack)
                    {
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(attack), tuning.combat);
                        using (var data = new SerializedObject(attack))
                            tuning.attackProfile = data.FindProperty("attackProfile").objectReferenceValue as PlayerAttackProfile;
                    }
                    break;
                }
            if (!tuning.attackProfile) tuning.attackProfile = AssetDatabase.LoadAssetAtPath<PlayerAttackProfile>("Assets/Scripts/Player/Actions/PlayerAttackProfile.asset");
            AssetDatabase.CreateAsset(tuning, AssetPath);
            AssetDatabase.SaveAssetIfDirty(tuning);
            SharedSettingsRuntime.Reload();
            return tuning;
        }

        public static void SaveAndRefresh()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuningProfile>(AssetPath);
            if (tuning) { AssetDatabase.SaveAssetIfDirty(tuning); if (tuning.attackProfile) AssetDatabase.SaveAssetIfDirty(tuning.attackProfile); }
            var visuals = SharedSettingsRuntime.Load<MeleeVisualProfile>();
            if (visuals) AssetDatabase.SaveAssetIfDirty(visuals);
            PlayerGlobalModifiersEditing.RefreshPlayers();
            SharedSettingsEditing.Refresh();
        }

        public static void Save(Object target)
        {
            EditorUtility.SetDirty(target);
            if (EditorUtility.IsPersistent(target)) AssetDatabase.SaveAssetIfDirty(target);
            PlayerGlobalModifiersEditing.RefreshPlayers();
            SharedSettingsEditing.Refresh();
        }

        public static Vector2 Window(AttackStage stage, PlayerTuningProfile tuning) =>
            stage.GetComboWindow(tuning.combat.sharedComboWindowSeconds, tuning.combat.useSharedComboWindow,
                tuning.combat.comboWindowAfterActivationWindow, tuning.combat.comboWindowEndNormalized);

        public static float Tail(AttackStage stage, MeleeVisualProfile visuals) => visuals == null ? 0 :
            stage.StageType == AttackStageType.PrimaryLunge ? visuals.thrustLingerSeconds :
            stage.StageType == AttackStageType.ComboSwipe ? visuals.volumeLingerSeconds : visuals.repulsorPulseLinger;

        public static string TailField(AttackStage stage) => stage.StageType == AttackStageType.PrimaryLunge ? "thrustLingerSeconds" :
            stage.StageType == AttackStageType.ComboSwipe ? "volumeLingerSeconds" : "repulsorPulseLinger";

        public static float TailStart(AttackStage stage) => stage.StageType == AttackStageType.PrimaryLunge ? stage.Duration : stage.ActivationEndNormalized * stage.Duration;

        public static void EnableCustomWindow(SerializedProperty stage, AttackStage source, PlayerTuningProfile tuning)
        {
            if (stage.FindPropertyRelative("customComboWindow").boolValue) return;
            var window = Window(source, tuning);
            stage.FindPropertyRelative("comboWindowStartSeconds").floatValue = window.x;
            stage.FindPropertyRelative("comboWindowEndSeconds").floatValue = window.y;
            stage.FindPropertyRelative("comboHandoffSeconds").floatValue = source.Duration;
            stage.FindPropertyRelative("customComboWindow").boolValue = true;
        }
    }

    [CustomEditor(typeof(PlayerAttackController))]
    public sealed class PlayerTuningAttackInspector : SharedComponentEditor
    {
        public override void OnInspectorGUI()
        {
            PlayerTuningEditing.GetOrCreate();
            if (GUILayout.Button("Open Player Tuning")) PlayerTuningWindow.Open();
            base.OnInspectorGUI();
            if (((PlayerAttackController)target).UseSharedSettings)
                EditorGUILayout.HelpBox("The attack profile in Player Tuning is used when shared settings are enabled. The local Profile field below is retained as a fallback.", MessageType.None);
        }
    }

    [CustomEditor(typeof(PlayerControllerScript))]
    public sealed class PlayerTuningMovementInspector : SharedComponentEditor
    {
        public override void OnInspectorGUI()
        {
            PlayerTuningEditing.GetOrCreate();
            if (GUILayout.Button("Open Player Tuning")) PlayerTuningWindow.Open();
            base.OnInspectorGUI();
        }
    }

    [CustomEditor(typeof(PlayerRepulsorAOE))]
    public sealed class PlayerTuningRepulsorInspector : SharedComponentEditor { }
}
