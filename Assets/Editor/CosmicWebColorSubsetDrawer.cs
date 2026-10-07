using Massive.Cosmos;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CosmicWebBackground.ColorSubset))]
public sealed class CosmicWebColorSubsetDrawer : PropertyDrawer
{
    static readonly GUIContent ShareLabel = new GUIContent("Percentage (%)", "Share of this palette using this color. The main color receives the remainder; totals above 100 are normalized.");
    static readonly GUIContent InterpolationLabel = new GUIContent("Interpolation (%)", "0 = exact palette colors, 100 = full filament/cluster mixing. The lower setting of the selected color pair limits their mix. Early Heat is separate.");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(line, property.FindPropertyRelative("color"), label);
        line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("percentage"), ShareLabel);
        line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
        var reduction = property.FindPropertyRelative("interpolationReduction");
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = reduction.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        float interpolation = EditorGUI.Slider(line, InterpolationLabel, 100 - reduction.floatValue, 0, 100);
        if (EditorGUI.EndChangeCheck()) reduction.floatValue = 100 - interpolation;
        EditorGUI.showMixedValue = previousMixed;
        EditorGUI.EndProperty();
    }
}
