#if UNITY_EDITOR
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyArenaLayout.Socket))]
public sealed class EnemyWallMountDrawer : PropertyDrawer
{
    static readonly string[] Shared = { "inward", "aimArc", "clearanceOffset", "support" };
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        (property.isExpanded ? (property.FindPropertyRelative("useRange").boolValue ? 11 : 9) : 1) * (EditorGUIUtility.singleLineHeight + 2);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        string id = property.FindPropertyRelative("id").stringValue;
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, string.IsNullOrEmpty(id) ? label : new GUIContent(id), true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            void Field(string name, string title = null)
            {
                row.y += EditorGUIUtility.singleLineHeight + 2;
                var value = property.FindPropertyRelative(name);
                EditorGUI.PropertyField(row, value, title == null ? new GUIContent(value.displayName, value.tooltip) : new GUIContent(title, value.tooltip));
            }
            Field("id"); Field("enabled"); Field("useRange", "Search wall range");
            if (property.FindPropertyRelative("useRange").boolValue) { Field("rangeStart"); Field("rangeEnd"); }
            Field("position", "Preferred position");
            foreach (string name in Shared) Field(name);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
#endif
