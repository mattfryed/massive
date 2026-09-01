using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Level Audio Profile", fileName = "LAP_")]
public class LevelAudioProfile : SceneAudioProfile
{

    
    [Header("Main BGM")]
    public AudioClip bgmClip;

    [Range(0f, 1f)]
    public float volume = 1f;

    public bool loop = true;

    [Min(0f)]
    public float fadeInSeconds = 0.75f;

    [Min(0f)]
    public float fadeOutSeconds = 0.75f;

    [Header("Routing (Optional)")]
    public AudioMixerGroup outputMixerGroup;




    
    [Header("Gameplay Beats")]
    public AudioCue timerLowStinger;           // last 10s / last 5s etc
    public AudioCue nearScoreCapStinger;       // “almost winning”

    [Header("Optional Music Overrides")]
    public MusicCue minigameMusicOverride;     // anomalies / subspace
    public AudioMixerSnapshot dangerSnapshot;  // “intense” state
    public float dangerSnapshotTransition = 0.25f;
}