using UnityEngine;

/// <summary>
/// Backwards-compatible alias for reward ScriptableObjects.
///
/// Your primary reward base type is <see cref="AnomalyReward"/> (declared in AnomalyDefinition.cs).
/// Some newer/experimental scripts may derive from <see cref="AnomalyRewardBase"/> instead.
///
/// Keeping this shim prevents type churn and keeps AnomalyDefinition.rewardsOnSuccess/rewardsOnFail
/// working without changing existing assets.
/// </summary>
public abstract class AnomalyRewardBase : AnomalyReward
{
    // Intentionally empty.
    // AnomalyReward already defines: public abstract void Apply(AnomalyResult result, AnomalyManager manager);
}
