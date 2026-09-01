using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Audio Database", fileName = "AudioDatabase")]
public class AudioDatabase : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        // Legacy compatibility: keep numeric stability even if enum regenerates.
        [HideInInspector] public AudioEventId id;

        [Tooltip("Enum member name to generate (e.g. Player_Death, UI_Navigate).")]
        public string name;

        [Tooltip("Stable integer value. Assigned once by tooling.")]
        public int value;

        [HideInInspector] public bool assigned;

        [Tooltip("If true, generator keeps this ID but marks it Obsolete.")]
        public bool deprecated;

        public AudioCue cue;
    }

    [Header("SFX Entries")]
    public List<Entry> entries = new();

    [Header("UI Entries")]
    public List<Entry> uiEntries = new();

    [Header("Ambient Entries")]
    public List<Entry> ambientEntries = new();

    [Header("VGM Entries (temporary; MusicCue later)")]
    public List<Entry> vgmEntries = new();

    private Dictionary<int, AudioCue> _map;

    private void OnValidate()
    {
        MigrateLegacyEntriesIfNeeded();
    }

    public void Build()
    {
        MigrateLegacyEntriesIfNeeded();

        _map = new Dictionary<int, AudioCue>(512);
        AddList(entries);
        AddList(uiEntries);
        AddList(ambientEntries);
        AddList(vgmEntries);
    }

    private void AddList(List<Entry> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];

            if (!e.assigned)
            {
                e.value = (int)e.id;
                if (string.IsNullOrWhiteSpace(e.name))
                    e.name = e.id.ToString();
                e.assigned = true;
                list[i] = e;
            }

            if (e.cue == null) continue;
            _map[e.value] = e.cue;
        }
    }

    public bool TryGet(AudioEventId id, out AudioCue cue)
    {
        if (_map == null || _map.Count == 0) Build();
        return _map.TryGetValue((int)id, out cue);
    }

    public IEnumerable<(Entry entry, string group)> EnumerateAllEntriesWithGroup(bool includeDeprecated = true)
    {
        foreach (var e in entries)
            if (includeDeprecated || !e.deprecated) yield return (e, "SFX");

        foreach (var e in uiEntries)
            if (includeDeprecated || !e.deprecated) yield return (e, "UI");

        foreach (var e in ambientEntries)
            if (includeDeprecated || !e.deprecated) yield return (e, "AMBIENT");

        foreach (var e in vgmEntries)
            if (includeDeprecated || !e.deprecated) yield return (e, "VGM");
    }

    public void MigrateLegacyEntriesIfNeeded()
    {
        MigrateList(entries);
        MigrateList(uiEntries);
        MigrateList(ambientEntries);
        MigrateList(vgmEntries);
    }

    private void MigrateList(List<Entry> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.assigned) continue;

            e.value = (int)e.id;
            if (string.IsNullOrWhiteSpace(e.name))
                e.name = e.id.ToString();

            e.assigned = true;
            list[i] = e;
        }
    }

    public int GetNextAvailableValue()
    {
        int max = -1;
        foreach (var (e, _) in EnumerateAllEntriesWithGroup(includeDeprecated: true))
        {
            int v = e.assigned ? e.value : (int)e.id;
            if (v > max) max = v;
        }
        return max + 1;
    }

    public bool ContainsName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        foreach (var (e, _) in EnumerateAllEntriesWithGroup(includeDeprecated: true))
        {
            if (string.Equals(e.name, name, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}