using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Audio Cue", fileName = "AC_")]
public class AudioCue : ScriptableObject
{
    public AudioClip[] clips;

    [Header("Routing")]
    public AudioMixerGroup output;

    [Header("Volume / Pitch")]
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 volumeJitter = new Vector2(0.95f, 1.05f);
    public Vector2 pitchJitter  = new Vector2(0.98f, 1.02f);

    [Header("Playback Rules")]
    public float cooldownSeconds = 0f;
    public int maxSimultaneous = 8;

    [Header("Spatial (optional)")]
    [Range(0f, 1f)] public float spatialBlend = 1f; // 0=2D, 1=3D
    public float minDistance = 2f;
    public float maxDistance = 20f;

public AudioClip PickClip() => GetRandomClip();

public AudioClip GetRandomClip()
{
    if (clips == null || clips.Length == 0) return null;
    return clips[Random.Range(0, clips.Length)];
}
}