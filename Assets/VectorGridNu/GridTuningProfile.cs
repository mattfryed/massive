using UnityEngine;

[CreateAssetMenu(menuName="MASSIVE/Grid Tuning Profile", fileName="New Grid Tuning")]
public class GridTuningProfile : ScriptableObject
{
    [Header("Simulation")]
    [Min(0f)] public float springK = 3.0f;
    [Range(0.8f, 0.999f)] public float damping = 0.97f;
    [Min(0f)] public float maxSpeed = 12f;     // 0 = off
    public bool pinEdges = true;

    [Header("Falloff")]
    [Tooltip("0=Linear  1=Smooth  2=Quadratic  3=Gaussian  4=InvSq")]
    [Range(0,4)] public int falloffMode = 3;
    [Min(0f)] public float falloffExp = 1.25f;
    [Range(0f,0.9f)] public float innerFrac = 0.2f;
    [Min(0f)] public float sharpness = 3.0f;

    [Header("Crowd/Clamp")]
    [Range(0f,1f)] public float weightCap = 0.8f;
    [Min(0f)] public float crowdStiffness = 0.6f;

    [Header("Cutoff & Edge")]
    public bool hardCutoff = true;
    [Range(0f,0.3f)] public float cutoffSmooth = 0.08f;
    [Min(0f)] public float edgeFeatherCells = 2f;
    [Min(0f)] public float edgeFeatherExp = 1.5f;
}
