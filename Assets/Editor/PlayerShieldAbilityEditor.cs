using Massive.Player;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    [CustomEditor(typeof(PlayerShieldAbility))]
    public sealed class PlayerShieldAbilityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PlayerTuningEditing.GetOrCreate();
            if (GUILayout.Button("Open Player Tuning")) PlayerTuningWindow.Open();
            if (SharedSettingsEditing.IsShared((PlayerShieldAbility)target))
                SharedSettingsEditing.DrawComponent(serializedObject, "activationCooldownSeconds");
            else SharedSettingsEditing.DrawComponent(serializedObject);
        }
    }
}
