using System;
using UnityEngine;

namespace Massive.Scoring
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerControllerScript))]
    public sealed class PlayerScoreChain : MonoBehaviour
    {
        [SerializeField] private PlayerControllerScript player;
        [SerializeField] private bool logStateChanges;

        private ScoreChainSettings _settings;
        private int _tierIndex;
        private float _charge;
        private bool _configured;

        public event Action<PlayerScoreChain> Changed;

        public PlayerControllerScript Player => player;
        public int TierIndex => _tierIndex;
        public double MinimumMultiplier => _settings.GetMultiplier(0);
        public double MaximumMultiplier => _settings.GetMultiplier(_settings.MaxIndex);
        public double CurrentMultiplier
        {
            get
            {
                double lower = _settings.GetMultiplier(_tierIndex);
                if (!_configured || IsAtMaxTier) return lower;
                double fraction = Math.Max(0d, Math.Min(1d, _charge / (double)ChargeRequired));
                return lower + (_settings.GetMultiplier(_tierIndex + 1) - lower) * fraction;
            }
        }
        public bool IsAtMaxTier => _configured && _tierIndex >= _settings.MaxIndex;
        public bool IsAtMaxMultiplier => IsAtMaxTier && MaximumMultiplier > MinimumMultiplier;
        public float Charge => Mathf.Max(0f, _charge);
        public float ChargeRequired => _settings.GetChargeRequired(_tierIndex);
        public float Progress01 => MaximumMultiplier > MinimumMultiplier
            ? Mathf.Clamp01((float)((CurrentMultiplier - MinimumMultiplier) / (MaximumMultiplier - MinimumMultiplier)))
            : 0f;

        [Obsolete("Personal multipliers now use persistent charge. Use Charge instead.")]
        public float TimeRemaining => Charge;

        [Obsolete("Personal multipliers now use persistent charge. Use Progress01 instead.")]
        public float TimeRemaining01 => Progress01;
        public bool IsChaining => CurrentMultiplier > MinimumMultiplier;

        private void Awake()
        {
            if (player == null)
                player = GetComponent<PlayerControllerScript>();
        }

        private void OnEnable()
        {
            if (player == null)
                player = GetComponent<PlayerControllerScript>();

            if (player != null)
            {
                player.DeathStarted += OnDeathStarted;
                player.HitAccepted += OnHitAccepted;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.DeathStarted -= OnDeathStarted;
                player.HitAccepted -= OnHitAccepted;
            }
        }

        public void Configure(ScoreChainSettings settings, bool reset = true)
        {
            _settings = settings;
            _configured = true;

            if (reset)
                ResetChain();
            else
            {
                ClampState();
                Changed?.Invoke(this);
            }
        }

        public void ApplyAward(ScoreChainAwardMode mode)
        {
            ApplyAward(mode, 1f);
        }

        public void ApplyAward(ScoreChainAwardMode mode, float charge)
        {
            if (!_configured || mode == ScoreChainAwardMode.None || float.IsNaN(charge) || charge <= 0f || IsAtMaxTier)
                return;

            int oldTier = _tierIndex;
            float oldCharge = _charge;
            _charge += Mathf.Max(0f, charge);

            while (_tierIndex < _settings.MaxIndex)
            {
                float required = _settings.GetChargeRequired(_tierIndex);
                if (_charge + 0.0001f < required)
                    break;

                _charge = Mathf.Max(0f, _charge - required);
                _tierIndex++;
            }

            if (IsAtMaxTier)
                _charge = 0f;

            if (oldTier != _tierIndex || !Mathf.Approximately(oldCharge, _charge))
                NotifyChanged("award");
        }

        public void ResetChain()
        {
            bool changed = _tierIndex != 0 || _charge > 0f;
            _tierIndex = 0;
            _charge = 0f;

            if (changed)
                NotifyChanged("reset");
            else
                Changed?.Invoke(this);
        }

        private void OnDeathStarted(PlayerControllerScript _)
        {
            ResetChain();
        }

        private void OnHitAccepted(PlayerHitResult hit)
        {
            if (!hit.accepted || !hit.disruptsScoreChain || !IsChaining)
                return;

            switch (_settings.hitPenalty)
            {
                case ScoreChainHitPenalty.None:
                    return;

                case ScoreChainHitPenalty.LoseTime:
                    float remainingPenalty = Mathf.Max(0f, _settings.hitTimePenaltySeconds);
                    while (remainingPenalty > _charge && _tierIndex > 0)
                    {
                        remainingPenalty -= _charge;
                        _tierIndex--;
                        _charge = _settings.GetChargeRequired(_tierIndex);
                    }
                    _charge = Mathf.Max(0f, _charge - remainingPenalty);
                    NotifyChanged("hit-charge");
                    return;

                case ScoreChainHitPenalty.DropOneTier:
                    _tierIndex = Mathf.Max(0, _tierIndex - 1);
                    _charge = 0f;
                    NotifyChanged("hit-drop");
                    return;

                case ScoreChainHitPenalty.Reset:
                    ResetChain();
                    return;
            }
        }

        private void ClampState()
        {
            _tierIndex = Mathf.Clamp(_tierIndex, 0, _settings.MaxIndex);
            _charge = IsAtMaxTier
                ? 0f
                : Mathf.Clamp(_charge, 0f, _settings.GetChargeRequired(_tierIndex));
        }

        private void NotifyChanged(string reason)
        {
            if (logStateChanges)
            {
                Debug.Log(
                    $"[PlayerScoreChain] P{(player != null ? player.playerID + 1 : 0)} {reason}: " +
                    $"tier={_tierIndex}, multiplier=x{CurrentMultiplier}, " +
                    $"charge={_charge:0.##}/{ChargeRequired:0.##}",
                    this);
            }

            Changed?.Invoke(this);
        }
    }
}
