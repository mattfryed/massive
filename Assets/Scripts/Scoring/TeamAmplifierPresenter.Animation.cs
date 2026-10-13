using Massive.Settings;
using Massive.TextAnimation;
using UnityEngine;

namespace Massive.Scoring
{
    public sealed partial class TeamAmplifierPresenter
    {
        TMPTextAnimator numberAnimator;
        TMPMaterialStateController numberMaterialState;
        TextAnimationPreset firstTextPreset, transitionMorphPreset;
        Vector4 transitionStart, visualLayout;
        float transitionElapsed, textAnimationLength, transitionIntensity = 1;
        float baseFaceDilate;
        bool transitionActive, firstReveal, textAnimationStarted;
        string previousNumber;
#if UNITY_EDITOR
        double lastPreviewTick;
#endif
        public bool GoalTransitionPlaying => transitionActive;
        public float GoalTransitionElapsed => transitionElapsed;
        public TMPTextAnimator GoalTextAnimator => numberAnimator;

        Vector4 LayoutFor(int multiplier)
        {
            var s = GoalNumberSettings;
            float whole = s.ScaleFor(multiplier);
            return new Vector4(whole, whole * s.WhiteCircleScaleFor(multiplier),
                whole * s.NumberScaleFor(multiplier), whole * s.NumberHorizontalOffsetFor(multiplier, teamID));
        }

        void BeginGoalTransition(int from, int to)
        {
            Vector4 start = transitionActive ? visualLayout : LayoutFor(from);
            StopGoalTransition();
            transitionStart = start;
            firstReveal = from <= 1;
            if (firstReveal)
            {
                var target = LayoutFor(to);
                transitionStart = new Vector4(0, 0, target.z, target.w);
            }
            previousNumber = GoalNumberSettings.prefix + from;
            transitionElapsed = textAnimationLength = 0;
            transitionActive = true;
            var shared = SharedSettingsRuntime.Load<TextAnimationSharedProfile>();
            transitionMorphPreset = shared && shared.sharedEnabled ? shared.scoreDigitMorphPreset : null;
            transitionIntensity = shared && shared.sharedEnabled ? shared.scoreDigitMorphIntensity : 1;
            if (!transitionMorphPreset)
                foreach (var board in FindObjectsByType<ScoreboardManagerScript>(FindObjectsSortMode.None))
                    if (board.gameObject.scene == gameObject.scene)
                    { transitionMorphPreset = board.ScoreDigitMorphPreset; transitionIntensity = board.ScoreDigitMorphIntensity; break; }
#if UNITY_EDITOR
            lastPreviewTick = UnityEditor.EditorApplication.timeSinceStartup;
#endif
        }

        static float EasedProgress(float elapsed, float duration, AnimationCurve curve)
        {
            if (duration <= 0 || elapsed >= duration) return 1;
            if (elapsed <= 0) return 0;
            float t = Mathf.Clamp01(elapsed / duration);
            return Mathf.Clamp01(curve != null ? curve.Evaluate(t) : t * t * (3 - 2 * t));
        }

        Vector4 EvaluateGoalLayout()
        {
            var target = LayoutFor(DisplayedMultiplier);
            if (!GoalNumberSettings.animateChanges) StopGoalTransition();
            visualLayout = transitionActive
                ? Vector4.LerpUnclamped(transitionStart, target,
                    EasedProgress(transitionElapsed, GoalNumberSettings.growthSeconds, GoalNumberSettings.growthEasing))
                : target;
            return visualLayout;
        }

        void EnsureNumberAnimator()
        {
            if (numberAnimator) return;
            numberMaterialState = goalText.gameObject.AddComponent<TMPMaterialStateController>();
            numberMaterialState.Configure(goalText);
            numberMaterialState.RuntimeMaterial.hideFlags = HideFlags.HideAndDontSave;
            numberAnimator = goalText.gameObject.AddComponent<TMPTextAnimator>();
            numberAnimator.Configure(goalText, goalText.transform);
            numberAnimator.ConfigureFixedVisibleCharacterSlots(true);
        }

