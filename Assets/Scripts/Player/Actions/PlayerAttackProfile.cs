using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Player
{
    [CreateAssetMenu(fileName = "PlayerAttackProfile", menuName = "Player/Attack Profile", order = 0)]
    public class PlayerAttackProfile : ScriptableObject
    {
        [SerializeField]
        private List<AttackStage> stages = new List<AttackStage>();

        public IReadOnlyList<AttackStage> Stages => stages;

        public AttackStage GetStage(int index)
        {
            if (index < 0 || index >= stages.Count)
            {
                return null;
            }

            return stages[index];
        }
    }

    [Serializable]
    public class AttackStage
    {
        [SerializeField]
        private string stageName = "Stage";

        [SerializeField]
        private AttackStageType stageType = AttackStageType.PrimaryLunge;

        [Header("Timing")]
        [SerializeField]
        private float duration = 0.5f;

        [SerializeField]
        private float activationFrameStart = 0f;

        [SerializeField]
        private float activationFrameEnd = 10f;

        [SerializeField]
        private float animationFrameRate = 60f;

        [SerializeField]
        private bool allowComboCancel = true;

        [Header("Next stage engagement")]
        [Tooltip("Use a per-stage input window. Off preserves the controller's existing combo window.")]
        [SerializeField] private bool customComboWindow;
        [Min(0), SerializeField] private float comboWindowStartSeconds = .15f;
        [Min(0), SerializeField] private float comboWindowEndSeconds = .3f;
        [Tooltip("Allow a queued next stage to replace this stage before its normal end. Existing visual trails may linger; damage windows never overlap.")]
        [SerializeField] private bool earlyComboHandoff;
        [Min(0), SerializeField] private float comboHandoffSeconds = .3f;

        [Header("Motion")]
        [SerializeField]
        private float travelDistance = 3f;

        [SerializeField]
        private AnimationCurve distanceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField]
        private float rotationArc = 0f;

        [Header("Presentation")]
        [SerializeField]
        private string animationStateName = string.Empty;

        [SerializeField]
        private float animationTransitionDuration = 0.05f;

        [Header("Repulsor (Finisher)")]
        [Tooltip("Overall Repulsor scale: hit radius, energy volume, detail and grid pulse. Multiplies player size; does not change the player, damage or attack timing.")]
        [Min(.1f), SerializeField] private float repulsorScale = 1f;

        [Tooltip("Outer-zone damage to each NPC enemy once per pulse, in enemy health units. The inner zone multiplies this amount. Zero disables NPC damage. Player knockback/mass loss are configured on RepulsorAOE separately.")]
        [Min(0f), SerializeField] private float repulsorEnemyDamage = 1f;

        [Tooltip("Final Repulsor radius in world units at player size 1. Starts at the live body outline and scales with the player.")]
        [SerializeField]
        private float repulsorMaxRadius = 3.0f;

        [Tooltip("Inner damage-zone radius relative to the player's physical body radius. 1.5 is 50% larger than the player. Clamped to the outer radius.")]
        [Min(0f), SerializeField] private float repulsorInnerRadiusPlayerMultiplier = 1.5f;
        [Tooltip("Damage multiplier inside the red inner zone, for both NPCs and players. Each target is hit only once per pulse.")]
        [Min(1f), SerializeField] private float repulsorInnerDamageMultiplier = 2f;
        [Tooltip("Editor-only tuning overlay: dotted red/yellow circles follow the live damage area in Play Mode. Excluded from player builds.")]
        [SerializeField] private bool repulsorShowDamageRings = true;

        [Tooltip("Radius over time (0..1 stage normalized -> 0..1 radius).")]
        [SerializeField]
        private AnimationCurve repulsorRadiusCurve = AnimationCurve.EaseInOut(0f, 0f, .8f, 1f);

        // Note: particle-prefab-based FX have been removed.
        // GPU-based attack trails are driven by AttackTrailGPU,
        // using stage events + StageType / StageNormalizedTime.

        public string StageName => stageName;

        public AttackStageType StageType => stageType;

        public float Duration => Mathf.Max(0.01f, duration);

        public float TravelDistance => travelDistance;

        public AnimationCurve DistanceCurve => distanceCurve;

        public float RotationArc => rotationArc;

        public bool AllowComboCancel => allowComboCancel;

        public bool CustomComboWindow => customComboWindow;
        public float ComboWindowStartSeconds => Mathf.Clamp(comboWindowStartSeconds, 0, Duration);
        public float ComboWindowEndSeconds => Mathf.Clamp(comboWindowEndSeconds, ComboWindowStartSeconds, Duration);
        public bool EarlyComboHandoff => customComboWindow && earlyComboHandoff && allowComboCancel;
        public float ComboHandoffSeconds => EarlyComboHandoff ? Mathf.Clamp(comboHandoffSeconds, .01f, Duration) : Duration;

        public Vector2 GetComboWindow(float fallbackSeconds, bool sharedWindow, bool afterActivation, float legacyEnd)
        {
            if (customComboWindow) return new Vector2(ComboWindowStartSeconds, ComboWindowEndSeconds);
            if (sharedWindow) return new Vector2(Mathf.Max(0, Duration - Mathf.Max(0, fallbackSeconds)), Duration);
            float start = (afterActivation ? ActivationEndNormalized : ActivationStartNormalized) * Duration;
            float end = (afterActivation ? Mathf.Clamp01(legacyEnd) : ActivationEndNormalized) * Duration;
            return new Vector2(start, end < start ? Duration : end);
        }

        public string AnimationStateName => animationStateName;

        public float AnimationTransitionDuration => animationTransitionDuration;

        public float RepulsorMaxRadius => Mathf.Max(0f, repulsorMaxRadius);

        public float RepulsorScale => Mathf.Max(.1f, repulsorScale);

        public float RepulsorEnemyDamage => Mathf.Max(0f, repulsorEnemyDamage);

        public float GetRepulsorRadius(float playerSize, float outlineRadius = 0f) =>
            Mathf.Max(outlineRadius, RepulsorMaxRadius * RepulsorScale * Mathf.Max(.01f, playerSize));

        public float RepulsorInnerRadiusPlayerMultiplier => Mathf.Max(0f, repulsorInnerRadiusPlayerMultiplier);
        public float RepulsorInnerDamageMultiplier => Mathf.Max(1f, repulsorInnerDamageMultiplier);
        public bool RepulsorShowDamageRings => repulsorShowDamageRings;
        public float GetRepulsorInnerRadius(float bodyRadius, float outerRadius) =>
            Mathf.Min(Mathf.Max(0f, outerRadius), Mathf.Max(0f, bodyRadius) * RepulsorInnerRadiusPlayerMultiplier);

        public AnimationCurve RepulsorRadiusCurve => repulsorRadiusCurve;

        public float ActivationFrameStart => activationFrameStart;

        public float ActivationFrameEnd => Mathf.Max(activationFrameStart, activationFrameEnd);

        public float AnimationFrameRate => Mathf.Max(1f, animationFrameRate);

        public float ActivationStartNormalized =>
            Mathf.Clamp01((ActivationFrameStart / AnimationFrameRate) / Duration);

        public float ActivationEndNormalized =>
            Mathf.Clamp01((ActivationFrameEnd / AnimationFrameRate) / Duration);
    }

    public enum AttackStageType
    {
        PrimaryLunge,
        ComboSwipe,
        FinisherRepulsor
    }
}

