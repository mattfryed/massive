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
        [Tooltip("Max world radius for FinisherRepulsor hitbox / VFX.")]
        [SerializeField]
        private float repulsorMaxRadius = 3.0f;

        [Tooltip("Radius over time (0..1 stage normalized -> 0..1 radius).")]
        [SerializeField]
        private AnimationCurve repulsorRadiusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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

        public string AnimationStateName => animationStateName;

        public float AnimationTransitionDuration => animationTransitionDuration;

        public float RepulsorMaxRadius => Mathf.Max(0f, repulsorMaxRadius);

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

