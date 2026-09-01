using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-stage configuration for anomalies.
/// Create one asset per stage and assign it to the AnomalyManager
/// in that scene.
/// </summary>
[CreateAssetMenu(menuName = "MASSIVE/Stage Profile")]
public class StageProfile : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Internal id (e.g. STAGE_002). Purely informational for now.")]
    public string stageId = "STAGE_001";

    [Tooltip("Display name (e.g. STAGE 1). Optional.")]
    public string displayName = "STAGE 1";

    [Header("Anomaly Scheduling")]
    [Tooltip("Minimum seconds between anomaly warnings.")]
    public float globalMinInterval = 15f;

    [Tooltip("Maximum seconds between anomaly warnings.")]
    public float globalMaxInterval = 35f;

    [Header("Available Anomalies")]
    [Tooltip("Which anomalies can spawn in this stage, with their per-anomaly weights.")]
    public List<AnomalyDefinition> anomalyPool = new List<AnomalyDefinition>();
}
