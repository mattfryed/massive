using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public sealed class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }

    [Header("Catalogs")]
    [SerializeField] private SceneAudioCatalog sceneCatalog;

    [Header("Mixer (optional)")]
    [SerializeField] private AudioMixer mixer;

    [Header("Players")]
    [SerializeField] private MusicPlayer musicPlayer;
    [SerializeField] private SfxBus sfxBus;

    private readonly Stack<MusicCue> musicOverrideStack = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void Start()
    {
        // Apply correct audio for the first scene
        ApplyProfileForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneChanged(Scene from, Scene to)
    {
        ApplyProfileForScene(to.name);
    }

    private void ApplyProfileForScene(string sceneName)
    {
        // 1) If this is the selected gameplay scene, use LevelDefinition audio profile
        GameFlowContext.EnsureExists();
        var selected = GameFlowContext.Instance.SelectedLevel;
        if (selected != null && selected.SceneName == sceneName && selected.audioProfile != null)
        {
            ApplyProfile(selected.audioProfile);
            return;
        }

        // 2) Otherwise, use catalog (front-end, results, etc.)
        var prof = sceneCatalog != null ? sceneCatalog.Get(sceneName) : null;
        if (prof != null) ApplyProfile(prof);
    }

    private void ApplyProfile(SceneAudioProfile profile)
    {
        if (profile.snapshot != null)
            profile.snapshot.TransitionTo(profile.snapshotTransition);

        // Music override stack wins if present
        var musicToPlay = (musicOverrideStack.Count > 0)
            ? musicOverrideStack.Peek()
            : profile.music;

        musicPlayer.Play(musicToPlay);

        // Optional ambience loop as “SFX” loop bus (kept simple here)
        if (profile.ambienceLoop != null)
            sfxBus.PlayLoop(profile.ambienceLoop);
        else
            sfxBus.StopLoop();
    }

    // ---------- Public API ----------
    public static void PlaySfx(AudioCue cue, Vector3 worldPos)
    {
        if (Instance == null || cue == null) return;
        Instance.sfxBus.PlayOneShot(cue, worldPos);
    }

    public static void PlayUi(AudioCue cue)
    {
        if (Instance == null || cue == null) return;
        Instance.sfxBus.PlayOneShot2D(cue);
    }

    public void PushMusicOverride(MusicCue cue)
    {
        if (cue == null) return;
        musicOverrideStack.Push(cue);
        musicPlayer.Play(cue);
    }

    public void PopMusicOverride()
    {
        if (musicOverrideStack.Count == 0) return;
        musicOverrideStack.Pop();
        ApplyProfileForScene(SceneManager.GetActiveScene().name);
    }

    public void TriggerDangerSnapshot(LevelAudioProfile levelProfile)
    {
        if (levelProfile?.dangerSnapshot == null) return;
        levelProfile.dangerSnapshot.TransitionTo(levelProfile.dangerSnapshotTransition);
    }
}