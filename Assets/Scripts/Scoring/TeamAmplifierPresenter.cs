using System.Collections;
using TMPro;
using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Event-driven view of one team's shared Amplifier multiplier.
    /// MatchScoreService remains the sole authority for the value.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamAmplifierPresenter : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] private int teamID = 1;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private string multiplierPrefix = "x";

        private MatchScoreService _service;
        private Coroutine _bindRoutine;

        public int TeamID => teamID;

        private void OnEnable()
        {
            if (_bindRoutine == null)
                _bindRoutine = StartCoroutine(BindWhenAvailable());
        }

        private void OnDisable()
        {
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
                SetMultiplier(snapshot.currentMultiplier);
        }

        private void Refresh()
        {
            SetMultiplier(_service != null
                ? _service.GetTeamAmplifierMultiplier(teamID)
                : 1);
        }

        private void SetMultiplier(int multiplier)
        {
            if (multiplierText != null)
                multiplierText.text = $"{multiplierPrefix}{Mathf.Max(1, multiplier)}";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            teamID = Mathf.Clamp(teamID, 1, 2);
        }
#endif
    }
}
