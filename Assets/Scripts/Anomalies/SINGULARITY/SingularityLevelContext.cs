using Massive.Scoring;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Level identity and the small lifetime bridge between the standard
    /// match owner and this level's portal. Does not own scoring or player input.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class SingularityLevelContext : MonoBehaviour
    {
        public LevelDefinition level;
        [Tooltip("Stage identity reserved for future scheduled anomalies. Its empty pool does not add gameplay.")]
        public StageProfile stage;
        public GameManagerScript match;
        public SingularityBlackHolePortal portal;

        private GameManagerScript subscribed;

        private void Awake()
        {
            GameFlowContext.EnsureExists();
            if (level != null) GameFlowContext.Instance.SelectLevel(level);
        }

        private void OnEnable()
        {
            subscribed = match;
            if (subscribed == null) return;
            subscribed.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(subscribed.Phase);
        }

        private void OnDisable()
        {
            if (subscribed != null) subscribed.PhaseChanged -= OnPhaseChanged;
            subscribed = null;
        }

        private void OnPhaseChanged(MatchRuntimePhase phase)
        {
            if (portal != null && (phase == MatchRuntimePhase.Resolving || phase == MatchRuntimePhase.Complete))
                portal.CancelAll();
        }
    }
}
