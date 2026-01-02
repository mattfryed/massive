using System.Collections;
using UnityEngine;

/// <summary>
/// Default GPU-friendly dissolve/form FX:
/// - Shrinks PlayerVisualController.baseRadius + outlineHalf
/// - Shrinks PlayerNuggetsGPU.dotRadius
/// No Animator needed.
/// </summary>
public class PlayerLifeFx_DissolveGPU : MonoBehaviour, IPlayerLifeFx
{
    [Header("Refs (auto-filled if empty)")]
    [SerializeField] private PlayerVisualController visuals;
    [SerializeField] private PlayerNuggetsGPU nuggets;

    [Header("Ease")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Death (Juicy)")]
    [SerializeField, Range(0f, 2f)] private float deathExtraHitImpulse = 0.8f;
    [SerializeField, Range(1f, 4f)] private float deathJitterMul = 1.8f;
    [SerializeField] private bool disableComponentsWhenHidden = true;

    [Header("Respawn")]
    [SerializeField, Range(0f, 0.35f)] private float respawnOvershoot = 0.12f;

    private float _baseRadius0;
    private float _outlineHalf0;
    private float _dotRadius0;
    private float _jitterAmp0;

    private void Awake()
    {
        if (!visuals) visuals = GetComponent<PlayerVisualController>();
        if (!nuggets) nuggets = GetComponentInChildren<PlayerNuggetsGPU>(true);

        if (visuals)
        {
            _baseRadius0 = visuals.baseRadius;
            _outlineHalf0 = visuals.outlineHalf;
        }

        if (nuggets)
        {
            _dotRadius0 = nuggets.dotRadius;
            _jitterAmp0 = nuggets.jitterAmp;
        }
    }

    public void SetVisibleInstant(PlayerControllerScript player, bool visible)
    {
        if (visible)
        {
            if (visuals)
            {
                visuals.enabled = true;
                visuals.baseRadius = _baseRadius0;
                visuals.outlineHalf = _outlineHalf0;
            }

            if (nuggets)
            {
                nuggets.enabled = true;
                nuggets.dotRadius = _dotRadius0;
                nuggets.jitterAmp = _jitterAmp0;
            }

            return;
        }

        // hidden
        if (visuals)
        {
            visuals.baseRadius = 0f;
            visuals.outlineHalf = 0f;
            if (disableComponentsWhenHidden) visuals.enabled = false;
        }

        if (nuggets)
        {
            nuggets.dotRadius = 0f;
            nuggets.jitterAmp = _jitterAmp0;
            if (disableComponentsWhenHidden) nuggets.enabled = false;
        }
    }

    public IEnumerator PlayDeath(PlayerControllerScript player, float duration)
    {
        if (duration <= 0.001f)
        {
            SetVisibleInstant(player, false);
            yield break;
        }

        if (visuals) visuals.enabled = true;
        if (nuggets) nuggets.enabled = true;

        if (visuals && deathExtraHitImpulse > 0f)
            visuals.OnHit(deathExtraHitImpulse);

        float r0  = visuals ? visuals.baseRadius  : _baseRadius0;
        float o0  = visuals ? visuals.outlineHalf : _outlineHalf0;
        float dr0 = nuggets ? nuggets.dotRadius  : _dotRadius0;

        float jitter0 = nuggets ? nuggets.jitterAmp : _jitterAmp0;
        if (nuggets) nuggets.jitterAmp = jitter0 * deathJitterMul;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float e = (ease != null) ? ease.Evaluate(u) : u;

            float k = 1f - e;

            if (visuals)
            {
                visuals.baseRadius = r0 * k;
                visuals.outlineHalf = o0 * k;
            }

            if (nuggets)
                nuggets.dotRadius = dr0 * k;

            yield return null;
        }

        if (nuggets) nuggets.jitterAmp = jitter0;

        if (visuals) { visuals.baseRadius = 0f; visuals.outlineHalf = 0f; }
        if (nuggets) nuggets.dotRadius = 0f;

    // ✅ HARD HIDE at the end so no 1px artifact remains
    SetVisibleInstant(player, false);
    }

    public IEnumerator PlayRespawn(PlayerControllerScript player, float duration)
    {
        if (duration <= 0.001f)
        {
            SetVisibleInstant(player, true);
            yield break;
        }

        if (visuals)
        {
            visuals.enabled = true;
            visuals.baseRadius = 0f;
            visuals.outlineHalf = 0f;
        }

        if (nuggets)
        {
            nuggets.enabled = true;
            nuggets.dotRadius = 0f;
            nuggets.jitterAmp = _jitterAmp0;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float e = (ease != null) ? ease.Evaluate(u) : u;

            // quick overshoot then settle
            float overshoot = 1f + respawnOvershoot * Mathf.Sin(e * Mathf.PI);
            float k = e * overshoot;

            if (visuals)
            {
                visuals.baseRadius  = _baseRadius0  * k;
                visuals.outlineHalf = _outlineHalf0 * k;
            }

            if (nuggets)
                nuggets.dotRadius = _dotRadius0 * k;

            yield return null;
        }

        SetVisibleInstant(player, true);
    }
}
