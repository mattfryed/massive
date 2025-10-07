// Assets/VectorGridNu/GridInteractionProfile.cs
using System;
using UnityEngine;

public enum GridModuleType {
    ConstantRadial,      // steady radial pull/push
    DirectionalWake,     // uses direction/velocity
    Vortex,              // rotating direction
    Jiggle,              // noise jitter
    BurstRadial,         // one-shot burst on trigger
    Pulse,               // repeating envelope while active
    TravelingWave        // expanding ring
}

[Serializable]
public struct GridInteractionModule
{
    public string tag;                 // optional label to trigger by name
    public GridModuleType type;

    // Common
    public float radius;
    public float strength;
    [Range(0, 0.9f)] public float innerFrac;

    // Directional (DirectionalWake)
    public bool directional;
    public bool useVelocity;
    public bool pullAgainstVelocity;
    public Vector3 fixedDirection;

    // Scaling
    public bool scaleBySpeed;
    public AnimationCurve radiusOverSpeed;
    public AnimationCurve strengthOverSpeed;

    // Vortex
    public float spinDegPerSec;

    // Jiggle
    public float noiseAmplitude;
    public float noiseFrequency;

    // Pulse / Burst
    public float duration;
    public bool autoStart;
    public bool loopPulse;          // (Pulse only)
    public bool  repel;              // NEW: outward if true, inward if false
    public AnimationCurve radiusOverTime; // 0..1 → multiplier (default 1)
    public AnimationCurve envelope;

    // Traveling Wave
    public float waveSpeed;
    public float waveThickness;

    // Local Tuning (ConstantRadial only)
    public bool tuningEnabled;
    public float tuningSpringK;
    public float tuningDamping;
    [Range(0, 4)] public int tuningFalloffMode;
    public float tuningFalloffExp;
    public float tuningSharpness;
    public float tuningMaxSpeed;
    [Range(0f, 1f)] public float tuningBlend;
    
    // [Header("Common")]
    // public float radius;               // base radius
    // public float strength;             // base strength (can be +/-)
    // [Range(0,0.9f)] public float innerFrac;
    // public bool directional;           // if true, uses "direction" below
    // public bool useVelocity;           // wake/jets from rigidbody velocity
    // public bool pullAgainstVelocity;   // wake behind motion
    // public Vector3 fixedDirection;     // fallback direction
    //
    // [Header("Scaling")]
    // public bool scaleBySpeed;
    // public AnimationCurve radiusOverSpeed;
    // public AnimationCurve strengthOverSpeed;
    //
    // [Header("Vortex")]
    // public float spinDegPerSec;
    //
    // [Header("Jiggle")]
    // public float noiseAmplitude;
    // public float noiseFrequency;
    //
    // [Header("Pulse/Burst")]
    // public float duration;             // seconds (BurstRadial ends, Pulse can loop)
    // public bool autoStart;
    // public bool loopPulse;
    // public AnimationCurve envelope;    // 0..1 time → 0..1 scale
    //
    // [Header("Traveling Wave")]
    // public float waveSpeed;            // units/sec
    // public float waveThickness;        // band width
    //
    // [Header("Local Tuning (ConstantRadial)")]
    // public bool  tuningEnabled;     // show in drawer only for ConstantRadial
    // public float tuningSpringK;
    // public float tuningDamping;
    // [Range(0,4)] public int   tuningFalloffMode;
    // public float tuningFalloffExp;
    // public float tuningSharpness;
    // public float tuningMaxSpeed;
    // [Range(0f,1f)] public float tuningBlend; // 0..1 blend toward these values
}

// A single asset may contain multiple modules.
[CreateAssetMenu(menuName="MASSIVE/Grid Interaction Profile", fileName="New Grid Interaction Profile")]
public class GridInteractionProfile : ScriptableObject
{
    [Header("Modules emitted by this object")]
    public GridInteractionModule[] modules = Array.Empty<GridInteractionModule>();

    [Header("Quick multipliers for entire profile")]
    public float strengthMultiplier = 1f;
    public float radiusMultiplier   = 1f;
}
