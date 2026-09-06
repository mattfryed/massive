using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Massive.TextAnimation
{
    /// <summary>
    /// Preset-driven animator for one TMP target. It animates generated glyph
    /// geometry, vertex colors, a dedicated motion root, and transient SDF
    /// offsets without mutating a shared font material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TMPTextAnimator : MonoBehaviour
    {
        [Serializable]
        public sealed class MarkerUnityEvent : UnityEvent<string>
        {
        }

        private sealed class PlaybackRequest
        {
            public int id;
            public TextAnimationPreset preset;
            public TextAnimationContext context;
            public bool queued;
            public bool completeRequested;
            public bool cancelled;
            public float lastMarkerProgress = -0.000001f;
            public readonly HashSet<int> firedMarkerIndices = new HashSet<int>();
        }

        private struct CharacterState
        {
            public bool visible;
            public char character;
            public int visibleOrdinal;
            public int lineNumber;
            public int lineVisibleOrdinal;
            public int lineVisibleCount;
            public int orderRank;
            public bool selected;
            public bool changed;
            public bool hasPreviousCharacter;
        }

        private sealed class EchoRendererState
        {
            public GameObject root;
            public TMP_Text text;
        }

        private sealed class PreviousTextRendererState
        {
            public GameObject root;
            public TMP_Text text;
            public TMP_MeshInfo[] baselineMeshInfo;
            public int[] characterIndicesByVisibleOrdinal = Array.Empty<int>();
        }

        private sealed class SdfMorphRendererState
        {
            public GameObject root;
            public RawImage image;
            public Mesh mesh;
            public MeshRenderer meshRenderer;
            public Material material;
        }

        [Header("Target")]
        [SerializeField] private TMP_Text target;

        [Tooltip("Optional dedicated wrapper for root motion. Leave empty to animate the TMP Transform itself.")]
        [SerializeField] private Transform motionRoot;

        [Tooltip("Owns the runtime SDF material and composes persistent style with transient preset offsets.")]
        [SerializeField] private TMPMaterialStateController materialState;

        [Tooltip("Keeps visible glyph centers in permanent slots when the text changes. Intended for counters and score displays.")]
        [SerializeField] private bool lockVisibleCharacterSlots;

        [Header("Auto Play")]
        [SerializeField] private TextAnimationPreset playOnEnablePreset;
        [SerializeField] private bool hideBeforeAutoPlay = true;
        [SerializeField] private bool autoPlayOnlyOnce;

        [Header("Events")]
        public UnityEvent onPlayStarted;
        public UnityEvent onPlayCompleted;
        public UnityEvent onPlayCancelled;
        public MarkerUnityEvent onMarker;

        private readonly Queue<PlaybackRequest> _queue = new Queue<PlaybackRequest>();
        private readonly Dictionary<int, PlaybackRequest> _requests =
            new Dictionary<int, PlaybackRequest>();

        private PlaybackRequest _current;
        private Coroutine _routine;
        private int _nextRequestId = 1;
        private bool _hasAutoPlayed;

        private TMP_MeshInfo[] _baselineMeshInfo;
        private CharacterState[] _characterStates = Array.Empty<CharacterState>();
        private char[] _lastRenderedCharacters = Array.Empty<char>();
        private string _capturedText = string.Empty;
        private int _selectedCount;

        private Transform _capturedMotionRoot;
        private Vector3 _baseRootLocalPosition;
        private Quaternion _baseRootLocalRotation;
        private Vector3 _baseRootLocalScale;

        private TMP_Text _rendererDefaultsTarget;
        private CanvasRenderer _canvasRenderer;
        private Renderer _renderer;
        private bool _baseCanvasCull;
        private bool _baseRendererEnabled = true;
        private bool _rendererDefaultsCaptured;
        private bool _hardVisible = true;

        private readonly List<EchoRendererState> _echoRenderers =
            new List<EchoRendererState>();
        private TMP_Text _echoPoolTarget;
        private PreviousTextRendererState _previousTextRenderer;
        private TMP_Text _previousTextRendererTarget;
        private readonly List<SdfMorphRendererState> _sdfMorphRenderers =
            new List<SdfMorphRendererState>();
        private TMP_Text _sdfMorphRendererTarget;
        private Vector3[] _fixedVisibleSlotCenters = Array.Empty<Vector3>();
        private readonly HashSet<string> _reportedSdfMorphFailures =
            new HashSet<string>();

        public TMP_Text Target => target;
        public Transform MotionRoot => motionRoot != null ? motionRoot : transform;
        public TMPMaterialStateController MaterialState => materialState;
        public bool IsPlaying => _current != null;
        public TextAnimationPreset CurrentPreset => _current != null ? _current.preset : null;
        public int SelectedCharacterCount => _selectedCount;

        private void Reset()
        {
            target = GetComponent<TMP_Text>();
            motionRoot = transform;
            materialState = GetComponent<TMPMaterialStateController>();
        }

        private void Awake()
        {
            EnsureReferences();
            CaptureRendererDefaults(force: true);

            bool willAutoPlay = playOnEnablePreset != null &&
                                (!autoPlayOnlyOnce || !_hasAutoPlayed);
            if (willAutoPlay && hideBeforeAutoPlay)
                SetHardVisible(false);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureReferences();
            CaptureRendererDefaults(force: false);

            bool willAutoPlay = playOnEnablePreset != null &&
                                (!autoPlayOnlyOnce || !_hasAutoPlayed);
            if (!willAutoPlay)
                return;

            _hasAutoPlayed = true;
            if (hideBeforeAutoPlay)
                SetHardVisible(false);

            Play(playOnEnablePreset, TextAnimationContext.Default);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            StopAll(restoreBaseline: true, restoreVisibility: true);
        }

        private void OnDestroy()
        {
            DestroyEchoRenderers();
            DestroyPreviousTextRenderer();
            DestroySdfMorphRenderers();
        }

        public void Configure(TMP_Text newTarget, Transform newMotionRoot = null)
        {
            if (newTarget != null && newTarget != target)
            {
                StopAll(restoreBaseline: true, restoreVisibility: true);
                DestroyEchoRenderers();
                DestroyPreviousTextRenderer();
                DestroySdfMorphRenderers();
                target = newTarget;
                materialState = target.GetComponent<TMPMaterialStateController>();
                _rendererDefaultsCaptured = false;
                _baselineMeshInfo = null;
                _lastRenderedCharacters = Array.Empty<char>();
                _fixedVisibleSlotCenters = Array.Empty<Vector3>();
            }

            if (newMotionRoot != null)
                motionRoot = newMotionRoot;
            else if (motionRoot == null && target != null)
                motionRoot = target.transform;

            EnsureReferences();
            CaptureRendererDefaults(force: false);
        }

        public void ConfigureFixedVisibleCharacterSlots(bool enabled)
        {
            lockVisibleCharacterSlots = enabled;
            if (!enabled)
            {
                _fixedVisibleSlotCenters = Array.Empty<Vector3>();
            }
        }

        public void SetMotionRoot(Transform newMotionRoot)
        {
            motionRoot = newMotionRoot != null
                ? newMotionRoot
                : (target != null ? target.transform : transform);
        }

        public TextAnimationPlaybackHandle Play(TextAnimationPreset preset)
        {
            return Play(preset, TextAnimationContext.Default);
        }

        public TextAnimationPlaybackHandle Play(
            TextAnimationPreset preset,
            TextAnimationContext context)
        {
            if (preset == null || !isActiveAndEnabled)
                return default;

            EnsureReferences();
            if (target == null)
                return default;

            context = context.Sanitized();

            PlaybackRequest request = new PlaybackRequest
            {
                id = NextRequestId(),
                preset = preset,
                context = context,
                queued = false,
                completeRequested = false,
                cancelled = false
            };
            _requests[request.id] = request;

            if (_current != null)
            {
                switch (preset.interruption)
                {
                    case TextAnimationInterruption.IgnoreWhilePlaying:
                        request.cancelled = true;
                        _requests.Remove(request.id);
                        return default;

                    case TextAnimationInterruption.Queue:
                        request.queued = true;
                        _queue.Enqueue(request);
                        return new TextAnimationPlaybackHandle(this, request.id);

                    case TextAnimationInterruption.CompleteAndReplace:
                        ClearQueuedRequests();
                        FinishCurrentImmediately(invokeCompletion: true, startNextQueued: false);
                        break;

                    default:
                        ClearQueuedRequests();
                        FinishCurrentAsCancelled(restoreBaseline: true, restoreVisibility: true);
                        break;
                }
            }

            Begin(request);
            return new TextAnimationPlaybackHandle(this, request.id);
        }

        public TextAnimationPlaybackHandle SetTextAndPlay(
            string newText,
            TextAnimationPreset preset)
        {
            return SetTextAndPlay(newText, preset, TextAnimationContext.Default);
        }

        public TextAnimationPlaybackHandle SetTextAndPlay(
            string newText,
            TextAnimationPreset preset,
            TextAnimationContext context)
        {
            EnsureReferences();
            if (target == null)
                return default;

            string oldText = target.text ?? string.Empty;
            target.text = newText ?? string.Empty;
            context.previousText = oldText;
            context.newText = target.text;
            return Play(preset, context);
        }

        public void StopAll(
            bool restoreBaseline = true,
            bool restoreVisibility = true)
        {
            FinishCurrentAsCancelled(restoreBaseline, restoreVisibility);
            ClearQueuedRequests();

            if (restoreBaseline && _current == null)
                RestoreRuntimeBaseline(restoreVisibility);
        }

        public void CompleteCurrent()
        {
            if (_current != null)
                Complete(_current.id);
        }

        /// <summary>
        /// Restores clean glyph geometry/root/material state. Hard visibility is
        /// optionally preserved so an OUT animation can remain hidden while text
        /// is swapped and recached.
        /// </summary>
        public void RestoreBaseline(bool restoreVisibility = true)
        {
            RestoreRuntimeBaseline(restoreVisibility);
        }

        /// <summary>
        /// Recaptures the current text geometry and persistent material style as
        /// the baseline for future presets. Does not unhide a deliberately hidden
        /// target.
        /// </summary>
        public void RefreshBaselineFromCurrent()
        {
            if (_current != null)
                return;

            EnsureReferences();
            if (target == null)
                return;

            RestoreRuntimeBaseline(restoreVisibility: false);
            materialState?.ClearTransient();
            CaptureBaseline(
                TextAnimationContext.Default,
                TextAnimationCharacterSelection.AllVisible,
                TextAnimationOrder.Forward,
                0);
            materialState?.CapturePersistentFromCurrentMaterial();
        }

        public void SetHardVisible(bool visible)
        {
            EnsureReferences();
            CaptureRendererDefaults(force: false);
            if (target == null)
                return;

            _hardVisible = visible;

            if (_canvasRenderer != null)
                _canvasRenderer.cull = visible ? _baseCanvasCull : true;

            if (_renderer != null)
                _renderer.enabled = visible ? _baseRendererEnabled : false;

            if (!visible)
            {
                HideEchoRenderers();
                HidePreviousTextRenderer();
                HideSdfMorphRenderers();
            }
        }

        internal bool IsPlaybackActive(int requestId)
        {
            return requestId > 0 && _requests.ContainsKey(requestId);
        }

        internal void Cancel(int requestId, bool restoreBaseline)
        {
            if (requestId <= 0)
                return;

            if (_current != null && _current.id == requestId)
            {
                FinishCurrentAsCancelled(
                    restoreBaseline,
                    restoreVisibility: restoreBaseline);
                StartNextQueued();
                return;
            }

            if (!_requests.TryGetValue(requestId, out PlaybackRequest request))
                return;

            request.cancelled = true;
            _requests.Remove(requestId);
        }

        internal void Complete(int requestId)
        {
            if (requestId <= 0)
                return;

            if (_current != null && _current.id == requestId)
            {
                FinishCurrentImmediately(
                    invokeCompletion: true,
                    startNextQueued: true);
                return;
            }

            if (_requests.TryGetValue(requestId, out PlaybackRequest request))
                request.completeRequested = true;
        }

        private int NextRequestId()
        {
            int id = _nextRequestId++;
            if (_nextRequestId <= 0)
                _nextRequestId = 1;
            return id;
        }

        private void Begin(PlaybackRequest request)
        {
            RestoreRuntimeBaseline(restoreVisibility: false);
            SetHardVisible(true);

            _current = request;
            request.queued = false;
            _reportedSdfMorphFailures.Clear();

            Coroutine started = StartCoroutine(PlayRoutine(request));
            if (_current == request)
                _routine = started;
        }

        private IEnumerator PlayRoutine(PlaybackRequest request)
        {
            TextAnimationPreset preset = request.preset;
            TextAnimationContext context = request.context;

            CaptureBaseline(
                context,
                preset.characterSelection,
                preset.characterOrder,
                preset.deterministicSeed + context.seedOffset);
            PreparePreviousTextRenderer(context, preset);
            CaptureMotionRootBaseline();

            // Apply t=0 immediately so reveal presets never flash fully visible.
            ApplyFrame(request, 0f);
            onPlayStarted?.Invoke();

            float delay = Mathf.Max(0f, preset.delaySeconds);
            float delayElapsed = 0f;
            while (delayElapsed < delay &&
                   _current == request &&
                   !request.completeRequested &&
                   !request.cancelled)
            {
                delayElapsed += DeltaTime(preset.timeMode);
                ApplyFrame(request, 0f);
                yield return null;
            }

            float total = GetCurrentTotalSeconds(preset);
            bool loop = preset.playbackMode == TextAnimationPlaybackMode.Loop;
            do
            {
                float elapsed = 0f;
                while (elapsed < total &&
                       _current == request &&
                       !request.completeRequested &&
                       !request.cancelled)
                {
                    elapsed += DeltaTime(preset.timeMode);
                    ApplyFrame(request, Mathf.Min(elapsed, total));
                    yield return null;
                }

                if (request.cancelled || _current != request)
                    yield break;

                ApplyFrame(request, total);

                if (!loop || request.completeRequested)
                    break;

                // A loop is one continuous playback request, but timeline
                // markers belong to a cycle and may fire again next time.
                request.lastMarkerProgress = -0.000001f;
                request.firedMarkerIndices.Clear();
                ApplyFrame(request, 0f);
                yield return null;
            }
            while (_current == request &&
                   !request.completeRequested &&
                   !request.cancelled);

            // Marker listeners are allowed to interrupt or replace playback.
            // Never let the old coroutine finalize over a newly-started request.
            if (request.cancelled || _current != request)
                yield break;

            ApplyCompletionMode(preset.completion);

            int finishedId = request.id;
            _requests.Remove(finishedId);
            _current = null;
            _routine = null;

            // Clear ownership before invoking callbacks so a listener can safely
            // start another preset without that new request being overwritten.
            onPlayCompleted?.Invoke();
            StartNextQueued();
        }

        private void StartNextQueued()
        {
            while (_current == null && _queue.Count > 0)
            {
                PlaybackRequest next = _queue.Dequeue();
                if (next == null || next.cancelled || !_requests.ContainsKey(next.id))
                    continue;

                Begin(next);
                return;
            }
        }

        private void ClearQueuedRequests()
        {
            while (_queue.Count > 0)
            {
                PlaybackRequest queued = _queue.Dequeue();
                if (queued == null)
                    continue;

                queued.cancelled = true;
                _requests.Remove(queued.id);
            }
        }

        private void FinishCurrentAsCancelled(
            bool restoreBaseline,
            bool restoreVisibility)
        {
            if (_current == null)
            {
                if (restoreBaseline)
                    RestoreRuntimeBaseline(restoreVisibility);
                return;
            }

            PlaybackRequest current = _current;
            current.cancelled = true;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _requests.Remove(current.id);
            _current = null;

            if (restoreBaseline)
                RestoreRuntimeBaseline(restoreVisibility);

            onPlayCancelled?.Invoke();
        }

        private void FinishCurrentImmediately(
            bool invokeCompletion,
            bool startNextQueued)
        {
            if (_current == null)
                return;

            PlaybackRequest current = _current;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            float total = GetCurrentTotalSeconds(current.preset);
            ApplyFrame(current, total);
            ApplyCompletionMode(current.preset.completion);

            _requests.Remove(current.id);
            _current = null;

            if (invokeCompletion)
                onPlayCompleted?.Invoke();

            if (startNextQueued)
                StartNextQueued();
        }

        private float GetCurrentTotalSeconds(TextAnimationPreset preset)
        {
            if (preset == null)
                return 0.000001f;

            float duration = Mathf.Max(0f, preset.durationSeconds);
            float staggerTail = Mathf.Max(0, _selectedCount - 1) *
                                Mathf.Max(0f, preset.characterStaggerSeconds);
            return Mathf.Max(0.000001f, duration + staggerTail);
        }

        private void ApplyCompletionMode(TextAnimationCompletionMode completion)
        {
            switch (completion)
            {
                case TextAnimationCompletionMode.HoldFinalFrame:
                    break;

                case TextAnimationCompletionMode.HideTarget:
                    RestoreRuntimeBaseline(restoreVisibility: false);
                    SetHardVisible(false);
                    break;

                default:
                    RestoreRuntimeBaseline(restoreVisibility: true);
                    break;
            }
        }

        private void ApplyFrame(PlaybackRequest request, float elapsed)
        {
            if (target == null || request == null || request.preset == null)
                return;

            if (NeedsBaselineRecapture())
            {
                CaptureBaseline(
                    request.context,
                    request.preset.characterSelection,
                    request.preset.characterOrder,
                    request.preset.deterministicSeed + request.context.seedOffset);
                PreparePreviousTextRenderer(request.context, request.preset);
                CaptureMotionRootBaseline();
            }

            RestoreMeshFromBaseline();
            RestorePreviousTextMeshAndHideGlyphs();
            HideSdfMorphRenderers();

            TextAnimationPreset preset = request.preset;
            float duration = Mathf.Max(0.000001f, preset.durationSeconds);
            float total = GetCurrentTotalSeconds(preset);
            float globalProgress = Mathf.Clamp01(elapsed / total);

            TMP_TextInfo textInfo = target.textInfo;
            List<TextAnimationModule> modules = preset.Modules;
            bool renderedPreviousGlyph = false;

            for (int i = 0; i < _characterStates.Length && i < textInfo.characterCount; i++)
            {
                CharacterState character = _characterStates[i];
                if (!character.visible || !character.selected)
                    continue;

                float localElapsed = elapsed -
                                     character.orderRank * Mathf.Max(0f, preset.characterStaggerSeconds);
                float characterProgress = Mathf.Clamp01(localElapsed / duration);

                TextAnimationEvaluationContext evaluation =
                    new TextAnimationEvaluationContext(
                        request.context,
                        globalProgress,
                        characterProgress,
                        elapsed,
                        total,
                        i,
                        character.visibleOrdinal,
                        character.orderRank,
                        _selectedCount,
                        character.lineNumber,
                        character.lineVisibleOrdinal,
                        character.lineVisibleCount,
                        character.character,
                        preset.deterministicSeed + request.context.seedOffset);

                TextAnimationGlyphState glyphState = TextAnimationGlyphState.Identity;
                if (modules != null)
                {
                    for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
                    {
                        TextAnimationModule module = modules[moduleIndex];
                        if (module != null && module.enabled)
                            module.ApplyGlyph(ref glyphState, in evaluation);
                    }
                }

                bool renderedSdfMorph = false;
                if (character.changed && character.hasPreviousCharacter)
                {
                    TextAnimationSdfMorphState sdfMorphState =
                        TextAnimationSdfMorphState.Identity;
                    if (modules != null)
                    {
                        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
                        {
                            TextAnimationModule module = modules[moduleIndex];
                            if (module != null && module.enabled)
                            {
                                module.ApplySdfGlyphMorph(
                                    ref sdfMorphState,
                                    in evaluation);
                            }
                        }
                    }

                    if (sdfMorphState.enabled &&
                        TryApplySdfGlyphMorph(
                            character.visibleOrdinal,
                            i,
                            in sdfMorphState))
                    {
                        glyphState.alphaMultiplier = 0f;
                        renderedSdfMorph = true;
                    }
                }

                ApplyGlyphState(textInfo, i, in character, in glyphState);

                if (!renderedSdfMorph &&
                    _previousTextRenderer != null &&
                    character.changed &&
                    character.hasPreviousCharacter)
                {
                    TextAnimationPreviousGlyphState previousState =
                        TextAnimationPreviousGlyphState.Identity;

                    if (modules != null)
                    {
                        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
                        {
                            TextAnimationModule module = modules[moduleIndex];
                            if (module != null && module.enabled)
                            {
                                module.ApplyPreviousGlyph(
                                    ref previousState,
                                    in evaluation);
                            }
                        }
                    }

                    if (previousState.visible &&
                        TryApplyPreviousGlyphState(
                            character.visibleOrdinal,
                            in character,
                            in previousState.glyph))
                    {
                        renderedPreviousGlyph = true;
                    }
                }
            }

            target.UpdateVertexData(
                TMP_VertexDataUpdateFlags.Vertices |
                TMP_VertexDataUpdateFlags.Colors32);
            CommitPreviousTextMesh(renderedPreviousGlyph);

            TextAnimationEvaluationContext globalEvaluation =
                new TextAnimationEvaluationContext(
                    request.context,
                    globalProgress,
                    globalProgress,
                    elapsed,
                    total,
                    -1,
                    0,
                    0,
                    _selectedCount,
                    0,
                    0,
                    0,
                    '\0',
                    preset.deterministicSeed + request.context.seedOffset);

            TextAnimationRootState rootState = TextAnimationRootState.Identity;
            TMPMaterialTransientState materialTransient =
                TMPMaterialTransientState.Identity;
            TextAnimationEchoState echoState = TextAnimationEchoState.Identity;

            if (modules != null &&
                (_selectedCount > 0 || preset.playObjectModulesWhenNoCharacters))
            {
                for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
                {
                    TextAnimationModule module = modules[moduleIndex];
                    if (module == null || !module.enabled)
                        continue;

                    module.ApplyRoot(ref rootState, in globalEvaluation);
                    module.ApplyMaterial(ref materialTransient, in globalEvaluation);
                    module.ApplyEcho(ref echoState, in globalEvaluation);
                }
            }

            ApplyRootState(in rootState);
            materialState?.SetTransient(in materialTransient);
            ApplyEchoState(in echoState);
            FireMarkers(request, globalProgress);
        }

        private void FireMarkers(PlaybackRequest request, float globalProgress)
        {
            List<TextAnimationMarker> markers = request.preset.markers;
            if (markers == null || markers.Count == 0)
            {
                request.lastMarkerProgress = globalProgress;
                return;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                if (request.firedMarkerIndices.Contains(i))
                    continue;

                TextAnimationMarker marker = markers[i];
                float markerTime = Mathf.Clamp01(marker.normalizedTime);
                bool crossed = markerTime <= globalProgress + 0.000001f &&
                               markerTime >= request.lastMarkerProgress - 0.000001f;
                if (!crossed)
                    continue;

                request.firedMarkerIndices.Add(i);
                if (!string.IsNullOrWhiteSpace(marker.id))
                    onMarker?.Invoke(marker.id);
            }

            request.lastMarkerProgress = globalProgress;
        }

        private bool NeedsBaselineRecapture()
        {
            if (target == null || _baselineMeshInfo == null)
                return true;

            if (!string.Equals(_capturedText, target.text ?? string.Empty, StringComparison.Ordinal))
                return true;

            TMP_TextInfo textInfo = target.textInfo;
            if (textInfo == null || textInfo.meshInfo == null ||
                textInfo.meshInfo.Length != _baselineMeshInfo.Length)
                return true;

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                Vector3[] liveVertices = textInfo.meshInfo[i].vertices;
                Vector3[] baselineVertices = _baselineMeshInfo[i].vertices;
                if (liveVertices == null || baselineVertices == null ||
                    liveVertices.Length != baselineVertices.Length)
                    return true;
            }

            return false;
        }

        private void CaptureBaseline(
            TextAnimationContext context,
            TextAnimationCharacterSelection selection,
            TextAnimationOrder order,
            int seed)
        {
            EnsureReferences();
            if (target == null)
                return;

            materialState?.ClearTransient();
            target.ForceMeshUpdate();

            TMP_TextInfo textInfo = target.textInfo;
            if (lockVisibleCharacterSlots && _fixedVisibleSlotCenters.Length == 0)
                CaptureFixedVisibleSlotCenters();

            _baselineMeshInfo = textInfo.CopyMeshInfoVertexData();
            if (lockVisibleCharacterSlots)
            {
                ApplyFixedVisibleSlotLayout(textInfo, _baselineMeshInfo);
                target.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
            _capturedText = target.text ?? string.Empty;

            int count = textInfo.characterCount;
            _characterStates = new CharacterState[count];

            int lineCount = Mathf.Max(1, textInfo.lineCount);
            int[] lineVisibleCounts = new int[lineCount];
            int totalVisible = 0;

            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo info = textInfo.characterInfo[i];
                if (!info.isVisible)
                    continue;

                int line = Mathf.Clamp(info.lineNumber, 0, lineCount - 1);
                lineVisibleCounts[line]++;
                totalVisible++;
            }

            int[] lineOrdinals = new int[lineCount];
            char[] currentRendered = new char[totalVisible];
            int visibleOrdinal = 0;

            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo info = textInfo.characterInfo[i];
                if (!info.isVisible)
                {
                    _characterStates[i] = new CharacterState
                    {
                        visible = false,
                        orderRank = -1
                    };
                    continue;
                }

                int line = Mathf.Clamp(info.lineNumber, 0, lineCount - 1);
                _characterStates[i] = new CharacterState
                {
                    visible = true,
                    character = info.character,
                    visibleOrdinal = visibleOrdinal,
                    lineNumber = line,
                    lineVisibleOrdinal = lineOrdinals[line]++,
                    lineVisibleCount = lineVisibleCounts[line],
                    orderRank = -1,
                    selected = false
                };
                currentRendered[visibleOrdinal] = info.character;
                visibleOrdinal++;
            }

            char[] previous = context.previousText != null
                ? ExtractComparableCharacters(context.previousText)
                : _lastRenderedCharacters;

            List<int> selectedIndices = new List<int>();
            for (int i = 0; i < _characterStates.Length; i++)
            {
                CharacterState character = _characterStates[i];
                if (!character.visible)
                    continue;

                bool hasPreviousCharacter =
                    character.visibleOrdinal < previous.Length;
                bool changed = !hasPreviousCharacter ||
                               previous[character.visibleOrdinal] != character.character;

                character.changed = changed;
                character.hasPreviousCharacter = hasPreviousCharacter;
                _characterStates[i] = character;

                bool selected;
                switch (selection)
                {
                    case TextAnimationCharacterSelection.DigitsOnly:
                        selected = char.IsDigit(character.character);
                        break;

                    case TextAnimationCharacterSelection.LettersOnly:
                        selected = char.IsLetter(character.character);
                        break;

                    case TextAnimationCharacterSelection.ChangedCharacters:
                        selected = changed;
                        break;

                    case TextAnimationCharacterSelection.ChangedDigits:
                        selected = changed && char.IsDigit(character.character);
                        break;

                    default:
                        selected = true;
                        break;
                }

                if (selected)
                    selectedIndices.Add(i);
            }

            ApplyOrder(selectedIndices, order, seed, totalVisible);
            for (int rank = 0; rank < selectedIndices.Count; rank++)
            {
                int index = selectedIndices[rank];
                CharacterState character = _characterStates[index];
                character.orderRank = rank;
                character.selected = true;
                _characterStates[index] = character;
            }

            _selectedCount = selectedIndices.Count;
            _lastRenderedCharacters = currentRendered;
        }

        private void ApplyOrder(
            List<int> indices,
            TextAnimationOrder order,
            int seed,
            int totalVisible)
        {
            switch (order)
            {
                case TextAnimationOrder.Reverse:
                    indices.Sort((a, b) =>
                        _characterStates[b].visibleOrdinal.CompareTo(
                            _characterStates[a].visibleOrdinal));
                    break;

                case TextAnimationOrder.CenterOut:
                    indices.Sort((a, b) =>
                    {
                        float center = (Mathf.Max(1, totalVisible) - 1) * 0.5f;
                        float distanceA = Mathf.Abs(_characterStates[a].visibleOrdinal - center);
                        float distanceB = Mathf.Abs(_characterStates[b].visibleOrdinal - center);
                        int comparison = distanceA.CompareTo(distanceB);
                        return comparison != 0
                            ? comparison
                            : _characterStates[a].visibleOrdinal.CompareTo(
                                _characterStates[b].visibleOrdinal);
                    });
                    break;

                case TextAnimationOrder.EdgesIn:
                    indices.Sort((a, b) =>
                    {
                        float center = (Mathf.Max(1, totalVisible) - 1) * 0.5f;
                        float distanceA = Mathf.Abs(_characterStates[a].visibleOrdinal - center);
                        float distanceB = Mathf.Abs(_characterStates[b].visibleOrdinal - center);
                        int comparison = distanceB.CompareTo(distanceA);
                        return comparison != 0
                            ? comparison
                            : _characterStates[a].visibleOrdinal.CompareTo(
                                _characterStates[b].visibleOrdinal);
                    });
                    break;

                case TextAnimationOrder.Alternating:
                    indices.Sort((a, b) =>
                    {
                        int ordinalA = _characterStates[a].visibleOrdinal;
                        int ordinalB = _characterStates[b].visibleOrdinal;
                        int comparison = (ordinalA & 1).CompareTo(ordinalB & 1);
                        return comparison != 0 ? comparison : ordinalA.CompareTo(ordinalB);
                    });
                    break;

                case TextAnimationOrder.DeterministicRandom:
                    System.Random random = new System.Random(seed);
                    for (int i = indices.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        int temp = indices[i];
                        indices[i] = indices[j];
                        indices[j] = temp;
                    }
                    break;

                default:
                    indices.Sort((a, b) =>
                        _characterStates[a].visibleOrdinal.CompareTo(
                            _characterStates[b].visibleOrdinal));
                    break;
            }
        }

        private void RestoreMeshFromBaseline()
        {
            if (target == null || _baselineMeshInfo == null)
                return;

            TMP_TextInfo textInfo = target.textInfo;
            int meshCount = Mathf.Min(textInfo.meshInfo.Length, _baselineMeshInfo.Length);

            for (int i = 0; i < meshCount; i++)
            {
                TMP_MeshInfo current = textInfo.meshInfo[i];
                TMP_MeshInfo baseline = _baselineMeshInfo[i];

                if (current.vertices != null && baseline.vertices != null)
                {
                    Array.Copy(
                        baseline.vertices,
                        current.vertices,
                        Mathf.Min(baseline.vertices.Length, current.vertices.Length));
                }

                if (current.colors32 != null && baseline.colors32 != null)
                {
                    Array.Copy(
                        baseline.colors32,
                        current.colors32,
                        Mathf.Min(baseline.colors32.Length, current.colors32.Length));
                }
            }
        }

        private void ApplyGlyphState(
            TMP_TextInfo textInfo,
            int characterIndex,
            in CharacterState character,
            in TextAnimationGlyphState state)
        {
            ApplyGlyphStateToMesh(
                textInfo,
                _baselineMeshInfo,
                characterIndex,
                in character,
                in state);
        }

        private static bool ApplyGlyphStateToMesh(
            TMP_TextInfo textInfo,
            TMP_MeshInfo[] baselineMeshInfo,
            int characterIndex,
            in CharacterState character,
            in TextAnimationGlyphState state)
        {
            if (textInfo == null ||
                characterIndex < 0 ||
                characterIndex >= textInfo.characterCount)
            {
                return false;
            }

            TMP_CharacterInfo info = textInfo.characterInfo[characterIndex];
            int meshIndex = info.materialReferenceIndex;
            int vertexIndex = info.vertexIndex;

            if (baselineMeshInfo == null ||
                meshIndex < 0 ||
                meshIndex >= textInfo.meshInfo.Length ||
                meshIndex >= baselineMeshInfo.Length)
            {
                return false;
            }

            Vector3[] vertices = textInfo.meshInfo[meshIndex].vertices;
            Vector3[] original = baselineMeshInfo[meshIndex].vertices;
            Color32[] colors = textInfo.meshInfo[meshIndex].colors32;
            Color32[] originalColors = baselineMeshInfo[meshIndex].colors32;

            if (vertices == null || original == null ||
                vertexIndex < 0 || vertexIndex + 3 >= vertices.Length ||
                vertexIndex + 3 >= original.Length)
            {
                return false;
            }

            Vector3 center = (original[vertexIndex] + original[vertexIndex + 2]) * 0.5f;
            float lineCenter = (Mathf.Max(1, character.lineVisibleCount) - 1) * 0.5f;
            float trackingOffset =
                (character.lineVisibleOrdinal - lineCenter) * state.tracking;

            float radians = state.rotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);

            for (int vertex = 0; vertex < 4; vertex++)
            {
                int index = vertexIndex + vertex;
                Vector3 local = original[index] - center;
                local.x *= state.scale.x;
                local.y *= state.scale.y;
                local.x += state.shearX * local.y;

                float rotatedX = local.x * cosine - local.y * sine;
                float rotatedY = local.x * sine + local.y * cosine;
                local.x = rotatedX;
                local.y = rotatedY;

                vertices[index] = center + local + state.positionOffset +
                                  Vector3.right * trackingOffset;

                if (colors == null || originalColors == null ||
                    index >= colors.Length || index >= originalColors.Length)
                    continue;

                Color color = originalColors[index];
                color.r *= state.colorMultiplier.r;
                color.g *= state.colorMultiplier.g;
                color.b *= state.colorMultiplier.b;
                color.a *= state.colorMultiplier.a *
                           Mathf.Clamp01(state.alphaMultiplier);
                colors[index] = color;
            }

            return true;
        }

        private void CaptureMotionRootBaseline()
        {
            _capturedMotionRoot = MotionRoot;
            if (_capturedMotionRoot == null)
                return;

            _baseRootLocalPosition = _capturedMotionRoot.localPosition;
            _baseRootLocalRotation = _capturedMotionRoot.localRotation;
            _baseRootLocalScale = _capturedMotionRoot.localScale;
        }

        private void ApplyRootState(in TextAnimationRootState state)
        {
            if (_capturedMotionRoot == null)
                return;

            _capturedMotionRoot.localPosition =
                _baseRootLocalPosition + state.positionOffset;
            _capturedMotionRoot.localRotation =
                _baseRootLocalRotation * Quaternion.Euler(state.eulerOffset);
            _capturedMotionRoot.localScale =
                Vector3.Scale(_baseRootLocalScale, state.scaleMultiplier);
        }

        private void RestoreRuntimeBaseline(bool restoreVisibility)
        {
            if (target != null && _baselineMeshInfo != null)
            {
                if (!string.Equals(
                        _capturedText,
                        target.text ?? string.Empty,
                        StringComparison.Ordinal) ||
                    NeedsBaselineRecapture())
                {
                    target.ForceMeshUpdate();
                }
                else
                {
                    RestoreMeshFromBaseline();
                    target.UpdateVertexData(
                        TMP_VertexDataUpdateFlags.Vertices |
                        TMP_VertexDataUpdateFlags.Colors32);
                }
            }

            if (_capturedMotionRoot != null)
            {
                _capturedMotionRoot.localPosition = _baseRootLocalPosition;
                _capturedMotionRoot.localRotation = _baseRootLocalRotation;
                _capturedMotionRoot.localScale = _baseRootLocalScale;
            }

            materialState?.ClearTransient();
            HideEchoRenderers();
            HidePreviousTextRenderer();
            HideSdfMorphRenderers();

            if (restoreVisibility)
                SetHardVisible(true);
        }

        private void EnsureReferences()
        {
            if (target == null)
                target = GetComponent<TMP_Text>();

            if (motionRoot == null)
                motionRoot = target != null ? target.transform : transform;

            if (target != null &&
                (materialState == null ||
                 (materialState.Target != null && materialState.Target != target)))
            {
                materialState = target.GetComponent<TMPMaterialStateController>();
            }

            if (materialState == null && Application.isPlaying && target != null)
                materialState = target.gameObject.AddComponent<TMPMaterialStateController>();

            if (materialState != null && target != null)
                materialState.Configure(target);
        }

        private void PreparePreviousTextRenderer(
            TextAnimationContext context,
            TextAnimationPreset preset)
        {
            HidePreviousTextRenderer();

            if (target == null ||
                preset == null ||
                string.IsNullOrEmpty(context.previousText) ||
                string.Equals(
                    context.previousText,
                    context.newText,
                    StringComparison.Ordinal) ||
                !UsesPreviousGlyphLayer(preset))
            {
                return;
            }

            EnsurePreviousTextRenderer();
            if (_previousTextRenderer == null ||
                _previousTextRenderer.root == null ||
                _previousTextRenderer.text == null)
            {
                return;
            }

            TMP_Text previousText = _previousTextRenderer.text;
            SyncEchoAppearance(previousText);
            previousText.text = context.previousText;
            previousText.color = target.color;
            MatchEchoTransform(
                _previousTextRenderer.root.transform,
                Vector2.zero,
                1f);

            _previousTextRenderer.root.SetActive(true);
            previousText.ForceMeshUpdate();
            _previousTextRenderer.baselineMeshInfo =
                previousText.textInfo.CopyMeshInfoVertexData();
            if (lockVisibleCharacterSlots)
            {
                ApplyFixedVisibleSlotLayout(
                    previousText.textInfo,
                    _previousTextRenderer.baselineMeshInfo);
                previousText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }

            List<int> visibleCharacterIndices = new List<int>();
            for (int i = 0; i < previousText.textInfo.characterCount; i++)
            {
                if (previousText.textInfo.characterInfo[i].isVisible)
                    visibleCharacterIndices.Add(i);
            }

            _previousTextRenderer.characterIndicesByVisibleOrdinal =
                visibleCharacterIndices.ToArray();
        }

        private static bool UsesPreviousGlyphLayer(TextAnimationPreset preset)
        {
            List<TextAnimationModule> modules = preset != null
                ? preset.Modules
                : null;
            if (modules == null)
                return false;

            for (int i = 0; i < modules.Count; i++)
            {
                TextAnimationModule module = modules[i];
                if (module != null && module.enabled && module.UsesPreviousGlyphs)
                    return true;
            }

            return false;
        }

        private static bool UsesSdfGlyphLayer(TextAnimationPreset preset)
        {
            List<TextAnimationModule> modules = preset != null
                ? preset.Modules
                : null;
            if (modules == null)
                return false;

            for (int i = 0; i < modules.Count; i++)
            {
                TextAnimationModule module = modules[i];
                if (module != null && module.enabled && module.UsesSdfGlyphMorphs)
                    return true;
            }

            return false;
        }

        private void EnsurePreviousTextRenderer()
        {
            if (_previousTextRendererTarget != target)
                DestroyPreviousTextRenderer();

            if (_previousTextRenderer != null)
                return;

            if (target == null)
                return;

            GameObject previousObject;
            TMP_Text previousText;

            if (target is TextMeshProUGUI sourceUi)
            {
                previousObject = new GameObject(
                    $"{target.name} [Previous Text]",
                    typeof(RectTransform));
                TextMeshProUGUI previousUi =
                    previousObject.AddComponent<TextMeshProUGUI>();
                previousUi.raycastTarget = false;
                previousUi.maskable = sourceUi.maskable;
                previousText = previousUi;
            }
            else if (target is TextMeshPro)
            {
                previousObject = new GameObject($"{target.name} [Previous Text]");
                previousText = previousObject.AddComponent<TextMeshPro>();
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(TMPTextAnimator)}] Previous-glyph effects support " +
                    $"TextMeshProUGUI and TextMeshPro targets; '{target.name}' " +
                    "uses an unsupported TMP renderer.",
                    this);
                return;
            }

            previousObject.hideFlags = HideFlags.DontSave;
            previousObject.transform.SetParent(
                target.transform.parent,
                worldPositionStays: false);
            previousObject.transform.SetSiblingIndex(target.transform.GetSiblingIndex());
            previousObject.SetActive(false);

            _previousTextRenderer = new PreviousTextRendererState
            {
                root = previousObject,
                text = previousText
            };
            _previousTextRendererTarget = target;
        }

        private void RestorePreviousTextMeshAndHideGlyphs()
        {
            if (_previousTextRenderer == null ||
                _previousTextRenderer.root == null ||
                _previousTextRenderer.text == null ||
                _previousTextRenderer.baselineMeshInfo == null)
            {
                return;
            }

            TMP_TextInfo textInfo = _previousTextRenderer.text.textInfo;
            TMP_MeshInfo[] baseline = _previousTextRenderer.baselineMeshInfo;
            int meshCount = Mathf.Min(textInfo.meshInfo.Length, baseline.Length);

            for (int i = 0; i < meshCount; i++)
            {
                TMP_MeshInfo current = textInfo.meshInfo[i];
                TMP_MeshInfo source = baseline[i];

                if (current.vertices != null && source.vertices != null)
                {
                    Array.Copy(
                        source.vertices,
                        current.vertices,
                        Mathf.Min(source.vertices.Length, current.vertices.Length));
                }

                if (current.colors32 != null && source.colors32 != null)
                {
                    Array.Copy(
                        source.colors32,
                        current.colors32,
                        Mathf.Min(source.colors32.Length, current.colors32.Length));

                    for (int colorIndex = 0;
                         colorIndex < current.colors32.Length;
                         colorIndex++)
                    {
                        Color32 color = current.colors32[colorIndex];
                        color.a = 0;
                        current.colors32[colorIndex] = color;
                    }
                }
            }
        }

        private bool TryApplyPreviousGlyphState(
            int visibleOrdinal,
            in CharacterState character,
            in TextAnimationGlyphState state)
        {
            if (_previousTextRenderer == null ||
                _previousTextRenderer.text == null ||
                _previousTextRenderer.baselineMeshInfo == null ||
                _previousTextRenderer.characterIndicesByVisibleOrdinal == null ||
                visibleOrdinal < 0 ||
                visibleOrdinal >= _previousTextRenderer.characterIndicesByVisibleOrdinal.Length)
            {
                return false;
            }

            int characterIndex =
                _previousTextRenderer.characterIndicesByVisibleOrdinal[visibleOrdinal];
            return ApplyGlyphStateToMesh(
                _previousTextRenderer.text.textInfo,
                _previousTextRenderer.baselineMeshInfo,
                characterIndex,
                in character,
                in state);
        }

        private void CommitPreviousTextMesh(bool visible)
        {
            if (_previousTextRenderer == null ||
                _previousTextRenderer.root == null ||
                _previousTextRenderer.text == null)
            {
                return;
            }

            _previousTextRenderer.root.SetActive(visible);
            if (!visible)
                return;

            _previousTextRenderer.text.UpdateVertexData(
                TMP_VertexDataUpdateFlags.Vertices |
                TMP_VertexDataUpdateFlags.Colors32);
        }

        private void HidePreviousTextRenderer()
        {
            if (_previousTextRenderer?.root != null &&
                _previousTextRenderer.root.activeSelf)
            {
                _previousTextRenderer.root.SetActive(false);
            }
        }

        private void DestroyPreviousTextRenderer()
        {
            if (_previousTextRenderer?.root != null)
            {
                if (Application.isPlaying)
                    Destroy(_previousTextRenderer.root);
                else
                    DestroyImmediate(_previousTextRenderer.root);
            }

            _previousTextRenderer = null;
            _previousTextRendererTarget = null;
        }

        private void CaptureFixedVisibleSlotCenters()
        {
            if (target == null)
                return;

            target.ForceMeshUpdate();
            TMP_TextInfo textInfo = target.textInfo;
            if (textInfo == null)
                return;

            List<Vector3> centers = new List<Vector3>();
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo info = textInfo.characterInfo[i];
                if (!info.isVisible ||
                    info.materialReferenceIndex < 0 ||
                    info.materialReferenceIndex >= textInfo.meshInfo.Length)
                {
                    continue;
                }

                Vector3[] vertices =
                    textInfo.meshInfo[info.materialReferenceIndex].vertices;
                if (vertices == null || info.vertexIndex + 3 >= vertices.Length)
                    continue;

                centers.Add(
                    (vertices[info.vertexIndex] + vertices[info.vertexIndex + 2]) * 0.5f);
            }

            _fixedVisibleSlotCenters = centers.ToArray();
        }

        private void ApplyFixedVisibleSlotLayout(
            TMP_TextInfo textInfo,
            TMP_MeshInfo[] baselineMeshInfo)
        {
            if (!lockVisibleCharacterSlots ||
                textInfo == null ||
                baselineMeshInfo == null ||
                _fixedVisibleSlotCenters.Length == 0)
            {
                return;
            }

            int visibleOrdinal = 0;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo info = textInfo.characterInfo[i];
                if (!info.isVisible)
                    continue;

                if (visibleOrdinal >= _fixedVisibleSlotCenters.Length)
                    break;

                int meshIndex = info.materialReferenceIndex;
                int vertexIndex = info.vertexIndex;
                if (meshIndex < 0 ||
                    meshIndex >= textInfo.meshInfo.Length ||
                    meshIndex >= baselineMeshInfo.Length)
                {
                    visibleOrdinal++;
                    continue;
                }

                Vector3[] liveVertices = textInfo.meshInfo[meshIndex].vertices;
                Vector3[] baselineVertices = baselineMeshInfo[meshIndex].vertices;
                if (liveVertices == null ||
                    baselineVertices == null ||
                    vertexIndex + 3 >= liveVertices.Length ||
                    vertexIndex + 3 >= baselineVertices.Length)
                {
                    visibleOrdinal++;
                    continue;
                }

                Vector3 center =
                    (baselineVertices[vertexIndex] + baselineVertices[vertexIndex + 2]) * 0.5f;
                Vector3 offset = _fixedVisibleSlotCenters[visibleOrdinal] - center;
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    int index = vertexIndex + vertex;
                    baselineVertices[index] += offset;
                    liveVertices[index] += offset;
                }

                visibleOrdinal++;
            }
        }

        private bool TryApplySdfGlyphMorph(
            int visibleOrdinal,
            int currentCharacterIndex,
            in TextAnimationSdfMorphState state)
        {
            if (state.progress >= 0.9999f)
                return false;

            if (!(target is TextMeshProUGUI) && !(target is TextMeshPro))
            {
                return ReportSdfMorphFailure(
                    "the target is not a supported TextMeshPro renderer");
            }

            RectTransform targetRectTransform = target.rectTransform;
            if (targetRectTransform == null)
                return ReportSdfMorphFailure("the target has no text rect");

            bool previousRendererMatchesTarget =
                target is TextMeshProUGUI
                    ? _previousTextRenderer?.text is TextMeshProUGUI
                    : _previousTextRenderer?.text is TextMeshPro;
            if (!previousRendererMatchesTarget ||
                _previousTextRenderer.baselineMeshInfo == null ||
                _previousTextRenderer.characterIndicesByVisibleOrdinal == null)
            {
                return ReportSdfMorphFailure("the outgoing TMP mesh was not prepared");
            }
            if (visibleOrdinal < 0 ||
                visibleOrdinal >= _previousTextRenderer.characterIndicesByVisibleOrdinal.Length)
            {
                return ReportSdfMorphFailure("the changed digit has no outgoing slot");
            }

            TMP_TextInfo currentTextInfo = target.textInfo;
            TMP_TextInfo previousTextInfo = _previousTextRenderer.text.textInfo;
            int previousCharacterIndex =
                _previousTextRenderer.characterIndicesByVisibleOrdinal[visibleOrdinal];
            if (currentCharacterIndex < 0 ||
                currentCharacterIndex >= currentTextInfo.characterCount ||
                previousCharacterIndex < 0 ||
                previousCharacterIndex >= previousTextInfo.characterCount)
            {
                return ReportSdfMorphFailure("the changed digit index is outside its TMP mesh");
            }

            TMP_CharacterInfo currentInfo =
                currentTextInfo.characterInfo[currentCharacterIndex];
            TMP_CharacterInfo previousInfo =
                previousTextInfo.characterInfo[previousCharacterIndex];
            if (!currentInfo.isVisible || !previousInfo.isVisible)
                return ReportSdfMorphFailure("one of the digit meshes has no visible quad");

            if (!TryGetGlyphUvRect(
                    currentTextInfo,
                    _baselineMeshInfo,
                    in currentInfo,
                    out Vector4 currentUvRect) ||
                !TryGetGlyphUvRect(
                    previousTextInfo,
                    _previousTextRenderer.baselineMeshInfo,
                    in previousInfo,
                    out Vector4 previousUvRect) ||
                !TryGetGlyphNormalizedBounds(
                    currentTextInfo,
                    _baselineMeshInfo,
                    in currentInfo,
                    targetRectTransform.rect,
                    out Vector4 currentBounds) ||
                !TryGetGlyphNormalizedBounds(
                    previousTextInfo,
                    _previousTextRenderer.baselineMeshInfo,
                    in previousInfo,
                    targetRectTransform.rect,
                    out Vector4 previousBounds))
            {
                return ReportSdfMorphFailure("the digit UV or fixed-cell bounds could not be read");
            }

            Texture currentAtlas = GetGlyphAtlas(currentTextInfo, in currentInfo);
            Texture previousAtlas = GetGlyphAtlas(previousTextInfo, in previousInfo);
            if (currentAtlas == null || previousAtlas == null)
                return ReportSdfMorphFailure("the TMP font atlas is unavailable");

            SdfMorphRendererState renderer =
                EnsureSdfMorphRenderer(visibleOrdinal);
            if (renderer == null ||
                renderer.root == null ||
                renderer.material == null)
            {
                return ReportSdfMorphFailure("the SDF overlay renderer could not be created");
            }

            SyncSdfMorphTransform(
                renderer.root.GetComponent<RectTransform>(),
                targetRectTransform);

            Color faceColor = GetMorphFaceColor(currentTextInfo, in currentInfo);
            if (renderer.image != null)
            {
                renderer.image.texture = currentAtlas;
                renderer.image.color = faceColor;
            }
            else
            {
                renderer.material.SetColor("_Color", faceColor);
            }

            renderer.material.SetTexture("_MainTex", currentAtlas);
            renderer.material.SetVector("_FromUVRect", previousUvRect);
            renderer.material.SetVector("_ToUVRect", currentUvRect);
            renderer.material.SetVector("_FromBounds", previousBounds);
            renderer.material.SetVector("_ToBounds", currentBounds);
            renderer.material.SetFloat("_Morph", Mathf.Clamp01(state.progress));
            renderer.material.SetFloat("_EdgeSoftness", Mathf.Max(0.25f, state.edgeSoftness));
            renderer.material.SetFloat("_ContourBias", state.contourBias);
            renderer.root.SetActive(true);
            return true;
        }

        private bool ReportSdfMorphFailure(string reason)
        {
            if (_reportedSdfMorphFailures.Add(reason))
            {
                Debug.LogWarning(
                    $"[{nameof(TMPTextAnimator)}] SDF digit morph was skipped because {reason}.",
                    this);
            }

            return false;
        }

        private SdfMorphRendererState EnsureSdfMorphRenderer(
            int visibleOrdinal)
        {
            if (_sdfMorphRendererTarget != target)
            {
                DestroySdfMorphRenderers();
                _sdfMorphRendererTarget = target;
            }

            while (_sdfMorphRenderers.Count <= visibleOrdinal)
                _sdfMorphRenderers.Add(null);

            SdfMorphRendererState existing = _sdfMorphRenderers[visibleOrdinal];
            if (existing != null && existing.root != null)
                return existing;

            Shader shader = Resources.Load<Shader>("TMPDigitSdfMorph") ??
                            Shader.Find("MASSIVE/UI/TMP Digit SDF Morph");
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[{nameof(TMPTextAnimator)}] The TMP Digit SDF Morph shader is missing.",
                    this);
                return null;
            }

            GameObject root = new GameObject(
                $"{target.name} [SDF Morph {visibleOrdinal}]",
                typeof(RectTransform));
            root.hideFlags = HideFlags.DontSave;
            root.layer = target.gameObject.layer;
            root.transform.SetParent(target.transform.parent, worldPositionStays: false);

            Material material = new Material(shader)
            {
                name = $"{target.name} SDF Morph {visibleOrdinal}",
                hideFlags = HideFlags.HideAndDontSave
            };

            SdfMorphRendererState created = new SdfMorphRendererState
            {
                root = root,
                material = material
            };

            if (target is TextMeshProUGUI sourceUi)
            {
                RawImage image = root.AddComponent<RawImage>();
                image.raycastTarget = false;
                image.maskable = sourceUi.maskable;
                image.material = material;
                created.image = image;
            }
            else if (target is TextMeshPro sourceWorld)
            {
                MeshFilter meshFilter = root.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
                Mesh mesh = CreateSdfMorphQuad(target.rectTransform.rect);
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
                meshRenderer.sortingLayerID = sourceWorld.sortingLayerID;
                meshRenderer.sortingOrder = sourceWorld.sortingOrder + 1;
                created.mesh = mesh;
                created.meshRenderer = meshRenderer;
            }
            else
            {
                DestroyImmediate(material);
                DestroyImmediate(root);
                return null;
            }

            _sdfMorphRenderers[visibleOrdinal] = created;
            SyncSdfMorphTransform(
                root.GetComponent<RectTransform>(),
                target.rectTransform);
            root.transform.SetSiblingIndex(
                Mathf.Min(target.transform.GetSiblingIndex() + 1, root.transform.parent.childCount - 1));
            root.SetActive(false);
            return created;
        }

        private static Mesh CreateSdfMorphQuad(Rect rect)
        {
            Mesh mesh = new Mesh
            {
                name = "TMP Digit SDF Morph Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(rect.xMin, rect.yMin, 0f),
                    new Vector3(rect.xMin, rect.yMax, 0f),
                    new Vector3(rect.xMax, rect.yMax, 0f),
                    new Vector3(rect.xMax, rect.yMin, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                },
                triangles = new[] { 0, 1, 2, 2, 3, 0 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SyncSdfMorphTransform(
            RectTransform destination,
            RectTransform source)
        {
            if (destination == null || source == null)
                return;

            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.sizeDelta = source.sizeDelta;
            destination.anchoredPosition3D = source.anchoredPosition3D;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        private static bool TryGetGlyphUvRect(
            TMP_TextInfo textInfo,
            TMP_MeshInfo[] meshInfo,
            in TMP_CharacterInfo character,
            out Vector4 rect)
        {
            rect = default;
            int meshIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            if (textInfo == null ||
                meshInfo == null ||
                meshIndex < 0 ||
                meshIndex >= meshInfo.Length)
            {
                return false;
            }

            Vector4[] uvs = meshInfo[meshIndex].uvs0;
            if (uvs == null || vertexIndex < 0 || vertexIndex + 3 >= uvs.Length)
                return false;

            float minX = uvs[vertexIndex].x;
            float minY = uvs[vertexIndex].y;
            float maxX = minX;
            float maxY = minY;
            for (int vertex = 1; vertex < 4; vertex++)
            {
                Vector4 uv = uvs[vertexIndex + vertex];
                minX = Mathf.Min(minX, uv.x);
                minY = Mathf.Min(minY, uv.y);
                maxX = Mathf.Max(maxX, uv.x);
                maxY = Mathf.Max(maxY, uv.y);
            }

            rect = new Vector4(minX, minY, maxX, maxY);
            return maxX > minX && maxY > minY;
        }

        private static bool TryGetGlyphNormalizedBounds(
            TMP_TextInfo textInfo,
            TMP_MeshInfo[] meshInfo,
            in TMP_CharacterInfo character,
            Rect targetRect,
            out Vector4 bounds)
        {
            bounds = default;
            int meshIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            if (textInfo == null ||
                meshInfo == null ||
                meshIndex < 0 ||
                meshIndex >= meshInfo.Length ||
                Mathf.Abs(targetRect.width) < 0.0001f ||
                Mathf.Abs(targetRect.height) < 0.0001f)
            {
                return false;
            }

            Vector3[] vertices = meshInfo[meshIndex].vertices;
            if (vertices == null || vertexIndex < 0 || vertexIndex + 3 >= vertices.Length)
                return false;

            float minX = vertices[vertexIndex].x;
            float minY = vertices[vertexIndex].y;
            float maxX = minX;
            float maxY = minY;
            for (int vertex = 1; vertex < 4; vertex++)
            {
                Vector3 point = vertices[vertexIndex + vertex];
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            bounds = new Vector4(
                Mathf.InverseLerp(targetRect.xMin, targetRect.xMax, minX),
                Mathf.InverseLerp(targetRect.yMin, targetRect.yMax, minY),
                Mathf.InverseLerp(targetRect.xMin, targetRect.xMax, maxX),
                Mathf.InverseLerp(targetRect.yMin, targetRect.yMax, maxY));
            return maxX > minX && maxY > minY;
        }

        private static Texture GetGlyphAtlas(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character)
        {
            int meshIndex = character.materialReferenceIndex;
            if (textInfo == null ||
                meshIndex < 0 ||
                meshIndex >= textInfo.meshInfo.Length ||
                textInfo.meshInfo[meshIndex].material == null)
            {
                return null;
            }

            return textInfo.meshInfo[meshIndex].material.GetTexture("_MainTex");
        }

        private Color GetMorphFaceColor(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character)
        {
            Color faceColor = Color.white;
            int meshIndex = character.materialReferenceIndex;
            if (textInfo != null &&
                meshIndex >= 0 &&
                meshIndex < textInfo.meshInfo.Length)
            {
                Material material = textInfo.meshInfo[meshIndex].material;
                if (material != null && material.HasProperty("_FaceColor"))
                    faceColor = material.GetColor("_FaceColor");
            }

            return MultiplyColors(target != null ? target.color : Color.white, faceColor);
        }

        private void HideSdfMorphRenderers()
        {
            for (int i = 0; i < _sdfMorphRenderers.Count; i++)
            {
                SdfMorphRendererState renderer = _sdfMorphRenderers[i];
                if (renderer?.root != null && renderer.root.activeSelf)
                    renderer.root.SetActive(false);
            }
        }

        private void DestroySdfMorphRenderers()
        {
            for (int i = 0; i < _sdfMorphRenderers.Count; i++)
            {
                SdfMorphRendererState renderer = _sdfMorphRenderers[i];
                if (renderer == null)
                    continue;

                if (renderer.material != null)
                {
                    if (Application.isPlaying)
                        Destroy(renderer.material);
                    else
                        DestroyImmediate(renderer.material);
                }

                if (renderer.mesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(renderer.mesh);
                    else
                        DestroyImmediate(renderer.mesh);
                }

                if (renderer.root != null)
                {
                    if (Application.isPlaying)
                        Destroy(renderer.root);
                    else
                        DestroyImmediate(renderer.root);
                }
            }

            _sdfMorphRenderers.Clear();
            _sdfMorphRendererTarget = null;
        }

        private void ApplyEchoState(in TextAnimationEchoState state)
        {
            if (target == null ||
                !state.visible ||
                state.copyCount <= 0 ||
                state.opacity <= 0.0001f)
            {
                HideEchoRenderers();
                return;
            }

            EnsureEchoRenderers(state.copyCount);

            int visibleCount = Mathf.Min(state.copyCount, _echoRenderers.Count);
            for (int i = 0; i < _echoRenderers.Count; i++)
            {
                EchoRendererState echo = _echoRenderers[i];
                if (echo == null || echo.root == null || echo.text == null)
                    continue;

                if (i >= visibleCount)
                {
                    echo.root.SetActive(false);
                    continue;
                }

                SyncEchoAppearance(echo.text);

                float opacity = state.opacity *
                                Mathf.Pow(state.opacityFalloff, i);
                Color color = MultiplyColors(target.color, state.tint);
                color.a = Mathf.Clamp01(target.color.a * state.tint.a * opacity);
                echo.text.color = color;

                float distance = state.spacing * state.spread * (i + 1);
                Vector2 offset = state.direction * distance;
                float scale = Mathf.Max(
                    0.01f,
                    1f + state.scaleStepPerCopy * (i + 1));

                MatchEchoTransform(echo.root.transform, offset, scale);
                if (!echo.root.activeSelf)
                    echo.root.SetActive(true);

                echo.text.ForceMeshUpdate();
            }
        }

        private void EnsureEchoRenderers(int count)
        {
            if (_echoPoolTarget != target)
            {
                DestroyEchoRenderers();
                _echoPoolTarget = target;
            }

            count = Mathf.Clamp(count, 0, 12);
            while (_echoRenderers.Count < count)
            {
                EchoRendererState echo = CreateEchoRenderer(_echoRenderers.Count);
                if (echo == null)
                    break;

                _echoRenderers.Add(echo);
            }
        }

        private EchoRendererState CreateEchoRenderer(int index)
        {
            if (target == null)
                return null;

            GameObject echoObject;
            TMP_Text echoText;

            if (target is TextMeshProUGUI sourceUi)
            {
                echoObject = new GameObject(
                    $"{target.name} [Ghost {index + 1}]",
                    typeof(RectTransform));
                TextMeshProUGUI echoUi = echoObject.AddComponent<TextMeshProUGUI>();
                echoUi.raycastTarget = false;
                echoUi.maskable = sourceUi.maskable;
                echoText = echoUi;
            }
            else if (target is TextMeshPro)
            {
                echoObject = new GameObject($"{target.name} [Ghost {index + 1}]");
                echoText = echoObject.AddComponent<TextMeshPro>();
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(TMPTextAnimator)}] Ghost Trail supports " +
                    $"TextMeshProUGUI and TextMeshPro targets; '{target.name}' " +
                    "uses an unsupported TMP renderer.",
                    this);
                return null;
            }

            echoObject.hideFlags = HideFlags.DontSave;
            echoObject.transform.SetParent(target.transform.parent, worldPositionStays: false);
            echoObject.transform.SetSiblingIndex(target.transform.GetSiblingIndex());
            echoObject.SetActive(false);

            SyncEchoAppearance(echoText);
            return new EchoRendererState
            {
                root = echoObject,
                text = echoText
            };
        }

        private void SyncEchoAppearance(TMP_Text echo)
        {
            if (echo == null || target == null)
                return;

            echo.text = target.text;
            echo.font = target.font;
            echo.fontSharedMaterial = target.fontSharedMaterial;
            echo.fontSize = target.fontSize;
            echo.fontStyle = target.fontStyle;
            echo.alignment = target.alignment;
            echo.enableAutoSizing = target.enableAutoSizing;
            echo.fontSizeMin = target.fontSizeMin;
            echo.fontSizeMax = target.fontSizeMax;
            echo.richText = target.richText;
            echo.overflowMode = target.overflowMode;
            echo.margin = target.margin;
            echo.characterSpacing = target.characterSpacing;
            echo.wordSpacing = target.wordSpacing;
            echo.lineSpacing = target.lineSpacing;
            echo.paragraphSpacing = target.paragraphSpacing;
            echo.isRightToLeftText = target.isRightToLeftText;

            if (target.rectTransform != null && echo.rectTransform != null)
            {
                RectTransform sourceRect = target.rectTransform;
                RectTransform echoRect = echo.rectTransform;
                echoRect.anchorMin = sourceRect.anchorMin;
                echoRect.anchorMax = sourceRect.anchorMax;
                echoRect.pivot = sourceRect.pivot;
                echoRect.sizeDelta = sourceRect.sizeDelta;
            }
        }

        private void MatchEchoTransform(
            Transform echoTransform,
            Vector2 offset,
            float scaleMultiplier)
        {
            if (echoTransform == null || target == null)
                return;

            Transform sourceTransform = target.transform;
            if (echoTransform.parent != sourceTransform.parent)
            {
                echoTransform.SetParent(
                    sourceTransform.parent,
                    worldPositionStays: false);
            }

            Vector3 localOffset = sourceTransform.localRotation *
                                  new Vector3(offset.x, offset.y, 0f);
            echoTransform.localPosition = sourceTransform.localPosition + localOffset;
            echoTransform.localRotation = sourceTransform.localRotation;
            echoTransform.localScale = Vector3.Scale(
                sourceTransform.localScale,
                new Vector3(scaleMultiplier, scaleMultiplier, 1f));
        }

        private void HideEchoRenderers()
        {
            for (int i = 0; i < _echoRenderers.Count; i++)
            {
                EchoRendererState echo = _echoRenderers[i];
                if (echo?.root != null && echo.root.activeSelf)
                    echo.root.SetActive(false);
            }
        }

        private void DestroyEchoRenderers()
        {
            for (int i = 0; i < _echoRenderers.Count; i++)
            {
                EchoRendererState echo = _echoRenderers[i];
                if (echo?.root == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(echo.root);
                else
                    DestroyImmediate(echo.root);
            }

            _echoRenderers.Clear();
            _echoPoolTarget = null;
        }

        private static Color MultiplyColors(Color a, Color b)
        {
            return new Color(
                a.r * b.r,
                a.g * b.g,
                a.b * b.b,
                a.a * b.a);
        }

        private void CaptureRendererDefaults(bool force)
        {
            if (target == null)
                return;

            if (!force && _rendererDefaultsCaptured && _rendererDefaultsTarget == target)
                return;

            _rendererDefaultsTarget = target;
            _canvasRenderer = target.canvasRenderer;
            _renderer = target.GetComponent<Renderer>();
            _baseCanvasCull = _canvasRenderer != null && _canvasRenderer.cull;
            _baseRendererEnabled = _renderer == null || _renderer.enabled;
            _rendererDefaultsCaptured = true;
            _hardVisible = true;
        }

        private static char[] ExtractComparableCharacters(string source)
        {
            if (string.IsNullOrEmpty(source))
                return Array.Empty<char>();

            List<char> characters = new List<char>(source.Length);
            bool insideTag = false;

            for (int i = 0; i < source.Length; i++)
            {
                char value = source[i];
                if (value == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (insideTag)
                {
                    if (value == '>')
                        insideTag = false;
                    continue;
                }

                if (!char.IsWhiteSpace(value))
                    characters.Add(value);
            }

            return characters.ToArray();
        }

        private static float DeltaTime(TextAnimationTimeMode mode)
        {
            return mode == TextAnimationTimeMode.Unscaled
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        }
    }
}
