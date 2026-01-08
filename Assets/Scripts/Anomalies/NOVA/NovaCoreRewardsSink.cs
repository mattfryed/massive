using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NovaCoreRewardsSink : MonoBehaviour
{
    [System.Serializable] public class TeamFloatEvent : UnityEvent<int, float> { }
    [System.Serializable] public class TeamIntEvent : UnityEvent<int, int> { }

    [Header("Wire these to your real score/mass system")]
    public TeamIntEvent onAwardTeamParticles = new TeamIntEvent();
    public TeamFloatEvent onAwardTeamMass = new TeamFloatEvent();

    [Header("Optional: auto-distribute mass to players via SendMessage")]
    public bool distributeMassToPlayers = false;

    [Tooltip("Method name on PlayerControllerScript (or its GameObject) that accepts (float massDelta).")]
    public string playerMassMethodName = "AddMass";

    [SerializeField] private bool applyToScoreSpheres = true;
    [SerializeField] private bool autoFindScoreSpheres = true;
    [SerializeField] private List<ScoreSphereScript> scoreSpheres = new();

    private readonly Dictionary<int, ScoreSphereScript> _sphereByTeam = new();

    public SendMessageOptions sendMessageOptions = SendMessageOptions.DontRequireReceiver;

    [SerializeField, Tooltip("How much ScoreSphere score01 to add per 1.0 mass from the minigame.")]
    private float score01PerMass = 0.02f; // tune this (start big like 0.1 just to verify it fires)

    [SerializeField, Tooltip("If true, treat incoming 'massAwarded' as already-normalized score01.")]
    private bool massIsAlreadyScore01 = false;

    [SerializeField] private bool debugLogs = false;

public void AwardTeamMass(int teamIndex, float massAwarded)
{
    onAwardTeamMass?.Invoke(teamIndex, massAwarded);

    if (massAwarded <= 0f)
    {
        if (debugLogs) Debug.Log($"[NovaCoreRewardsSink] AwardTeamMass team={teamIndex} mass<=0 (ignored).", this);
        return;
    }

    // Optional: also distribute to players (your helper existed but wasn’t called)
    if (distributeMassToPlayers)
        DistributeMassToTeamPlayers(teamIndex, massAwarded);

    if (!applyToScoreSpheres) return;

    var sphere = GetSphere(teamIndex);
    if (sphere == null)
    {
        Debug.LogWarning($"[NovaCoreRewardsSink] No ScoreSphereScript found for team {teamIndex}.", this);
        return;
    }

    float delta01 = massIsAlreadyScore01 ? massAwarded : (massAwarded * score01PerMass);

    // Alternative normalization if you prefer:
    // float delta01 = massAwarded / sphere.MaxSize;  // uses MaxSize property

    sphere.AddScore01(delta01); // clamps internally

    if (debugLogs)
        Debug.Log($"[NovaCoreRewardsSink] AwardTeamMass team={teamIndex} mass={massAwarded} -> +{delta01} score01", this);
}

public void AwardTeamParticles(int teamIndex, int particlesAwarded)
{
    onAwardTeamParticles?.Invoke(teamIndex, particlesAwarded);
}

public void AwardTeam(int teamIndex, int particlesAwarded, float massAwarded)
{
    AwardTeamParticles(teamIndex, particlesAwarded);
    AwardTeamMass(teamIndex, massAwarded);
}


    private void CacheScoreSpheres()
{
    _sphereByTeam.Clear();

    if (autoFindScoreSpheres && (scoreSpheres == null || scoreSpheres.Count == 0))
    {
#if UNITY_6000_0_OR_NEWER
        scoreSpheres = new List<ScoreSphereScript>(
            FindObjectsByType<ScoreSphereScript>(FindObjectsSortMode.None)
        );
#else
        scoreSpheres = new List<ScoreSphereScript>(FindObjectsOfType<ScoreSphereScript>());
#endif
    }

    if (scoreSpheres == null) return;

    foreach (var s in scoreSpheres)
    {
        if (s == null) continue;
        _sphereByTeam[s.teamID] = s;
    }
}

private ScoreSphereScript GetSphere(int teamID)
{
    if (_sphereByTeam.Count == 0)
        CacheScoreSpheres();

    if (_sphereByTeam.TryGetValue(teamID, out var s) && s != null)
        return s;

    // try recache once (handles scene reloads)
    CacheScoreSpheres();
    _sphereByTeam.TryGetValue(teamID, out s);
    return s;
}

    private void DistributeMassToTeamPlayers(int teamIndex, float mass)
    {
        List<PlayerControllerScript> teamPlayers = new();

#if UNITY_6000_0_OR_NEWER
        var players = FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None);
#else
        var players = FindObjectsOfType<PlayerControllerScript>();
#endif

        foreach (var p in players)
        {
            if (p != null && p.teamID == teamIndex)
                teamPlayers.Add(p);
        }

        if (teamPlayers.Count == 0) return;

        float perPlayer = mass / teamPlayers.Count;

        foreach (var p in teamPlayers)
        {
            if (p == null) continue;
            p.gameObject.SendMessage(playerMassMethodName, perPlayer, sendMessageOptions);
        }
    }
}
