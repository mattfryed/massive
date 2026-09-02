using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Massive.Scoring
{
    [DisallowMultipleComponent]
    public sealed class PlayerScoreChainPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerScoreChain chain;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private Image timeFill;
        [SerializeField] private GameObject activeRoot;
        [SerializeField] private string multiplierPrefix = "×";

        private Coroutine _bindRoutine;
        private bool _subscribed;

        private void OnEnable()
        {
            TryBindImmediate();

            if (chain == null && _bindRoutine == null)
                _bindRoutine = StartCoroutine(BindWhenAvailable());

            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }
        }

        private void Update()
        {
            if (chain != null && timeFill != null)
                timeFill.fillAmount = chain.TimeRemaining01;
        }

        private IEnumerator BindWhenAvailable()
        {
            while (isActiveAndEnabled && chain == null)
            {
                TryBindImmediate();
                if (chain == null)
                    yield return null;
            }

            _bindRoutine = null;
            Refresh();
        }

        private void TryBindImmediate()
        {
            if (chain == null)
                chain = GetComponentInParent<PlayerScoreChain>();

            if (chain == null || _subscribed)
                return;

            chain.Changed += OnChainChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && chain != null)
                chain.Changed -= OnChainChanged;

            _subscribed = false;
        }

        private void OnChainChanged(PlayerScoreChain _)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool active = chain != null && chain.IsChaining;

            if (activeRoot != null)
                activeRoot.SetActive(active);

            if (multiplierText != null)
                multiplierText.text = chain != null
                    ? $"{multiplierPrefix}{chain.CurrentMultiplier}"
                    : $"{multiplierPrefix}1";

            if (timeFill != null)
                timeFill.fillAmount = chain != null ? chain.TimeRemaining01 : 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (activeRoot == null) return;

            Transform activeTransform = activeRoot.transform;
            if (transform == activeTransform || transform.IsChildOf(activeTransform))
            {
                Debug.LogWarning(
                    "[PlayerScoreChainPresenter] Active Root contains the presenter. " +
                    "Disabling it at x1 would also disable this listener. Use a separate child visual root.",
                    this);
            }
        }
#endif
    }
}
