using System.Collections.Generic;
using UnityEngine;

public class NovaScoreSphereBridge : MonoBehaviour
{
    [SerializeField] private bool autoFind = true;
    [SerializeField] private List<ScoreSphereScript> spheres = new();

    private readonly Dictionary<int, ScoreSphereScript> _byTeam = new();

    private void Awake() => Cache();

    private void Cache()
    {
        _byTeam.Clear();

        if (autoFind && (spheres == null || spheres.Count == 0))
        {
#if UNITY_6000_0_OR_NEWER
            spheres = new List<ScoreSphereScript>(FindObjectsByType<ScoreSphereScript>(FindObjectsSortMode.None));
#else
            spheres = new List<ScoreSphereScript>(FindObjectsOfType<ScoreSphereScript>());
#endif
        }

        foreach (var s in spheres)
            if (s != null) _byTeam[s.teamID] = s; // ScoreSphereScript.teamID
    }

    // Hook this to NovaCoreRewardsSink.onAwardTeamMass
    public void AwardTeamScore01(int teamIndex, float delta01)
    {
        if (_byTeam.Count == 0) Cache();
        if (_byTeam.TryGetValue(teamIndex, out var sphere) && sphere != null)
            sphere.AddScore01(delta01); // clamps and updates visuals
    }
}
