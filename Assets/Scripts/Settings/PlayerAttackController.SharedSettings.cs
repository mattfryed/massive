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
            return ownerController && !ownerController.UsesGameplayTuning ? null : SharedSettingsRuntime.Resolve<PlayerTuningProfile>(this, useSharedSettings);
        }
    }
    public float Effective_attackCooldown => SharedPlayerTuning != null ? SharedPlayerTuning.combat.attackCooldown : attackCooldown;
    public float Effective_comboInputBuffer => SharedPlayerTuning != null ? SharedPlayerTuning.combat.comboInputBuffer : comboInputBuffer;
    public bool Effective_useSharedComboWindow => SharedPlayerTuning != null ? SharedPlayerTuning.combat.useSharedComboWindow : useSharedComboWindow;
    public float Effective_sharedComboWindowSeconds => SharedPlayerTuning != null ? SharedPlayerTuning.combat.sharedComboWindowSeconds : sharedComboWindowSeconds;
    public bool Effective_comboWindowAfterActivationWindow => SharedPlayerTuning != null ? SharedPlayerTuning.combat.comboWindowAfterActivationWindow : comboWindowAfterActivationWindow;
    public float Effective_comboWindowEndNormalized => SharedPlayerTuning != null ? SharedPlayerTuning.combat.comboWindowEndNormalized : comboWindowEndNormalized;
    public AnimationCurve Effective_swipeArcCurve => SharedPlayerTuning != null ? SharedPlayerTuning.combat.swipeArcCurve : swipeArcCurve;
    public float Effective_thrustMaxTurnDegrees => SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustMaxTurnDegrees : thrustMaxTurnDegrees;
    public bool Effective_thrustChargeEnabled => SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeEnabled : thrustChargeEnabled;
    public float Effective_thrustChargeMaxSeconds => Mathf.Max(.05f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeMaxSeconds : thrustChargeMaxSeconds);
    public float Effective_thrustChargeMaxDistanceMultiplier => Mathf.Max(1f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeMaxDistanceMultiplier : thrustChargeMaxDistanceMultiplier);
    public float Effective_thrustChargeMaxDamageMultiplier => Mathf.Max(1f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeMaxDamageMultiplier : thrustChargeMaxDamageMultiplier);
    public float Effective_thrustChargeVibrationAmplitude => Mathf.Max(0f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeVibrationAmplitude : thrustChargeVibrationAmplitude);
    public float Effective_thrustChargeVibrationFrequency => Mathf.Max(1f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeVibrationFrequency : thrustChargeVibrationFrequency);
    public float Effective_thrustChargeMinMoveMultiplier => Mathf.Clamp01(SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeMinMoveMultiplier : thrustChargeMinMoveMultiplier);
    public bool Effective_thrustChargeGuideEnabled => SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideEnabled : thrustChargeGuideEnabled;
    public int Effective_thrustChargeGuideArrowCount => Mathf.Clamp(SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideArrowCount : thrustChargeGuideArrowCount, 1, 12);
    public float Effective_thrustChargeGuideDistanceFraction => Mathf.Clamp01(SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideDistanceFraction : thrustChargeGuideDistanceFraction);
    public float Effective_thrustChargeGuideStrokeWidth => Mathf.Max(0f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideStrokeWidth : thrustChargeGuideStrokeWidth);
    public float Effective_thrustChargeGuideArrowLength => Mathf.Max(.01f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideArrowLength : thrustChargeGuideArrowLength);
    public float Effective_thrustChargeGuideArrowWidth => Mathf.Max(.01f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideArrowWidth : thrustChargeGuideArrowWidth);
    public float Effective_thrustChargeGuideRevealSeconds => Mathf.Max(.01f, SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideRevealSeconds : thrustChargeGuideRevealSeconds);
    public Color Effective_thrustChargeGuideColor => SharedPlayerTuning != null ? SharedPlayerTuning.combat.thrustChargeGuideColor : thrustChargeGuideColor;
    public bool Effective_visualDirectionFollowsCombatFacing => SharedPlayerTuning != null ? SharedPlayerTuning.combat.visualDirectionFollowsCombatFacing : visualDirectionFollowsCombatFacing;
    public bool Effective_lockOnEnabled => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnEnabled : lockOnEnabled;
    public float Effective_lockOnConeHalfAngleDeg => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnConeHalfAngleDeg : lockOnConeHalfAngleDeg;
    public float Effective_lockOnMaxDistanceOverride => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnMaxDistanceOverride : lockOnMaxDistanceOverride;
    public float Effective_lockOnDirectionBlend => SharedPlayerTuning != null ? SharedPlayerTuning.combat.lockOnDirectionBlend : lockOnDirectionBlend;
}
}
