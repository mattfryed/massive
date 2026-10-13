using Massive.Settings;
using TMPro;
using UnityEngine;

namespace Massive.Scoring
{
    public sealed partial class TeamAmplifierPresenter : ISharedSettingsConsumer
    {
        [SerializeField] private bool useSharedSettings = true;
        [SerializeField] private TeamAmplifierGoalNumberSettings goalNumber = new TeamAmplifierGoalNumberSettings();
        [Tooltip("Optional explicit binding. Otherwise resolves this team's score void in the same scene.")]
        [SerializeField] private ScoreVoidMetaballsVisual goalSurface;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public SharedSettingsProfile SharedSettingsAsset => SharedSettingsRuntime.Load<AmplifierSharedProfile>();
        public string SharedSettingsGroup => "goalNumber";
        public TeamAmplifierGoalNumberSettings GoalNumberSettings
        {
            get
            {
                var profile = SharedSettingsRuntime.Resolve<AmplifierSharedProfile>(this, useSharedSettings);
                return profile != null ? profile.goalNumber : goalNumber;
            }
        }

        int displayedMultiplier = 1, previewMultiplier;
        Renderer[] legacyRenderers;
        bool[] legacyHidden;
        TextMeshPro goalText;
        Transform textPlacement;
        Material numberMaterial;
        TMP_FontAsset currentFont;
        string currentPrefix;
        int currentMultiplier;
        Bounds glyphBounds;
        float nextResolveTime;
        public TMP_Text GoalNumberText => goalText;
        public Bounds GoalNumberGlyphBounds => glyphBounds;
        public int DisplayedMultiplier => !Application.isPlaying && previewMultiplier > 0 ? previewMultiplier : displayedMultiplier;

        void InitializeGoalNumber()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += TickGoalPreview;
#endif
            displayedMultiplier = 1;
            previewMultiplier = 0;
            // Suppression is temporary, not a scene/prefab edit. Keep the old serialized
            // text reference for compatibility with existing layouts and setup tools.
            legacyRenderers = GetComponentsInChildren<Renderer>(true);
            legacyHidden = new bool[legacyRenderers.Length];
            for (int i = 0; i < legacyRenderers.Length; i++)
            {
                legacyHidden[i] = legacyRenderers[i].forceRenderingOff;
                legacyRenderers[i].forceRenderingOff = true;
            }
            nextResolveTime = 0;
            RefreshGoalNumber();
        }

        void LateUpdate()
        {
            if (Application.isPlaying && demonstrationMultiplier != null)
            {
                int next = demonstrationMultiplier();
                if (next != displayedMultiplier) SetMultiplier(next, true);
            }
            if (Application.isPlaying && transitionActive) transitionElapsed += Time.unscaledDeltaTime;
            RefreshGoalNumber();
        }

        public void PreviewGoalMultiplier(int multiplier)
        {
            if (Application.isPlaying) return;
            StopGoalTransition();
            previewMultiplier = Mathf.Max(0, multiplier);
            RefreshGoalNumber();
        }

        public void RefreshGoalNumber()
        {
            if (!isActiveAndEnabled) return;
            // Layout controllers can replace/hide a goal while switching a scene layout.
            if (!goalSurface && Time.realtimeSinceStartup >= nextResolveTime)
            {
                nextResolveTime = Time.realtimeSinceStartup + 1f;
                foreach (var candidate in FindObjectsByType<ScoreVoidMetaballsVisual>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene && candidate.TeamID == teamID)
                    { goalSurface = candidate; break; }
            }
            var settings = GoalNumberSettings;
            bool visible = settings != null && settings.enabled && DisplayedMultiplier > 1 && goalSurface &&
                goalSurface.isActiveAndEnabled && (goalSurface.ContainerBoundary || goalSurface.EllipticalContainer);
            if (!visible) { StopGoalTransition(); if (textPlacement) textPlacement.gameObject.SetActive(false); SetBadgeVisible(false); return; }
            var font = settings.font ? settings.font : multiplierText ? multiplierText.font : TMP_Settings.defaultFontAsset;
            if (!font) return;
            EnsureGoalText(font);
            if (!goalText) return;
            textPlacement.gameObject.SetActive(true);

            if (DisplayedMultiplier != currentMultiplier || settings.prefix != currentPrefix || currentFont != font)
            {
                currentMultiplier = DisplayedMultiplier; currentPrefix = settings.prefix; currentFont = font;
                goalText.text = settings.prefix + DisplayedMultiplier;
                goalText.ForceMeshUpdate();
                // Center the visible glyph, not the font's baseline or advance box.
                glyphBounds = VisibleGlyphBounds(goalText);
            }
            GetGoalFrame(out var center, out var right, out var up, out float depth,
                out float halfHeight, out float side, out float seamRatio, out float ringWidth);
            var layout = EvaluateGoalLayout();
            float badgeDiameter = halfHeight * 2 * layout.x;
            float scale = halfHeight * 2 * layout.z / Mathf.Max(.001f, glyphBounds.size.y);
            textPlacement.rotation = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            var parentScale = transform.lossyScale;
            textPlacement.localScale = new Vector3(scale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), scale / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
            textPlacement.position = center + right * (side * depth * settings.outwardOffset + halfHeight * 2 * layout.w - glyphBounds.center.x * scale)
                + up * (halfHeight * settings.verticalOffset - glyphBounds.center.y * scale);
            goalText.color = teamID == 1 ? settings.lightColor : settings.darkColor;
            numberMaterial.SetColor("_OutlineColor", settings.outlineColor);
            numberMaterial.SetFloat("_OutlineWidth", settings.outlineWidth);
            numberMaterial.SetColor("_FaceColor", Color.white);
            // World-space clipping remains stationary when font size/offset changes.
            numberMaterial.SetVector("_GoalOrigin", center);
            numberMaterial.SetVector("_GoalRight", new Vector4(right.x, right.y, right.z, side));
            numberMaterial.SetVector("_GoalUp", up);
            numberMaterial.SetVector("_GoalShape", new Vector4(depth, halfHeight, seamRatio,
                Mathf.Max(0, settings.ringInset) + ringWidth * .5f));
            RefreshBadge(center, right, up, depth, halfHeight, side, badgeDiameter, halfHeight * 2 * layout.y, settings);
            RefreshAnimatedTextMaterial(settings);
            AnimateGoalText();
        }

