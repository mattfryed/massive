using Massive.Settings;
using UnityEngine;
namespace Massive.Player
{
public partial class PlayerAttackController : ISharedSettingsConsumer
{
    [SerializeField, Tooltip("Use project-wide Player Tuning. Off restores this component's local values.")]
    private bool useSharedSettings = true;
    public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
    public SharedSettingsProfile SharedSettingsAsset => SharedSettingsRuntime.Load<PlayerTuningProfile>();
    public string SharedSettingsGroup => "combat";
    private PlayerTuningProfile SharedPlayerTuning
    {
        get
        {
            // Profile inspection and edit-mode VFX can run before Awake.
            if (!ownerController) ownerController = GetComponent<PlayerControllerScript>();
            return ownerController && ownerController.IsPseudoPlayer ? null : SharedSettingsRuntime.Resolve<PlayerTuningProfile>(this, useSharedSettings);
        }
    }
    public float Effective_attackCooldown => SharedPlayerTuning != null ? SharedPlayerTuning.combat.attackCooldown : attackCooldown;
    public float Effective_comboInputBuffer => SharedPlayerTuning != null ? SharedPlayerTuning.combat.comboInputBuffer : comboInputBuffer;
    public bool Effective_useSharedComboWindow => SharedPlayerTuning != null ? SharedPlayerTuning.combat.useSharedComboWindow : useSharedComboWindow;
    public float Effective_sharedComboWindowSeconds => SharedPlayerTuning != null ? SharedPlayerTuning.combat.sharedComboWindowSeconds : sharedComboWindowSeconds;
    public bool Effective_comboWindowAfterActivationWindow => SharedPlayerTuning != null ? SharedPlayerTuning.combat.comboWindowAfterActivationWindow : comboWindowAfterActivationWindow;
    public float Effective_comboWindowEndNormalized => SharedPlayerTuning != null ? SharedPlayerTuning.combat.comboWindowEndNormalized : comboWindowEndNormalized;
    public AnimationCurve Effective_swipeArcCurve => SharedPlayerTuning != null ? SharedPlayerTuning.combat.swipeArcCurve : swipeArcCurve;
    public bool Effective_visualDirectionFollowsCombatFacing => SharedPlayerTuning != null ? SharedPlayerTuning.combat.visualDirectionFollowsCombatFacing : visualDirectionFollowsCombatFacing;
    public bool Effective_lockOnEnabled => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnEnabled : lockOnEnabled;
    public float Effective_lockOnConeHalfAngleDeg => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnConeHalfAngleDeg : lockOnConeHalfAngleDeg;
    public float Effective_lockOnMaxDistanceOverride => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnMaxDistanceOverride : lockOnMaxDistanceOverride;
    public float Effective_lockOnDirectionBlend => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnDirectionBlend : lockOnDirectionBlend;
}
}
