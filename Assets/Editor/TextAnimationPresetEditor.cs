#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.TextAnimation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TextAnimationPreset))]
public sealed class TextAnimationPresetEditor : Editor
{
    private SerializedProperty _modules;

    private void OnEnable()
    {
        _modules = serializedObject.FindProperty("modules");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "modules");
        EditorGUILayout.Space(8f);
        DrawModules();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawModules()
    {
        EditorGUILayout.LabelField("Effect Modules", EditorStyles.boldLabel);

        if (_modules == null)
        {
            EditorGUILayout.HelpBox("Could not find the managed-reference module list.", MessageType.Error);
            return;
        }

        for (int i = 0; i < _modules.arraySize; i++)
        {
            SerializedProperty element = _modules.GetArrayElementAtIndex(i);
            object value = element.managedReferenceValue;
            string title = value != null
                ? FriendlyTypeName(value.GetType())
                : "Missing Module";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}. {title}", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(i <= 0))
            {
                if (GUILayout.Button("▲", GUILayout.Width(28f)))
                    _modules.MoveArrayElement(i, i - 1);
            }

            using (new EditorGUI.DisabledScope(i >= _modules.arraySize - 1))
            {
                if (GUILayout.Button("▼", GUILayout.Width(28f)))
                    _modules.MoveArrayElement(i, i + 1);
            }

            if (GUILayout.Button("×", GUILayout.Width(28f)))
            {
                element.managedReferenceValue = null;
                _modules.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            if (value == null)
            {
                EditorGUILayout.HelpBox(
                    "This managed-reference type is missing. Remove it and add a replacement module.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.PropertyField(element, GUIContent.none, includeChildren: true);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3f);
        }

        if (GUILayout.Button("Add Module", GUILayout.Height(24f)))
            ShowAddModuleMenu();
    }

    private void ShowAddModuleMenu()
    {
        GenericMenu menu = new GenericMenu();
        List<Type> moduleTypes = new List<Type>();

        foreach (Type type in TypeCache.GetTypesDerivedFrom<TextAnimationModule>())
        {
            if (type.IsAbstract || type.IsGenericType)
                continue;
            moduleTypes.Add(type);
        }

        moduleTypes.Sort((a, b) =>
            string.Compare(FriendlyTypeName(a), FriendlyTypeName(b), StringComparison.Ordinal));

        for (int i = 0; i < moduleTypes.Count; i++)
        {
            Type type = moduleTypes[i];
            menu.AddItem(
                new GUIContent(FriendlyTypeName(type)),
                false,
                () => AddModule(type));
        }

        if (moduleTypes.Count == 0)
            menu.AddDisabledItem(new GUIContent("No module types found"));

        menu.ShowAsContext();
    }

    private void AddModule(Type type)
    {
        serializedObject.Update();
        int index = _modules.arraySize;
        _modules.arraySize++;
        SerializedProperty element = _modules.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(type);
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(target);
    }

    private static string FriendlyTypeName(Type type)
    {
        string value = type.Name
            .Replace("TextAnimationModule", string.Empty)
            .Replace("Module", string.Empty);
        return ObjectNames.NicifyVariableName(value);
    }
}
#endif