        void EnsureGoalText(TMP_FontAsset font)
        {
            if (!goalText)
            {
                var shader = Resources.Load<Shader>("TeamAmplifierGoalNumber");
                if (!shader) return;
                var root = new GameObject("Team amplifier goal number (generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
                root.transform.SetParent(transform, false);
                textPlacement = root.transform;
                var textObject = new GameObject("Multiplier text", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
                textObject.transform.SetParent(textPlacement, false);
                goalText = textObject.AddComponent<TextMeshPro>();
                goalText.fontSize = 36;
                goalText.alignment = TextAlignmentOptions.Center;
                goalText.textWrappingMode = TextWrappingModes.NoWrap;
                goalText.overflowMode = TextOverflowModes.Overflow;
                goalText.rectTransform.sizeDelta = new Vector2(100, 100);
                goalText.richText = false;
                goalText.raycastTarget = false;
                numberMaterial = new Material(shader) { name = "Team goal number (generated)", hideFlags = HideFlags.HideAndDontSave };
            }
            if (currentFont == font) return;
            goalText.font = font;
            numberMaterial.CopyPropertiesFromMaterial(font.material);
            numberMaterial.SetFloat("_GoalClipEnabled", 1);
            baseFaceDilate = numberMaterial.GetFloat("_FaceDilate");
            numberMaterial.renderQueue = 2992; // Core -> white disc -> number -> arena border.
            numberMaterial.shaderKeywords = new[] { "OUTLINE_ON" };
            numberMaterial.SetFloat("_OutlineWidth", .12f);
            goalText.fontSharedMaterial = numberMaterial;
            if (numberMaterialState)
            {
                StopGoalTransition();
                numberMaterialState.RuntimeMaterial.CopyPropertiesFromMaterial(numberMaterial);
                numberMaterialState.CapturePersistentFromCurrentMaterial();
            }
            goalText.extraPadding = true;
            goalText.UpdateMeshPadding();
        }

        static Bounds VisibleGlyphBounds(TMP_Text text)
        {
            // TMP.textBounds uses font ascenders/descenders and advance widths,
            // including empty line space. Glyph metrics exclude that padding.
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0);
            bool found = false;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                var c = text.textInfo.characterInfo[i];
                if (!c.isVisible || c.textElement == null) continue;
                var m = c.textElement.glyph.metrics;
                float left = c.origin + m.horizontalBearingX * c.scale;
                float top = c.baseLine + m.horizontalBearingY * c.scale;
                min = Vector3.Min(min, new Vector3(left, top - m.height * c.scale, 0));
                max = Vector3.Max(max, new Vector3(left + m.width * c.scale, top, 0));
                found = true;
            }
            return found ? new Bounds((min + max) * .5f, max - min) : text.textBounds;
        }

        void GetGoalFrame(out Vector3 center, out Vector3 right, out Vector3 up,
            out float depth, out float halfHeight, out float side, out float seamRatio, out float ringWidth)
        {
            right = goalSurface.transform.right.normalized;
            up = goalSurface.transform.forward.normalized;
            side = goalSurface.halfPlane == ScoreVoidMetaballsVisual.HalfPlane.NegativeX ? -1 : 1;
            var ellipse = goalSurface.EllipticalContainer;
            var ring = goalSurface.ContainerBoundary;
            if (ellipse && ellipse.arena)
            {
                center = ellipse.MapDiscPoint(Vector2.zero);
                depth = ellipse.DepthWorld; halfHeight = ellipse.HalfHeightWorld; seamRatio = ellipse.SeamRatio;
            }
            else
            {
                center = ring.transform.position;
                depth = halfHeight = .5f * (ring.transform.TransformVector(Vector3.right * ring.Radius).magnitude +
                    ring.transform.TransformVector(Vector3.up * ring.Radius).magnitude);
                seamRatio = 0;
            }
            ringWidth = ring ? ring.Thickness * halfHeight / Mathf.Max(.001f, ring.Radius) : .04f;
        }

        void ReleaseGoalNumber()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= TickGoalPreview;
#endif
            ReleaseGoalAnimation();
            ReleaseBadge();
            if (legacyRenderers != null) for (int i = 0; i < legacyRenderers.Length; i++)
                if (legacyRenderers[i]) legacyRenderers[i].forceRenderingOff = legacyHidden[i];
            legacyRenderers = null; legacyHidden = null;
            if (textPlacement) { if (Application.isPlaying) Destroy(textPlacement.gameObject); else DestroyImmediate(textPlacement.gameObject); }
            if (numberMaterial) { if (Application.isPlaying) Destroy(numberMaterial); else DestroyImmediate(numberMaterial); }
            goalText = null; textPlacement = null; numberMaterial = null; currentFont = null; currentPrefix = null; currentMultiplier = 0;
        }
    }
}
