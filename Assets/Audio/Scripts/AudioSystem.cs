using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-100)]
public class AudioSystem : MonoBehaviour
{
    public static AudioSystem I { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (I != null) return;

        var existing = Object.FindFirstObjectByType<AudioSystem>();
        if (existing != null)
        {
            I = existing;
            return;
        }

        var go = new GameObject("AudioRoot_Auto");
        go.AddComponent<AudioSystem>();
    }

    [Header("Database (AudioEventId -> AudioCue)")]
    public AudioDatabase database;

    [Header("Optional Mixer Routing (can be null for now)")]
    public AudioMixerGroup defaultSfxGroup;

    [Header("Pool")]
    public int poolSize = 24;

    private readonly List<AudioSource> _pool = new();
    private readonly Dictionary<AudioCue, float> _nextAllowedTime = new();
    private readonly Dictionary<AudioCue, int> _activeCounts = new();

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("SFX_Source");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.outputAudioMixerGroup = defaultSfxGroup; // may be null
            _pool.Add(src);
        }

        if (database != null) database.Build();
    }

    public void Play(AudioEventId id, Vector3 position)
    {
        if (database == null) return;
        if (!database.TryGet(id, out var cue) || cue == null) return;

        PlayCueInternal(cue, position, force2D: false);
    }

    public void Play2D(AudioEventId id)
    {
        if (database == null) return;
        if (!database.TryGet(id, out var cue) || cue == null) return;

        PlayCueInternal(cue, Vector3.zero, force2D: true);
    }

    private void PlayCueInternal(AudioCue cue, Vector3 position, bool force2D)
    {
        float now = Time.unscaledTime;

        if (cue.cooldownSeconds > 0f &&
            _nextAllowedTime.TryGetValue(cue, out float next) &&
            now < next)
            return;

        if (cue.maxSimultaneous > 0)
        {
            _activeCounts.TryGetValue(cue, out int count);
            if (count >= cue.maxSimultaneous) return;
            _activeCounts[cue] = count + 1;
        }

        _nextAllowedTime[cue] = now + Mathf.Max(0f, cue.cooldownSeconds);

        var clip = cue.GetRandomClip();
        if (clip == null) { DecActive(cue); return; }

        var src = GetFreeSource();
        if (src == null) { DecActive(cue); return; }

        src.transform.position = position;

        src.outputAudioMixerGroup = cue.output != null ? cue.output : defaultSfxGroup;

        src.spatialBlend = force2D ? 0f : cue.spatialBlend;
        src.minDistance = cue.minDistance;
        src.maxDistance = cue.maxDistance;

        float volMul = Random.Range(cue.volumeJitter.x, cue.volumeJitter.y);
        src.volume = cue.volume * volMul;
        src.pitch = Random.Range(cue.pitchJitter.x, cue.pitchJitter.y);

        src.clip = clip;
        src.loop = false;
        src.Play();

        float pitchAbs = Mathf.Max(0.01f, Mathf.Abs(src.pitch));
        StartCoroutine(ReleaseAfter(src, cue, clip.length / pitchAbs));
    }

    private AudioSource GetFreeSource()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].isPlaying)
                return _pool[i];
        return null;
    }

    private IEnumerator ReleaseAfter(AudioSource src, AudioCue cue, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        if (src != null)
        {
            src.Stop();
            src.clip = null;
        }

        DecActive(cue);
    }

    private void DecActive(AudioCue cue)
    {
        if (cue == null || cue.maxSimultaneous <= 0) return;
        _activeCounts.TryGetValue(cue, out int count);
        _activeCounts[cue] = Mathf.Max(0, count - 1);
    }
}