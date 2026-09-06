using System.Collections;
using Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Massive.Scoring
{
    [DisallowMultipleComponent]
    public sealed class PlayerScoreChainPresenter : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField, Min(0)] private int playerID;
        [SerializeField] private PlayerScoreChain chain;

        [Header("View")]
        [SerializeField] private TMP_Text multiplierText;
        [FormerlySerializedAs("timeFill")]
        [SerializeField] private Image progressImage;
        [SerializeField] private Rectangle progressRectangle;
        [Tooltip("Authored full-width background for a Shapes progress bar.")]
        [SerializeField] private Rectangle progressTrackRectangle;
        [SerializeField, Min(0.01f)] private float progressSmoothSeconds = 0.16f;
        [SerializeField] private GameObject activeRoot;
        [SerializeField] private bool hideAtBaseMultiplier;
        [SerializeField] private string multiplierPrefix = "";

        private Coroutine _bindRoutine;
        private bool _subscribed;
        private float _minimumRectangleWidth;
        private float _fullRectangleWidth;
        private Vector3 _fullRectangleLocalPosition;
        private bool _capturedRectangleLayout;
        private float _displayedProgress;
        private float _targetProgress;
        private float _progressVelocity;
        private bool _progressInitialized;

        public int PlayerID => playerID;
        public PlayerScoreChain Chain => chain;

        private void OnEnable()
        {
            CaptureRectangleLayout();
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
            if (!_progressInitialized)
                return;

            float previous = _displayedProgress;
            _displayedProgress = Mathf.SmoothDamp(
                _displayedProgress,
                _targetProgress,
                ref _progressVelocity,
                Mathf.Max(0.01f, progressSmoothSeconds),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (Mathf.Abs(_displayedProgress - _targetProgress) < 0.0005f)
            {
                _displayedProgress = _targetProgress;
                _progressVelocity = 0f;
            }

            if (!Mathf.Approximately(previous, _displayedProgress))
                ApplyDisplayedProgress();
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
            {
                chain = GetComponentInParent<PlayerScoreChain>();
                if (chain == null && MatchScoreService.Instance != null)
                    chain = MatchScoreService.Instance.GetPlayerChain(playerID);
            }

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
            bool active = !hideAtBaseMultiplier || (chain != null && chain.IsChaining);

            if (activeRoot != null)
                activeRoot.SetActive(active);

            if (multiplierText != null)
                multiplierText.text = chain != null
                    ? $"{multiplierPrefix}{chain.CurrentMultiplier}"
                    : $"{multiplierPrefix}1";

            float progress = chain != null ? chain.Progress01 : 0f;
            SetProgressTarget(progress);
        }

        private void CaptureRectangleLayout()
        {
            if (_capturedRectangleLayout || progressRectangle == null)
                return;

            _minimumRectangleWidth = Mathf.Max(0.001f, progressRectangle.Width);
            _fullRectangleWidth = progressTrackRectangle != null
                ? Mathf.Max(_minimumRectangleWidth, progressTrackRectangle.Width)
                : _minimumRectangleWidth;
            _fullRectangleLocalPosition = progressRectangle.transform.localPosition;
            _capturedRectangleLayout = true;
        }

        private void SetProgressTarget(float progress01)
        {
            _targetProgress = Mathf.Clamp01(progress01);
            if (_progressInitialized)
                return;

            _displayedProgress = _targetProgress;
            _progressInitialized = true;
            ApplyDisplayedProgress();
        }

        private void ApplyDisplayedProgress()
        {
            if (progressImage != null)
                progressImage.fillAmount = _displayedProgress;

            ApplyRectangleProgress(_displayedProgress);
        }

        private void ApplyRectangleProgress(float progress01)
        {
            if (progressRectangle == null)
                return;

            CaptureRectangleLayout();
            float progress = Mathf.Clamp01(progress01);
            float width = Mathf.Lerp(_minimumRectangleWidth, _fullRectangleWidth, progress);
            progressRectangle.Width = width;

            // The authored Shapes bars use Corner pivots, so retaining their
            // local origin makes the visible bar grow cleanly left-to-right.
            progressRectangle.transform.localPosition = _fullRectangleLocalPosition;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            playerID = Mathf.Max(0, playerID);
            progressSmoothSeconds = Mathf.Max(0.01f, progressSmoothSeconds);

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
