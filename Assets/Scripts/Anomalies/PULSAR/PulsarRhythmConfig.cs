using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/PULSAR Rhythm Config", fileName = "PulsarRhythmConfig")]
public class PulsarRhythmConfig : ScriptableObject
{
    [Header("Judgement Windows (seconds)")]
    [Tooltip("± window for Perfect.")]
    public float perfectWindowSec = 0.045f;

    [Tooltip("± window for Good.")]
    public float goodWindowSec = 0.10f;

    [Header("Chord")]
    [Tooltip("Max separation between Sword & Shield presses to count as BOTH.")]
    public float chordSeparationSec = 0.08f;

    [Header("Mass Values (per player, per prompt)")]
    public float massPerfect = 10f;
    public float massGood = 6f;
    public float massMiss = -10f;

    [Header("Resonance")]
    [Tooltip("Combo -> resonance mapping: resonance = combo / (combo + comboK).")]
    public float comboK = 6f;

    [Tooltip("Positive mass is multiplied by (1 + resonance * maxBonusMultiplier).")]
    public float maxBonusMultiplier = 1.25f;

    [Tooltip("If team average quality >= this, combo increases; otherwise combo resets.")]
    [Range(0f, 1f)]
    public float comboSuccessQualityThreshold = 0.5f;

    [Header("Visual Radii")]
    public float startRadius = 9f;
    public float hitRadius = 3f;
}
