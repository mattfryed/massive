using UnityEngine;

namespace Massive.TextAnimation
{
    /// <summary>
    /// Inspector and UnityEvent bridge for playing one reusable text-animation
    /// preset without writing a bespoke scene component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MASSIVE/Text Animation/Trigger")]
    public sealed class TMPTextAnimationTrigger : MonoBehaviour
    {
        [Header("Playback")]
        [SerializeField] private TMPTextAnimator animator;
        [SerializeField] private TextAnimationPreset preset;

        [Header("Runtime Context")]
        [Min(0f)] [SerializeField] private float intensity = 1f;
        [SerializeField] private Vector2 direction = Vector2.right;
        [SerializeField] private bool useAccentColor;
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private int seedOffset;

        [Header("Stop Behavior")]
        [SerializeField] private bool restoreBaselineOnStop = true;
        [SerializeField] private bool restoreVisibilityOnStop = true;

        public TMPTextAnimator Animator => ResolveAnimator();
        public TextAnimationPreset Preset => preset;

        private void Reset()
        {
            animator = GetComponent<TMPTextAnimator>();
        }

        private void OnValidate()
        {
            intensity = Mathf.Max(0f, intensity);
            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector2.right;
        }

        public void SetAnimator(TMPTextAnimator value)
        {
            animator = value;
        }

        public void SetPreset(TextAnimationPreset value)
        {
            preset = value;
        }

        public void Play()
        {
            TMPTextAnimator targetAnimator = ResolveAnimator();
            if (targetAnimator == null || preset == null)
                return;

            targetAnimator.Play(preset, BuildContext());
        }

        public void Stop()
        {
            TMPTextAnimator targetAnimator = ResolveAnimator();
            if (targetAnimator == null)
                return;

            targetAnimator.StopAll(
                restoreBaselineOnStop,
                restoreVisibilityOnStop);
        }

        public void Complete()
        {
            ResolveAnimator()?.CompleteCurrent();
        }

        public void SetTextAndPlay(string value)
        {
            TMPTextAnimator targetAnimator = ResolveAnimator();
            if (targetAnimator == null)
                return;

            if (preset == null)
            {
                SetTargetText(value);
                return;
            }

            targetAnimator.SetTextAndPlay(value, preset, BuildContext());
        }

        public void SetTargetText(string value)
        {
            TMPTextAnimator targetAnimator = ResolveAnimator();
            if (targetAnimator == null || targetAnimator.Target == null)
                return;

            targetAnimator.StopAll(
                restoreBaseline: true,
                restoreVisibility: true);
            targetAnimator.Target.text = value ?? string.Empty;
            targetAnimator.Target.ForceMeshUpdate();
            targetAnimator.RefreshBaselineFromCurrent();
        }

        private TMPTextAnimator ResolveAnimator()
        {
            if (animator == null)
                animator = GetComponent<TMPTextAnimator>();

            return animator;
        }

        private TextAnimationContext BuildContext()
        {
            TextAnimationContext context = TextAnimationContext.Default
                .WithIntensity(intensity)
                .WithDirection(direction)
                .WithSeedOffset(seedOffset);

            if (useAccentColor)
                context = context.WithAccentColor(accentColor);

            return context;
        }
    }
}
