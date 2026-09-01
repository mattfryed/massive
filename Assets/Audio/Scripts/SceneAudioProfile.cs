using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Scene Audio Profile", fileName = "SAP_")]
public class SceneAudioProfile : ScriptableObject
{
    public MusicCue music;
    public AudioCue ambienceLoop;              // optional
    public AudioMixerSnapshot snapshot;        // optional
    public float snapshotTransition = 0.25f;
}