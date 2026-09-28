using Massive.Settings;
using UnityEngine;

public partial class PlayerControllerScript : ISharedSettingsConsumer
{
    [SerializeField, Tooltip("Use project-wide Player Tuning. Off restores this component's local values.")]
    private bool useSharedSettings = true;
    public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
    public SharedSettingsProfile SharedSettingsAsset => SharedSettingsRuntime.Load<PlayerTuningProfile>();
    public string SharedSettingsGroup => "movement";
    private PlayerTuningProfile SharedPlayerTuning => !UsesGameplayTuning ? null : SharedSettingsRuntime.Resolve<PlayerTuningProfile>(this, useSharedSettings);
    public float Effective_movePower => SharedPlayerTuning != null ? SharedPlayerTuning.movement.movePower : movePower;
    public float Effective_maxMoveSpeed => SharedPlayerTuning != null ? SharedPlayerTuning.movement.maxMoveSpeed : maxMoveSpeed;
    public bool Effective_clampSpeed => SharedPlayerTuning != null ? SharedPlayerTuning.movement.clampSpeed : clampSpeed;
    public float Effective_lateralFriction => SharedPlayerTuning != null ? SharedPlayerTuning.movement.lateralFriction : lateralFriction;
    public float Effective_reverseBrake => SharedPlayerTuning != null ? SharedPlayerTuning.movement.reverseBrake : reverseBrake;
    public float Effective_reverseDotThreshold => SharedPlayerTuning != null ? SharedPlayerTuning.movement.reverseDotThreshold : reverseDotThreshold;
    public float Effective_idleBrake => SharedPlayerTuning != null ? SharedPlayerTuning.movement.idleBrake : idleBrake;
    public float Effective_moveDeadzone => SharedPlayerTuning != null ? SharedPlayerTuning.movement.moveDeadzone : moveDeadzone;
    public float Effective_attackingMoveScale => SharedPlayerTuning != null ? SharedPlayerTuning.movement.attackingMoveScale : attackingMoveScale;
    public float Effective_shieldMoveMultiplier => SharedPlayerTuning != null ? SharedPlayerTuning.movement.shieldMoveMultiplier : shieldMoveMultiplier;
}


