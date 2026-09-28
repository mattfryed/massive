#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Preserves consumers when converting flat roster objects to nested actors.</summary>
public static class PlayerActorReferenceMigration
{
    public sealed class Snapshot
    {
        public string path, guid;
        public readonly Dictionary<string, long> objects = new();
    }

    public static Snapshot[] Capture(params string[] paths)
    {
        return paths.Select(path =>
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!root) throw new InvalidOperationException("Missing roster prefab: " + path);
            var snapshot = new Snapshot { path = path, guid = AssetDatabase.AssetPathToGUID(path) };
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string relative = AnimationUtility.CalculateTransformPath(t, root.transform);
                Add(t.gameObject, relative + "|GameObject");
                foreach (var component in t.GetComponents<Component>())
                {
                    if (!component) throw new InvalidOperationException("Missing script in " + path + ":" + relative);
                    string kind = component.GetType().FullName;
                    if (component is MonoBehaviour behaviour)
                        kind = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behaviour)));
                    Add(component, relative + "|" + kind);
                }
            }
            return snapshot;

            void Add(Object target, string key)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long id) || guid != snapshot.guid)
                    throw new InvalidOperationException("Cannot resolve prefab identity: " + path + ":" + key);
                // Ambiguous matches must not silently rewire the wrong object.
                if (!snapshot.objects.TryAdd(key, id)) throw new InvalidOperationException("Ambiguous actor object: " + key);
            }
        }).ToArray();
    }

    public static int Remap(Snapshot[] before)
    {
        var maps = new Dictionary<string, Dictionary<long, long>>();
        foreach (var previous in before)
        {
            AssetDatabase.ImportAsset(previous.path, ImportAssetOptions.ForceUpdate);
            var current = Capture(previous.path)[0];
            var map = new Dictionary<long, long>();
            foreach (var item in previous.objects)
                if (current.objects.TryGetValue(item.Key, out long id) && id != item.Value) map.Add(item.Value, id);
            maps.Add(previous.guid, map);
        }
        var pattern = new Regex(@"(\{fileID:\s*)(-?\d+)(,\s*guid:\s*)(" + string.Join("|", maps.Keys) + @")(?=[,}])");
        var changes = new List<KeyValuePair<string, byte[]>>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !(path.EndsWith(".unity") || path.EndsWith(".prefab") || path.EndsWith(".asset"))) continue;
            // Exact token replacement preserves local object IDs, overrides, BOM
            // and line endings. Broken source targets cannot be resolved by SerializedObject.
            string original = Encoding.UTF8.GetString(File.ReadAllBytes(path));
            if (!maps.Keys.Any(original.Contains)) continue;
            string updated = pattern.Replace(original, m =>
                maps[m.Groups[4].Value].TryGetValue(long.Parse(m.Groups[2].Value), out long id)
                    ? m.Groups[1].Value + id + m.Groups[3].Value + m.Groups[4].Value : m.Value);
            if (updated != original) changes.Add(new KeyValuePair<string, byte[]>(path, Encoding.UTF8.GetBytes(updated)));
        }
        AssetDatabase.StartAssetEditing();
        try { foreach (var change in changes) File.WriteAllBytes(change.Key, change.Value); }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        return changes.Count;
    }
}
#endif
