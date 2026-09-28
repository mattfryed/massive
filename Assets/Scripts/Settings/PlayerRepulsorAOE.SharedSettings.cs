using Massive.Settings;
using UnityEngine;
namespace Massive.Player
{
public partial class PlayerRepulsorAOE : ISharedSettingsConsumer
{
    [SerializeField] private bool useSharedSettings = true;
    public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
    public SharedSettingsProfile SharedSettingsAsset => SharedSettingsRuntime.Load<PlayerTuningProfile>();
    public string SharedSettingsGroup => "repulsor";
    private PlayerControllerScript tuningOwner;
    private PlayerTuningProfile SharedPlayerTuning
    {
        get
        {
            if (!tuningOwner) tuningOwner = owner ? owner : GetComponentInParent<PlayerControllerScript>(true);
            return tuningOwner && !tuningOwner.UsesGameplayTuning ? null : SharedSettingsRuntime.Resolve<PlayerTuningProfile>(this, useSharedSettings);
        }
    }
    public bool Effective_applyKnockback => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.applyKnockback : applyKnockback;
    public float Effective_knockbackVelocity => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.knockbackVelocity : knockbackVelocity;
    public float Effective_maxPlanarSpeedAfterHit => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.maxPlanarSpeedAfterHit : maxPlanarSpeedAfterHit;
    public bool Effective_applyStun => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.applyStun : applyStun;
    public float Effective_stunStrength01 => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.stunStrength01 : stunStrength01;
    public bool Effective_applyMassLoss => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.applyMassLoss : applyMassLoss;
    public float Effective_massLossScale01 => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.massLossScale01 : massLossScale01;
    public bool Effective_giveAttackerMass => SharedPlayerTuning != null ? SharedPlayerTuning.repulsor.giveAttackerMass : giveAttackerMass;
}
}
