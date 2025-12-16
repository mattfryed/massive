using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BoxIntroOutro : MonoBehaviour
{
    [Header("Bars")]
    public Transform whitePivot;
    public Transform whiteBar;
    public Transform blackPivot;
    public Transform blackBar;

    [Tooltip("If true, bar scales assume pivots are placed at the left edge.")]
    public bool usePivotAnchoring = true;

    [Header("Timing")]
    public float inDuration = 0.30f;
    public float outDuration = 0.22f;

    [Tooltip("Black bar starts this many seconds after white bar.")]
    public float blackDelay = 0.04f;

    [Tooltip("Text starts after bars begin.")]
    public float textDelay = 0.10f;

    [Header("Easing")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Text Animation")]
    public TMP_Text[] texts;

    public enum TextAnimMode { None, Alpha, Typewriter, SdfGrow, SdfGrowAndAlpha }
    public TextAnimMode textMode = TextAnimMode.SdfGrowAndAlpha;

    [Tooltip("If using Typewriter: duration for revealing characters.")]
    public float typewriterDuration = 0.22f;

    [Tooltip("If using SDF grow: start/end values for TMP _FaceDilate.")]
    public float faceDilateFrom = -1.0f;
    public float faceDilateTo = 0.0f;

    // cached
    Vector3 _whiteScale0, _whiteScale1;
    Vector3 _blackScale0, _blackScale1;

    Vector3 _whitePos0, _whitePos1;
    Vector3 _blackPos0, _blackPos1;

    // TMP material instances
    readonly List<Material> _textMats = new();

    void Awake()
    {
        CacheBars();
        CacheTextMaterials();
        SetInstantHidden();
    }

    void CacheBars()
    {
        if (whiteBar != null)
        {
            _whiteScale1 = whiteBar.localScale;
            _whiteScale0 = new Vector3(0f, _whiteScale1.y, _whiteScale1.z);

            _whitePos1 = whiteBar.localPosition;
            _whitePos0 = _whitePos1;
        }

        if (blackBar != null)
        {
            _blackScale1 = blackBar.localScale;
            _blackScale0 = new Vector3(0f, _blackScale1.y, _blackScale1.z);

            _blackPos1 = blackBar.localPosition;
            _blackPos0 = _blackPos1;
        }
    }

    void CacheTextMaterials()
    {
        _textMats.Clear();
        if (texts == null) return;

        foreach (var t in texts)
        {
            if (t == null) continue;

            // Ensure a per-instance material so animating properties doesn't affect other text
            var inst = new Material(t.fontSharedMaterial);
            t.fontMaterial = inst;
            _textMats.Add(inst);
        }
    }

    public void SetInstantHidden()
    {
        SetBarsScale01(0f, 0f);
        SetTextProgress01(0f);
    }

    public void SetInstantShown()
    {
        SetBarsScale01(1f, 1f);
        SetTextProgress01(1f);
    }

public bool waitForTextOnIn = false; // if true, PlayIn blocks until text finishes

public IEnumerator PlayIn()
{
    // Start text tween independently so it can outlast inDuration.
    float textDur = Mathf.Max(0.001f, typewriterDuration);
    IEnumerator textTween = TextTween01(0f, 1f, textDur);

    if (textMode != TextAnimMode.None)
    {
        if (waitForTextOnIn)
            StartCoroutine(textTween); // we'll wait later by tracking progress
        else
            StartCoroutine(textTween);
    }

    float t = 0f;

    while (t < inDuration)
    {
        t += Time.deltaTime;

        float whiteT = Mathf.Clamp01(t / inDuration);
        float blackT = Mathf.Clamp01((t - blackDelay) / inDuration);

        SetBarsScale01(ease.Evaluate(whiteT), ease.Evaluate(blackT));

        yield return null;
    }

    SetBarsScale01(1f, 1f);

    // Optionally wait until the text tween finishes (rarely needed)
    if (waitForTextOnIn && textMode != TextAnimMode.None)
    {
        // wait by polling our progress (TextTween drives SetTextProgress01 internally)
        yield return new WaitForSeconds(textDur);
    }
}

IEnumerator TextTween01(float from, float to, float duration)
{
    // Ensure we're starting from a known state
    SetTextProgress01(from);

    float t = 0f;
    duration = Mathf.Max(0.001f, duration);

    while (t < duration)
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / duration);
        float v = Mathf.Lerp(from, to, ease.Evaluate(u));
        SetTextProgress01(v);
        yield return null;
    }

    SetTextProgress01(to);
}


    public IEnumerator PlayOut()
    {
        // Reverse: text out, then black, then white.
        float t = 0f;

        while (t < outDuration)
        {
            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / outDuration);

            // reverse ordering with slight staging
            float textU  = Mathf.Clamp01((u - 0.0f) / 0.55f);
            float blackU = Mathf.Clamp01((u - 0.15f) / 0.60f);
            float whiteU = Mathf.Clamp01((u - 0.25f) / 0.75f);

            SetTextProgress01(1f - ease.Evaluate(textU));
            SetBarsScale01(1f - ease.Evaluate(whiteU), 1f - ease.Evaluate(blackU));

            yield return null;
        }

        SetBarsScale01(0f, 0f);
        SetTextProgress01(0f);
    }

    void SetBarsScale01(float white01, float black01)
    {
        if (whiteBar != null)
        {
            ApplyBar(whiteBar, _whiteScale0, _whiteScale1, _whitePos0, _whitePos1, white01, whitePivot);
        }

        if (blackBar != null)
        {
            ApplyBar(blackBar, _blackScale0, _blackScale1, _blackPos0, _blackPos1, black01, blackPivot);
        }
    }

    void ApplyBar(Transform bar, Vector3 s0, Vector3 s1, Vector3 p0, Vector3 p1, float t, Transform pivot)
    {
        // scale x from 0..1
        Vector3 s = Vector3.Lerp(s0, s1, t);
        bar.localScale = s;

        if (usePivotAnchoring && pivot != null)
        {
            // pivot is physically at left edge, so scale is enough
            return;
        }

        // no pivot: shift bar so its left edge stays put while scaling
        // This assumes bar's localPosition is its center at full scale.
        float halfWFull = s1.x * 0.5f;
        float halfWNow  = s.x  * 0.5f;

        Vector3 pos = p1;
        pos.x = p1.x - (halfWFull - halfWNow);
        bar.localPosition = pos;
    }

    void SetTextProgress01(float t01)
    {
        if (texts == null) return;

        for (int i = 0; i < texts.Length; i++)
        {
            var tmp = texts[i];
            if (tmp == null) continue;

            switch (textMode)
            {
                case TextAnimMode.None:
                    break;

                case TextAnimMode.Alpha:
                    SetTMPAlpha(tmp, t01);
                    break;

                case TextAnimMode.Typewriter:
                    Typewrite(tmp, t01);
                    break;

                case TextAnimMode.SdfGrow:
                    SdfGrow(tmp, t01, false);
                    break;

                case TextAnimMode.SdfGrowAndAlpha:
                    SdfGrow(tmp, t01, true);
                    break;
            }
        }
    }

    void SetTMPAlpha(TMP_Text tmp, float a)
    {
        var c = tmp.color;
        c.a = a;
        tmp.color = c;
    }

    void Typewrite(TMP_Text tmp, float t01)
    {
        // Ensure textInfo is valid
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;
        tmp.maxVisibleCharacters = Mathf.RoundToInt(Mathf.Lerp(0, total, t01));
        SetTMPAlpha(tmp, 1f); // keep opaque
    }

    void SdfGrow(TMP_Text tmp, float t01, bool alsoAlpha)
    {
        // TMP SDF shaders generally support _FaceDilate
        float dilate = Mathf.Lerp(faceDilateFrom, faceDilateTo, t01);

        var mat = tmp.fontMaterial; // instance
        if (mat != null && mat.HasProperty("_FaceDilate"))
            mat.SetFloat("_FaceDilate", dilate);

        if (alsoAlpha)
            SetTMPAlpha(tmp, t01);
        else
            SetTMPAlpha(tmp, 1f);
    }
}
