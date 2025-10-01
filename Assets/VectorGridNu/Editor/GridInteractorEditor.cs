using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridInteractor))]
public class GridInteractorEditor : Editor
{
    SerializedProperty gridProp;
    SerializedProperty profileProp;
    SerializedProperty overridesEnabledProp;
    SerializedProperty overrideRadiusProp, overrideStrengthProp, overrideInnerFracProp;

    string[] profileNames = System.Array.Empty<string>();
    Object[] profileAssets = System.Array.Empty<Object>();
    int selectedIndex = -1;

    void OnEnable()
    {
        gridProp              = serializedObject.FindProperty("grid");
        profileProp           = serializedObject.FindProperty("profile");
        overridesEnabledProp  = serializedObject.FindProperty("overridesEnabled");
        overrideRadiusProp    = serializedObject.FindProperty("overrideRadius");
        overrideStrengthProp  = serializedObject.FindProperty("overrideStrength");
        overrideInnerFracProp = serializedObject.FindProperty("overrideInnerFrac");

        RefreshProfiles();
        SyncSelectionFromProperty();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Grid reference
        EditorGUILayout.PropertyField(gridProp);

        // Profile row
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            int newIdx = EditorGUILayout.Popup("Grid Interaction Profile",
                Mathf.Max(selectedIndex, 0), profileNames);

            if (newIdx != selectedIndex && profileAssets.Length > 0)
            {
                selectedIndex = newIdx;
                profileProp.objectReferenceValue = profileAssets[selectedIndex];
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                RefreshProfiles();

            if (GUILayout.Button("Create New…", GUILayout.Width(100)))
                CreateProfileAsset();

            if (GUILayout.Button("Open", GUILayout.Width(60)) && profileProp.objectReferenceValue)
            {
                Selection.activeObject = profileProp.objectReferenceValue;
                EditorGUIUtility.PingObject(profileProp.objectReferenceValue);
            }
        }

        // (No inline rendering of the profile here—avoids layout conflicts)

        // Overrides
        EditorGUILayout.Space(8);
        EditorGUILayout.PropertyField(overridesEnabledProp);
        if (overridesEnabledProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(overrideRadiusProp,   new GUIContent("Radius (override)"));
            EditorGUILayout.PropertyField(overrideStrengthProp, new GUIContent("Strength (override)"));
            EditorGUILayout.Slider(overrideInnerFracProp, 0f, 0.9f, new GUIContent("Inner Frac (override)"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Tip: Select the profile asset and edit its Modules there. "
          + "Use the ▶ Trigger / On–Off buttons in Play Mode from the profile inspector.",
            MessageType.Info);
    }

    // ---------- helpers ----------
    void RefreshProfiles()
    {
        var guids = AssetDatabase.FindAssets("t:GridInteractionProfile");
        profileAssets = guids.Select(g => AssetDatabase.LoadAssetAtPath<GridInteractionProfile>(
            AssetDatabase.GUIDToAssetPath(g))).Cast<Object>().ToArray();
        profileNames  = profileAssets.Select(p => p ? p.name : "(null)").ToArray();
        SyncSelectionFromProperty();
    }

    void SyncSelectionFromProperty()
    {
        var current = profileProp.objectReferenceValue;
        selectedIndex = -1;
        for (int i = 0; i < profileAssets.Length; i++)
            if (profileAssets[i] == current) { selectedIndex = i; break; }
    }

    void CreateProfileAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Grid Interaction Profile", "New Grid Interaction Profile",
            "asset", "Pick a location under Assets/");
        if (string.IsNullOrEmpty(path)) return;

        var asset = ScriptableObject.CreateInstance<GridInteractionProfile>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RefreshProfiles();
        profileProp.objectReferenceValue = asset;
        SyncSelectionFromProperty();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
