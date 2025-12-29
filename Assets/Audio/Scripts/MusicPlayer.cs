using System.Collections;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource a;
    [SerializeField] private AudioSource b;

    private AudioSource current;
    private Coroutine routine;

    private void Awake()
    {
        current = a;
        a.loop = true;
        b.loop = true;
        a.playOnAwake = false;
        b.playOnAwake = false;
    }

    public void Play(MusicCue cue)
    {
        if (cue == null || cue.loop == null) return;

        var next = (current == a) ? b : a;

        next.outputAudioMixerGroup = cue.output;
        next.clip = cue.loop;
        next.loop = true;
        next.volume = 0f;
        next.Play();

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Crossfade(current, next, cue.fadeOut, cue.fadeIn));

        current = next;
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float outTime, float inTime)
    {
        float t = 0f;

        float fromStart = from != null ? from.volume : 0f;
        while (t < Mathf.Max(outTime, inTime))
        {
            t += Time.unscaledDeltaTime;

            if (from != null && outTime > 0f)
                from.volume = Mathf.Lerp(fromStart, 0f, t / outTime);

            if (to != null && inTime > 0f)
                to.volume = Mathf.Lerp(0f, 1f, t / inTime);

            yield return null;
        }

        if (from != null)
        {
            from.volume = 0f;
            from.Stop();
        }

        if (to != null) to.volume = 1f;
    }
}