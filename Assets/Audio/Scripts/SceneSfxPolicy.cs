using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Scene-owned SFX silence, including AudioSources added by future prefabs.</summary>
public static class SceneSfxPolicy
{
    private static readonly HashSet<Object> owners = new();
    private static readonly Dictionary<AudioSource, bool> musicSources = new();
    private static readonly Dictionary<AudioSource, bool> previousBypasses = new();
    private static bool previousListenerPause;

    public static bool IsSuppressed { get; private set; }
    public static event Action SilenceChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (IsSuppressed) Restore();
        owners.Clear();
        musicSources.Clear();
        previousBypasses.Clear();
        SilenceChanged = null;
    }

    public static void BeginSilence(Object owner)
    {
        if (!owner || !owners.Add(owner) || IsSuppressed) return;
        IsSuppressed = true;
        previousListenerPause = AudioListener.pause;
        foreach (var entry in musicSources)
            if (entry.Key) entry.Key.ignoreListenerPause = true;

        // Listener pause also covers direct Play/PlayOneShot and play-on-awake
        // on sources instantiated later, without scanning the scene every frame.
        AudioListener.pause = true;
        foreach (var source in SceneSources())
        {
            if (musicSources.ContainsKey(source)) continue;
            previousBypasses[source] = source.ignoreListenerPause;
            source.ignoreListenerPause = false;
            source.Stop();
        }
        SilenceChanged?.Invoke();
    }

    public static void EndSilence(Object owner)
    {
        if (!owners.Remove(owner) || owners.Count != 0) return;
        Restore();
    }

    private static void Restore()
    {
        // Discard requests made during listener pause before restoring audio,
        // including sources that survive the scene in DontDestroyOnLoad.
        foreach (var source in SceneSources())
            if (!musicSources.ContainsKey(source)) source.Stop();
        SilenceChanged?.Invoke();
        foreach (var entry in previousBypasses)
            if (entry.Key) entry.Key.ignoreListenerPause = entry.Value;
        previousBypasses.Clear();
        foreach (var entry in musicSources)
            if (entry.Key) entry.Key.ignoreListenerPause = entry.Value;
        AudioListener.pause = previousListenerPause;
        IsSuppressed = false;
    }

    private static IEnumerable<AudioSource> SceneSources()
    {
        // FindObjectsByType returns no objects during scene teardown. The loaded
        // object lookup still includes persistent sources at this boundary.
        // Exclude prefab/assets; this scan runs only when silence starts/ends.
        foreach (var source in Resources.FindObjectsOfTypeAll<AudioSource>())
            if (source && source.gameObject.scene.IsValid()) yield return source;
    }

    // Only music players opt out; ordinary/new AudioSources remain silent by default.
    public static void RegisterMusicSource(AudioSource source)
    {
        if (!source || musicSources.ContainsKey(source)) return;
        bool original = previousBypasses.TryGetValue(source, out var saved) ? saved : source.ignoreListenerPause;
        previousBypasses.Remove(source);
        musicSources.Add(source, original);
        if (IsSuppressed) source.ignoreListenerPause = true;
    }

    public static void UnregisterMusicSource(AudioSource source)
    {
        if (source && musicSources.TryGetValue(source, out var original))
            source.ignoreListenerPause = original;
        if (!ReferenceEquals(source, null)) musicSources.Remove(source);
    }
}
