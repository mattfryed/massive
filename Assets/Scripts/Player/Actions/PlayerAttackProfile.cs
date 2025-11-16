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

        [SerializeField]
        private ParticleSystem particlePrefab = null;

        public string StageName => stageName;

        public AttackStageType StageType => stageType;

        public float Duration => Mathf.Max(0.01f, duration);

        public float TravelDistance => travelDistance;

        public AnimationCurve DistanceCurve => distanceCurve;

        public float RotationArc => rotationArc;

        public bool AllowComboCancel => allowComboCancel;

        public string AnimationStateName => animationStateName;

        public float AnimationTransitionDuration => animationTransitionDuration;

        public ParticleSystem ParticlePrefab => particlePrefab;

        public float ActivationFrameStart => activationFrameStart;

        public float ActivationFrameEnd => Mathf.Max(activationFrameStart, activationFrameEnd);

        public float AnimationFrameRate => Mathf.Max(1f, animationFrameRate);

        public float ActivationStartNormalized => Mathf.Clamp01((ActivationFrameStart / AnimationFrameRate) / Duration);

        public float ActivationEndNormalized => Mathf.Clamp01((ActivationFrameEnd / AnimationFrameRate) / Duration);
    }

    public enum AttackStageType
    {
        PrimaryLunge,
        ComboSwipe,
        FinisherRepulsor
    }
}
