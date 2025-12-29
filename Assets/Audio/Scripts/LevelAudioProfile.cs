using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Level Audio Profile", fileName = "LAP_")]
public class LevelAudioProfile : SceneAudioProfile
{
    [Header("Gameplay Beats")]
    public AudioCue timerLowStinger;           // last 10s / last 5s etc
    public AudioCue nearScoreCapStinger;       // “almost winning”

    [Header("Optional Music Overrides")]
    public MusicCue minigameMusicOverride;     // anomalies / subspace
    public AudioMixerSnapshot dangerSnapshot;  // “intense” state
    public float dangerSnapshotTransition = 0.25f;
}