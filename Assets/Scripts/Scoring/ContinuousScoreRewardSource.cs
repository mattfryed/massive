using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Generic fixed-tick bridge for continuous objectives and bonus rounds.
    /// A stage controller can call BeginForPlayer/BeginForTeam and Stop without
    /// knowing score values. Use a reward rule whose repeat policy is Unlimited.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScoreRewardEmitter))]
    public sealed class ContinuousScoreRewardSource : MonoBehaviour
    {
        [SerializeField] private ScoreRewardEmitter rewardEmitter;
        [SerializeField, Min(0.02f)] private float tickIntervalSeconds = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool stopWhenDisabled = true;

        private PlayerControllerScript _multiplierOwner;
        private int _teamID;
        private bool _running;
        private float _accumulator;

        public bool IsRunning => _running;
        public int TeamID => _teamID;

        private void Awake()
        {
            if (rewardEmitter == null)
                rewardEmitter = GetComponent<ScoreRewardEmitter>();
        }

        private void OnDisable()
        {
            if (stopWhenDisabled)
                Stop();
        }

        private void Update()
        {
            if (!_running || rewardEmitter == null)
                return;

            MatchScoreService service = MatchScoreService.Instance;
            if (service == null || !service.IsScoringOpen)
                return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _accumulator += dt;

            float interval = Mathf.Max(0.02f, tickIntervalSeconds);
            int safety = 0;
            while (_accumulator >= interval && safety++ < 16)
            {
                _accumulator -= interval;
                rewardEmitter.TryAwardToTeam(
                    _teamID,
                    _multiplierOwner,
                    transform.position,
                    quantity: 1L,
                    eventSuffix: null);
            }
        }

        public void BeginForPlayer(PlayerControllerScript player)
        {
            if (player == null)
            {
                Stop();
                return;
            }

            BeginForTeam(player.teamID, player);
        }

        public void BeginForTeam(int teamID, PlayerControllerScript multiplierOwner = null)
        {
            if (teamID != 1 && teamID != 2)
            {
                Stop();
                return;
            }

            _teamID = teamID;
            _multiplierOwner = multiplierOwner;
            _accumulator = 0f;
            _running = true;
        }

        public void Stop()
        {
            _running = false;
            _teamID = 0;
            _multiplierOwner = null;
            _accumulator = 0f;
        }
    }
}
