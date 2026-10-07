#if UNITY_EDITOR
using Massive.Cosmos;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CosmicSupernovae)), CanEditMultipleObjects]
public sealed class CosmicSupernovaeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        bool super = ((CosmicSupernovae)target).kind == CosmicSupernovae.SupernovaKind.Superluminous;
        EditorGUILayout.LabelField(super ? "Superluminous supernovae" : "Normal supernovae", EditorStyles.boldLabel);
        var property = serializedObject.GetIterator();
        bool children = true;
        while (property.NextVisible(children))
        {
            children = false;
            if ((property.name == "shellColor" || property.name == "shellDetail") && !super) continue;
            if ((property.name == "cloudExpansionSeconds" || property.name == "cloudFadeSeconds" ||
                property.name == "cloudDistortion" || property.name == "cloudMixingSpeed" || property.name == "telegraphDistortion") && !super) continue;
            if ((property.name == "cyanWhiteColor" || property.name == "cyanBlueColor" || property.name == "lightPurpleColor") &&
                !serializedObject.FindProperty("randomizeColor").boolValue) continue;
            using (new EditorGUI.DisabledScope(property.name == "m_Script"))
            {
                if (property.name == "frequencyOverAge")
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Supernova frequency over Age", EditorStyles.boldLabel);
                    // A shared 0..1 scale makes the half-height superluminous curve visible.
                    EditorGUILayout.CurveField(property, super ? new Color(.4f,.8f,1) : new Color(1,.7f,.25f),
                        new Rect(0,0,1,1), GUILayout.Height(56));
                }
                else
                {
                    string label = property.name == "nuggletPrefab" ? (super ? "Mass Nugget Prefab" : "Mass Nugglet Prefab") :
                        property.name == "nuggletLifetime" ? "Pickup Lifetime" :
                        property.name == "nuggletCapacity" ? "Pickup Capacity" :
                        property.name == "flashSeconds" && super ? "Center Flash Seconds" :
                        property.name == "shellColor" && super ? "Warm Ejecta Color" :
                        property.name == "shellDetail" && super ? "Cloud Fine Detail" : property.displayName;
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
                }
            }
        }
        serializedObject.ApplyModifiedProperties();
        if (targets.Length == 1)
            EditorGUILayout.LabelField("Rate at Age 0.5", ((CosmicSupernovae)target).FrequencyAtAge(.5f).ToString("0.###") + " / second");
    }
}
#endif
