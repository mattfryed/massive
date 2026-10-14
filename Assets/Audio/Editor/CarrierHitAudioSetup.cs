#if UNITY_EDITOR
using System;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

/// <summary>Registers the Carrier's original impact clip through the existing audio database.</summary>
public static class CarrierHitAudioSetup
{
    public const string ClipPath = "Assets/Audio/Assets/SFX/Carrier_CrystallineHit.wav";
    public const string CuePath = "Assets/Audio/Assets/SFX/AC_Enemy_CarrierHit.asset";
    public const string DatabasePath = "Assets/Audio/Assets/SFX/AudioDatabase_Main.asset";
    public const string DefinitionPath = "Assets/Enemy System/Enemy Types/Melee/Carrier/ED_Carrier.asset";
    public const string EventName = "Enemy_CarrierHit";

    [MenuItem("Tools/MASSIVE/Audio/Install Carrier Hit Audio")]
    private static void InstallFromMenu() => Debug.Log(Install());

    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Install Carrier hit audio in Edit Mode.");

        var database = AssetDatabase.LoadAssetAtPath<AudioDatabase>(DatabasePath);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!database || !definition)
            throw new InvalidOperationException("Carrier definition or main audio database is missing.");

        int index = database.entries.FindIndex(entry => entry.name == EventName);
        if (index < 0 && database.ContainsName(EventName))
            throw new InvalidOperationException("Carrier hit event already exists outside the SFX group.");
        int value = index >= 0 ? database.entries[index].value : database.GetNextAvailableValue();
        if ((int)definition.sfxHit != 0 && (int)definition.sfxHit != value)
            throw new InvalidOperationException("Carrier already has a different hit sound; preserving its assignment.");

        var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(CuePath);
        if (index >= 0 && database.entries[index].cue && database.entries[index].cue != cue)
            throw new InvalidOperationException("Carrier hit event points to a different cue; preserving its assignment.");
        if (index >= 0 && (!database.entries[index].assigned || database.entries[index].deprecated))
            throw new InvalidOperationException("Carrier hit event is unassigned or deprecated; preserving it.");

        AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (!clip) throw new InvalidOperationException("Carrier crystalline impact WAV is missing or could not import.");
        var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
        if (importer)
        {
            var settings = importer.defaultSampleSettings;
            if (settings.compressionFormat != AudioCompressionFormat.PCM ||
                settings.loadType != AudioClipLoadType.DecompressOnLoad || importer.forceToMono || importer.loadInBackground)
            {
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                // Already mono; don't run a downmix/normalization pass on the bounded source.
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            }
        }

        if (!cue)
        {
            cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.clips = new[] { clip };
            cue.volume = .75f;
            cue.volumeJitter = new Vector2(.94f, 1f);
            cue.pitchJitter = new Vector2(.96f, 1.04f);
            cue.cooldownSeconds = .04f;
            cue.maxSimultaneous = 4;
            // Match the game's existing screen-space impact mix; audio suppression
            // and voice limits still run through AudioSystem, including Enemy Lab.
            cue.spatialBlend = 0f;
            AssetDatabase.CreateAsset(cue, CuePath);
            Undo.RegisterCreatedObjectUndo(cue, "Create Carrier hit audio cue");
        }

        if (index < 0 || database.entries[index].cue != cue)
        {
            Undo.RecordObject(database, "Register Carrier hit sound");
            if (index < 0)
                database.entries.Add(new AudioDatabase.Entry
                {
                    id = (AudioEventId)value, name = EventName, value = value,
                    assigned = true, deprecated = false, cue = cue
                });
            else
            {
                var entry = database.entries[index];
                entry.cue = cue;
                database.entries[index] = entry;
            }
            EditorUtility.SetDirty(database);
        }
        if ((int)definition.sfxHit != value)
        {
            Undo.RecordObject(definition, "Assign Carrier hit sound");
            definition.sfxHit = (AudioEventId)value;
            EditorUtility.SetDirty(definition);
        }
        database.Build();
        AssetDatabase.SaveAssetIfDirty(cue);
        AssetDatabase.SaveAssetIfDirty(database);
        AssetDatabase.SaveAssetIfDirty(definition);

        // Generate through the project's existing stable-ID tool, never hand-edit
        // AudioEventId.cs. Repeated installation preserves all existing tuning.
        if (!Enum.TryParse<AudioEventId>(EventName, out var generated) || (int)generated != value)
        {
            AudioEventIdGenerator.Generate(database, AudioEventIdGenerator.DefaultOutputPath, false);
            AssetDatabase.SaveAssetIfDirty(database);
        }
        return $"Carrier hit audio installed: {EventName} = {value}, {clip.length:F2}s, {clip.channels} channel(s).";
    }
}
#endif
