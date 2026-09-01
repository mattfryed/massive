using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Serializable]
    public struct SceneMusicOverride
    {
        public string sceneName;

        [Tooltip("If true, music will fade out and stop when this scene loads.")]
        public bool stopMusic;

        [Tooltip("If stopMusic is false, this profile will be played for the scene (optional).")]
        public LevelAudioProfile profile;
    }

    [Header("Scene Overrides (optional)")]
    [Tooltip("Use this for menu scenes (Attract/Level Select/PostGame/etc). Gameplay uses SelectedLevel.audioProfile.")]
    [SerializeField] private SceneMusicOverride[] sceneOverrides;

    [Header("Gameplay Rules")]
    [SerializeField] private bool useSelectedLevelProfileForGameplayScene = true;

    [Tooltip("If true, the Instructions scene will use SelectedLevel.audioProfile (so the level track can start before gameplay).")]
    [SerializeField] private bool useSelectedLevelProfileForInstructionsScene = true;

    [Header("Fallback (optional)")]
    [Tooltip("If nothing else requests music, and nothing is currently playing, play this.")]
    [SerializeField] private LevelAudioProfile defaultProfile;

    [Header("Audio")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Tooltip("Uses unscaled time so fades still work during Time.timeScale = 0 (menus/pause).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("If not assigned, sources will be auto-created as children.")]
    [SerializeField] private AudioSource sourceA;

    [Tooltip("If not assigned, sources will be auto-created as children.")]
    [SerializeField] private AudioSource sourceB;

    [Header("Mixer (optional)")]
    [SerializeField] private AudioMixer audioMixer; // for future modulation (snapshots/params)

    private AudioSource _active;
    private AudioSource _inactive;
    private Coroutine _fadeRoutine;
    private LevelAudioProfile _currentProfile;

    private float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void Bootstrap() => EnsureExists();


    public static void EnsureExists()
    {
        if (Instance != null) return;

        Instance = FindFirstObjectByType<BGMManager>();
        if (Instance != null)
        {
            DontDestroyOnLoad(Instance.gameObject);
            return;
        }

        var go = new GameObject("BGMManager");
        Instance = go.AddComponent<BGMManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSourcesExistAndConfigured();

        _active = sourceA;
        _inactive = sourceB;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Handle the already-active scene (in case we were placed in scene instead of bootstrapped)
        ApplyMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMusicForScene(scene.name);
    }

    private void EnsureSourcesExistAndConfigured()
    {
        if (sourceA == null) sourceA = CreateChildSource("MusicSourceA");
        if (sourceB == null) sourceB = CreateChildSource("MusicSourceB");

        ConfigureSource(sourceA);
        ConfigureSource(sourceB);
    }

    private AudioSource CreateChildSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        return src;
    }

    private void ConfigureSource(AudioSource src)
    {
        src.playOnAwake = false;
        src.spatialBlend = 0f;       // 2D
        src.dopplerLevel = 0f;
        src.loop = true;
        src.volume = 0f;
    }

    private void ApplyMusicForScene(string sceneName)
    {
        // 1) Explicit override?
        if (TryGetOverride(sceneName, out var ov))
        {
            if (ov.stopMusic)
            {
                StopMusic(); // uses current profile fadeOut if available
                return;
            }

            if (ov.profile != null)
            {
                PlayProfile(ov.profile);
                return;
            }

            // override exists but no profile + not stop => do nothing
            return;
        }

        // 2) Gameplay scene uses SelectedLevel.audioProfile (data-driven).
        GameFlowContext.EnsureExists();
        var ctx = GameFlowContext.Instance;

        if (ctx != null && ctx.SelectedLevel != null)
        {
            // If the loaded scene is the selected gameplay scene, use the level's audio profile
            if (useSelectedLevelProfileForGameplayScene &&
                string.Equals(ctx.SelectedLevel.SceneName, sceneName, StringComparison.Ordinal))
            {
                PlayProfile(ctx.SelectedLevel.audioProfile);
                return;
            }

            // Optionally also start / maintain the level music during Instructions
            if (useSelectedLevelProfileForInstructionsScene &&
                string.Equals(sceneName, SceneFlow.InstructionsScene, StringComparison.Ordinal))
            {
                PlayProfile(ctx.SelectedLevel.audioProfile);
                return;
            }
        }

        // 3) Fallback only if nothing is playing already
        if ((_currentProfile == null || _active == null || _active.clip == null || !_active.isPlaying) && defaultProfile != null)
        {
            PlayProfile(defaultProfile);
        }
    }

    private bool TryGetOverride(string sceneName, out SceneMusicOverride ov)
    {
        if (sceneOverrides != null)
        {
            for (int i = 0; i < sceneOverrides.Length; i++)
            {
                if (string.Equals(sceneOverrides[i].sceneName, sceneName, StringComparison.Ordinal))
                {
                    ov = sceneOverrides[i];
                    return true;
                }
            }
        }

        ov = default;
        return false;
    }

    public void PlayProfile(LevelAudioProfile profile)
    {
        if (profile == null || profile.bgmClip == null)
        {
            StopMusic();
            return;
        }

        EnsureSourcesExistAndConfigured();
        if (_active == null) _active = sourceA;
        if (_inactive == null) _inactive = sourceB;

        // If we're already playing the same clip, KEEP PLAYING (no restart).
        // We can still smoothly adjust volume/output/loop settings.
        if (_active.isPlaying && _active.clip == profile.bgmClip)
        {
            _active.loop = profile.loop;
            if (profile.outputMixerGroup != null) _active.outputAudioMixerGroup = profile.outputMixerGroup;

            float targetVol = Mathf.Clamp01(profile.volume * masterVolume);
            StartFadeRoutine(FadeVolumeRoutine(_active, targetVol, profile.fadeInSeconds));
            _currentProfile = profile;
            return;
        }

        // If nothing is playing, just fade in on the active source.
        if (!_active.isPlaying || _active.clip == null)
        {
            _active.clip = profile.bgmClip;
            _active.loop = profile.loop;
            if (profile.outputMixerGroup != null) _active.outputAudioMixerGroup = profile.outputMixerGroup;

            _active.volume = 0f;
            _active.Play();

            float targetVol = Mathf.Clamp01(profile.volume * masterVolume);
            StartFadeRoutine(FadeVolumeRoutine(_active, targetVol, profile.fadeInSeconds));
            _currentProfile = profile;
            return;
        }

        // Otherwise: crossfade
        StartFadeRoutine(CrossfadeRoutine(_active, _inactive, profile));
    }

    public void StopMusic()
    {
        if (_active == null || !_active.isPlaying)
        {
            _currentProfile = null;
            return;
        }

        float fadeOut = _currentProfile != null ? _currentProfile.fadeOutSeconds : 0.25f;
        StartFadeRoutine(FadeOutAndStopRoutine(_active, fadeOut));
        _currentProfile = null;
    }

    private void StartFadeRoutine(IEnumerator routine)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(routine);
    }

    private IEnumerator FadeVolumeRoutine(AudioSource src, float targetVolume, float seconds)
    {
        if (src == null) yield break;

        float start = src.volume;
        if (seconds <= 0f)
        {
            src.volume = targetVolume;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float a = Mathf.Clamp01(t / seconds);
            src.volume = Mathf.Lerp(start, targetVolume, a);
            yield return null;
        }

        src.volume = targetVolume;
    }

    private IEnumerator FadeOutAndStopRoutine(AudioSource src, float seconds)
    {
        if (src == null) yield break;

        float start = src.volume;
        if (seconds <= 0f)
        {
            src.Stop();
            src.clip = null;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float a = Mathf.Clamp01(t / seconds);
            src.volume = Mathf.Lerp(start, 0f, a);
            yield return null;
        }

        src.volume = 0f;
        src.Stop();
        src.clip = null;
    }

    private IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, LevelAudioProfile next)
    {
        if (to == null || next == null || next.bgmClip == null)
            yield break;

        // Prepare "to"
        to.Stop();
        to.clip = next.bgmClip;
        to.loop = next.loop;
        if (next.outputMixerGroup != null) to.outputAudioMixerGroup = next.outputMixerGroup;

        float inTarget = Mathf.Clamp01(next.volume * masterVolume);

        to.volume = 0f;
        to.Play();

        float outSeconds = _currentProfile != null ? _currentProfile.fadeOutSeconds : 0.5f;
        float inSeconds = next.fadeInSeconds;

        // Single timeline so it feels like a cohesive crossfade
        float duration = Mathf.Max(outSeconds, inSeconds, 0.0001f);

        float fromStartVol = (from != null) ? from.volume : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Dt;

            float in01 = (inSeconds <= 0f) ? 1f : Mathf.Clamp01(t / inSeconds);
            float out01 = (outSeconds <= 0f) ? 1f : Mathf.Clamp01(t / outSeconds);

            to.volume = Mathf.Lerp(0f, inTarget, in01);

            if (from != null && from.isPlaying)
                from.volume = Mathf.Lerp(fromStartVol, 0f, out01);

            yield return null;
        }

        to.volume = inTarget;

        if (from != null)
        {
            from.volume = 0f;
            from.Stop();
            from.clip = null;
        }

        // Swap active/inactive references
        _active = to;
        _inactive = from;
        _currentProfile = next;
    }

    // ---- Optional: expose mixer helpers for modulation scripts ----

    public bool TrySetMixerFloat(string paramName, float value)
    {
        if (audioMixer == null || string.IsNullOrEmpty(paramName)) return false;
        return audioMixer.SetFloat(paramName, value);
    }

    public AudioMixer Mixer => audioMixer;
}
