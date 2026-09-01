using System.Collections;
using UnityEngine;

public class PlayerLifeFx_DissolveGPU : MonoBehaviour, IPlayerLifeFx
{
    [Header("Refs (auto-filled if empty)")]
    [SerializeField] private PlayerVisualController visuals;
    [SerializeField] private PlayerNuggetsGPU nuggets;

    [Header("Time")]
    [Tooltip("Turn this ON for menus / attract screens that set Time.timeScale = 0.")]
    [SerializeField] private bool useUnscaledTime = true;

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

    private float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

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
            t += Dt;
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

        SetVisibleInstant(player, false);
    }

    public IEnumerator PlayRespawn(PlayerControllerScript player, float duration)
    {
        if (duration <= 0.001f)
        {
            SetVisibleInstant(player, true);
            yield break;
        }

        if (visuals) visuals.enabled = true;

        if (nuggets)
        {
            nuggets.enabled = true;
            nuggets.jitterAmp = _jitterAmp0;
        }

        // start from current (safe for rapid toggling)
        float rStart  = visuals ? visuals.baseRadius  : 0f;
        float oStart  = visuals ? visuals.outlineHalf : 0f;
        float drStart = nuggets ? nuggets.dotRadius  : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / duration);
            float e = (ease != null) ? ease.Evaluate(u) : u;

            float overshoot = 1f + respawnOvershoot * Mathf.Sin(e * Mathf.PI);
            float k = e * overshoot;

            if (visuals)
            {
                visuals.baseRadius  = Mathf.LerpUnclamped(rStart, _baseRadius0, k);
                visuals.outlineHalf = Mathf.LerpUnclamped(oStart, _outlineHalf0, k);
            }

            if (nuggets)
                nuggets.dotRadius = Mathf.LerpUnclamped(drStart, _dotRadius0, k);

            yield return null;
        }

        SetVisibleInstant(player, true);
    }
}
