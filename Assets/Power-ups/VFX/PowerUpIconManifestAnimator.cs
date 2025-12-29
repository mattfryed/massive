using System.Collections;
using UnityEngine;
using Massive.PowerUps;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class PowerUpIconManifestAnimator : MonoBehaviour
{
    [Header("Optional: Player hookup (leave empty for world pickups)")]
    [SerializeField] private PlayerPowerUpController controller;

    [Header("Auto-find if null")]
    [SerializeField] private ParametricPolyhedronWire wire;
    [SerializeField] private MetaballManifest[] metaballs;

    [Header("Auto Play (when no controller)")]
    [SerializeField] private bool playIntroOnEnable = true;
    [SerializeField] private bool startHiddenOnAwake = true;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private float wireInDuration = 0.35f;
    [SerializeField] private float wireOutDuration = 0.25f;
    [SerializeField] private float afterWireBeforeMetas = 0.02f;

    [Header("Wire Ease")]
    [SerializeField] private AnimationCurve drawEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve foldEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Despawn")]
    [SerializeField] private bool disableCollidersOnDespawn = true;
    [SerializeField] private bool destroyOnComplete = true;
    [SerializeField] private float destroyDelay = 0f;

    public bool IsDespawning { get; private set; }

    private Coroutine _co;
    private bool _subscribed;
    private Collider[] _colliders;

    private void Awake()
    {
        if (controller == null) controller = GetComponentInParent<PlayerPowerUpController>();
        if (wire == null) wire = GetComponentInChildren<ParametricPolyhedronWire>(true);
        if (metaballs == null || metaballs.Length == 0)
            metaballs = GetComponentsInChildren<MetaballManifest>(true);

        _colliders = GetComponentsInChildren<Collider>(true);

        if (startHiddenOnAwake)
            SetHiddenInstant();
    }

    private void OnEnable()
    {
        // Player mode (optional)
        if (controller != null && !_subscribed)
        {
            controller.OnEquipped += OnEquipped;
            controller.OnExpired += OnExpired;
            _subscribed = true;
            return;
        }

        // World pickup mode
        if (controller == null && playIntroOnEnable && !IsDespawning)
            PlayIn();
    }

    private void OnDisable()
    {
        if (controller != null && _subscribed)
        {
            controller.OnEquipped -= OnEquipped;
            controller.OnExpired -= OnExpired;
            _subscribed = false;
        }
    }

    private void OnEquipped(PowerUpDefinition _) => PlayIn();
    private void OnExpired() => BeginDespawn();

    public void PlayIn()
    {
        if (IsDespawning) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoIn());
    }

    /// Call this to play the OUTRO (pickup, timeout, etc.)
    public void BeginDespawn()
    {
        if (IsDespawning) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoOut());
    }

    private IEnumerator CoIn()
    {
        SetHiddenInstant();

        yield return AnimateWire(0f, 1f, wireInDuration);

        if (afterWireBeforeMetas > 0f)
            yield return Wait(afterWireBeforeMetas);

        if (metaballs != null)
            foreach (var m in metaballs)
                if (m) m.PlayIn();
    }

    private IEnumerator CoOut()
    {
        IsDespawning = true;

        if (disableCollidersOnDespawn && _colliders != null)
            foreach (var c in _colliders)
                if (c) c.enabled = false;

        // metaballs out first
        float wait = 0f;
        if (metaballs != null)
        {
            foreach (var m in metaballs)
            {
                if (!m) continue;
                m.PlayOut();
                wait = Mathf.Max(wait, m.EstimatedOutTime());
            }
        }

        if (wait > 0f) yield return Wait(wait);

        // then wire out
        yield return AnimateWire(1f, 0f, wireOutDuration);

        SetHiddenInstant();

        if (destroyOnComplete) Destroy(gameObject, destroyDelay);
        else gameObject.SetActive(false);
    }

    private void SetHiddenInstant()
    {
        if (wire != null) { wire.drawProgress = 0f; wire.foldProgress = 0f; }
        if (metaballs != null)
            foreach (var m in metaballs)
                if (m) m.SetHiddenInstant();
    }

    private IEnumerator AnimateWire(float from, float to, float duration)
    {
        if (wire == null || duration <= 0f)
        {
            if (wire != null) { wire.drawProgress = to; wire.foldProgress = to; }
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Dt();
            float u = Mathf.Clamp01(t / duration);
            wire.drawProgress = Mathf.Lerp(from, to, drawEase.Evaluate(u));
            wire.foldProgress = Mathf.Lerp(from, to, foldEase.Evaluate(u));
            yield return null;
        }

        wire.drawProgress = to;
        wire.foldProgress = to;
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Dt(); yield return null; }
    }

    private float Dt() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
