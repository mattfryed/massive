using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TMPTextTransition : MonoBehaviour
{
    [Flags]
    public enum Effects
    {
        None          = 0,
        Alpha         = 1 << 0,
        Typewriter    = 1 << 1,
        SdfGrow       = 1 << 2,
        SpacingShrink = 1 << 3
    }

    public enum EasingMode
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut,
        Smoothstep,
        Smootherstep,
        Custom
    }

    [Header("Targets")]
    [Tooltip("Leave empty to auto-find TMP_Text on this GameObject (recommended for 1 GO per text).")]
    [SerializeField] private TMP_Text[] targets;

    [SerializeField] private bool autoFindTargets = true;
    [SerializeField] private bool includeChildren = false;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Auto Play")]
    [SerializeField] private bool autoPlayInOnEnable = true;
    [SerializeField] private bool autoPlayInOnce = false;

    [Tooltip("Guarantees the text is never visible before the IN animation begins (covers first frame + whole inDelay).")]
    [SerializeField] private bool hideBeforeFirstFrame = true;

    [SerializeField] private bool useUnscaledTime = true;

    [Header("Timings")]
    [Min(0f)] [SerializeField] private float inDelay = 0f;
    [Min(0f)] [SerializeField] private float inDuration = 0.35f;
    [Min(0f)] [SerializeField] private float outDuration = 0.25f;

    [Header("Stagger (per target)")]
    [Min(0f)] [SerializeField] private float inTargetStagger = 0f;
    [Min(0f)] [SerializeField] private float outTargetStagger = 0f;

    [Header("Easing (separate for IN/OUT)")]
    [SerializeField] private EasingMode inEasing = EasingMode.EaseOut;
    [SerializeField] private AnimationCurve inCustomEasing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private EasingMode outEasing = EasingMode.EaseIn;
    [SerializeField] private AnimationCurve outCustomEasing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Effects (checkboxes)")]
    [SerializeField] private Effects inEffects = Effects.Alpha | Effects.Typewriter;
    [SerializeField] private Effects outEffects = Effects.Alpha;

    [Header("Typewriter + Alpha behavior")]
    [Tooltip("If Typewriter and Alpha are both enabled, fade letters individually instead of fading the whole object.")]
    [SerializeField] private bool perLetterAlphaWhenTypewriter = true;

    [Tooltip("How many 'letter slots' each character uses to fade 0->1 (1 = fades across one character interval; 0 = pop).")]
    [Min(0f)] [SerializeField] private float perLetterFadeWidthChars = 1f;

    [Tooltip("Apply IN/OUT easing to the per-letter fade ramp (not the sequencing).")]
    [SerializeField] private bool easePerLetterFadeRamp = true;

    [Tooltip("Typewriter during OUT: if true, letters disappear in reverse order. If false, letters remain visible and only other OUT effects apply.")]
    [SerializeField] private bool reverseTypewriterOnOut = true;

    [Header("Spacing Shrink (from/to)")]
    [Tooltip("Multiplier applied to TMP_Text.characterSpacing at the start.")]
    [SerializeField] private float spacingFromMultiplier = 1.25f;

    [Tooltip("Multiplier applied to TMP_Text.characterSpacing at the end.")]
    [SerializeField] private float spacingToMultiplier = 1.0f;

    [Header("SDF Grow (from/to)")]
    [Tooltip("Offset added to base _FaceDilate at the start (often negative).")]
    [SerializeField] private float sdfFromOffset = -0.8f;

    [Tooltip("Offset added to base _FaceDilate at the end.")]
    [SerializeField] private float sdfToOffset = 0.0f;

    [Header("Completion / Cleanup")]
    [SerializeField] private bool ensureHiddenAtOutEnd = true;
    [SerializeField] private bool disableGameObjectAfterOut = false;

    [Header("Events")]
    public UnityEvent onInStart;
    public UnityEvent onInComplete;
    public UnityEvent onOutStart;
    public UnityEvent onOutComplete;

    private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");

    private class TargetCache
    {
        public TMP_Text text;

        public Color baseColor;
        public float baseCharSpacing;

        public Material originalSharedMaterial;
        public Material instancedMaterial;

        public float baseFaceDilate;
        public int cachedCharCount;

        public Color32[][] baseColorsByMesh;
        public bool baseColorsValid;
        public bool modifiedVertexColors;

        public CanvasRenderer canvasRenderer; // TMP UGUI
        public Renderer renderer;             // TMP 3D
        public bool baseCull;
        public bool baseRendererEnabled;
    }

    private readonly List<TargetCache> _cache = new();
    private Coroutine _routine;
    private bool _hasAutoPlayed;
    private bool _skipRequested;

    void Awake()
    {
        if (!Application.isPlaying) return;
        RebuildCache();

        if (hideBeforeFirstFrame && autoPlayInOnEnable)
            HardHide(true);
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (_cache.Count == 0)
            RebuildCache();

        bool willAutoPlay = autoPlayInOnEnable && (!autoPlayInOnce || !_hasAutoPlayed);

        if (hideBeforeFirstFrame && willAutoPlay)
            HardHide(true);

        if (willAutoPlay)
        {
            _hasAutoPlayed = true;
            PlayIn();
        }
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        StopCurrent();
    }

    void OnDestroy()
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null) continue;

            if (c.text != null && c.originalSharedMaterial != null)
                c.text.fontSharedMaterial = c.originalSharedMaterial;

            if (c.instancedMaterial != null)
                Destroy(c.instancedMaterial);

            // restore rendering state
            if (c.canvasRenderer != null) c.canvasRenderer.cull = c.baseCull;
            if (c.renderer != null) c.renderer.enabled = c.baseRendererEnabled;
        }
        _cache.Clear();
    }

    [ContextMenu("Play IN")]
    public void PlayIn()
    {
        if (!Application.isPlaying) return;
        EnsureCache();
        StartRoutine(InRoutine());
    }

    [ContextMenu("Play OUT")]
    public void PlayOut()
    {
        if (!Application.isPlaying) return;
        EnsureCache();
        StartRoutine(OutRoutine());
    }

    public void Skip()
    {
        if (!Application.isPlaying) return;
        if (_routine == null) return;
        _skipRequested = true;
    }

    private void EnsureCache()
    {
        if (_cache.Count == 0) RebuildCache();
    }

    private void RebuildCache()
    {
        StopCurrent();
        _cache.Clear();

        TMP_Text[] found = targets;

        if (autoFindTargets && (found == null || found.Length == 0))
        {
            found = includeChildren
                ? GetComponentsInChildren<TMP_Text>(includeInactiveChildren)
                : GetComponents<TMP_Text>();
        }

        if (found == null) found = Array.Empty<TMP_Text>();

        bool needsSdf = (inEffects | outEffects).HasFlag(Effects.SdfGrow);

        for (int i = 0; i < found.Length; i++)
        {
            var t = found[i];
            if (t == null) continue;

            var cr = t.canvasRenderer;                 // exists for TMP UGUI
            var r = t.GetComponent<Renderer>();        // exists for TMP 3D

            var c = new TargetCache
            {
                text = t,
                baseColor = t.color,
                baseCharSpacing = t.characterSpacing,
                originalSharedMaterial = t.fontSharedMaterial,

                canvasRenderer = cr,
                renderer = r,
                baseCull = cr != null && cr.cull,
                baseRendererEnabled = r != null && r.enabled
            };

            if (needsSdf && c.originalSharedMaterial != null)
            {
                c.instancedMaterial = new Material(c.originalSharedMaterial);
                t.fontMaterial = c.instancedMaterial;

                c.baseFaceDilate = c.instancedMaterial.HasProperty(FaceDilateId)
                    ? c.instancedMaterial.GetFloat(FaceDilateId)
                    : 0f;
            }

            _cache.Add(c);
        }

        RefreshTextInfoAndBaseColors(force: true);
    }

    private void RefreshTextInfoAndBaseColors(bool force)
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null || c.text == null) continue;

            if (force || c.text.havePropertiesChanged)
                c.text.ForceMeshUpdate();

            c.cachedCharCount = c.text.textInfo.characterCount;

            var ti = c.text.textInfo;
            int meshCount = ti.meshInfo.Length;

            c.baseColorsByMesh = new Color32[meshCount][];
            for (int m = 0; m < meshCount; m++)
            {
                var colors = ti.meshInfo[m].colors32;
                c.baseColorsByMesh[m] = colors != null ? (Color32[])colors.Clone() : Array.Empty<Color32>();
            }

            c.baseColorsValid = true;
            c.modifiedVertexColors = false;
        }
    }

    private void EnsureBaseColorsUpToDate(TargetCache c)
    {
        if (c == null || c.text == null) return;

        bool recache = false;

        // TMP might rebuild meshes after first frame / after text is set by other scripts.
        if (!c.baseColorsValid || c.baseColorsByMesh == null || c.text.havePropertiesChanged)
            recache = true;
        else
        {
            var ti = c.text.textInfo;
            if (ti.meshInfo.Length != c.baseColorsByMesh.Length)
                recache = true;
            else
            {
                for (int m = 0; m < ti.meshInfo.Length; m++)
                {
                    var live = ti.meshInfo[m].colors32;
                    var baseCols = c.baseColorsByMesh[m];
                    if (live == null || baseCols == null || live.Length != baseCols.Length)
                    {
                        recache = true;
                        break;
                    }
                }
            }
        }

        if (recache)
        {
            c.text.ForceMeshUpdate();
            c.cachedCharCount = c.text.textInfo.characterCount;

            var ti = c.text.textInfo;
            int meshCount = ti.meshInfo.Length;

            c.baseColorsByMesh = new Color32[meshCount][];
            for (int m = 0; m < meshCount; m++)
            {
                var colors = ti.meshInfo[m].colors32;
                c.baseColorsByMesh[m] = colors != null ? (Color32[])colors.Clone() : Array.Empty<Color32>();
            }

            c.baseColorsValid = true;
            c.modifiedVertexColors = false;
        }
    }

    private void StartRoutine(IEnumerator routine)
    {
        StopCurrent();
        _routine = StartCoroutine(routine);
    }

    private void StopCurrent()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        _skipRequested = false;
    }

    // Hard hide that works for both TMP UGUI and TMP 3D.
    private void HardHide(bool hidden)
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null || c.text == null) continue;

            // Keep baseline RGB; only clamp visibility.
            var col = c.text.color;
            col.a = hidden ? 0f : c.baseColor.a;
            c.text.color = col;

            if (c.canvasRenderer != null)
                c.canvasRenderer.cull = hidden;

            if (c.renderer != null)
                c.renderer.enabled = hidden ? false : c.baseRendererEnabled;
        }
    }

    private IEnumerator InRoutine()
    {
        _skipRequested = false;

        // Cache *after* any other scripts may have assigned text in Awake/OnEnable.
        RefreshTextInfoAndBaseColors(force: true);

        // Apply baseline first, then hard-hide.
        ApplyHiddenBaseline(inEffects);

        if (hideBeforeFirstFrame)
            HardHide(true);

        onInStart?.Invoke();

        // During delay, keep enforcing hard-hide because TMP can rebuild.
        if (inDelay > 0f)
        {
            float t0 = Now();
            while (Now() - t0 < inDelay)
            {
                if (hideBeforeFirstFrame)
                    HardHide(true);
                yield return null;
            }
        }

        // Let TMP settle one frame (still hidden).
        yield return null;

        // Re-cache & re-apply baseline right before revealing.
        RefreshTextInfoAndBaseColors(force: true);
        ApplyHiddenBaseline(inEffects);

        // Reveal.
        if (hideBeforeFirstFrame)
            HardHide(false);

        if (inDuration <= 0f)
        {
            ApplyStateAllTargets(1f, 1f, isIn: true, inEffects);
            _routine = null;
            onInComplete?.Invoke();
            yield break;
        }

        float start = Now();
        float total = inDuration + Mathf.Max(0, _cache.Count - 1) * inTargetStagger;

        while (!_skipRequested)
        {
            float elapsed = Now() - start;
            if (elapsed >= total) break;

            ApplyStatePerTarget(elapsed, inDuration, inTargetStagger, isIn: true, inEffects, inEasing, inCustomEasing);
            yield return null;
        }

        ApplyStateAllTargets(1f, 1f, isIn: true, inEffects);

        _skipRequested = false;
        _routine = null;
        onInComplete?.Invoke();
    }

    private IEnumerator OutRoutine()
    {
        _skipRequested = false;

        // Ensure visible while animating OUT
        if (hideBeforeFirstFrame)
            HardHide(false);

        RefreshTextInfoAndBaseColors(force: true);

        onOutStart?.Invoke();

        yield return null;

        if (outDuration <= 0f)
        {
            ApplyStateAllTargets(0f, 1f, isIn: false, outEffects);
            if (ensureHiddenAtOutEnd) ForceHiddenHard();

            _routine = null;
            onOutComplete?.Invoke();

            if (disableGameObjectAfterOut)
                gameObject.SetActive(false);

            yield break;
        }

        float start = Now();
        float total = outDuration + Mathf.Max(0, _cache.Count - 1) * outTargetStagger;

        while (!_skipRequested)
        {
            float elapsed = Now() - start;
            if (elapsed >= total) break;

            ApplyStatePerTarget(elapsed, outDuration, outTargetStagger, isIn: false, outEffects, outEasing, outCustomEasing);
            yield return null;
        }

        ApplyStateAllTargets(0f, 1f, isIn: false, outEffects);
        if (ensureHiddenAtOutEnd) ForceHiddenHard();

        _skipRequested = false;
        _routine = null;
        onOutComplete?.Invoke();

        if (disableGameObjectAfterOut)
            gameObject.SetActive(false);
    }

    private void ApplyHiddenBaseline(Effects effectsToPrime)
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null || c.text == null) continue;

            if (effectsToPrime.HasFlag(Effects.SpacingShrink))
                c.text.characterSpacing = c.baseCharSpacing * spacingFromMultiplier;
            else
                c.text.characterSpacing = c.baseCharSpacing;

            if (effectsToPrime.HasFlag(Effects.SdfGrow))
                SetFaceDilate(c, c.baseFaceDilate + sdfFromOffset);
            else
                SetFaceDilate(c, c.baseFaceDilate);

            bool perLetter = effectsToPrime.HasFlag(Effects.Typewriter)
                             && effectsToPrime.HasFlag(Effects.Alpha)
                             && perLetterAlphaWhenTypewriter;

            if (perLetter)
            {
                EnsureBaseColorsUpToDate(c);

                c.text.maxVisibleCharacters = int.MaxValue;
                SetAlpha(c, 1f);
                ApplyPerLetterAlpha(c, uLinear01: 0f, isIn: true);
            }
            else
            {
                if (effectsToPrime.HasFlag(Effects.Alpha))
                    SetAlpha(c, 0f);

                if (effectsToPrime.HasFlag(Effects.Typewriter))
                    c.text.maxVisibleCharacters = 0;
                else
                    c.text.maxVisibleCharacters = int.MaxValue;

                RestoreVertexColorsIfNeeded(c);
            }
        }
    }

    private void ApplyStatePerTarget(
        float elapsed,
        float duration,
        float stagger,
        bool isIn,
        Effects effects,
        EasingMode easeMode,
        AnimationCurve customCurve)
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null || c.text == null) continue;

            float localElapsed = elapsed - i * stagger;

            float uLinear = (duration <= 0f) ? 1f : Mathf.Clamp01(localElapsed / duration);
            float uEased = Ease01(uLinear, easeMode, customCurve);
            float pDirectional = isIn ? uEased : (1f - uEased);

            ApplyStateSingle(c, pDirectional, uLinear, isIn, effects);
        }
    }

    private void ApplyStateAllTargets(float pDirectional, float uLinear, bool isIn, Effects effects)
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null || c.text == null) continue;
            ApplyStateSingle(c, pDirectional, uLinear, isIn, effects);
        }
    }

    private void ApplyStateSingle(TargetCache c, float pDirectional, float uLinear, bool isIn, Effects effects)
    {
        bool hasTypewriter = effects.HasFlag(Effects.Typewriter);
        bool hasAlpha = effects.HasFlag(Effects.Alpha);

        if (effects.HasFlag(Effects.SpacingShrink))
        {
            float from = c.baseCharSpacing * spacingFromMultiplier;
            float to = c.baseCharSpacing * spacingToMultiplier;
            c.text.characterSpacing = Mathf.LerpUnclamped(from, to, pDirectional);
        }
        else
        {
            c.text.characterSpacing = c.baseCharSpacing;
        }

        if (effects.HasFlag(Effects.SdfGrow))
        {
            float from = c.baseFaceDilate + sdfFromOffset;
            float to = c.baseFaceDilate + sdfToOffset;
            SetFaceDilate(c, Mathf.LerpUnclamped(from, to, pDirectional));
        }
        else
        {
            SetFaceDilate(c, c.baseFaceDilate);
        }

        bool perLetter = hasTypewriter && hasAlpha && perLetterAlphaWhenTypewriter;

        if (perLetter)
        {
            EnsureBaseColorsUpToDate(c);

            SetAlpha(c, 1f);
            c.text.maxVisibleCharacters = int.MaxValue;

            if (!isIn && !reverseTypewriterOnOut)
            {
                ApplyPerLetterAlpha(c, uLinear01: 1f, isIn: true); // fully visible
                return;
            }

            ApplyPerLetterAlpha(c, uLinear01: uLinear, isIn: isIn);
            return;
        }

        RestoreVertexColorsIfNeeded(c);

        if (hasAlpha)
            SetAlpha(c, pDirectional);
        else if (isIn)
            SetAlpha(c, 1f);

        if (hasTypewriter)
        {
            int charCount = Mathf.Max(0, c.cachedCharCount);

            if (!isIn && !reverseTypewriterOnOut)
                c.text.maxVisibleCharacters = int.MaxValue;
            else
                c.text.maxVisibleCharacters = Mathf.Clamp(Mathf.CeilToInt(pDirectional * charCount), 0, charCount);
        }
        else
        {
            c.text.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void ApplyPerLetterAlpha(TargetCache c, float uLinear01, bool isIn)
    {
        if (!c.baseColorsValid || c.baseColorsByMesh == null)
            return;

        var text = c.text;
        var ti = text.textInfo;
        int charCount = Mathf.Max(0, ti.characterCount);
        if (charCount == 0)
            return;

        float u = Mathf.Clamp01(uLinear01);

        float span01 = (perLetterFadeWidthChars <= 0.0001f) ? 0f : (perLetterFadeWidthChars / charCount);
        bool pop = span01 <= 0.000001f;

        for (int i = 0; i < charCount; i++)
        {
            var ch = ti.characterInfo[i];
            if (!ch.isVisible) continue;

            float start01 = isIn
                ? (i / (float)charCount)
                : ((charCount - 1 - i) / (float)charCount);

            float a;
            if (pop)
            {
                bool on = u >= start01;
                a = isIn ? (on ? 1f : 0f) : (on ? 0f : 1f);
            }
            else
            {
                float local = (u - start01) / span01;
                float ramp = Mathf.Clamp01(local);

                if (easePerLetterFadeRamp)
                {
                    ramp = isIn
                        ? Ease01(ramp, inEasing, inCustomEasing)
                        : Ease01(ramp, outEasing, outCustomEasing);
                }

                a = isIn ? ramp : (1f - ramp);
            }

            int meshIndex = ch.materialReferenceIndex;
            int v = ch.vertexIndex;

            var live = ti.meshInfo[meshIndex].colors32;
            var baseCols = c.baseColorsByMesh[meshIndex];

            // SAFETY: avoid exceptions if TMP has rebuilt the mesh unexpectedly.
            if (live == null || baseCols == null) continue;
            if (v + 3 >= live.Length || v + 3 >= baseCols.Length) continue;

            for (int k = 0; k < 4; k++)
            {
                var bc = baseCols[v + k];
                bc.a = (byte)Mathf.RoundToInt(bc.a * a);
                live[v + k] = bc;
            }
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        c.modifiedVertexColors = true;
    }

    private void RestoreVertexColorsIfNeeded(TargetCache c)
    {
        if (!c.modifiedVertexColors) return;
        if (!c.baseColorsValid || c.baseColorsByMesh == null) return;

        EnsureBaseColorsUpToDate(c);

        var ti = c.text.textInfo;
        int meshCount = ti.meshInfo.Length;

        for (int m = 0; m < meshCount; m++)
        {
            var live = ti.meshInfo[m].colors32;
            var baseCols = c.baseColorsByMesh[m];
            if (live == null || baseCols == null) continue;

            int len = Mathf.Min(live.Length, baseCols.Length);
            Array.Copy(baseCols, live, len);
        }

        c.text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        c.modifiedVertexColors = false;
    }

    private void ForceHiddenHard()
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            var c = _cache[i];
            if (c == null || c.text == null) continue;

            SetAlpha(c, 0f);
            c.text.maxVisibleCharacters = 0;

            if (c.canvasRenderer != null) c.canvasRenderer.cull = true;
            if (c.renderer != null) c.renderer.enabled = false;
        }
    }

    private void SetAlpha(TargetCache c, float a01)
    {
        var col = c.baseColor;
        col.a = c.baseColor.a * Mathf.Clamp01(a01);
        c.text.color = col;
    }

    private void SetFaceDilate(TargetCache c, float value)
    {
        if (c.instancedMaterial == null) return;
        if (!c.instancedMaterial.HasProperty(FaceDilateId)) return;
        c.instancedMaterial.SetFloat(FaceDilateId, value);
    }

    private float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;

    private object Wait(float seconds) =>
        useUnscaledTime ? (object)new WaitForSecondsRealtime(seconds) : new WaitForSeconds(seconds);

    private static float Ease01(float t, EasingMode mode, AnimationCurve custom)
    {
        t = Mathf.Clamp01(t);

        switch (mode)
        {
            case EasingMode.Linear:       return t;
            case EasingMode.EaseIn:       return t * t;
            case EasingMode.EaseOut:      return 1f - (1f - t) * (1f - t);
            case EasingMode.EaseInOut:    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            case EasingMode.Smoothstep:   return t * t * (3f - 2f * t);
            case EasingMode.Smootherstep: return t * t * t * (t * (6f * t - 15f) + 10f);
            case EasingMode.Custom:       return custom != null ? Mathf.Clamp01(custom.Evaluate(t)) : t;
            default:                      return t;
        }
    }
}
