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
        private float _timeRemaining;
        private bool _configured;

        public event Action<PlayerScoreChain> Changed;

        public PlayerControllerScript Player => player;
        public int TierIndex => _tierIndex;
        public int CurrentMultiplier => _settings.GetMultiplier(_tierIndex);
        public float TimeRemaining => Mathf.Max(0f, _timeRemaining);
        public float TimeRemaining01 => _settings.timeoutSeconds > 0f
            ? Mathf.Clamp01(_timeRemaining / _settings.timeoutSeconds)
            : 0f;
        public bool IsChaining => _tierIndex > 0;

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

        private void Update()
        {
            if (!_configured || _tierIndex <= 0)
                return;

            MatchScoreService service = MatchScoreService.Instance;
            if (service != null && !service.IsChainClockRunning)
                return;

            float dt = _settings.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _timeRemaining -= dt;

            if (_timeRemaining <= 0f)
                ResetChain();
        }

        public void Configure(ScoreChainSettings settings, bool reset = true)
        {
            _settings = settings;
            _configured = true;

            if (reset)
                ResetChain();
            else
                ClampState();
        }

        public void ApplyAward(ScoreChainAwardMode mode)
        {
            if (!_configured || mode == ScoreChainAwardMode.None)
                return;

            int oldTier = _tierIndex;
            float oldTime = _timeRemaining;

            if (mode == ScoreChainAwardMode.AdvanceAndRefresh)
                _tierIndex = Mathf.Min(_tierIndex + 1, _settings.MaxIndex);

            if (_tierIndex > 0)
                _timeRemaining = _settings.timeoutSeconds;

            if (oldTier != _tierIndex || !Mathf.Approximately(oldTime, _timeRemaining))
                NotifyChanged("award");
        }

        public void ResetChain()
        {
            bool changed = _tierIndex != 0 || _timeRemaining > 0f;
            _tierIndex = 0;
            _timeRemaining = 0f;

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
            if (!hit.accepted || !hit.disruptsScoreChain || _tierIndex <= 0)
                return;

            switch (_settings.hitPenalty)
            {
                case ScoreChainHitPenalty.None:
                    return;

                case ScoreChainHitPenalty.LoseTime:
                    _timeRemaining = Mathf.Max(0f, _timeRemaining - _settings.hitTimePenaltySeconds);
                    if (_timeRemaining <= 0f)
                    {
                        ResetChain();
                        return;
                    }
                    NotifyChanged("hit-time");
                    return;

                case ScoreChainHitPenalty.DropOneTier:
                    _tierIndex = Mathf.Max(0, _tierIndex - 1);
                    if (_tierIndex == 0)
                        _timeRemaining = 0f;
                    else
                        _timeRemaining = Mathf.Min(_timeRemaining, _settings.timeoutSeconds);
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
            _timeRemaining = Mathf.Clamp(_timeRemaining, 0f, _settings.timeoutSeconds);
        }

        private void NotifyChanged(string reason)
        {
            if (logStateChanges)
            {
                Debug.Log(
                    $"[PlayerScoreChain] P{(player != null ? player.playerID + 1 : 0)} {reason}: " +
                    $"tier={_tierIndex}, multiplier=x{CurrentMultiplier}, time={_timeRemaining:0.00}",
                    this);
            }

            Changed?.Invoke(this);
        }
    }
}
