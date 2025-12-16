using UnityEngine;

/// <summary>
/// Where the anomaly plays out visually.
/// </summary>
public enum AnomalySpaceMode
{
    OverlayInArena,   // A: stays in the main field, UI mirrors it
    SubspacePanel     // B: player(s) enter the lower-third minigame area
}

/// <summary>
/// What happens to the main arena while the anomaly is active.
/// </summary>
public enum ArenaModeDuringAnomaly
{
    Unchanged,
    PauseAllPlayers,
    GhostParticipantsOnly
}

/// <summary>
/// How participants are chosen from the active players.
/// </summary>
public enum ParticipantSelection
{
    AllPlayers,
    RandomSingle,
    RandomTeam,
    LowestMassPlayer,
    HighestMassPlayer
}



[CreateAssetMenu(menuName = "MASSIVE/Anomaly Definition")]
public class AnomalyDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id used in code / analytics. e.g. MAGNETOSPHERE_ALIGN")]
    public string id;

    [Tooltip("Display name that appears in the UI banner.")]
    public string displayName;

    [TextArea]
    [Tooltip("Very short description for tooltips / docs.")]
    public string shortDescription;

    [Header("Behavior")]
    public AnomalySpaceMode spaceMode = AnomalySpaceMode.SubspacePanel;
    public ArenaModeDuringAnomaly arenaMode = ArenaModeDuringAnomaly.GhostParticipantsOnly;

    [Tooltip("Seconds from warning banner to anomaly actually starting (random in range).")]
    public float minStartDelay = 10f;

    public float maxStartDelay = 30f;

    [Tooltip("Approximate duration of the anomaly / minigame (random in range).")]
    public float minDuration = 8f;

    public float maxDuration = 15f;

    [Tooltip("Relative chance of this anomaly being picked compared to others in the StageProfile.")]
    public float selectionWeight = 1f;

    [Header("Participants")]
    [Tooltip("How the system decides which players participate in this anomaly.")]
    public ParticipantSelection participantSelection = ParticipantSelection.AllPlayers;

    [Tooltip("Optional hint for team-based selection modes. -1 = any team.")]
    public int preferredTeamIndex = -1;

    [Header("Prefabs & UI")]
    [Tooltip("Prefab that implements AnomalyMinigameBase; instantiated under AnomalyUIController.minigameContentRoot.")]
    public GameObject minigameLogicPrefab;     // has AnomalyMinigameBase

    public Sprite warningIcon;
    public Sprite controlIconA;
    public Sprite controlIconB;

    [Header("UI Copy")]
public AnomalyUICopy uiCopyOverride;

    [TextArea]
    [Tooltip("Short instructions that appear in the lower third while the anomaly is active.")]
    public string instructionText;

    [Header("Results")]
    [Tooltip("Rewards applied when the anomaly is completed successfully.")]
    public AnomalyReward[] rewardsOnSuccess;

    [Tooltip("Rewards applied when the anomaly fails or times out.")]
    public AnomalyReward[] rewardsOnFail;

    /// <summary>Returns a random warning-to-start delay based on this definition.</summary>
    public float GetRandomStartDelay()
    {
        return Random.Range(minStartDelay, maxStartDelay);
    }

    /// <summary>Returns a random minigame duration based on this definition.</summary>
    public float GetRandomDuration()
    {
        return Random.Range(minDuration, maxDuration);
    }
}

/// <summary>
/// ScriptableObject describing a reward / consequence applied when an anomaly finishes.
/// Concrete subclasses implement the actual effect (mass buffs, shields, hazards, etc).
/// </summary>
public abstract class AnomalyReward : ScriptableObject
{
    public abstract void Apply(AnomalyResult result, AnomalyManager manager);
}
