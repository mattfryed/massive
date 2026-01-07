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

    public SendMessageOptions sendMessageOptions = SendMessageOptions.DontRequireReceiver;

    public void AwardTeam(int teamIndex, int particles, float mass)
    {
        onAwardTeamParticles?.Invoke(teamIndex, particles);
        onAwardTeamMass?.Invoke(teamIndex, mass);

        if (distributeMassToPlayers && mass > 0f)
            DistributeMassToTeamPlayers(teamIndex, mass);
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
