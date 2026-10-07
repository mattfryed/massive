using UnityEngine;
using Massive.PowerUps;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class PowerUpIconManifestAnimator : MonoBehaviour
{
    [Header("Optional: Player hookup (leave empty for world pickups)")]
    [SerializeField] private PlayerPowerUpController controller;
    [Header("Despawn Target")]
    [SerializeField] private Transform despawnRoot;
    [Header("Auto-find if null")]
    [SerializeField] private ParametricPolyhedronWire wire;
    [SerializeField] private MetaballManifest[] metaballs;
    [Header("Auto Play (when no controller)")]
    [SerializeField] private bool playIntroOnEnable = true;
    [SerializeField] private bool startHiddenOnAwake = true;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(0.001f)] private float wireInDuration = 0.35f;
    // Retain old prefab data. Natural despawn now reverses the complete spawn timeline.
    [SerializeField, HideInInspector] private float wireOutDuration;
    [SerializeField, HideInInspector] private float afterWireBeforeMetas;
    [SerializeField, HideInInspector] private AnimationCurve foldEase;
    [SerializeField] private AnimationCurve drawEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Despawn")]
    [SerializeField] private bool disableCollidersOnDespawn = true;
    [SerializeField] private bool destroyOnComplete = true;
    [SerializeField] private float destroyDelay = 0f;
    [Header("Attack Despawn (Faces)")]
    [SerializeField] private float wireShatterDuration = 0.20f;
    [SerializeField] private float wireCollapseDuration = 0.15f;
    [SerializeField] private float shatterDistance = 0.35f;
    [SerializeField] private float shatterSpinDegrees = 220f;
    [SerializeField] private AnimationCurve shatterEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve collapseEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsDespawning { get; private set; }
    private enum Phase { Hidden, Spawning, Shown, Despawning, Acquiring }
    private Phase _phase;
    private float _acquireTime;
    private float _acquireDuration;
    private bool _subscribed;
    private Collider[] _colliders;
    private bool _isWorldPickup;
    private bool[] _colliderEnabled;
    private PowerUpIconParticleSizeEase[] _particles;
    private float _sequenceTime;
    private float _authoredRadius;
    private PowerUpSettings _settings;
    private int _settingsRevision = -1;
    private float InnerStartFraction => _settings ? _settings.visuals.innerStartFraction : .5f;

    private void Awake()
    {
        if (controller == null) controller = GetComponentInParent<PlayerPowerUpController>();
        if (despawnRoot == null)
        {
            var pickup = controller == null ? GetComponentInParent<PowerUpPickup>() : null;
            despawnRoot = pickup ? pickup.transform : transform;
        }
        if (wire == null) wire = despawnRoot.GetComponentInChildren<ParametricPolyhedronWire>(true);
        if (metaballs == null || metaballs.Length == 0)
            metaballs = despawnRoot.GetComponentsInChildren<MetaballManifest>(true);
        _particles = despawnRoot.GetComponentsInChildren<PowerUpIconParticleSizeEase>(true);
        _colliders = despawnRoot.GetComponentsInChildren<Collider>(true);
        _isWorldPickup = despawnRoot.GetComponent<PowerUpPickup>() != null;
        _colliderEnabled = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++) _colliderEnabled[i] = _colliders[i].enabled;
        if (wire) { wire.UsePickupAnimation = true; _authoredRadius = wire.radius; }
        RefreshSharedSettings();
        if (startHiddenOnAwake) SampleSpawn(0f);
    }

    private void OnEnable()
    {
        RefreshSharedSettings();
        IsDespawning = false;
        for (int i = 0; i < _colliders.Length; i++)
            if (_colliders[i]) _colliders[i].enabled = _colliderEnabled[i];
        if (controller != null && !_subscribed)
        {
            controller.OnEquipped += OnEquipped;
            controller.OnExpired += OnExpired;
            _subscribed = true;
        }
        else if (controller == null && playIntroOnEnable) PlayIn();
    }

    private void OnDisable()
    {
        _phase = Phase.Hidden;
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
        foreach (var p in _particles) if (p) p.PrepareSpawn();
        SampleSpawn(0f);
        _phase = Phase.Spawning;
    }

    public void BeginDespawn()
    {
        if (IsDespawning) return;
        BeginOut();
        _phase = Phase.Despawning;
    }

    public void BeginAttackDespawn()
    {
        if (IsDespawning) return;
        BeginOut();
        // Snapshot the CURRENT envelope, including a hit during the intro.
        // Every ball/particle starts shrinking on this hit, without stagger or overshoot.
        foreach (var m in metaballs) if (m) m.BeginAcquireDespawn();
        foreach (var p in _particles) if (p) p.BeginAcquireDespawn();
        if (wire)
        {
            wire.IsAcquiring = true;
            wire.shatterDistance = shatterDistance;
            wire.shatterSpinDegrees = shatterSpinDegrees;
        }
        _acquireTime = 0f;
        _acquireDuration = Mathf.Max(wireShatterDuration, UndrawStart + wireCollapseDuration);
        foreach (var m in metaballs) if (m) _acquireDuration = Mathf.Max(_acquireDuration, m.AcquireDuration);
        foreach (var p in _particles) if (p) _acquireDuration = Mathf.Max(_acquireDuration, p.DespawnDuration);
        _phase = Phase.Acquiring;
    }

    private void BeginOut()
    {
        IsDespawning = true;
        if (disableCollidersOnDespawn)
            foreach (var c in _colliders) if (c) c.enabled = false;
    }

    private float SpawnDuration()
    {
        float inner = 0f;
        foreach (var m in metaballs) if (m) inner = Mathf.Max(inner, m.EstimatedInTime());
        foreach (var p in _particles) if (p) inner = Mathf.Max(inner, p.SpawnDuration);
        return Mathf.Max(wireInDuration, wireInDuration * InnerStartFraction + inner);
    }

    private void SampleSpawn(float seconds)
    {
        _sequenceTime = Mathf.Max(0f, seconds);
        if (wire)
        {
            wire.IsAcquiring = false;
            wire.foldProgress = 1f;
            wire.drawProgress = Ease(drawEase, _sequenceTime, wireInDuration);
            wire.shatterProgress = wire.collapseProgress = 0f;
        }
        float innerTime = Mathf.Max(0f, _sequenceTime - wireInDuration * InnerStartFraction);
        foreach (var m in metaballs) if (m) m.SampleSpawnTime(innerTime);
        foreach (var p in _particles) if (p) p.SampleSpawnTime(innerTime);
    }

    private float UndrawStart => _settings ? _settings.visuals.faceUndrawDelay : Mathf.Max(0f, wireShatterDuration) * .15f;

    public void RefreshSharedSettings()
    {
        var settings = PowerUpSettings.Current;
        if (!settings || (_settings == settings && _settingsRevision == settings.Revision)) return;
        _settings = settings; _settingsRevision = settings.Revision;
        var v = settings.visuals;
        destroyDelay = settings.lifecycle.cleanupDelaySeconds;
        wireInDuration = v.shellSeconds; drawEase = v.drawEase; useUnscaledTime = v.useUnscaledAnimationTime;
        wireShatterDuration = v.faceSeparateSeconds; wireCollapseDuration = v.faceUndrawSeconds;
        shatterDistance = v.faceDistance; shatterSpinDegrees = v.faceSpinDegrees;
        shatterEase = v.separateEase; collapseEase = v.undrawEase;
        if (wire)
        {
            wire.radius = _authoredRadius * v.shellScale; wire.thickness = v.lineThickness; wire.color = v.shellColor;
            wire.drawTimingVariation = v.edgeTimingVariation; wire.drawSpeedVariation = v.edgeSpeedVariation;
            wire.faceTimingVariation = v.faceTimingVariation; wire.faceSpeedVariation = v.faceSpeedVariation;
            wire.shatterRandomness = v.faceDirectionRandomness;
            wire.shatterDistance = v.faceDistance; wire.shatterSpinDegrees = v.faceSpinDegrees;
        }
        foreach (var m in metaballs) if (m) m.ApplyPickupSettings(v);
        foreach (var p in _particles) if (p) p.ApplyPickupSettings(v);
        FitPickupCollidersToShell();
        if (_phase == Phase.Shown) SampleSpawn(SpawnDuration());
    }

    private void FitPickupCollidersToShell()
    {
        if (!_isWorldPickup || !wire) return;
        // The trigger and solid body share the intact shell's circumsphere.
        // Fit in world space so root and child collider transforms agree, without
        // scaling the hierarchy (which would also resize the inner icon).
        float worldRadius = wire.radius * LargestAxis(wire.transform.lossyScale);
        foreach (var collider in _colliders)
        {
            if (collider is not SphereCollider sphere) continue;
            sphere.center = sphere.transform.InverseTransformPoint(wire.transform.position);
            sphere.radius = worldRadius / Mathf.Max(.00001f, LargestAxis(sphere.transform.lossyScale));
        }
    }

    private static float LargestAxis(Vector3 scale) =>
        Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

    // Run before the inner-icon producers so shell and icon render the same time sample.
    private void Update()
    {
        RefreshSharedSettings();
        if (_phase == Phase.Spawning)
        {
            // Producers report their ball counts while hidden, before the midpoint.
            float duration = SpawnDuration();
            SampleSpawn(Mathf.Min(duration, _sequenceTime + Dt()));
            if (_sequenceTime >= duration) _phase = Phase.Shown;
        }
        else if (_phase == Phase.Despawning)
        {
            // Reverse the same timeline and seeds, including an interrupted intro.
            SampleSpawn(Mathf.Max(0f, _sequenceTime - Dt()));
            if (_sequenceTime <= 0f) CompleteDespawn();
        }
        else if (_phase == Phase.Acquiring)
        {
            _acquireTime += Dt();
            foreach (var m in metaballs) if (m) m.SampleAcquireTime(_acquireTime);
            foreach (var p in _particles) if (p) p.SampleAcquireTime(_acquireTime);
            if (wire)
            {
                wire.shatterProgress = Ease(shatterEase, _acquireTime, wireShatterDuration);
                wire.collapseProgress = Ease(collapseEase, _acquireTime - UndrawStart, wireCollapseDuration);
            }
            if (_acquireTime >= _acquireDuration) CompleteDespawn();
        }
    }

    private void CompleteDespawn()
    {
        SampleSpawn(0f);
        _phase = Phase.Hidden;
        if (destroyOnComplete) Destroy(despawnRoot.gameObject, destroyDelay);
        else despawnRoot.gameObject.SetActive(false);
    }

    private static float Ease(AnimationCurve curve, float time, float duration)
    {
        if (time <= 0f) return 0f;
        if (duration <= 0f || time >= duration) return 1f;
        float u = time / duration;
        return curve != null ? Mathf.Clamp01(curve.Evaluate(u)) : u;
    }
    private float Dt() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
