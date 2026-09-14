using System;
using Massive.Scoring;
using TMPro;
using UnityEngine;

namespace Massive.Player
{
    public enum PlayerHudLayoutMode { Automatic, OneVOne, TwoVTwo }

    /// <summary>Fits each team's personal HUDs to the stable team-score text frame.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class TeamPlayerHudLayout : MonoBehaviour
    {
        [Serializable]
        private sealed class PlayerPanel
        {
            public PlayerHealthPresenter health;
            public PlayerScoreChainPresenter multiplier;
            [NonSerialized] public PlayerControllerScript player;
            [NonSerialized] public TMP_Text healthLabel, multiplierLabel;
            public int ID => multiplier != null ? multiplier.PlayerID : health.PlayerID;
        }

        [Tooltip("Automatic follows roster player roots, including demo scenes. Overrides only preview the HUD layout.")]
        [SerializeField] private PlayerHudLayoutMode layoutMode;
        [SerializeField] private TMP_Text teamScoreText;
        [SerializeField] private PlayerPanel firstPlayer = new PlayerPanel();
        [SerializeField] private PlayerPanel secondPlayer = new PlayerPanel();
        [SerializeField, Min(0f)] private float twoPlayerGap = 0.25f;

        private int _lastMask = -1;
        private float _lastLeft, _lastRight, _lastGap;
        private float _nextPlayerSearch;
        private readonly Vector3[] _corners = new Vector3[4];
        public bool IsSinglePlayerLayout { get; private set; }

        private void OnEnable()
        {
            CacheLabels(firstPlayer);
            CacheLabels(secondPlayer);
            _lastMask = -1;
            _nextPlayerSearch = 0f;
            RefreshLayout();
        }

        private void LateUpdate() => RefreshLayout();

        /// <summary>Changes presentation only; Automatic returns control to the roster.</summary>
        public void SetLayoutMode(PlayerHudLayoutMode mode)
        {
            layoutMode = mode;
            _lastMask = -1;
            CacheLabels(firstPlayer);
            CacheLabels(secondPlayer);
            RefreshLayout();
        }

        private static void CacheLabels(PlayerPanel panel)
        {
            if (panel.health != null && panel.healthLabel == null)
                panel.healthLabel = panel.health.transform.Find("Player text")?.GetComponent<TMP_Text>();
            if (panel.multiplier != null && panel.multiplierLabel == null)
                panel.multiplierLabel = panel.multiplier.transform.Find("Player text")?.GetComponent<TMP_Text>();
        }

        private void RefreshLayout()
        {
            if (teamScoreText == null || firstPlayer.health == null || firstPlayer.multiplier == null ||
                secondPlayer.health == null || secondPlayer.multiplier == null) return;

            int mask = ResolvePlayerMask();
            teamScoreText.rectTransform.GetWorldCorners(_corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            for (int i = 0; i < _corners.Length; i++)
            {
                float x = transform.InverseTransformPoint(_corners[i]).x;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
            }
            if (right <= left) return;
            if (mask == _lastMask && Mathf.Approximately(left, _lastLeft) &&
                Mathf.Approximately(right, _lastRight) && Mathf.Approximately(twoPlayerGap, _lastGap)) return;
            _lastMask = mask; _lastLeft = left; _lastRight = right; _lastGap = twoPlayerGap;

            IsSinglePlayerLayout = mask != 3;
            if (IsSinglePlayerLayout)
            {
                PlayerPanel visible = mask == 2 ? secondPlayer : firstPlayer;
                PlayerPanel hidden = mask == 2 ? firstPlayer : secondPlayer;
                SetVisible(hidden, false);
                LayoutPanel(visible, left, right);
                SetVisible(visible, true);
            }
            else
            {
                float half = (right - left - Mathf.Max(0f, twoPlayerGap)) * 0.5f;
                LayoutPanel(firstPlayer, left, left + half);
                LayoutPanel(secondPlayer, right - half, right);
                SetVisible(firstPlayer, true);
                SetVisible(secondPlayer, true);
            }
        }

        private int ResolvePlayerMask()
        {
            if (layoutMode == PlayerHudLayoutMode.OneVOne) return 1;
            if (layoutMode == PlayerHudLayoutMode.TwoVTwo) return 3;

            ResolveBoundPlayer(firstPlayer);
            ResolveBoundPlayer(secondPlayer);
            if ((firstPlayer.player == null || secondPlayer.player == null) && Time.unscaledTime >= _nextPlayerSearch)
            {
                _nextPlayerSearch = Time.unscaledTime + 0.5f;
                // Include disabled roster members; temporary gameplay suppression must not resize the HUD.
                var players = FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    if (player.IsPseudoPlayer || player.gameObject.scene != gameObject.scene) continue;
                    if (player.playerID == firstPlayer.ID) firstPlayer.player = player;
                    if (player.playerID == secondPlayer.ID) secondPlayer.player = player;
                }
            }
            int mask = IsRosterMember(firstPlayer.player) ? 1 : 0;
            if (IsRosterMember(secondPlayer.player)) mask |= 2;
            if (mask != 0) return mask;
            // Keep the last layout while a whole rig is hidden or rebuilt.
            if (_lastMask >= 0) return _lastMask;
            return GameFlowContext.Instance != null && !GameFlowContext.Instance.IsTwoVTwo ? 1 : 3;
        }

        private static void ResolveBoundPlayer(PlayerPanel panel)
        {
            var candidate = panel.multiplier.Chain != null ? panel.multiplier.Chain.Player : panel.health.Player;
            if (candidate != null && !candidate.IsPseudoPlayer && candidate.playerID == panel.ID)
                panel.player = candidate;
        }

        private static bool IsRosterMember(PlayerControllerScript player)
        {
            // Death, disabled input, and hidden child rigs do not change participation.
            return player != null && player.gameObject.activeSelf;
        }

        private static void SetVisible(PlayerPanel panel, bool visible)
        {
            if (panel.health.gameObject.activeSelf != visible) panel.health.gameObject.SetActive(visible);
            if (panel.multiplier.gameObject.activeSelf != visible) panel.multiplier.gameObject.SetActive(visible);
        }

        private void LayoutPanel(PlayerPanel panel, float left, float right)
        {
            LayoutRow(panel.health.transform, panel.healthLabel, left, right, panel.health.SetBarWidth);
            LayoutRow(panel.multiplier.transform, panel.multiplierLabel, left, right, panel.multiplier.SetBarWidth);
        }

        private void LayoutRow(Transform row, TMP_Text label, float left, float right, Action<float> setWidth)
        {
            if (label == null) return;
            // Align text boxes rather than animated glyph bounds, so score changes never move the bars.
            Vector3 labelLeft = label.rectTransform.TransformPoint(new Vector3(label.rectTransform.rect.xMin + label.margin.x, 0f, 0f));
            float shift = left - transform.InverseTransformPoint(labelLeft).x;
            Vector3 position = row.localPosition;
            position.x += shift;
            row.localPosition = position;
            // Both HUD rows retain their authored type size, bar height, and number badge.
            var frame = row.GetComponentInChildren<Shapes.Rectangle>(true);
            if (frame == null) return;
            float barLeft = transform.InverseTransformPoint(frame.transform.position).x;
            float localUnits = transform.InverseTransformVector(frame.transform.TransformVector(Vector3.right)).x;
            if (localUnits > 0f) setWidth(Mathf.Max(0.01f, (right - barLeft) / localUnits));
        }
    }
}
