// Assets/Editor/ComponentJsonExporter.cs
// Right-click any component header in the Inspector -> Copy Serialized Properties as JSON

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ComponentJsonExporter
{
    private const int MaxCurveKeysPreview = 16;

    [MenuItem("CONTEXT/Component/Copy Serialized Properties as JSON")]
    private static void CopyAsJson(MenuCommand command)
    {
        var comp = command.context as Component;
        if (comp == null) return;

        var dump = BuildDump(comp);
        string json = EditorJsonUtility.ToJson(dump, true);

        EditorGUIUtility.systemCopyBuffer = json;
        Debug.Log($"Copied JSON for {comp.GetType().Name} on {GetHierarchyPath(comp.gameObject)} to clipboard.");
    }

    [MenuItem("CONTEXT/Component/Save Serialized Properties as JSON...")]
    private static void SaveAsJson(MenuCommand command)
    {
        var comp = command.context as Component;
        if (comp == null) return;

        var dump = BuildDump(comp);
        string json = EditorJsonUtility.ToJson(dump, true);

        string defaultName = $"{SanitizeFileName(comp.gameObject.name)}_{SanitizeFileName(comp.GetType().Name)}.json";
        string path = EditorUtility.SaveFilePanel("Save component JSON", Application.dataPath, defaultName, "json");
        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllText(path, json);
        Debug.Log($"Saved JSON to: {path}");
    }

    [Serializable]
    private class ComponentDump
    {
        public string unityVersion;
        public string exportedAtUtc;

        public string componentType;
        public string gameObjectName;
        public string hierarchyPath;
        public string sceneName;

        public List<Entry> entries = new List<Entry>();
    }

    [Serializable]
    private class Entry
    {
        public string path;
        public string type;
        public int depth;

        public bool isArray;
        public int arraySize;

        // Human-readable value (primitives, vectors, etc.)
        public string value;

        // Populated when propertyType is ObjectReference / ExposedReference
        public ReferenceInfo reference;
    }

    [Serializable]
    private class ReferenceInfo
    {
        public string refType;
        public string name;

        public bool isAsset;
        public string assetPath;

        public string sceneName;
        public string hierarchyPath;

        public int instanceId;
    }

    private static ComponentDump BuildDump(Component comp)
    {
        var dump = new ComponentDump
        {
            unityVersion = Application.unityVersion,
            exportedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),

            componentType = comp.GetType().FullName,
            gameObjectName = comp.gameObject != null ? comp.gameObject.name : null,
            hierarchyPath = comp.gameObject != null ? GetHierarchyPath(comp.gameObject) : null,
            sceneName = comp.gameObject != null ? comp.gameObject.scene.name : null,
        };

        var so = new SerializedObject(comp);
        var it = so.GetIterator();

        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = true;

            // Skip the script reference field for MonoBehaviours
            if (it.propertyPath == "m_Script") continue;

            var entry = new Entry
            {
                path = it.propertyPath,
                type = it.propertyType.ToString(),
                depth = it.depth,
                isArray = it.isArray && it.propertyType != SerializedPropertyType.String,
                arraySize = (it.isArray && it.propertyType != SerializedPropertyType.String) ? it.arraySize : 0,
            };

            FillEntryValue(entry, it);
            dump.entries.Add(entry);
        }

        return dump;
    }

    private static void FillEntryValue(Entry entry, SerializedProperty p)
    {
        try
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    entry.value = p.intValue.ToString();
                    break;

                case SerializedPropertyType.Boolean:
                    entry.value = p.boolValue ? "true" : "false";
                    break;

                case SerializedPropertyType.Float:
                    entry.value = p.floatValue.ToString("R");
                    break;

                case SerializedPropertyType.String:
                    entry.value = p.stringValue;
                    break;

                case SerializedPropertyType.Color:
                {
                    Color c = p.colorValue;
                    entry.value = $"({c.r:R}, {c.g:R}, {c.b:R}, {c.a:R})";
                    break;
                }

                case SerializedPropertyType.ObjectReference:
                    entry.reference = BuildReferenceInfo(p.objectReferenceValue);
                    entry.value = entry.reference == null ? "null" : $"{entry.reference.refType}:{entry.reference.name}";
                    break;

                case SerializedPropertyType.ExposedReference:
                    entry.reference = BuildReferenceInfo(p.exposedReferenceValue);
                    entry.value = entry.reference == null ? "null" : $"{entry.reference.refType}:{entry.reference.name}";
                    break;

                case SerializedPropertyType.LayerMask:
                    entry.value = p.intValue.ToString();
                    break;

                case SerializedPropertyType.Enum:
                {
                    string enumName =
                        (p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length)
                            ? p.enumDisplayNames[p.enumValueIndex]
                            : p.enumValueIndex.ToString();
                    entry.value = enumName;
                    break;
                }

                case SerializedPropertyType.Vector2:
                {
                    Vector2 v = p.vector2Value;
                    entry.value = $"({v.x:R}, {v.y:R})";
                    break;
                }

                case SerializedPropertyType.Vector3:
                {
                    Vector3 v = p.vector3Value;
                    entry.value = $"({v.x:R}, {v.y:R}, {v.z:R})";
                    break;
                }

                case SerializedPropertyType.Vector4:
                {
                    Vector4 v = p.vector4Value;
                    entry.value = $"({v.x:R}, {v.y:R}, {v.z:R}, {v.w:R})";
                    break;
                }

                case SerializedPropertyType.Vector2Int:
                {
                    Vector2Int v = p.vector2IntValue;
                    entry.value = $"({v.x}, {v.y})";
                    break;
                }

                case SerializedPropertyType.Vector3Int:
                {
                    Vector3Int v = p.vector3IntValue;
                    entry.value = $"({v.x}, {v.y}, {v.z})";
                    break;
                }

                case SerializedPropertyType.Rect:
                {
                    Rect r = p.rectValue;
                    entry.value = $"(x:{r.x:R}, y:{r.y:R}, w:{r.width:R}, h:{r.height:R})";
                    break;
                }

                case SerializedPropertyType.RectInt:
                {
                    RectInt r = p.rectIntValue;
                    entry.value = $"(x:{r.x}, y:{r.y}, w:{r.width}, h:{r.height})";
                    break;
                }

                case SerializedPropertyType.Bounds:
                {
                    Bounds b = p.boundsValue;
                    entry.value = $"(center:{b.center}, size:{b.size})";
                    break;
                }

                case SerializedPropertyType.BoundsInt:
                {
                    BoundsInt b = p.boundsIntValue;
                    entry.value = $"(pos:{b.position}, size:{b.size})";
                    break;
                }

#if UNITY_2021_2_OR_NEWER
                case SerializedPropertyType.Quaternion:
                {
                    Quaternion q = p.quaternionValue;
                    entry.value = $"({q.x:R}, {q.y:R}, {q.z:R}, {q.w:R})";
                    break;
                }
#endif

                case SerializedPropertyType.AnimationCurve:
                {
                    AnimationCurve curve = p.animationCurveValue;
                    if (curve == null)
                    {
                        entry.value = "null";
                        break;
                    }

                    int k = curve.keys != null ? curve.keys.Length : 0;
                    entry.value = $"keys:{k}";
                    if (k > 0)
                    {
                        int preview = Mathf.Min(k, MaxCurveKeysPreview);
                        var parts = new List<string>(preview);
                        for (int i = 0; i < preview; i++)
                        {
                            var key = curve.keys[i];
                            parts.Add($"({key.time:R}->{key.value:R})");
                        }

                        entry.value += $" preview:[{string.Join(", ", parts)}]{(k > preview ? "..." : "")}";
                    }
                    break;
                }

                case SerializedPropertyType.ManagedReference:
                    entry.value = p.managedReferenceFullTypename;
                    break;

                case SerializedPropertyType.Generic:
                    // This is a container; its child fields will show up as separate entries.
                    entry.value = "<Generic>";
                    break;

                case SerializedPropertyType.Gradient:
                    // Gradient isn't directly readable without reflection; still useful to know it exists.
                    entry.value = "<Gradient>";
                    break;

                default:
                    entry.value = $"<{p.propertyType}>";
                    break;
            }
        }
        catch (Exception ex)
        {
            entry.value = $"<ERROR: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static ReferenceInfo BuildReferenceInfo(UnityEngine.Object obj)
    {
        if (obj == null) return null;

        var info = new ReferenceInfo
        {
            refType = obj.GetType().FullName,
            name = obj.name,
            instanceId = obj.GetInstanceID(),
            isAsset = EditorUtility.IsPersistent(obj),
            assetPath = null,
            sceneName = null,
            hierarchyPath = null
        };

        if (info.isAsset)
        {
            info.assetPath = AssetDatabase.GetAssetPath(obj);
        }
        else
        {
            // Scene object reference
            if (obj is GameObject go)
            {
                info.sceneName = go.scene.name;
                info.hierarchyPath = GetHierarchyPath(go);
            }
            else if (obj is Component c)
            {
                info.sceneName = c.gameObject.scene.name;
                info.hierarchyPath = GetHierarchyPath(c.gameObject) + $" ({c.GetType().Name})";
            }
        }

        return info;
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null) return "";
        Transform t = go.transform;

        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }

        return string.Join("/", stack);
    }

    private static string SanitizeFileName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}