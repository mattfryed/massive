using UnityEngine;

namespace Massive.Player
{
    /// <summary>Visual-only body recoil and a short locomotion recovery for the third attack.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MASSIVE/Player/Repulsor Body and Recovery")]
    public sealed class PlayerRepulsorFeedback : MonoBehaviour
    {
        [Header("Body Pulse")]
        public bool bodyPulseEnabled = true;
        [Range(0f, .3f)] public float contraction = .10f;
        [Tooltip("Extra visual size at the peak. .15 means 115% of this player's normal size.")]
        [Range(0f, .3f)] public float expansion = .15f;
        [Min(.01f)] public float reboundRiseTime = .055f;
        [Min(.02f)] public float settleTime = .30f;
        [Range(0f, .06f)] public float vibrationAmount = .018f;
        [Range(1f, 60f)] public float vibrationFrequency = 28f;

        [Header("Movement Recovery")]
        public bool recoveryEnabled = true;
        [Tooltip("Movement available immediately after Repulsor finishes; eases back to normal.")]
        [Range(.05f, 1f)] public float recoveryMovement = .55f;
        [Min(0f)] public float recoveryDuration = .35f;

        PlayerAttackController attack;
        PlayerControllerScript player;
        PlayerVisualController visuals;
        PlayerRepulsorAOE repulsor;
        AttackStage activeStage;
        float startedAt;
        float recoveryAt = float.NegativeInfinity;
        bool preview;

        public float RecoveryMultiplier { get; private set; } = 1f;

        void Resolve()
        {
            if (!attack) attack = GetComponent<PlayerAttackController>();
            if (!player) player = GetComponent<PlayerControllerScript>();
            if (!visuals) visuals = GetComponentInChildren<PlayerVisualController>(true);
            if (!repulsor) repulsor = GetComponentInChildren<PlayerRepulsorAOE>(true);
        }

        void OnEnable()
        {
            Resolve();
            if (!attack) return;
            attack.OnStageStarted.AddListener(StageStarted);
            attack.OnStageCompleted.AddListener(StageCompleted);
            attack.StageCancelled += StageCancelled;
        }

        void OnDisable()
        {
            if (attack)
            {
                attack.OnStageStarted.RemoveListener(StageStarted);
                attack.OnStageCompleted.RemoveListener(StageCompleted);
                attack.StageCancelled -= StageCancelled;
            }
            ResetFeedback();
        }

        void StageStarted(AttackStage stage)
        {
            if (stage.StageType != AttackStageType.FinisherRepulsor) return;
            activeStage = stage;
            startedAt = Time.time;
            preview = false;
            recoveryAt = float.NegativeInfinity;
            if (player) player.RemoveMovementInfluence(this);
        }

        void StageCompleted(AttackStage stage)
        {
            if (stage == null || stage.StageType != AttackStageType.FinisherRepulsor) return;
            if (activeStage == null) return; // Cancellation may also signal legacy completion.
            if (recoveryEnabled && recoveryDuration > 0f)
            {
                recoveryAt = Time.time;
                ApplyRecovery(0f);
            }
        }

        void StageCancelled(AttackStage stage)
        {
            if (stage != null && stage.StageType == AttackStageType.FinisherRepulsor) ResetFeedback();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || preview) return;
            if (!player || player.temporarilyEliminated || !player.enabled || !attack || !attack.enabled)
            { ResetFeedback(); return; }
            if (activeStage != null) ApplyBody(Time.time - startedAt, activeStage);
            if (!float.IsNegativeInfinity(recoveryAt)) ApplyRecovery(Time.time - recoveryAt);
        }

        void ApplyRecovery(float elapsed)
        {
            float t = recoveryDuration > 0f ? Mathf.Clamp01(elapsed / recoveryDuration) : 1f;
            RecoveryMultiplier = recoveryEnabled ? Mathf.Lerp(recoveryMovement, 1f, Mathf.SmoothStep(0f, 1f, t)) : 1f;
            if (player)
            {
                if (t < 1f && recoveryEnabled) player.SetMovementInfluence(this, RecoveryMultiplier);
                else player.RemoveMovementInfluence(this);
            }
            if (t >= 1f || !recoveryEnabled) recoveryAt = float.NegativeInfinity;
        }

        void ApplyBody(float elapsed, AttackStage stage)
        {
            if (!visuals) return;
            if (!bodyPulseEnabled || elapsed < 0f) { visuals.ClearRepulsorVisual(); return; }
            float release = (repulsor ? repulsor.EffectiveActivationStart(stage) : stage.ActivationStartNormalized) * stage.Duration;
            float scale;
            Vector3 offset = Vector3.zero;
            if (elapsed < release)
            {
                float t = release > 0f ? elapsed / release : 1f;
                scale = Mathf.Lerp(1f, 1f - contraction, Mathf.SmoothStep(0f, 1f, t));
                float a = vibrationAmount * PlayerScaleAdjuster.SizeOf(this) * Mathf.Sin(t * Mathf.PI);
                float phase = elapsed * vibrationFrequency * 2f * Mathf.PI;
                offset = new Vector3(Mathf.Sin(phase) + .3f * Mathf.Sin(phase * 1.71f), 0f,
                    Mathf.Cos(phase * 1.23f)) * a;
            }
            else
            {
                float age = elapsed - release;
                if (age < reboundRiseTime)
                    scale = Mathf.Lerp(release > 0f ? 1f - contraction : 1f, 1f + expansion, Mathf.SmoothStep(0f, 1f, age / Mathf.Max(.01f, reboundRiseTime)));
                else
                {
                    float t = Mathf.Clamp01((age - reboundRiseTime) / Mathf.Max(.02f, settleTime));
                    scale = 1f + expansion * Mathf.Cos(t * Mathf.PI * 2f) * Mathf.Exp(-4f * t) * (1f - t);
                }
            }
            visuals.SetRepulsorVisual(scale, offset);
        }

        public void Preview(float elapsedSeconds, AttackStage stage)
        {
            if (Application.isPlaying || !enabled) return;
            Resolve(); preview = true;
            if (stage != null) ApplyBody(elapsedSeconds, stage);
        }

        public void StopPreview()
        {
            if (!preview) return;
            preview = false;
            if (visuals) visuals.ClearRepulsorVisual();
        }

        void ResetFeedback()
        {
            activeStage = null; preview = false;
            recoveryAt = float.NegativeInfinity; RecoveryMultiplier = 1f;
            if (visuals) visuals.ClearRepulsorVisual();
            if (player) player.RemoveMovementInfluence(this);
        }
    }
}
