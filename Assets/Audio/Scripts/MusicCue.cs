using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Music Cue", fileName = "MC_")]
public class MusicCue : ScriptableObject
{
    public AudioClip intro;      // optional
    public AudioClip loop;

    [Header("Routing")]
    public AudioMixerGroup output;

    [Header("Fades")]
    public float fadeIn = 0.75f;
    public float fadeOut = 0.75f;
}