using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

namespace Massive.Multiplier.Editor
{
    [CustomEditor(typeof(AmplifierCoreGameplay)), CanEditMultipleObjects]
    public sealed class AmplifierCoreGameplayEditor : UnityEditor.Editor
    {
        private bool showLifecycle;

        public override void OnInspectorGUI()
        {
            Massive.EditorTools.SharedSettingsEditing.DrawComponent(serializedObject);
        }

        private void Draw(string field, string label)
        {
            var property = serializedObject.FindProperty(field);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }
    }
}
