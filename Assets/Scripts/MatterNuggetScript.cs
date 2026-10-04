using Massive.Enemies;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MatterNuggetScript : MonoBehaviour
{
    [Range(0f, 1f)] public float rewardMultiplier = 1f;
    public EnemyScoreToast rewardToastPrefab;
    [Min(.01f)] public float spawnSeconds = .18f;
    [Min(.01f)] public float despawnSeconds = .16f;
    [Tooltip("Zero leaves scene nuggets alive until collected. Ejected pickups supply their own lifetime.")]
    [Min(0f)] public float lifetimeSeconds;
    [Tooltip("With Rigidbody linear damping enabled, stop residual drift below this speed (units/second).")]
    [Min(0f)] public float restSpeed = .03f;
    [Range(0f, 1f)] public float wallRestitution = .85f;
    public PhysicsMaterial glideMaterial;

    private Rigidbody body;
    private ParticleSystem[] particles;
    private Collider[] colliders;
    private bool[] colliderEnabled;
    private ParticleSystem.MinMaxCurve[] sizeCurves;
    private float age, lifetime, exitAge, exitStart;
    private bool ending;
    private bool queuedEjection;
    private Vector3 queuedPoint, queuedVelocity;
    private float queuedLifetime;
    private Vector3 incomingVelocity;
    public float VisualScale { get; private set; }
    public float LastMassRestored { get; private set; }
    public bool IsDespawning => ending;

    private static readonly HashSet<MatterNuggetScript> activePickups = new();

    // Match by component, not layer: the Dyson prefabs also have Default-layer colliders.
    // Register from both ends so pickups and enemies can spawn in either order.
    internal static void IgnoreEnemyContacts(EnemyBase enemy)
    {
        if (activePickups.Count == 0) return;
        var shapes = enemy.GetComponentsInChildren<Collider>(true);
        foreach (var pickup in activePickups)
            if (pickup && pickup.gameObject.scene == enemy.gameObject.scene) pickup.IgnoreContacts(shapes);
    }
    private void IgnoreContacts(Collider[] enemyShapes)
    {
        foreach (var pickupShape in colliders)
            foreach (var enemyShape in enemyShapes)
                if (pickupShape && enemyShape) Physics.IgnoreCollision(pickupShape, enemyShape);
    }

    protected virtual void Awake() { Initialize(); }
    private void Initialize()
    {
        if (body) return;
        body = GetComponent<Rigidbody>();
        body.sleepThreshold = 0f;
        particles = GetComponentsInChildren<ParticleSystem>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        colliderEnabled = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderEnabled[i] = colliders[i].enabled;
            if (!colliders[i].isTrigger && glideMaterial) colliders[i].sharedMaterial = glideMaterial;
        }
        sizeCurves = new ParticleSystem.MinMaxCurve[particles.Length * 3];
        for (int i = 0; i < particles.Length; i++)
        {
            var size = particles[i].sizeOverLifetime;
            sizeCurves[i * 3] = size.enabled ? size.x : new ParticleSystem.MinMaxCurve(1f);
            sizeCurves[i * 3 + 1] = size.enabled && size.separateAxes ? size.y : sizeCurves[i * 3];
            sizeCurves[i * 3 + 2] = size.enabled && size.separateAxes ? size.z : sizeCurves[i * 3];
        }
    }

    protected virtual void OnEnable() { Initialize(); activePickups.Add(this); BeginLife(lifetimeSeconds); }
    protected virtual void OnDisable() { activePickups.Remove(this); }
    private void BeginLife(float seconds)
    {
        age = exitAge = 0f; lifetime = seconds; ending = queuedEjection = false; LastMassRestored = 0f;
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderEnabled[i];
        foreach (var enemy in EnemyBase.ActiveEnemies)
            if (enemy && enemy.gameObject.scene == gameObject.scene)
                IgnoreContacts(enemy.GetComponentsInChildren<Collider>(true));
        SetVisualSize(0f);
        foreach (var p in particles) { p.Clear(); p.Play(); }
        body.WakeUp();
    }

    public void Eject(Vector3 point, Vector3 velocity, float seconds)
    {
        Initialize();
        transform.position = point;
        gameObject.SetActive(true);
        BeginLife(seconds);
        body.position = point; body.linearVelocity = velocity; body.angularVelocity = Vector3.zero;
        incomingVelocity = velocity;
    }

    public void EjectSmoothly(Vector3 point, Vector3 velocity, float seconds)
    {
        // A full pool retires the old visible pickup before reusing its slot at a new contact.
        if (!gameObject.activeInHierarchy || VisualScale <= 0f) { Eject(point, velocity, seconds); return; }
        queuedPoint = point; queuedVelocity = velocity; queuedLifetime = seconds; queuedEjection = true;
        BeginExit();
    }

    private void BeginExit()
    {
        if (ending) return;
        ending = true; exitAge = 0f; exitStart = VisualScale;
        foreach (var c in colliders) c.enabled = false;
        body.linearVelocity = Vector3.zero;
    }

    private void CompleteLife()
    {
        if (queuedEjection) Eject(queuedPoint, queuedVelocity, queuedLifetime);
        else FinishDespawn();
    }

    protected virtual void Update()
    {
        if (ending)
        {
            exitAge += Time.deltaTime;
            SetVisualSize(exitStart * (1f - Mathf.SmoothStep(0f, 1f, exitAge / Mathf.Max(.01f, despawnSeconds))));
            if (exitAge >= despawnSeconds) CompleteLife();
            return;
        }
        age += Time.deltaTime;
        float size = Mathf.SmoothStep(0f, 1f, age / Mathf.Max(.01f, spawnSeconds));
        if (lifetime > 0f)
        {
            size *= Mathf.SmoothStep(0f, 1f, (lifetime - age) / Mathf.Max(.01f, despawnSeconds));
            if (age >= lifetime) { SetVisualSize(0f); CompleteLife(); return; }
        }
        SetVisualSize(size);
    }

    private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve curve, float scale)
    {
        if (curve.mode == ParticleSystemCurveMode.Constant) curve.constant *= scale;
        else if (curve.mode == ParticleSystemCurveMode.TwoConstants) { curve.constantMin *= scale; curve.constantMax *= scale; }
        else curve.curveMultiplier *= scale;
        return curve;
    }

    private void SetVisualSize(float scale)
    {
        VisualScale = Mathf.Clamp01(scale);
        for (int i = 0; i < particles.Length; i++)
        {
            var size = particles[i].sizeOverLifetime;
            size.enabled = true;
            size.x = Scaled(sizeCurves[i * 3], VisualScale);
            if (size.separateAxes)
            {
                size.y = Scaled(sizeCurves[i * 3 + 1], VisualScale);
                size.z = Scaled(sizeCurves[i * 3 + 2], VisualScale);
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        incomingVelocity = body.linearVelocity;
        if (body.linearDamping > 0f && incomingVelocity.sqrMagnitude < restSpeed * restSpeed)
            body.linearVelocity = incomingVelocity = Vector3.zero;

        // Physics applies damping after FixedUpdate. Match it in the bounce snapshot so
        // reflecting from a wall cannot restore the velocity lost to drag this step.
        incomingVelocity *= Mathf.Max(0f, 1f - body.linearDamping * Time.fixedDeltaTime);
    }
    protected virtual void OnCollisionEnter(Collision collision) { Bounce(collision); }
    protected virtual void OnCollisionStay(Collision collision) { Bounce(collision); }
    private void Bounce(Collision collision)
    {
        if (ending || (collision.rigidbody && !collision.rigidbody.isKinematic)) return;
        // Reflect even below Physics.bounceThreshold; the frictionless material preserves the tangent.
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.y) > .5f) continue;
            normal.y = 0f; normal.Normalize();
            float towardWall = Vector3.Dot(incomingVelocity, normal);
            if (towardWall >= 0f) continue;
            incomingVelocity -= (1f + wallRestitution) * towardWall * normal;
            body.linearVelocity = incomingVelocity;
            body.WakeUp();
        }
    }

    protected virtual void OnTriggerEnter(Collider other) { TryCollect(other); }
    protected virtual void OnTriggerStay(Collider other) { TryCollect(other); }
    private void TryCollect(Collider other)
    {
        if (ending || age < .3f || (lifetime > 0f && lifetime - age <= despawnSeconds)) return;
        var player = other.GetComponentInParent<PlayerControllerScript>();
        if (!player || !player.isActiveAndEnabled || player.IsPseudoPlayer || player.temporarilyEliminated || player.IsMatchInputLocked) return;
        BeginExit();
        player.playSFX("massNuggetSFX");
        float before = player.massScore;
        player.GrowScaled(rewardMultiplier); player.GrowScaled(rewardMultiplier); player.GrowScaled(rewardMultiplier);
        LastMassRestored = Mathf.Max(0f, player.massScore - before);
        EnemyScoreToast.ShowMass(rewardToastPrefab, LastMassRestored, player.massScoreMax - player.massScoreMin,
            transform.position, gameObject.scene);
    }

    protected virtual void FinishDespawn() { Destroy(gameObject); }
}
