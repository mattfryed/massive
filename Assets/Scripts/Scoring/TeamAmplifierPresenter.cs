using System.Collections;
using TMPro;
using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Event-driven view of one team's shared Amplifier multiplier.
    /// MatchScoreService remains the sole authority for the value.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed partial class TeamAmplifierPresenter : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] private int teamID = 1;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private string multiplierPrefix = "x";

        private MatchScoreService _service;
        private Coroutine _bindRoutine;

        public int TeamID => teamID;
        private System.Func<int> demonstrationMultiplier;
        private Renderer demonstrationBadgeSource;
        private float demonstrationBadgeScale = 1f;
        public void ConfigureDemonstration(int team, ScoreVoidMetaballsVisual surface, System.Func<int> multiplier,
            Renderer badgeSource = null, float badgeScale = 1f)
        {
            teamID = team; goalSurface = surface; demonstrationMultiplier = multiplier; demonstrationBadgeSource = badgeSource;
            demonstrationBadgeScale = Mathf.Max(.01f, badgeScale);
        }

        private void OnEnable()
        {
            InitializeGoalNumber();
            if (demonstrationMultiplier != null) SetMultiplier(demonstrationMultiplier());
            else if (Application.isPlaying && _bindRoutine == null)
                _bindRoutine = StartCoroutine(BindWhenAvailable());
        }

        private void OnDisable()
        {
            ReleaseGoalNumber();
            Unbind();

            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }
        }

        private IEnumerator BindWhenAvailable()
        {
            while (isActiveAndEnabled && _service == null)
            {
                _service = MatchScoreService.Instance;
                if (_service == null)
                    _service = FindFirstObjectByType<MatchScoreService>();

                if (_service == null)
                    yield return null;
            }

            _bindRoutine = null;
            if (!isActiveAndEnabled || _service == null)
                yield break;

            _service.TeamAmplifierChanged += OnTeamAmplifierChanged;
            _service.ScoresReset += Refresh;
            Refresh();
        }

        private void Unbind()
        {
            if (_service == null)
                return;

            _service.TeamAmplifierChanged -= OnTeamAmplifierChanged;
            _service.ScoresReset -= Refresh;
            _service = null;
        }

        private void OnTeamAmplifierChanged(TeamAmplifierSnapshot snapshot)
        {
            if (snapshot.teamID == teamID)
                SetMultiplier(snapshot.currentMultiplier, true);
        }

        private void Refresh()
        {
            SetMultiplier(_service != null
                ? _service.GetTeamAmplifierMultiplier(teamID)
                : 1);
        }

        private void SetMultiplier(int multiplier, bool animate = false)
        {
            int next = Mathf.Max(1, multiplier);
            if (animate && next > displayedMultiplier && GoalNumberSettings.animateChanges)
                BeginGoalTransition(displayedMultiplier, next);
            else if (next != displayedMultiplier || !animate)
                StopGoalTransition();
            displayedMultiplier = next;
            if (multiplierText != null)
                multiplierText.text = $"{multiplierPrefix}{Mathf.Max(1, multiplier)}";
            RefreshGoalNumber();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            teamID = Mathf.Clamp(teamID, 1, 2);
        }
#endif
    }
}
