using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Massive.Scoring
{
    /// <summary>Scene-owned capture announcements and capped-goal rejection notices.</summary>
    [DisallowMultipleComponent]
    public sealed partial class TeamAmplifierToastPresenter : MonoBehaviour
    {
        [Header("HUD references")]
        [Tooltip("Optional existing HUD canvas. If empty, this presenter owns a dedicated overlay canvas.")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset font;

        [Header("Placement and type")]
        [Tooltip("Screen position: X 0 = left, 1 = right; Y 0 = bottom, 1 = top.")]
        [SerializeField] private Vector2 lightAnchor = new Vector2(.18f, .63f);
        [Tooltip("Screen position: X 0 = left, 1 = right; Y 0 = bottom, 1 = top.")]
        [SerializeField] private Vector2 darkAnchor = new Vector2(.82f, .63f);
        [SerializeField, Min(.1f)] private float overallScale = 1f;
        [SerializeField, Min(8f)] private float teamLabelSize = 24f;
        [SerializeField, Min(12f)] private float multiplierSize = 88f;
        [SerializeField] private string lightLabel = "LIGHT TEAM AMPLIFIED";
        [SerializeField] private string darkLabel = "DARK TEAM AMPLIFIED";

        [Header("Timing")]
        [SerializeField, Min(.05f)] private float lifetime = 2.2f;
        [SerializeField, Min(0f)] private float fadeInSeconds = .12f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = .45f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Max amplification notice")]
        [SerializeField] private bool showMaxAmplification = true;
        [SerializeField] private string maxAmplificationLabel = "MAX AMPLIFICATION";
        [SerializeField, Min(8f)] private float maxNoticeFontSize = 28f;
        [SerializeField, Min(.1f)] private float maxNoticeScale = 1f;
        [Tooltip("Offset from each team's anchor in reference-resolution pixels. Negative Y places it below the capture toast.")]
        [SerializeField] private Vector2 maxNoticeOffset = new Vector2(0f, -85f);
        [SerializeField, Min(.05f)] private float maxNoticeLifetime = 1.35f;
        [Tooltip("Minimum time between notices for the same team, even if several Cores arrive together.")]
        [SerializeField, Min(0f)] private float maxNoticeRepeatDelay = 1.75f;

        [Header("Tier effects — ×2 always stays plain white")]
        [SerializeField] private bool rainbowColors = true;
        [SerializeField, Min(0f)] private float rainbowSpeed = .32f;
        [SerializeField] private bool vibration = true;
        [SerializeField, Min(0f)] private float vibrationPixels = 3f;
        [SerializeField, Min(0f)] private float vibrationFrequency = 24f;
        [SerializeField, Range(0f, .5f)] private float scalePunch = .16f;
        [SerializeField] private bool ghostTrails = true;
        [SerializeField, Range(0f, 1f)] private float ghostOpacity = .34f;
        [SerializeField, Min(.02f)] private float ghostLifetime = .3f;
        [SerializeField, Min(0f)] private float ghostTravelPixels = 34f;
        [SerializeField, Range(1f, 30f)] private float ghostsPerSecond = 16f;

        private const int GhostCapacity = 6;
        private readonly Card[] _cards = new Card[2];
        private readonly Notice[] _notices = new Notice[2];
        private readonly float[] _nextNoticeTime = new float[2];
        private MatchScoreService _service;
        private Coroutine _bindRoutine;
        private RectTransform _generatedRoot;
        private Canvas _ownedCanvas;
        private uint _randomState = 2718281;

        private sealed class Ghost
        {
            public TextMeshProUGUI text;
            public Vector2 start, travel;
            public float age;
            public bool active;
        }

        private sealed class Card
        {
            public int teamIndex;
            public RectTransform root;
            public CanvasGroup group;
            public TextMeshProUGUI header, amount;
            public readonly Ghost[] ghosts = new Ghost[GhostCapacity];
            public float age, strength, nextGhost;
            public int nextGhostIndex;
            public bool active;
        }

        private sealed class Notice
        {
            public RectTransform root;
            public CanvasGroup group;
            public TextMeshProUGUI text;
            public float age;
            public bool active;
        }

        public int ActiveToastCount => (_cards[0] != null && _cards[0].active ? 1 : 0) +
                                       (_cards[1] != null && _cards[1].active ? 1 : 0);
        public int ActiveMaxNoticeCount => (_notices[0] != null && _notices[0].active ? 1 : 0) +
                                           (_notices[1] != null && _notices[1].active ? 1 : 0);

        /// <summary>Assign an optional HUD canvas and typeface; null canvas creates an owned overlay.</summary>
        public void Configure(Canvas canvas, TMP_FontAsset typeface)
        {
            if (targetCanvas != canvas || Effective_font != typeface)
                ReleaseViews();
            targetCanvas = canvas;
            font = typeface;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                _bindRoutine = StartCoroutine(BindWhenAvailable());
        }

        private IEnumerator BindWhenAvailable()
        {
            while (isActiveAndEnabled && _service == null)
            {
                _service = MatchScoreService.Instance;
                if (_service == null)
                    yield return null;
            }
            _bindRoutine = null;
            if (_service == null || !isActiveAndEnabled) yield break;
            _service.TeamAmplifierChanged += OnTeamAmplifierChanged;
            _service.TeamAmplifierCaptureRejected += OnCaptureRejected;
            _service.ScoresReset += HideAll;
            // Binding reads no existing multiplier: entering a scene is not a capture.
        }

        private void OnDisable()
        {
            if (_bindRoutine != null) StopCoroutine(_bindRoutine);
            _bindRoutine = null;
            if (_service != null)
            {
                _service.TeamAmplifierChanged -= OnTeamAmplifierChanged;
                _service.TeamAmplifierCaptureRejected -= OnCaptureRejected;
                _service.ScoresReset -= HideAll;
            }
            _service = null;
            ReleaseViews();
        }

        private void OnDestroy() { ReleaseViews(); }

        private void OnTeamAmplifierChanged(TeamAmplifierSnapshot snapshot)
        {
            // Reset snapshots and duplicate/max-tier notifications must never make announcements.
            if (snapshot.currentTierIndex == 0 && snapshot.teamID >= 1 && snapshot.teamID <= 2)
            {
                Card previous = _cards[snapshot.teamID - 1];
                if (previous != null)
                {
                    previous.active = false;
                    previous.root.gameObject.SetActive(false);
                }
                HideNotice(snapshot.teamID - 1);
                _nextNoticeTime[snapshot.teamID - 1] = 0f;
            }
            if (!IsCaptureIncrease(snapshot)) return;
            int maximumTier = _service != null && _service.Profile != null
                ? _service.Profile.TeamAmplifierSettings.MaxIndex
                : snapshot.currentTierIndex;
            ShowCapture(snapshot, maximumTier);
        }

        public static bool IsCaptureIncrease(TeamAmplifierSnapshot snapshot)
        {
            return (snapshot.teamID == 1 || snapshot.teamID == 2) &&
                   snapshot.currentTierIndex > snapshot.previousTierIndex &&
                   snapshot.currentMultiplier > snapshot.previousMultiplier &&
                   snapshot.currentMultiplier > 1;
        }

        public static float GetEffectStrength(int multiplier, int tierIndex, int maximumTierIndex)
        {
            if (multiplier <= 2) return 0f;
            // Normalize using the authored tier list, not an assumed ×8 cap.
            return maximumTierIndex <= 1 ? 1f : Mathf.InverseLerp(1f, maximumTierIndex, tierIndex);
        }

        /// <summary>Also permits a UI-only preview without changing score or emitting capture events.</summary>
        public bool ShowCapture(TeamAmplifierSnapshot snapshot, int maximumTierIndex)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !IsCaptureIncrease(snapshot) || !EnsureViews())
                return false;
            Card card = _cards[snapshot.teamID - 1];
            card.age = 0f;
            card.strength = GetEffectStrength(snapshot.currentMultiplier, snapshot.currentTierIndex, maximumTierIndex);
            card.nextGhost = .05f;
            card.nextGhostIndex = 0;
            card.active = true;
            card.root.gameObject.SetActive(true);
            card.root.localScale = Vector3.one * Effective_overallScale;
            card.header.text = snapshot.teamID == 1 ? Effective_lightLabel : Effective_darkLabel;
            card.header.fontSize = Effective_teamLabelSize;
            card.amount.fontSize = Effective_multiplierSize;
            card.amount.text = "×" + snapshot.currentMultiplier;
            card.amount.color = Color.white;
            card.amount.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            card.amount.ForceMeshUpdate();
            for (int i = 0; i < card.ghosts.Length; i++)
            {
                Ghost ghost = card.ghosts[i];
                ghost.active = false;
                ghost.text.gameObject.SetActive(false);
                ghost.text.fontSize = Effective_multiplierSize;
                ghost.text.text = card.amount.text;
            }
            Animate(card, 0f);
            return true;
        }

        public void HideAll()
        {
            for (int team = 0; team < 2; team++)
            {
                HideNotice(team);
                _nextNoticeTime[team] = 0f;
            }
            foreach (Card card in _cards)
            {
                if (card == null) continue;
                card.active = false;
                if (card.root != null) card.root.gameObject.SetActive(false);
                foreach (Ghost ghost in card.ghosts) ghost.active = false;
            }
        }

        private void OnCaptureRejected(int teamID) { ShowMaxAmplification(teamID); }

        /// <summary>UI-only notice; authoritative rejection events call this without changing score.</summary>
        public bool ShowMaxAmplification(int teamID)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !Effective_showMaxAmplification ||
                teamID < 1 || teamID > 2) return false;
            int team = teamID - 1;
            if (Time.unscaledTime < _nextNoticeTime[team] || !EnsureViews()) return false;
            var notice = _notices[team];
            notice.age = 0f;
            notice.active = true;
            notice.root.gameObject.SetActive(true);
            _nextNoticeTime[team] = Time.unscaledTime + Mathf.Max(Effective_maxNoticeRepeatDelay, Effective_maxNoticeLifetime);
            AnimateNotice(notice, team);
            return true;
        }

        private void HideNotice(int team)
        {
            var notice = _notices[team];
            if (notice == null) return;
            notice.active = false;
            if (notice.root != null) notice.root.gameObject.SetActive(false);
        }

        private void AnimateNotice(Notice notice, int team)
        {
            notice.root.anchorMin = notice.root.anchorMax = team == 0 ? Effective_lightAnchor : Effective_darkAnchor;
            notice.root.anchoredPosition = Effective_maxNoticeOffset;
            notice.root.localScale = Vector3.one * Effective_maxNoticeScale;
            notice.text.fontSize = Effective_maxNoticeFontSize;
            notice.text.text = Effective_maxAmplificationLabel;
            float intro = Effective_fadeInSeconds > 0f ? Mathf.Clamp01(notice.age / Effective_fadeInSeconds) : 1f;
            float outro = Effective_fadeOutSeconds > 0f ? Mathf.Clamp01((Effective_maxNoticeLifetime - notice.age) / Effective_fadeOutSeconds) : 1f;
            notice.group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(intro, outro));
        }

        private bool EnsureViews()
        {
            if (_generatedRoot != null) return true;
            Canvas canvas = targetCanvas;
            if (canvas == null)
            {
                var canvasObject = new GameObject("Amplifier announcements canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObject.transform.SetParent(transform, false);
                _ownedCanvas = canvasObject.GetComponent<Canvas>();
                _ownedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _ownedCanvas.sortingOrder = 2000;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                canvas = _ownedCanvas;
            }
            _generatedRoot = Rect("Amplifier capture announcements", canvas.transform, Vector2.zero);
            _generatedRoot.anchorMin = Vector2.zero;
            _generatedRoot.anchorMax = Vector2.one;
            _generatedRoot.offsetMin = _generatedRoot.offsetMax = Vector2.zero;
            for (int team = 0; team < 2; team++)
            {
                Card card = new Card { teamIndex = team };
                card.root = Rect(team == 0 ? "Light team amplification" : "Dark team amplification", _generatedRoot, new Vector2(500f, 160f));
                card.root.anchorMin = card.root.anchorMax = team == 0 ? Effective_lightAnchor : Effective_darkAnchor;
                card.group = card.root.gameObject.AddComponent<CanvasGroup>();
                card.group.blocksRaycasts = false;
                card.group.interactable = false;
                // Ghosts stay behind the main number, and never duplicate the small team label.
                for (int i = 0; i < GhostCapacity; i++)
                    card.ghosts[i] = new Ghost { text = Text("Afterimage " + i, card.root, Effective_multiplierSize, new Vector2(0f, -14f)) };
                card.amount = Text("Multiplier", card.root, Effective_multiplierSize, new Vector2(0f, -14f));
                card.header = Text("Team label", card.root, Effective_teamLabelSize, new Vector2(0f, 55f));
                card.header.characterSpacing = 4f;
                card.header.rectTransform.sizeDelta = new Vector2(490f, 36f);
                _cards[team] = card;
                card.root.gameObject.SetActive(false);
                var notice = new Notice();
                notice.root = Rect(team == 0 ? "Light max amplification" : "Dark max amplification", _generatedRoot, new Vector2(500f, 60f));
                notice.group = notice.root.gameObject.AddComponent<CanvasGroup>();
                notice.group.blocksRaycasts = false;
                notice.group.interactable = false;
                notice.text = Text("MAX AMPLIFICATION", notice.root, Effective_maxNoticeFontSize, Vector2.zero);
                _notices[team] = notice;
                notice.root.gameObject.SetActive(false);
            }
            return true;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }

        private TextMeshProUGUI Text(string name, Transform parent, float size, Vector2 position)
        {
            var rect = Rect(name, parent, new Vector2(490f, 126f));
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Effective_font != null) text.font = Effective_font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private void LateUpdate()
        {
            float delta = Effective_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            for (int team = 0; team < 2; team++)
            {
                var notice = _notices[team];
                if (notice == null || !notice.active) continue;
                notice.age += delta;
                if (!Effective_showMaxAmplification || notice.age >= Effective_maxNoticeLifetime) HideNotice(team);
                else AnimateNotice(notice, team);
            }
            foreach (Card card in _cards)
            {
                if (card == null || !card.active) continue;
                card.age += delta;
                if (card.age >= Effective_lifetime)
                {
                    card.active = false;
                    card.root.gameObject.SetActive(false);
                    continue;
                }
                Animate(card, delta);
            }
        }

        private void Animate(Card card, float delta)
        {
            card.root.anchorMin = card.root.anchorMax = card.teamIndex == 0 ? Effective_lightAnchor : Effective_darkAnchor;
            card.header.fontSize = Effective_teamLabelSize;
            card.header.text = card.teamIndex == 0 ? Effective_lightLabel : Effective_darkLabel;
            card.amount.fontSize = Effective_multiplierSize;
            foreach (Ghost ghost in card.ghosts) ghost.text.fontSize = Effective_multiplierSize;
            float intro = Effective_fadeInSeconds > 0f ? Mathf.Clamp01(card.age / Effective_fadeInSeconds) : 1f;
            float outro = Effective_fadeOutSeconds > 0f ? Mathf.Clamp01((Effective_lifetime - card.age) / Effective_fadeOutSeconds) : 1f;
            card.group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(intro, outro));
            float punch = Mathf.Sin(Mathf.Clamp01(card.age / .32f) * Mathf.PI) * Effective_scalePunch * card.strength;
            card.root.localScale = Vector3.one * (Effective_overallScale * (1f + punch));
            Vector2 shake = Vector2.zero;
            if (Effective_vibration && card.strength > 0f)
            {
                float t = card.age * Effective_vibrationFrequency;
                shake = new Vector2(Mathf.PerlinNoise(t, 13.7f) - .5f,
                                    Mathf.PerlinNoise(41.3f, t * 1.173f) - .5f) * (Effective_vibrationPixels * 4f * card.strength);
            }
            card.amount.rectTransform.anchoredPosition = new Vector2(0f, -14f) + shake;
            PaintMultiplier(card);

            if (Effective_ghostTrails && card.strength > 0f && card.age >= card.nextGhost && outro >= .75f)
            {
                Ghost ghost = card.ghosts[card.nextGhostIndex];
                card.nextGhostIndex = (card.nextGhostIndex + 1) % card.ghosts.Length;
                ghost.active = true;
                ghost.age = 0f;
                ghost.start = new Vector2(0f, -14f) + shake;
                float direction = Random01() * Mathf.PI * 2f;
                ghost.travel = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * Effective_ghostTravelPixels * Mathf.Lerp(.55f, 1f, Random01()) * card.strength;
                ghost.text.gameObject.SetActive(true);
                ghost.text.enableVertexGradient = Effective_rainbowColors;
                ghost.text.colorGradient = new VertexGradient(
                    Rainbow(0f, card.age, card.strength), Rainbow(.33f, card.age, card.strength),
                    Rainbow(.66f, card.age, card.strength), Rainbow(1f, card.age, card.strength));
                card.nextGhost = card.age + 1f / Mathf.Max(1f, Effective_ghostsPerSecond * Mathf.Lerp(.4f, 1f, card.strength));
            }

            foreach (Ghost ghost in card.ghosts)
            {
                if (!ghost.active) continue;
                ghost.age += delta;
                float t = ghost.age / Mathf.Max(.02f, Effective_ghostLifetime);
                if (t >= 1f || !Effective_ghostTrails)
                {
                    ghost.active = false;
                    ghost.text.gameObject.SetActive(false);
                    continue;
                }
                ghost.text.rectTransform.anchoredPosition = ghost.start + ghost.travel * t;
                ghost.text.color = new Color(1f, 1f, 1f, Effective_ghostOpacity * card.strength * Mathf.Pow(1f - t, 2f));
            }
        }

        private void PaintMultiplier(Card card)
        {
            // Color the glyph geometry rather than a glow material: ×2 has exactly white glyphs.
            card.amount.ForceMeshUpdate();
            TMP_TextInfo info = card.amount.textInfo;
            Bounds bounds = card.amount.textBounds;
            float width = Mathf.Max(.01f, bounds.size.x);
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = character.vertexIndex + corner;
                    float x = (mesh.vertices[vertex].x - bounds.min.x) / width;
                    mesh.colors32[vertex] = Effective_rainbowColors && card.strength > 0f
                        ? Rainbow(x, card.age, card.strength) : Color.white;
                }
            }
            card.amount.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private Color Rainbow(float position, float age, float strength)
        {
            return Color.Lerp(Color.white, Color.HSVToRGB(Mathf.Repeat(position * .85f + age * Effective_rainbowSpeed, 1f), .88f, 1f), strength);
        }

        private float Random01()
        {
            // Local state keeps decorative randomness from changing gameplay spawn sequences.
            _randomState ^= _randomState << 13;
            _randomState ^= _randomState >> 17;
            _randomState ^= _randomState << 5;
            return (_randomState & 0x00ffffff) / 16777216f;
        }

        private void ReleaseViews()
        {
            HideAll();
            GameObject ownedObject = _ownedCanvas != null ? _ownedCanvas.gameObject :
                _generatedRoot != null ? _generatedRoot.gameObject : null;
            if (ownedObject != null)
            {
                if (Application.isPlaying) Destroy(ownedObject);
                else DestroyImmediate(ownedObject);
            }
            _generatedRoot = null;
            _ownedCanvas = null;
            _cards[0] = _cards[1] = null;
            _notices[0] = _notices[1] = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            lifetime = Mathf.Max(.05f, lifetime);
            overallScale = Mathf.Max(.1f, overallScale);
            ghostLifetime = Mathf.Max(.02f, ghostLifetime);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            maxNoticeFontSize = Mathf.Max(8f, maxNoticeFontSize);
            maxNoticeScale = Mathf.Max(.1f, maxNoticeScale);
            maxNoticeLifetime = Mathf.Max(.05f, maxNoticeLifetime);
            maxNoticeRepeatDelay = Mathf.Max(0f, maxNoticeRepeatDelay);
        }
#endif
    }
}
