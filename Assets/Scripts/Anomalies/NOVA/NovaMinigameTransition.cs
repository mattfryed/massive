using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;


public class NovaMinigameTransition : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform playAreaRect;
    public RectTransform wipeCircleRect;   // white circle inside playAreaRect (masked)
    public RectTransform coreDotRect;      // black core dot (minigame center)
    Image _playAreaBg;


    [Header("Timing")]
    public float introDuration = 0.45f;
    public float outroDuration = 0.35f;

    [Header("Step Fractions")]
    [Range(0.05f, 0.9f)] public float introWipeFrac  = 0.45f;
    [Range(0.05f, 0.9f)] public float introCoreFrac  = 0.15f;
    [Range(0.05f, 0.9f)] public float introIconsFrac = 0.15f;
    [Range(0.05f, 0.9f)] public float introLabelsFrac= 0.10f;
    [Range(0.05f, 0.9f)] public float introRaysFrac  = 0.15f;

    [Header("Easing")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Wipe")]
    public float wipeCoverPadding = 1.15f; // >1 ensures circle covers corners

    public Behaviour worldCoreEffect; // assign NovaCoreGPU here if that's what you're seeing

    [Header("UI Boxes")]
    public BoxIntroOutro topBannerBox;
    public BoxIntroOutro[] lowerBoxes;   // e.g. middle, left, right score boxes
    public float boxStagger = 0.06f;

    public AnomalyUISequencer uiSequencer;

public void BindUISequencer(AnomalyUISequencer seq) => uiSequencer = seq;




    NovaCoreMinigame _minigame;
    public NovaCoreGPU coreGPU;


void Awake()
{
    _minigame = GetComponent<NovaCoreMinigame>();

    if (playAreaRect != null)
        _playAreaBg = playAreaRect.GetComponent<Image>();

    // Prefer explicit coreGPU; otherwise try to derive it.
    if (coreGPU == null)
        coreGPU = GetComponentInChildren<NovaCoreGPU>(true);

    if (coreGPU == null && worldCoreEffect is NovaCoreGPU gpu)
        coreGPU = gpu;
}


    public IEnumerator PlayIntro()
    {
        // Ensure GPU core starts hidden (prevents "pop at full size")
if (coreGPU != null)
{
    coreGPU.enabled = true;      // make sure Update runs
    coreGPU.SetReveal01(0f);     // hide (activeCount becomes 0)
}


if (worldCoreEffect != null && worldCoreEffect != coreGPU)
    worldCoreEffect.enabled = false;


        if (_minigame != null)
            _minigame.SetGameplayEnabled(false);

            // Make playAreaRect background invisible so the wipe is the only thing that "fills" white.
            if (_playAreaBg != null)
            {
                var c = _playAreaBg.color;
                c.a = 0f;
                _playAreaBg.color = c;
            }

            // Ensure wipe is behind everything else (background layer).
            if (wipeCircleRect != null)
                wipeCircleRect.SetAsFirstSibling();


        // Ensure initial states
        if (wipeCircleRect) { wipeCircleRect.gameObject.SetActive(true); wipeCircleRect.localScale = Vector3.zero; }
        if (coreDotRect != null)
        {
            coreDotRect.gameObject.SetActive(true);
            coreDotRect.SetAsLastSibling(); // ensure it renders above the wipe/background
            coreDotRect.localScale = Vector3.zero;
        }

        if (coreDotRect) coreDotRect.localScale = Vector3.zero;

        if (_minigame != null)
        {
            _minigame.SetIconsScale(0f);
            _minigame.SetLabelsAlpha(0f);
            _minigame.SetRayDotsScale01(0f);
        }

        float wipeTime, coreTime, iconTime, labelTime, rayTime;
        SplitTime(introDuration, introWipeFrac, introCoreFrac, introIconsFrac, introLabelsFrac, introRaysFrac,
                  out wipeTime, out coreTime, out iconTime, out labelTime, out rayTime);

                  // Force wipe circle into the masked play area center so RectMask2D doesn't clip it away.
            if (wipeCircleRect != null && playAreaRect != null)
            {
                // Make sure it's actually under the mask
                if (wipeCircleRect.parent != playAreaRect)
                    wipeCircleRect.SetParent(playAreaRect, false);

                wipeCircleRect.anchorMin = wipeCircleRect.anchorMax = new Vector2(0.5f, 0.5f);
                wipeCircleRect.pivot     = new Vector2(0.5f, 0.5f);

                // If your coreRect exists and is a child of playAreaRect, use that position; otherwise center at 0.
                wipeCircleRect.anchoredPosition = Vector2.zero;
            }


        // 1) Wipe circle expand to cover playAreaRect
        if (wipeCircleRect && playAreaRect)
        {
            float target = ComputeWipeTargetScale(playAreaRect, wipeCircleRect, wipeCoverPadding);
            yield return Tween01(wipeTime, t =>
            {
                float s = Mathf.Lerp(0f, target, ease.Evaluate(t));
                wipeCircleRect.localScale = Vector3.one * s;
            });

            // Leave wipeCircle ON as the background for the minigame.
            // Make sure playArea background stays transparent (or has no Image).
            if (_playAreaBg != null)
            {
                var c = _playAreaBg.color;
                c.a = 0f;
                _playAreaBg.color = c;
            }


        }
        // After wipe completes, animate UI boxes in (scene UI)
        if (uiSequencer != null)
            yield return StartCoroutine(uiSequencer.PlayIn());





        // 2) Core dot scale in (+ GPU core reveal)
        if (coreDotRect)
        {
            yield return Tween01(coreTime, t =>
            {
                float s = ease.Evaluate(t);

                // UI core dot (if you're still using it)
                coreDotRect.localScale = Vector3.one * s;

                // GPU core reveal
                if (coreGPU != null)
                    coreGPU.SetReveal01(s);
            });
        }


        // 3) Icons scale in
        if (_minigame != null)
        {
            yield return Tween01(iconTime, t => _minigame.SetIconsScale(ease.Evaluate(t)));
        }

        // 4) Labels fade in
        if (_minigame != null)
        {
            yield return Tween01(labelTime, t => _minigame.SetLabelsAlpha(ease.Evaluate(t)));
        }

        // 5) Rays scale in
        if (_minigame != null)
        {
            yield return Tween01(rayTime, t => _minigame.SetRayDotsScale01(ease.Evaluate(t)));
        }

        if (_minigame != null)
            _minigame.SetGameplayEnabled(true);
    }

    public IEnumerator PlayOutro()
    {

        if (uiSequencer != null)
    yield return StartCoroutine(uiSequencer.PlayOut());

        if (_minigame != null)
            _minigame.SetGameplayEnabled(false);

        if (_minigame != null)
        _minigame.BeginOutroDissolve();


        // Reverse the sequence quickly
        float wipeTime, coreTime, iconTime, labelTime, rayTime;
        SplitTime(outroDuration, introWipeFrac, introCoreFrac, introIconsFrac, introLabelsFrac, introRaysFrac,
                  out wipeTime, out coreTime, out iconTime, out labelTime, out rayTime);

        // Labels out
        if (_minigame != null)
            yield return Tween01(labelTime, t => _minigame.SetLabelsAlpha(1f - ease.Evaluate(t)));

        // Rays out
        if (_minigame != null)
            yield return Tween01(rayTime, t => _minigame.SetRayDotsScale01(1f - ease.Evaluate(t)));

        // Icons out
        if (_minigame != null)
            yield return Tween01(iconTime, t => _minigame.SetIconsScale(1f - ease.Evaluate(t)));

        // Core out (+ GPU core hide)
        if (coreDotRect)
        {
            yield return Tween01(coreTime, t =>
            {
                float s = 1f - ease.Evaluate(t);

                // UI core dot (if you're still using it)
                coreDotRect.localScale = Vector3.one * s;

                // GPU core reveal
                if (coreGPU != null)
                    coreGPU.SetReveal01(s);
            });
            if (coreGPU != null)
{
    coreGPU.SetReveal01(0f);
    // optional: disable to ensure it never renders outside minigame
    coreGPU.enabled = false;
}

        }
        else
        {
            // If no UI dot, still run a tween for GPU fade so timing stays consistent.
            yield return Tween01(coreTime, t =>
            {
                float s = 1f - ease.Evaluate(t);
                if (coreGPU != null)
                    coreGPU.SetReveal01(s);
            });
        }

        // Start outro UI box collapse early
        if (lowerBoxes != null)
        {
            for (int i = lowerBoxes.Length - 1; i >= 0; i--)
            {
                if (lowerBoxes[i] != null)
                    StartCoroutine(lowerBoxes[i].PlayOut());
                yield return new WaitForSeconds(boxStagger);
            }
        }

        if (topBannerBox != null)
            yield return StartCoroutine(topBannerBox.PlayOut());



        // Wipe out
        if (wipeCircleRect && playAreaRect)
        {
            float target = ComputeWipeTargetScale(playAreaRect, wipeCircleRect, wipeCoverPadding);
            yield return Tween01(wipeTime, t =>
            {
                float s = Mathf.Lerp(target, 0f, ease.Evaluate(t));
                wipeCircleRect.localScale = Vector3.one * s;
            });

            wipeCircleRect.gameObject.SetActive(false);
        }
    }

    static IEnumerator Tween01(float seconds, System.Action<float> apply)
    {
        seconds = Mathf.Max(0.001f, seconds);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / seconds);
            apply(u);
            yield return null;
        }
        apply(1f);
    }

    static void SplitTime(float total, float a, float b, float c, float d, float e,
                          out float ta, out float tb, out float tc, out float td, out float te)
    {
        float sum = Mathf.Max(0.0001f, a + b + c + d + e);
        ta = total * (a / sum);
        tb = total * (b / sum);
        tc = total * (c / sum);
        td = total * (d / sum);
        te = total * (e / sum);
    }

    static float ComputeWipeTargetScale(RectTransform area, RectTransform circle, float padding)
    {
        // circle localScale multiplies its sizeDelta; compute a scale that covers the rect diagonal
        var r = area.rect;
        float halfDiag = Mathf.Sqrt(r.width * r.width + r.height * r.height) * 0.5f;
        float circleRadius = Mathf.Max(0.0001f, circle.sizeDelta.x * 0.5f);
        return (halfDiag / circleRadius) * padding;
    }
}
