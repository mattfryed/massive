#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises scene audio boundaries in Play Mode without saving authored assets.</summary>
[InitializeOnLoad]
public static class HowToPlayAudioValidation
{
    const string Key = "MASSIVE.HowToAudioValidation";
    const string Output = "Library/HowToPlayAudioValidation/report.txt";
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool originalBackground;

    static HowToPlayAudioValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Audio/Validate How To Play Silence")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, "STARTING\n");
        SessionState.SetBool(Key, true);
        // Load How To only after entering Play; the user's unsaved editor scene is preserved.
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1;
            deadline = EditorApplication.timeSinceStartup + 120;
            originalBackground = Application.runInBackground; Application.runInBackground = true;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log; Application.runInBackground = originalBackground;
            routine = null;
        }
    }

    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            frame = Time.frameCount;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Audio validation timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        bool passed = failure == null && errors.Count == 0;
        File.WriteAllText(Output, (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) +
            "\n" + failure + "\n" + string.Join("\n", errors));
        Debug.Log("HOW TO AUDIO VALIDATION " + (passed ? "PASSED" : "FAILED: " + failure));
        EditorApplication.isPlaying = false;
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks.Add("PASS " + label);
        File.WriteAllText(Output, "RUNNING\n" + string.Join("\n", checks));
    }

    static AudioSource Source(Transform parent, string name, AudioClip clip, bool playOnAwake)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(parent);
        var source = go.AddComponent<AudioSource>(); source.clip = clip;
        source.playOnAwake = playOnAwake; source.loop = true; source.volume = .37f;
        go.SetActive(true); return source;
    }

    static IEnumerator Checks()
    {
        yield return null;
        if (SceneSfxPolicy.IsSuppressed)
        {
            SceneManager.LoadScene(SceneFlow.ChooseModeScene);
            yield return null;
        }
        Check(!AudioListener.pause && !SceneSfxPolicy.IsSuppressed, "Normal scene starts without an SFX mute");
        var clip = AudioClip.Create("Silent audio validation fixture", 44100 * 30, 1, 44100, false);
        var profile = ScriptableObject.CreateInstance<LevelAudioProfile>();
        profile.bgmClip = clip; profile.volume = .41f; profile.fadeInSeconds = 0; profile.fadeOutSeconds = 0;
        BGMManager.EnsureExists();
        // Isolate the policy from authored music crossfades. These are runtime-only
        // instance edits; the user's scene settings return when Play Mode stops.
        var musicSettings = new SerializedObject(BGMManager.Instance);
        musicSettings.FindProperty("sceneOverrides").arraySize = 0;
        musicSettings.ApplyModifiedPropertiesWithoutUndo();
        BGMManager.Instance.PlayProfile(profile);
        yield return null;
        var music = BGMManager.Instance.GetComponentsInChildren<AudioSource>().Single(s => s.isPlaying);
        bool previousMusicBypass = music.ignoreListenerPause;
        float musicVolume = music.volume;
        SceneManager.LoadScene(SceneFlow.HowToPlayScene);
        yield return null;
        var controller = Object.FindFirstObjectByType<HowToPlayScreenController>();
        Check(controller && SceneSfxPolicy.IsSuppressed && AudioListener.pause, "How To acquires silence on scene entry");
        controller.StopAllCoroutines(); controller.enabled = false;
        Check(SceneSfxPolicy.IsSuppressed, "Disabling the continue controller does not unmute the scene");
        Check(music && music.isPlaying && music.ignoreListenerPause && Mathf.Approximately(music.volume, musicVolume),
            "Existing BGM keeps playing at its original volume");

        var fixtures = new GameObject("How To audio validation fixtures"); Object.DontDestroyOnLoad(fixtures);
        var direct = Source(fixtures.transform, "Future direct SFX", clip, false); direct.Play(); direct.PlayOneShot(clip);
        var automatic = Source(fixtures.transform, "Future play-on-awake SFX", clip, true);
        var lateMusic = Source(fixtures.transform, "Late registered music", clip, false);
        SceneSfxPolicy.RegisterMusicSource(lateMusic); lateMusic.Play();
        int directSample = direct.timeSamples, automaticSample = automatic.timeSamples;
        int musicSample = music.timeSamples, lateMusicSample = lateMusic.timeSamples;

        var system = AudioSystem.I; Check(system != null, "Real How To AudioSystem is present");
        var originalDatabase = system.database;
        var cue = ScriptableObject.CreateInstance<AudioCue>(); cue.clips = new[] { clip };
        cue.cooldownSeconds = 30; cue.maxSimultaneous = 1;
        var database = ScriptableObject.CreateInstance<AudioDatabase>();
        database.entries.Add(new AudioDatabase.Entry { id = AudioEventId.Player_Charge, cue = cue }); database.Build();
        system.database = database;
        var legacy = direct.gameObject.AddComponent<SfxPlayerScript>(); legacy.testSFX = clip;
        system.database = null; legacy.SafePlay("testSFX"); system.database = database;

        var template = Source(fixtures.transform, "SFX pool template", null, false);
        var busObject = new GameObject("SFX bus validation"); busObject.SetActive(false); busObject.transform.SetParent(fixtures.transform);
        var bus = busObject.AddComponent<SfxBus>();
        var serialized = new SerializedObject(bus); serialized.FindProperty("oneShotPrefab").objectReferenceValue = template;
        serialized.FindProperty("poolSize").intValue = 2; serialized.ApplyModifiedPropertiesWithoutUndo(); busObject.SetActive(true);
        float until = Time.unscaledTime + 12;
        while (Time.unscaledTime < until)
        {
            system.Play(AudioEventId.Player_Charge, Vector3.zero); system.Play2D(AudioEventId.Player_Charge);
            bus.PlayOneShot(cue, Vector3.zero); bus.PlayOneShot2D(cue); bus.PlayLoop(cue);
            yield return null;
        }
        Check(Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None).Any(d => d.Primary), "Real player demonstrations run during the silence check");
        Check(system.GetComponentsInChildren<AudioSource>().All(s => !s.isPlaying && !s.clip), "Repeated pooled 2D/3D SFX requests allocate no playback");
        Check(bus.GetComponentsInChildren<AudioSource>().All(s => !s.isPlaying && !s.clip), "SfxBus one-shots and ambience loops are blocked");
        Check(direct.timeSamples == directSample && automatic.timeSamples == automaticSample,
            "New direct, legacy fallback, and play-on-awake sources do not advance");
        Check(music.timeSamples != musicSample && lateMusic.timeSamples != lateMusicSample,
            "Existing and newly registered music continue advancing during silence");
        Check(Mathf.Approximately(direct.volume, .37f), "SFX volume settings remain unchanged");
        // Leave a pending direct sound just before unloading to catch delayed leakage.
        direct.Play(); automatic.Play();
        SceneManager.LoadScene(SceneFlow.ChooseModeScene);
        yield return null;
        Check(!SceneSfxPolicy.IsSuppressed && !AudioListener.pause, "Scene exit restores normal audio automatically");
        Check(!direct.isPlaying && !automatic.isPlaying, $"Queued direct SFX cannot spill into the following scene (direct={direct.isPlaying}, automatic={automatic.isPlaying}, directSample={direct.timeSamples}, automaticSample={automatic.timeSamples})");
        Check(music.ignoreListenerPause == previousMusicBypass && !lateMusic.ignoreListenerPause, "Music listener settings are restored");
        system.Play2D(AudioEventId.Player_Charge);
        bus.PlayOneShot(cue, Vector3.zero);
        yield return null;
        Check(system.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying), "Central SFX plays after exit with no consumed cooldown/concurrency budget");
        Check(bus.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying), "SfxBus plays normally after exit");
        SceneManager.LoadScene(SceneFlow.HowToPlayScene);
        yield return null;
        Check(SceneSfxPolicy.IsSuppressed && system.GetComponentsInChildren<AudioSource>().All(s => !s.clip),
            "Re-entering How To stops prior pooled SFX and clears the pool");
        SceneManager.LoadScene(SceneFlow.ChooseModeScene);
        yield return null;
        AudioListener.pause = true;
        SceneSfxPolicy.BeginSilence(fixtures); SceneSfxPolicy.BeginSilence(fixtures);
        SceneSfxPolicy.EndSilence(fixtures);
        Check(AudioListener.pause && !SceneSfxPolicy.IsSuppressed, "Idempotent silence restores a pre-existing listener pause");
        AudioListener.pause = false;
        system.database = originalDatabase; SceneSfxPolicy.UnregisterMusicSource(lateMusic);
        Object.Destroy(fixtures); Object.Destroy(database); Object.Destroy(cue);
        // Transient music profile/clip are released automatically on exiting Play Mode.
    }
}
#endif