        void RefreshAnimatedTextMaterial(TeamAmplifierGoalNumberSettings settings)
        {
            if (!numberMaterialState) return;
            var style = numberMaterialState.PersistentStyle;
            style.faceColor = Color.white;
            style.faceDilate = baseFaceDilate;
            style.outlineColor = settings.outlineColor;
            style.outlineWidth = settings.outlineWidth;
            numberMaterialState.SetPersistentStyle(style);
            CopyGoalMask(numberMaterialState.RuntimeMaterial);
            numberMaterialState.RuntimeMaterial.renderQueue = numberMaterial.renderQueue;
            numberMaterialState.RuntimeMaterial.SetFloat("_GoalClipEnabled", 1);
        }

        void AnimateGoalText()
        {
            if (!transitionActive) return;
            var settings = GoalNumberSettings;
            float growth = Mathf.Max(0, settings.growthSeconds);
            float textStart = firstReveal ? growth : 0;
            // The first text is completely absent throughout the badge growth.
            goalText.renderer.forceRenderingOff = transitionElapsed < textStart;
            if (transitionElapsed < textStart) return;
            if (!textAnimationStarted)
            {
                EnsureNumberAnimator();
                RefreshAnimatedTextMaterial(settings);
                TextAnimationPreset preset = transitionMorphPreset;
                var context = TextAnimationContext.Default.WithIntensity(transitionIntensity).WithDirection(Vector2.up);
                context.previousText = previousNumber;
                context.newText = goalText.text;
                if (firstReveal)
                {
                    if (!firstTextPreset)
                    {
                        firstTextPreset = ScriptableObject.CreateInstance<TextAnimationPreset>();
                        firstTextPreset.hideFlags = HideFlags.HideAndDontSave;
                        firstTextPreset.name = "Goal first multiplier SDF grow (generated)";
                    }
                    firstTextPreset.durationSeconds = Mathf.Max(0, settings.firstTextGrowSeconds);
                    firstTextPreset.characterStaggerSeconds = 0;
                    firstTextPreset.Modules.Clear();
                    firstTextPreset.Modules.Add(new SdfTextAnimationModule {
                        faceDilateFromOffset = -1.25f - baseFaceDilate,
                        faceDilateToOffset = 0,
                        outlineWidthFromOffset = -settings.outlineWidth,
                        curve = settings.firstTextGrowEasing
                    });
                    context = TextAnimationContext.Default;
                    preset = firstTextPreset;
                }
                textAnimationLength = preset ? numberAnimator.BeginManual(preset, context) : 0;
                textAnimationStarted = true;
            }
            numberAnimator.SampleManual(transitionElapsed - textStart);
            if (transitionElapsed >= Mathf.Max(growth, textStart + textAnimationLength))
            {
                transitionActive = false;
                numberAnimator.StopAll();
                goalText.renderer.forceRenderingOff = false;
            }
        }

        void StopGoalTransition()
        {
            transitionActive = textAnimationStarted = false;
            numberAnimator?.StopAll();
            if (goalText) goalText.renderer.forceRenderingOff = false;
        }

        public void PreviewGoalTransition(int from, int to)
        {
            if (Application.isPlaying) return;
            PreviewGoalMultiplier(from);
            BeginGoalTransition(from, to);
            previewMultiplier = Mathf.Max(1, to);
            RefreshGoalNumber();
        }

        // Deterministic samples support visual checks and frame capture without scoring.
        public void SampleGoalTransition(float seconds)
        {
            if (Application.isPlaying || !transitionActive) return;
            transitionElapsed = Mathf.Max(0, seconds);
            RefreshGoalNumber();
        }

#if UNITY_EDITOR
        void TickGoalPreview()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (!Application.isPlaying && transitionActive)
            {
                transitionElapsed += (float)(now - lastPreviewTick);
                RefreshGoalNumber();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
            lastPreviewTick = now;
        }
#endif

        void ReleaseGoalAnimation()
        {
            StopGoalTransition();
            ReleaseBadgeObject(firstTextPreset);
            firstTextPreset = null;
            numberAnimator = null;
            numberMaterialState = null;
        }
    }
}
