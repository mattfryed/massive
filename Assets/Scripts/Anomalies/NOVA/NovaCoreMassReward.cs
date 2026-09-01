using UnityEngine;

// Change base class name if needed to match your project’s reward base type.
[CreateAssetMenu(menuName = "MASSIVE/Anomalies/Rewards/Nova Core Mass Reward")]
public class NovaCoreMassReward : AnomalyRewardBase
{
    [Header("When to award")]
    public bool awardOnFailure = true;

    [Header("Star Shield (reduces bounce intensity)")]
    public bool registerStarShield = true;
    public float shieldMultiplier = 1f;

    [Header("Mass/Score Transfer")]
    public bool awardMassToTeams = true;
    public float massMultiplier = 1f;

    public override void Apply(AnomalyResult result, AnomalyManager manager)
    {
        if (!result.success && !awardOnFailure) return;
        if (result.payload is not NovaCoreWrapUpPayload wrap) return;

        // 1) Register shielding to the star
        if (registerStarShield)
        {
            var star = FindStar();
            if (star != null)
            {
                star.RegisterCoreCapture(wrap.lightTeamIndex, wrap.lightMass * shieldMultiplier);
                star.RegisterCoreCapture(wrap.darkTeamIndex,  wrap.darkMass  * shieldMultiplier);
            }
        }

        // 2) Forward to your mass/score system
        if (awardMassToTeams)
        {
            var sink = FindSink(manager);
            if (sink != null)
            {
                sink.AwardTeam(wrap.lightTeamIndex, wrap.lightParticles, wrap.lightMass * massMultiplier);
                sink.AwardTeam(wrap.darkTeamIndex,  wrap.darkParticles,  wrap.darkMass  * massMultiplier);
            }
            else
            {
                Debug.LogWarning("[NovaCoreMassReward] No NovaCoreRewardsSink found; mass/score not applied.");
            }
        }
    }

    private NovaStarController FindStar()
    {
#if UNITY_6000_0_OR_NEWER
        return Object.FindFirstObjectByType<NovaStarController>();
#else
        return Object.FindObjectOfType<NovaStarController>();
#endif
    }

    private NovaCoreRewardsSink FindSink(AnomalyManager manager)
    {
        if (manager != null)
        {
            // Prefer explicit sink on the manager GO.
            var local = manager.GetComponent<NovaCoreRewardsSink>();
            if (local != null) return local;
        }

#if UNITY_6000_0_OR_NEWER
        return Object.FindFirstObjectByType<NovaCoreRewardsSink>();
#else
        return Object.FindObjectOfType<NovaCoreRewardsSink>();
#endif
    }
}
