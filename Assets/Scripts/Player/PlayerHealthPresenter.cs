using System.Globalization;
using Massive.Scoring;
using Shapes;
using TMPro;
using UnityEngine;

namespace Massive.Player
{
    /// <summary>Reads the same bounded mass/integrity that drives the player's nuggets.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthPresenter : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField, Min(0)] private int playerID;
        [SerializeField] private PlayerControllerScript player;
        [Tooltip("The adjacent multiplier view can supply its bound player. The player registry is a fallback.")]
        [SerializeField] private PlayerScoreChainPresenter playerSource;

        [Header("View")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Rectangle healthFill;
        [SerializeField] private Rectangle healthTrack;
        [SerializeField, Min(0.01f)] private float fillSmoothSeconds = 0.12f;

        [Header("Low Health Warning")]
        [SerializeField] private Rectangle healthFrame;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.15f;
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.08f, 0.08f, 1f);
        [Tooltip("Complete bright/dim flashes per second. Uses unscaled time.")]
        [SerializeField, Min(0.1f)] private float lowHealthBlinkFrequency = 1.5f;
        [SerializeField, Range(0f, 1f)] private float lowHealthDimBrightness = 0.18f;

        private float _displayedHealth;
        private float _fillVelocity;
        private bool _initialized;
        private int _lastPercentage = int.MinValue;
        private Color _normalFillColor, _normalFrameColor, _normalTextColor;
        private bool _capturedColors;
        private float _blinkStartedAt;

        public int PlayerID => playerID;
        public PlayerControllerScript Player => player;
        public float Health01 { get; private set; }
        public float DisplayedHealth01 => _displayedHealth;
        public bool IsLowHealth { get; private set; }

        public void SetBarWidth(float width)
        {
            if (healthTrack == null || healthFill == null) return;
            float fraction = healthTrack.Width > 0f ? healthFill.Width / healthTrack.Width : 0f;
            healthTrack.Width = Mathf.Max(0f, width);
            Rectangle frame = healthFrame != null ? healthFrame : healthTrack.transform.parent.GetComponent<Rectangle>();
            if (frame != null) frame.Width = healthTrack.Width;
            healthFill.Width = healthTrack.Width * Mathf.Clamp01(fraction);
        }

        private void OnEnable()
        {
            CaptureNormalColors();
            _initialized = false;
            _lastPercentage = int.MinValue;
            Refresh();
        }

        private void OnDisable()
        {
            RestoreNormalColors();
        }

        private void LateUpdate()
        {
            // Integrity is also changed directly by legacy helpers and authoring tools.
            // Four scalar reads keep healing, hits, custom bounds, and respawns in sync
            // without changing those gameplay paths or allocating every frame.
            Refresh();
        }

        private void Refresh()
        {
            ResolvePlayer();
            Health01 = ReadHealth01(player);
            int percentage = player != null ? DisplayPercentage(Health01) : -1;
            if (healthText != null && percentage != _lastPercentage)
            {
                healthText.text = percentage < 0 ? "--" : percentage.ToString(CultureInfo.InvariantCulture) + "%";
                _lastPercentage = percentage;
            }

            // Death is immediately empty; an entering or re-enabled HUD is authoritative.
            if (!_initialized || Health01 <= 0f)
            {
                _displayedHealth = Health01;
                _fillVelocity = 0f;
                _initialized = true;
            }
            else
            {
                _displayedHealth = Mathf.SmoothDamp(_displayedHealth, Health01, ref _fillVelocity,
                    Mathf.Max(0.01f, fillSmoothSeconds), Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(_displayedHealth - Health01) < 0.0005f)
                {
                    _displayedHealth = Health01;
                    _fillVelocity = 0f;
                }
            }

            if (healthFill != null && healthTrack != null)
            {
                float width = Mathf.Max(0f, healthTrack.Width) * Mathf.Clamp01(_displayedHealth);
                if (!Mathf.Approximately(healthFill.Width, width)) healthFill.Width = width;
                // No fixed white number badge: zero health means zero white fill.
                healthFill.enabled = width > 0f;
            }
            UpdateLowHealthWarning();
        }

        private void CaptureNormalColors()
        {
            if (_capturedColors) return;
            if (healthFrame == null && healthTrack != null && healthTrack.transform.parent != null)
                healthFrame = healthTrack.transform.parent.GetComponent<Rectangle>();
            _normalFillColor = healthFill != null ? healthFill.Color : Color.white;
            _normalFrameColor = healthFrame != null ? healthFrame.Color : Color.white;
            _normalTextColor = healthText != null ? healthText.color : Color.black;
            _capturedColors = true;
        }

        private void UpdateLowHealthWarning()
        {
            // Use actual health, so the warning never waits for the smoothed fill.
            bool low = player != null && Health01 > 0f && Health01 < lowHealthThreshold;
            if (!low)
            {
                if (IsLowHealth) RestoreNormalColors();
                return;
            }

            if (!IsLowHealth) _blinkStartedAt = Time.unscaledTime;
            IsLowHealth = true;
            float phase = Mathf.Repeat((Time.unscaledTime - _blinkStartedAt) *
                Mathf.Max(0.1f, lowHealthBlinkFrequency), 1f);
            float brightness = phase < 0.5f ? 1f : lowHealthDimBrightness;
            Color color = new Color(lowHealthColor.r * brightness, lowHealthColor.g * brightness,
                lowHealthColor.b * brightness, lowHealthColor.a);
            if (healthFill != null) healthFill.Color = color;
            if (healthFrame != null) healthFrame.Color = color;
            if (healthText != null) healthText.color = color;
        }

        private void RestoreNormalColors()
        {
            IsLowHealth = false;
            if (!_capturedColors) return;
            if (healthFill != null) healthFill.Color = _normalFillColor;
            if (healthFrame != null) healthFrame.Color = _normalFrameColor;
            if (healthText != null) healthText.color = _normalTextColor;
        }

        private void ResolvePlayer()
        {
            PlayerControllerScript sourcePlayer = playerSource != null && playerSource.Chain != null
                ? playerSource.Chain.Player : null;
            if (IsUsable(sourcePlayer)) player = sourcePlayer;
            if (IsUsable(player)) return;
            player = null;
            var players = PlayerControllerScript.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (!IsUsable(players[i])) continue;
                player = players[i];
                break;
            }
        }

        private bool IsUsable(PlayerControllerScript candidate)
        {
            return candidate != null && candidate.playerID == playerID && !candidate.IsPseudoPlayer &&
                   candidate.gameObject.activeInHierarchy;
        }

        public static float ReadHealth01(PlayerControllerScript source)
        {
            if (source == null || source.temporarilyEliminated || source.massScoreMax <= source.massScoreMin ||
                float.IsNaN(source.massScore) || float.IsInfinity(source.massScore)) return 0f;
            return Mathf.InverseLerp(source.massScoreMin, source.massScoreMax, source.massScore);
        }

        public static int DisplayPercentage(float health01)
        {
            if (health01 <= 0f) return 0;
            if (health01 >= 1f) return 100;
            // Reserve 0 and 100 for actual empty/full states, including tiny fractions.
            return Mathf.Clamp(Mathf.RoundToInt(health01 * 100f), 1, 99);
        }
    }
}
