using System.Collections.Generic;
using UnityEngine;

public class SfxBus : MonoBehaviour
{
    [SerializeField] private AudioSource oneShotPrefab;
    [SerializeField] private int poolSize = 24;

    private readonly List<AudioSource> pool = new();
    private readonly Dictionary<AudioCue, float> lastPlayTime = new();
    private readonly Dictionary<AudioCue, int> activeCounts = new();

    private AudioSource loopSource;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var src = Instantiate(oneShotPrefab, transform);
            src.playOnAwake = false;
            pool.Add(src);
        }

        loopSource = Instantiate(oneShotPrefab, transform);
        loopSource.loop = true;
        loopSource.playOnAwake = false;
    }

    public void PlayOneShot(AudioCue cue, Vector3 pos)
    {
        if (!CanPlay(cue)) return;

        var clip = cue.GetRandomClip();
        if (clip == null) return;

        var src = GetFreeSource();
        if (src == null) return;

        ConfigureSource(src, cue, pos);
        src.clip = clip;
        src.loop = false;
        src.Play();

        MarkPlayed(cue);
    }

    public void PlayOneShot2D(AudioCue cue)
    {
        PlayOneShot(cue, Vector3.zero);
    }

    public void PlayLoop(AudioCue cue)
    {
        if (cue == null)
        {
            StopLoop();
            return;
        }

        var clip = cue.GetRandomClip();
        if (clip == null) return;

        ConfigureSource(loopSource, cue, Vector3.zero);
        loopSource.clip = clip;
        loopSource.loop = true;

        if (!loopSource.isPlaying)
            loopSource.Play();
    }

    public void StopLoop()
    {
        if (loopSource.isPlaying)
            loopSource.Stop();
        loopSource.clip = null;
    }

    private void ConfigureSource(AudioSource src, AudioCue cue, Vector3 pos)
    {
        src.outputAudioMixerGroup = cue.output;
        src.transform.position = pos;

        float vol = cue.volume * Random.Range(cue.volumeJitter.x, cue.volumeJitter.y);
        float pit = Random.Range(cue.pitchJitter.x, cue.pitchJitter.y);

        src.volume = vol;
        src.pitch = pit;

        src.spatialBlend = cue.spatialBlend;
        src.minDistance = cue.minDistance;
        src.maxDistance = cue.maxDistance;
    }

    private AudioSource GetFreeSource()
    {
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].isPlaying)
                return pool[i];
        return null;
    }

    private bool CanPlay(AudioCue cue)
    {
        if (cue == null) return false;

        float now = Time.unscaledTime;

        if (cue.cooldownSeconds > 0f &&
            lastPlayTime.TryGetValue(cue, out var t) &&
            (now - t) < cue.cooldownSeconds)
            return false;

        if (!activeCounts.TryGetValue(cue, out var count)) count = 0;
        if (count >= cue.maxSimultaneous) return false;

        return true;
    }

    private void MarkPlayed(AudioCue cue)
    {
        float now = Time.unscaledTime;
        lastPlayTime[cue] = now;

        if (!activeCounts.TryGetValue(cue, out var count)) count = 0;
        activeCounts[cue] = count + 1;

        // decrement later (simple approach)
        StartCoroutine(DecrementAfter(cue, 0.25f));
    }

    private System.Collections.IEnumerator DecrementAfter(AudioCue cue, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (activeCounts.TryGetValue(cue, out var count))
            activeCounts[cue] = Mathf.Max(0, count - 1);
    }
}