using TMPro;
using UnityEngine;

namespace Massive.Scoring
{
    [DisallowMultipleComponent]
    public sealed class MatchTimerPresenter : MonoBehaviour
    {
        [SerializeField] private GameManagerScript gameManager;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private bool useTimerTextForCountdown = true;
        [SerializeField] private bool showTenthsBelowTenSeconds = true;

        private MatchRuntimePhase _phase = MatchRuntimePhase.Preparing;
        private float _lastRegulationSeconds;

        private void Start()
        {
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManagerScript>();

            if (gameManager == null) return;

            gameManager.RegulationTimeChanged += OnTimeChanged;
            gameManager.CountdownTimeChanged += OnCountdownChanged;
            gameManager.PhaseChanged += OnPhaseChanged;

            _lastRegulationSeconds = gameManager.RegulationRemainingSeconds;
            OnPhaseChanged(gameManager.Phase);
            OnTimeChanged(_lastRegulationSeconds);
        }

        private void OnDestroy()
        {
            if (gameManager == null) return;

            gameManager.RegulationTimeChanged -= OnTimeChanged;
            gameManager.CountdownTimeChanged -= OnCountdownChanged;
            gameManager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnTimeChanged(float seconds)
        {
            _lastRegulationSeconds = Mathf.Max(0f, seconds);
            if (_phase != MatchRuntimePhase.Countdown || !useTimerTextForCountdown)
                ApplyRegulationText(_lastRegulationSeconds);
        }

        private void OnCountdownChanged(float seconds)
        {
            int count = Mathf.Max(0, Mathf.CeilToInt(seconds));
            string value = count > 0 ? count.ToString() : "GO";

            if (countdownText != null)
                countdownText.text = value;

            if (useTimerTextForCountdown && timerText != null)
                timerText.text = value;
        }

        private void OnPhaseChanged(MatchRuntimePhase phase)
        {
            _phase = phase;

            if (phaseText != null)
            {
                phaseText.text = phase switch
                {
                    MatchRuntimePhase.Preparing => "PREPARING",
                    MatchRuntimePhase.Countdown => "READY",
                    MatchRuntimePhase.Regulation => "REGULATION",
                    MatchRuntimePhase.Bonus => "BONUS",
                    MatchRuntimePhase.Resolving => "RESULT",
                    _ => string.Empty
                };
            }

            if (countdownText != null)
                countdownText.gameObject.SetActive(phase == MatchRuntimePhase.Countdown);

            if (phase != MatchRuntimePhase.Countdown)
                ApplyRegulationText(_lastRegulationSeconds);
        }

        private void ApplyRegulationText(float seconds)
        {
            if (timerText == null) return;

            seconds = Mathf.Max(0f, seconds);
            if (showTenthsBelowTenSeconds && seconds < 10f)
            {
                timerText.text = seconds.ToString("00.0");
                return;
            }

            int total = Mathf.CeilToInt(seconds);
            int minutes = total / 60;
            int remainingSeconds = total % 60;
            timerText.text = $"{minutes:00}:{remainingSeconds:00}";
        }
    }
}
