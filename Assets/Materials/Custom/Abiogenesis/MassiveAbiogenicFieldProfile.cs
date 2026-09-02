using UnityEngine;

[CreateAssetMenu(
    fileName = "MassiveAbiogenicFieldProfile",
    menuName = "MASSIVE/Visuals/Abiogenic Field Profile")]
public sealed class MassiveAbiogenicFieldProfile : ScriptableObject
{
    [Header("Colors")]
    public Color backgroundColor = new(0.01f, 0.01f, 0.01f, 1f);
    public Color fieldColor = Color.white;

    [Header("Virtual Perspective")]
    [Min(0.01f)] public float virtualCameraDistance = 3.0f;
    [Range(0.01f, 0.5f)] public float viewScale = 0.125f;
    public Vector2 vanishingPoint = Vector2.zero;
    public Vector2 virtualCameraOffset = Vector2.zero;
    [Range(0f, 0.25f)] public float cameraDriftAmplitude = 0f;
    public float cameraDriftSpeed = 0.20f;

    [Header("Raymarch")]
    [Range(8, 128)] public int maxSteps = 80;
    [Min(0.0001f)] public float hitEpsilon = 0.001f;
    [Min(0.25f)] public float maxDistance = 14f;
    [Min(0f)] public float rayStartOffset = 0f;
    [Range(0.25f, 1f)] public float stepScale = 0.78f;

    [Header("Shape")]
    [Min(0.1f)] public float cellScale = 3.0f;
    [Min(0.05f)] public float layerDepth = 0.5f;
    [Min(0.01f)] public float sphereRadius = 0.10f;
    [Range(0f, 1f)] public float sphereRadiusAmplitude = 0.50f;
    [Min(0.01f)] public float sphereRadiusFrequency = 1.0f;

    [Header("Motion")]
    public float scrollSpeed = 0.15f;
    public float swirlSpeed = 0.20f;
    public float bendAmplitude = 0.10f;
    public float warpStrength = 0.14f;
    public float phaseOffset = 0f;

    [Header("Shading")]
    [Range(0f, 1f)] public float baseLuma = 0.02f;
    [Min(0f)] public float glowStrength = 0.90f;
    [Min(0f)] public float stripeFrequency = 6.0f;
    [Range(0f, 1f)] public float stripeContrast = 0.16f;
    [Min(0f)] public float fogDensity = 0.10f;
    [Min(0.0001f)] public float surfaceSoftness = 0.020f;
    [Range(0f, 1f)] public float normalShading = 0.75f;
    [Range(0f, 2f)] public float nearBoost = 0.65f;
    [Range(0f, 0.5f)] public float edgeFade = 0.03f;
}
