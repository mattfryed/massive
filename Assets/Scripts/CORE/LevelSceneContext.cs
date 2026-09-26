using Massive.Scoring;
using Rewired;
using UnityEngine;

namespace Massive.Levels
{
    /// <summary>Scene composition and session identity. Match rules stay in
    /// GameManagerScript and MatchScoreService; only session/input persist.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-500)]
    public sealed class LevelSceneContext : MonoBehaviour
    {
        public LevelDefinition level;
        public StageProfile stage;
        public GameManagerScript match;
        public PlayerRosterController roster;
        public GameObject inputManagerPrefab;
        public GameObject spawners;
        public AnomalyManager anomalies;
        public TMPro.TMP_Text stageTitleText;
        public TMPro.TMP_Text stageNumberText;
        [Tooltip("Freeze scaled world simulation during a bonus. Bonus UI/input uses realtime.")]
        public bool pauseWorldDuringBonus = true;

        private GameManagerScript subscribed;
        private bool ownsTimePause;
        private float previousTimeScale;

        private void Awake()
        {
            GameFlowContext.EnsureExists();
            if (level)
            {
                GameFlowContext.Instance.SelectLevel(level);
                if (stageTitleText) stageTitleText.text = level.levelTitle;
                if (stageNumberText) stageNumberText.text = "STAGE_" + level.levelNumber.ToString("000");
            }
            if (!ReInput.isReady && !FindFirstObjectByType<InputManager>() && inputManagerPrefab)
            {
                var input = Instantiate(inputManagerPrefab);
                input.name = "Rewired Input Manager";
                DontDestroyOnLoad(input);
            }
            if (spawners) spawners.SetActive(false);
        }

        private void OnEnable()
        {
            // Script reload can clear the static session reference while its
            // persistent object survives. Rebind before restoring scene events.
            if (GameFlowContext.Instance == null)
            {
                GameFlowContext.EnsureExists();
                if (level) GameFlowContext.Instance.SelectLevel(level);
            }
            subscribed = match;
            if (!subscribed) return;
            subscribed.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(subscribed.Phase);
        }

        private void OnPhaseChanged(MatchRuntimePhase phase)
        {
            bool bonus = pauseWorldDuringBonus && phase == MatchRuntimePhase.Bonus;
            if (bonus && !ownsTimePause)
            {
                previousTimeScale = Time.timeScale;
                ownsTimePause = true;
                Time.timeScale = 0f;
            }
            else if (!bonus) RestoreWorldTime();

            // Bonus keeps the existing objects alive; their scaled clocks and
            // physics stop without triggering destructive OnDisable cleanup.
            if (spawners) spawners.SetActive(phase == MatchRuntimePhase.Regulation ||
                (phase == MatchRuntimePhase.Bonus && !match.IsTerminalBonus));
            if ((phase == MatchRuntimePhase.Resolving || phase == MatchRuntimePhase.Complete) && anomalies)
                anomalies.CancelCurrentAnomaly();
        }

        private void RestoreWorldTime()
        {
            if (!ownsTimePause) return;
            Time.timeScale = previousTimeScale;
            ownsTimePause = false;
        }

        private void OnDisable()
        {
            if (subscribed) subscribed.PhaseChanged -= OnPhaseChanged;
            subscribed = null;
            RestoreWorldTime();
        }
    }
}
