#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioDatabase))]
public class AudioDatabaseEditor : Editor
{
    private enum NewEventGroup { SFX, UI, AMBIENT, VGM }

    // Create Event UI
    private NewEventGroup _group = NewEventGroup.SFX;
    private string _eventName = "";
    private AudioClip _initialClip;
    private bool _autoCreateCue = true;
    private bool _autoRegenerateEnum = true;

    // Sync UI
    private DefaultAsset _scanFolderAsset;
    private string _fallbackScanFolder = "Assets/Audio/Assets";
    private bool _syncCreateMissingEntries = true;
    private bool _syncFillMissingCues = true;
    private bool _syncAutoRegenerate = true;

    public override void OnInspectorGUI()
    {
        var db = (AudioDatabase)target;

        EditorGUILayout.HelpBox(
            "AudioDatabase is the source of truth for AudioEventId.\n" +
            "You can (1) Create Event, (2) Sync from existing AC_* AudioCue assets, (3) Regenerate AudioEventId.cs.\n" +
            "Do NOT hand-edit AudioEventId.cs anymore.",
            MessageType.Info);

        // Normal inspector (your lists)
        DrawDefaultInspector();

        // ------------------------------------------------------------
        // Create new event
        // ------------------------------------------------------------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Create New Audio Event", EditorStyles.boldLabel);

        _group = (NewEventGroup)EditorGUILayout.EnumPopup("Group", _group);
        _eventName = EditorGUILayout.TextField("Event name", _eventName);
        _initialClip = (AudioClip)EditorGUILayout.ObjectField("Initial clip (optional)", _initialClip, typeof(AudioClip), false);

        _autoCreateCue = EditorGUILayout.Toggle("Auto-create AudioCue asset", _autoCreateCue);
        _autoRegenerateEnum = EditorGUILayout.Toggle("Auto-regenerate AudioEventId.cs", _autoRegenerateEnum);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_eventName)))
        {
            if (GUILayout.Button("Create Event"))
            {
                try
                {
                    CreateNewEvent(db, _group, _eventName, _initialClip, _autoCreateCue, _autoRegenerateEnum);
                    _eventName = "";
                    _initialClip = null;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Create Event Failed", ex.Message, "OK");
                }
            }
        }

        // ------------------------------------------------------------
        // Sync from existing AudioCue assets (THIS preserves your work)
        // ------------------------------------------------------------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Sync From Existing AudioCue Assets", EditorStyles.boldLabel);

        _scanFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
            "Scan Folder (optional)",
            _scanFolderAsset,
            typeof(DefaultAsset),
            false);

        string scanPath = GetScanPath();
        EditorGUILayout.LabelField("Scan Path:", scanPath);

        _syncCreateMissingEntries = EditorGUILayout.Toggle("Create missing entries", _syncCreateMissingEntries);
        _syncFillMissingCues = EditorGUILayout.Toggle("Fill missing cue refs", _syncFillMissingCues);
        _syncAutoRegenerate = EditorGUILayout.Toggle("Auto-regenerate AudioEventId.cs", _syncAutoRegenerate);

        using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(scanPath)))
        {
            if (GUILayout.Button("SYNC NOW (from AC_* cues)"))
            {
                try
                {
                    int created, updated;
                    SyncFromCues(db, scanPath, _syncCreateMissingEntries, _syncFillMissingCues, out created, out updated);

                    Debug.Log($"[AudioDatabaseEditor] Sync complete. Created {created} entries, updated {updated} existing entries.");

                    if (_syncAutoRegenerate)
                        RegenerateEnum(db, showDialogOnSuccess: true, openScript: true);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Sync Failed", ex.Message, "OK");
                }
            }
        }

        // ------------------------------------------------------------
        // Tools
        // ------------------------------------------------------------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate AudioEventId.cs"))
            {
                try { RegenerateEnum(db, showDialogOnSuccess: true, openScript: true); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Regenerate Failed", ex.Message, "OK");
                }
            }

            if (GUILayout.Button("Validate DB vs Code"))
            {
                try { ValidateDbVsCode(db); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Validate Failed", ex.Message, "OK");
                }
            }
        }
    }

    private string GetScanPath()
    {
        if (_scanFolderAsset == null) return _fallbackScanFolder;

        string path = AssetDatabase.GetAssetPath(_scanFolderAsset);
        if (AssetDatabase.IsValidFolder(path)) return path;
        return _fallbackScanFolder;
    }

    // ----------------------------
    // Sync logic
    // ----------------------------
    private static void SyncFromCues(
        AudioDatabase db,
        string scanFolder,
        bool createMissing,
        bool fillMissingCues,
        out int created,
        out int updated)
    {
        created = 0;
        updated = 0;

        Undo.RecordObject(db, "Sync audio database from cues");
        db.MigrateLegacyEntriesIfNeeded();

        // Build a fast lookup of existing names -> (which list, index)
        var index = BuildNameIndex(db);

        // Determine next available stable value (do not reuse values)
        int nextValue = db.GetNextAvailableValue();

        // Find all AudioCue assets under the folder
        string[] guids = AssetDatabase.FindAssets("t:AudioCue", new[] { scanFolder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
            if (cue == null) continue;

            string cueName = cue.name;

            // Convention: AC_Player_Spawn -> Player_Spawn
            string eventName = cueName.StartsWith("AC_", StringComparison.Ordinal) ? cueName.Substring(3) : cueName;
            eventName = AudioEventIdGenerator.SanitizeToIdentifier(eventName);

            NewEventGroup group = GuessGroup(eventName, path);

            if (index.TryGetValue(eventName, out var loc))
            {
                // Entry already exists. Optionally fill the cue if missing.
                if (fillMissingCues)
                {
                    if (TryAssignCueIfMissing(db, loc, cue))
                        updated++;
                }
                continue;
            }

            if (!createMissing)
                continue;

            // Create a new entry
            var entry = new AudioDatabase.Entry
            {
                id = (AudioEventId)nextValue,
                name = eventName,
                value = nextValue,
                assigned = true,
                deprecated = false,
                cue = cue
            };

            nextValue++;

            AddEntryToGroup(db, group, entry);
            created++;

            // Update index so duplicates in same sync pass don't collide
            index[eventName] = FindEntryLocation(db, eventName);
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private struct EntryLocation
    {
        public NewEventGroup group;
        public int index;
    }

    private static Dictionary<string, EntryLocation> BuildNameIndex(AudioDatabase db)
    {
        var dict = new Dictionary<string, EntryLocation>(StringComparer.Ordinal);

        void AddList(List<AudioDatabase.Entry> list, NewEventGroup group)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (string.IsNullOrWhiteSpace(e.name)) continue;
                if (!dict.ContainsKey(e.name))
                    dict.Add(e.name, new EntryLocation { group = group, index = i });
            }
        }

        AddList(db.entries, NewEventGroup.SFX);
        AddList(db.uiEntries, NewEventGroup.UI);
        AddList(db.ambientEntries, NewEventGroup.AMBIENT);
        AddList(db.vgmEntries, NewEventGroup.VGM);

        return dict;
    }

    private static EntryLocation FindEntryLocation(AudioDatabase db, string name)
    {
        int idx = FindIndex(db.entries, name);
        if (idx >= 0) return new EntryLocation { group = NewEventGroup.SFX, index = idx };

        idx = FindIndex(db.uiEntries, name);
        if (idx >= 0) return new EntryLocation { group = NewEventGroup.UI, index = idx };

        idx = FindIndex(db.ambientEntries, name);
        if (idx >= 0) return new EntryLocation { group = NewEventGroup.AMBIENT, index = idx };

        idx = FindIndex(db.vgmEntries, name);
        if (idx >= 0) return new EntryLocation { group = NewEventGroup.VGM, index = idx };

        return new EntryLocation { group = NewEventGroup.SFX, index = -1 };
    }

    private static int FindIndex(List<AudioDatabase.Entry> list, string name)
    {
        if (list == null) return -1;
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i].name, name, StringComparison.Ordinal))
                return i;
        return -1;
    }

    private static bool TryAssignCueIfMissing(AudioDatabase db, EntryLocation loc, AudioCue cue)
    {
        var list = GetList(db, loc.group);
        if (list == null || loc.index < 0 || loc.index >= list.Count) return false;

        var e = list[loc.index];
        if (e.cue != null) return false;

        e.cue = cue;
        list[loc.index] = e;

        return true;
    }

    private static void AddEntryToGroup(AudioDatabase db, NewEventGroup group, AudioDatabase.Entry entry)
    {
        switch (group)
        {
            case NewEventGroup.UI: db.uiEntries.Add(entry); break;
            case NewEventGroup.AMBIENT: db.ambientEntries.Add(entry); break;
            case NewEventGroup.VGM: db.vgmEntries.Add(entry); break;
            default: db.entries.Add(entry); break; // SFX
        }
    }

    private static List<AudioDatabase.Entry> GetList(AudioDatabase db, NewEventGroup group)
    {
        return group switch
        {
            NewEventGroup.UI => db.uiEntries,
            NewEventGroup.AMBIENT => db.ambientEntries,
            NewEventGroup.VGM => db.vgmEntries,
            _ => db.entries,
        };
    }

    private static NewEventGroup GuessGroup(string eventName, string assetPath)
    {
        if (eventName.StartsWith("UI_", StringComparison.Ordinal)) return NewEventGroup.UI;

        if (eventName.StartsWith("Ambient_", StringComparison.Ordinal) ||
            eventName.StartsWith("AMBIENT_", StringComparison.Ordinal))
            return NewEventGroup.AMBIENT;

        if (assetPath.IndexOf("/BGM/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            assetPath.IndexOf("/Music/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            eventName.StartsWith("BGM_", StringComparison.Ordinal) ||
            eventName.StartsWith("MX_", StringComparison.Ordinal))
            return NewEventGroup.VGM;

        return NewEventGroup.SFX;
    }

    // ----------------------------
    // Existing tools: regen + validate + create
    // ----------------------------
    private static void RegenerateEnum(AudioDatabase db, bool showDialogOnSuccess, bool openScript)
    {
        string outputPath = AudioEventIdGenerator.DefaultOutputPath;
        AudioEventIdGenerator.Generate(db, outputPath, showDialogOnSuccess: false);

        Debug.Log($"[AudioDatabaseEditor] Regenerated AudioEventId.cs at: {outputPath}");

        if (openScript)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(outputPath);
            if (script != null)
            {
                EditorGUIUtility.PingObject(script);
                AssetDatabase.OpenAsset(script);
            }
        }

        if (showDialogOnSuccess)
        {
            EditorUtility.DisplayDialog(
                "Regenerate AudioEventId.cs",
                $"Generated:\n{outputPath}\n\nUnity will recompile scripts now.",
                "OK");
        }
    }

    private static void ValidateDbVsCode(AudioDatabase db)
    {
        var existingNames = new HashSet<string>(
            db.EnumerateAllEntriesWithGroup(includeDeprecated: true)
              .Select(t => t.entry.name)
              .Where(n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.Ordinal);

        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

            string text = File.ReadAllText(path);

            int idx = 0;
            while (true)
            {
                idx = text.IndexOf("AudioEventId.", idx, StringComparison.Ordinal);
                if (idx < 0) break;
                idx += "AudioEventId.".Length;

                int start = idx;
                while (idx < text.Length)
                {
                    char c = text[idx];
                    if (!(char.IsLetterOrDigit(c) || c == '_')) break;
                    idx++;
                }

                if (idx > start)
                    referenced.Add(text.Substring(start, idx - start));
            }
        }

        var missing = referenced.Where(r => !existingNames.Contains(r)).OrderBy(r => r).ToList();

        if (missing.Count == 0)
        {
            EditorUtility.DisplayDialog("Validate DB vs Code", "✅ All AudioEventId.* references in code exist in the AudioDatabase.", "OK");
            return;
        }

        string msg = "❌ These AudioEventId names are referenced in code but missing from the AudioDatabase:\n\n" +
                     string.Join("\n", missing.Take(120));

        if (missing.Count > 120)
            msg += $"\n\n...and {missing.Count - 120} more.";

        msg += "\n\nFix: add entries (or run SYNC if you have matching AC_* cues), then Regenerate.";

        EditorUtility.DisplayDialog("Validate DB vs Code", msg, "OK");
    }

    private static void CreateNewEvent(
        AudioDatabase db,
        NewEventGroup group,
        string rawName,
        AudioClip clip,
        bool createCueAsset,
        bool autoRegenerate)
    {
        Undo.RecordObject(db, "Create audio event");
        db.MigrateLegacyEntriesIfNeeded();

        string enumName = AudioEventIdGenerator.SanitizeToIdentifier(rawName);

        if (db.ContainsName(enumName))
            throw new Exception($"An audio event named '{enumName}' already exists in this database.");

        int value = db.GetNextAvailableValue();

        AudioCue cue = null;
        if (createCueAsset)
            cue = CreateCueAsset(enumName, group, clip);

        var entry = new AudioDatabase.Entry
        {
            id = (AudioEventId)value,
            name = enumName,
            value = value,
            assigned = true,
            deprecated = false,
            cue = cue
        };

        AddEntryToGroup(db, group, entry);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        if (autoRegenerate)
            RegenerateEnum(db, showDialogOnSuccess: false, openScript: false);
    }

    private static AudioCue CreateCueAsset(string enumName, NewEventGroup group, AudioClip clip)
    {
        const string root = "Assets/Audio/Assets";

        string folder = group switch
        {
            NewEventGroup.UI => $"{root}/SFX/UI",
            NewEventGroup.AMBIENT => $"{root}/SFX/Ambient",
            NewEventGroup.VGM => $"{root}/BGM",
            _ => $"{root}/SFX",
        };

        EnsureFolder(folder);

        var cue = ScriptableObject.CreateInstance<AudioCue>();
        cue.clips = clip != null ? new[] { clip } : new AudioClip[0];

        if (group == NewEventGroup.UI)
        {
            cue.spatialBlend = 0f;
            cue.cooldownSeconds = 0.07f;
            cue.pitchJitter = new Vector2(0.99f, 1.01f);
            cue.volumeJitter = new Vector2(0.98f, 1.02f);
        }
        else
        {
            cue.spatialBlend = 1f;
            cue.cooldownSeconds = 0f;
        }

        string assetPath = $"{folder}/AC_{enumName}.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(cue, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        EditorGUIUtility.PingObject(cue);
        return cue;
    }

    private static void EnsureFolder(string folderPath)
    {
        var parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif