using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace Massive.TextAnimation
{
    /// <summary>
    /// Binds scene TMPTextAnimators to reusable string slots and plays
    /// ScriptableObject sequences without placing scene references in assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TMPTextAnimationGroup : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public string slotId = "PRIMARY";
            public TMPTextAnimator animator;
        }

        [Header("Scene Bindings")]
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        [Header("Events")]
        public UnityEvent onSequenceStarted;
        public UnityEvent onSequenceCompleted;
        public UnityEvent onSequenceCancelled;

        private readonly List<TextAnimationPlaybackHandle> _activeHandles =
            new List<TextAnimationPlaybackHandle>();
        private readonly HashSet<TMPTextAnimator> _launchedAnimators =
            new HashSet<TMPTextAnimator>();

        private Coroutine _routine;
        private int _playbackId;
        private bool _playing;
        private bool _forceCompleteRequested;

        public bool IsPlaying => _playing;
        public int PlaybackId => _playbackId;
        public IReadOnlyList<Binding> Bindings => bindings;

        private void OnDisable()
        {
            StopCurrent(restoreBaselines: true);
        }

        public int Play(
            TextAnimationSequence sequence,
            TextAnimationContext context)
        {
            if (sequence == null || !isActiveAndEnabled)
                return 0;

            StopCurrent(restoreBaselines: true);
            _playbackId++;
            if (_playbackId <= 0)
                _playbackId = 1;

            int id = _playbackId;
            _playing = true;
            _forceCompleteRequested = false;
            _routine = StartCoroutine(
                SequenceRoutine(id, sequence, context.Sanitized()));
            return id;
        }

        public int Play(TextAnimationSequence sequence)
        {
            return Play(sequence, TextAnimationContext.Default);
        }

        public IEnumerator PlayAndWait(
            TextAnimationSequence sequence,
            TextAnimationContext context)
        {
            int id = Play(sequence, context);
            if (id <= 0)
                yield break;

            while (isActiveAndEnabled && IsPlaybackActive(id))
                yield return null;
        }

        public IEnumerator PlayAndWait(TextAnimationSequence sequence)
        {
            return PlayAndWait(sequence, TextAnimationContext.Default);
        }

        public bool IsPlaybackActive(int playbackId)
        {
            return playbackId > 0 && _playing && playbackId == _playbackId;
        }

        public void StopCurrent(bool restoreBaselines)
        {
            bool wasPlaying = _playing;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            foreach (TMPTextAnimator animator in _launchedAnimators)
            {
                if (animator != null)
                {
                    animator.StopAll(
                        restoreBaseline: restoreBaselines,
                        restoreVisibility: restoreBaselines);
                }
            }

            _activeHandles.Clear();
            _launchedAnimators.Clear();
            _forceCompleteRequested = false;
            _playing = false;

            if (wasPlaying)
                onSequenceCancelled?.Invoke();
        }

        /// <summary>
        /// Requests every active track to resolve to its final frame. Tracks that
        /// have not reached their start delay are launched and completed by the
        /// sequence coroutine on the next frame.
        /// </summary>
        public void CompleteCurrent()
        {
            if (!_playing)
                return;

            _forceCompleteRequested = true;
            for (int i = 0; i < _activeHandles.Count; i++)
            {
                TextAnimationPlaybackHandle handle = _activeHandles[i];
                if (handle.IsActive)
                    handle.CompleteImmediately();
            }
        }

        public void RefreshBaselines()
        {
            VisitUniqueAnimators(animator => animator.RefreshBaselineFromCurrent());
        }

        public void RestoreBaselines(bool restoreVisibility = true)
        {
            VisitUniqueAnimators(animator => animator.RestoreBaseline(restoreVisibility));
        }

        public void SetHardVisible(bool visible)
        {
            VisitUniqueAnimators(animator => animator.SetHardVisible(visible));
        }

        public TMPTextAnimator FindAnimator(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || bindings == null)
                return null;

            for (int i = 0; i < bindings.Length; i++)
            {
                Binding binding = bindings[i];
                if (binding == null || binding.animator == null)
                    continue;

                if (string.Equals(
                        binding.slotId,
                        slotId,
                        StringComparison.OrdinalIgnoreCase))
                    return binding.animator;
            }

            return null;
        }

        [ContextMenu("Auto Populate Bindings From Child Animators")]
        public void AutoPopulateBindingsFromChildren()
        {
            TMPTextAnimator[] animators =
                GetComponentsInChildren<TMPTextAnimator>(includeInactive: true);
            bindings = new Binding[animators.Length];

            for (int i = 0; i < animators.Length; i++)
            {
                TMPTextAnimator animator = animators[i];
                bindings[i] = new Binding
                {
                    slotId = BuildSlotId(animator != null ? animator.name : $"TEXT_{i}"),
                    animator = animator
                };
            }
        }

        private IEnumerator SequenceRoutine(
            int playbackId,
            TextAnimationSequence sequence,
            TextAnimationContext baseContext)
        {
            _activeHandles.Clear();
            _launchedAnimators.Clear();
            onSequenceStarted?.Invoke();

            // Sequence callbacks may synchronously start or stop another
            // sequence. Do not let this superseded coroutine touch its targets.
            if (playbackId != _playbackId || !_playing)
                yield break;

            List<TextAnimationSequence.Track> tracks =
                new List<TextAnimationSequence.Track>();
            if (sequence.tracks != null)
            {
                for (int i = 0; i < sequence.tracks.Count; i++)
                {
                    TextAnimationSequence.Track track = sequence.tracks[i];
                    if (track != null && track.preset != null)
                        tracks.Add(track);
                }
            }

            tracks.Sort((a, b) => a.startDelay.CompareTo(b.startDelay));

            if (sequence.hideTargetsUntilTheirTrackStarts)
                PrimeSequenceTargetsHidden(sequence, tracks);

            float elapsed = 0f;
            int nextTrack = 0;

            while (nextTrack < tracks.Count && playbackId == _playbackId)
            {
                if (_forceCompleteRequested)
                {
                    while (nextTrack < tracks.Count)
                    {
                        LaunchTrack(
                            sequence,
                            tracks[nextTrack],
                            baseContext,
                            completeImmediately: true);
                        nextTrack++;
                    }
                    break;
                }

                TextAnimationSequence.Track next = tracks[nextTrack];
                while (elapsed < next.startDelay &&
                       playbackId == _playbackId &&
                       !_forceCompleteRequested)
                {
                    elapsed += DeltaTime(sequence.delayTimeMode);
                    yield return null;
                }

                if (_forceCompleteRequested)
                    continue;

                float currentDelay = next.startDelay;
                while (nextTrack < tracks.Count &&
                       Mathf.Abs(tracks[nextTrack].startDelay - currentDelay) <= 0.0001f)
                {
                    LaunchTrack(
                        sequence,
                        tracks[nextTrack],
                        baseContext,
                        completeImmediately: false);
                    nextTrack++;
                }
            }

            while (playbackId == _playbackId && AnyHandleActive())
            {
                if (_forceCompleteRequested)
                {
                    for (int i = 0; i < _activeHandles.Count; i++)
                    {
                        TextAnimationPlaybackHandle handle = _activeHandles[i];
                        if (handle.IsActive)
                            handle.CompleteImmediately();
                    }
                }

                yield return null;
            }

            float tail = 0f;
            while (playbackId == _playbackId &&
                   !_forceCompleteRequested &&
                   tail < sequence.tailSeconds)
            {
                tail += DeltaTime(sequence.delayTimeMode);
                yield return null;
            }

            if (playbackId != _playbackId)
                yield break;

            _activeHandles.Clear();
            _launchedAnimators.Clear();
            _routine = null;
            _forceCompleteRequested = false;
            _playing = false;
            onSequenceCompleted?.Invoke();
        }

        private void PrimeSequenceTargetsHidden(
            TextAnimationSequence sequence,
            List<TextAnimationSequence.Track> tracks)
        {
            HashSet<TMPTextAnimator> primed = new HashSet<TMPTextAnimator>();
            for (int i = 0; i < tracks.Count; i++)
            {
                TextAnimationSequence.Track track = tracks[i];
                TMPTextAnimator animator = FindAnimator(track.slotId);
                if (animator == null)
                {
                    if (sequence.warnAboutMissingSlots)
                    {
                        Debug.LogWarning(
                            $"[{nameof(TMPTextAnimationGroup)}] '{name}' has no binding " +
                            $"for slot '{track.slotId}' used by sequence '{sequence.name}'.",
                            this);
                    }
                    continue;
                }

                if (!primed.Add(animator))
                    continue;

                animator.SetHardVisible(false);
                // Treat primed targets as touched so cancellation restores any
                // delayed track that had not launched yet.
                _launchedAnimators.Add(animator);
            }
        }

        private void LaunchTrack(
            TextAnimationSequence sequence,
            TextAnimationSequence.Track track,
            TextAnimationContext baseContext,
            bool completeImmediately)
        {
            TMPTextAnimator animator = FindAnimator(track.slotId);
            if (animator == null)
            {
                if (sequence.warnAboutMissingSlots)
                {
                    Debug.LogWarning(
                        $"[{nameof(TMPTextAnimationGroup)}] '{name}' has no binding " +
                        $"for slot '{track.slotId}' used by sequence '{sequence.name}'.",
                        this);
                }
                return;
            }

            TextAnimationContext context = baseContext;
            context.intensity *= Mathf.Max(0f, track.intensityMultiplier);

            if (track.overrideDirection)
                context.direction = track.direction;

            if (track.overrideAccentColor)
            {
                context.useAccentColor = true;
                context.accentColor = track.accentColor;
            }

            TextAnimationPlaybackHandle handle = animator.Play(track.preset, context);
            if (!handle.IsValid)
                return;

            _activeHandles.Add(handle);
            _launchedAnimators.Add(animator);

            if (completeImmediately)
                handle.CompleteImmediately();
        }

        private bool AnyHandleActive()
        {
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                if (_activeHandles[i].IsActive)
                    continue;

                _activeHandles.RemoveAt(i);
            }

            return _activeHandles.Count > 0;
        }

        private void VisitUniqueAnimators(Action<TMPTextAnimator> action)
        {
            if (action == null || bindings == null)
                return;

            HashSet<TMPTextAnimator> visited = new HashSet<TMPTextAnimator>();
            for (int i = 0; i < bindings.Length; i++)
            {
                TMPTextAnimator animator = bindings[i]?.animator;
                if (animator == null || !visited.Add(animator))
                    continue;

                action(animator);
            }
        }

        private static string BuildSlotId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "PRIMARY";

            StringBuilder builder = new StringBuilder(source.Length);
            bool previousWasSeparator = false;

            for (int i = 0; i < source.Length; i++)
            {
                char value = source[i];
                if (char.IsLetterOrDigit(value))
                {
                    builder.Append(char.ToUpperInvariant(value));
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        private static float DeltaTime(TextAnimationTimeMode mode)
        {
            return mode == TextAnimationTimeMode.Unscaled
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        }
    }
}
