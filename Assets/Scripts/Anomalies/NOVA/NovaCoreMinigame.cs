using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rewired;

/// <summary>
/// NOVA anomaly minigame:
/// - Orbiting player icons + dotted ray + projectile waves
/// - Procedural incoming blob particles (1-3 subparticles + glue bridges)
/// - Only wave collision destroys subparticles
/// - Score pushed to AnomalyUIController via Context.manager.ui.SetParticleCounts(...)
/// </summary>
public class NovaCoreMinigame : AnomalyMinigameBase, IOnTimeParticipantsReceiver
{
    // -------------------- Inspector --------------------

    [Header("Timing")]
    [SerializeField] private float overrideDuration = 0f;

    [Header("Orbit UI Roots")]
    [SerializeField] private RectTransform playAreaRect;
    [SerializeField] private RectTransform coreRect;
    [SerializeField] private RectTransform playersRoot;
    [SerializeField] private RectTransform particlesRoot;
    [SerializeField, Range(0.05f, 0.9f)] private float orbitRadiusFactor = 0.35f;
    [SerializeField] private float angularSpeedDegreesPerSecond = 180f;

    [Header("Team Mapping")]
    [SerializeField] private int lightTeamIndex = 1;
    [SerializeField] private int darkTeamIndex  = 2;

    [Header("Late Join Penalty")]
    [SerializeField] private float lateJoinPenaltySeconds = 2f;

    [SerializeField] private AnimationCurve lateJoinScaleEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    [Header("Scoring")]
    [SerializeField] private float massPerSubparticle = 1f;
    [SerializeField] private float minTotalMassForSuccess = 1f;

    [Header("Hit Tuning")]
    [Tooltip("Extra hit radius (in UI units) around each subparticle when wave passes.")]
    [SerializeField] private float projectileHitPadding = 0.2f;

    // Circle shader for icons/dots
    private static Material _iconBaseMaterial;
    private static Material _rayDotMaterial;
    private const string IconShaderName = "MASSIVE/UI/CircleIcon";

    [Header("Icon Shape")]
    [SerializeField] private float iconDiameter = 40f;
    [SerializeField, Range(0f, 0.5f)] private float iconOutlineWidth = 0.12f;
    [SerializeField] private TMP_FontAsset iconLabelFont;
    [SerializeField] private float iconLabelFontSize = 20f;

    [Header("Light Team Icon Colors")]
    [SerializeField] private Color lightFillColor    = Color.white;
    [SerializeField] private Color lightOutlineColor = Color.black;
    [SerializeField] private Color lightTextColor    = Color.black;

    [Header("Dark Team Icon Colors")]
    [SerializeField] private Color darkFillColor    = Color.black;
    [SerializeField] private Color darkOutlineColor = Color.black;
    [SerializeField] private Color darkTextColor    = Color.white;

    [Header("Aim Ray Dots")]
    [SerializeField] private Color rayDotColor       = Color.black;
    [SerializeField] private float rayDotSize        = 6f;   // px (RectTransform sizeDelta)
    [SerializeField] private float rayDotSpacing     = 8f;   // local units
    [SerializeField] private float rayDotBaseScale   = 1f;

    [Tooltip("How much larger the projectile dot is (e.g. 2 -> 3x at peak).")]
    [SerializeField] private float projectileScaleIncrease = 2f;

    [Tooltip("Projectile speed in dots per second.")]
    [SerializeField] private float projectileSpeed = 15f;

    [Header("Projectile Rays")]
    [SerializeField] private RectTransform projectilesRoot; // optional: assign playAreaRect or playersRoot
    [SerializeField, Range(8, 200)] private int projectileMaxDots = 80;


    [Header("Core Particles")]
    [SerializeField] private float subparticleRadius      = 0.2f;
    [SerializeField] private float particleSpeed          = 250f;       // base
    [SerializeField, Range(0f, 1f)] private float particleSpeedVariance = 0.25f; // ±%
    [SerializeField] private float spawnRatePerSecond     = 3f;
    [SerializeField] private float spawnRateJitter        = 1f;
    [SerializeField] private float subShrinkDuration      = 0.2f;
    [SerializeField] private float coreHitRadius          = 0.3f;
    // outro dissolve state
    private bool _outroDissolveActive = false;

    // prevent score changes during outro
    private bool _scoringEnabled = true;


    [Header("Particle Layout & Motion FX")]
    [SerializeField] private float subSpacingFactor       = 0.7f; // multiple of radius
    [SerializeField] private float particleSpinSpeed      = 90f;  // deg/sec max magnitude
    [SerializeField] private float particleVibrationAmplitude = 0.03f;
    [SerializeField] private float particleVibrationFrequency = 3f;

    [Header("Particle Glue")]
    [SerializeField] private float bridgeDotRadiusFactor = 0.55f;
    [SerializeField] private int   bridgeDotsPerConnection = 3;

    [Header("Spawn Bias")]
    [Tooltip("Minimum acceptance weight at top/bottom (never 0).")]
    [SerializeField, Range(0f, 1f)] private float spawnMinWeight = 0.15f;
    [Tooltip("Higher -> more strongly biased to left/right.")]
    [SerializeField, Range(1f, 12f)] private float spawnSideExponent = 5f;
    [Tooltip("Rejection-sampling attempts before falling back to uniform.")]
    [SerializeField, Range(1, 32)] private int spawnBiasAttempts = 10;

    [Header("Destruction FX")]
    [SerializeField] private int explosionCountMin = 10;
    [SerializeField] private int explosionCountMax = 15;
    [SerializeField] private float explosionDotRadiusFactor = 0.2f;  // of subparticleRadius
    [SerializeField] private float explosionLifetime = 0.18f;
    [SerializeField] private float explosionSpeed = 1.5f;            // UI units/sec




    [Header("Instructions Preview")]
    [SerializeField] private bool previewAutoScale = true;
    [SerializeField] private bool previewUseUnscaledTime = true;

    // Subparticle radius as % of play area min dimension.
    // Example: 0.015 on a 400px box => 6px radius
    [SerializeField, Range(0.005f, 0.05f)]
    private float previewSubRadiusPercent = 0.015f;

    // core hit radius is derived from the coreRect visual size
    [SerializeField, Range(0.4f, 1.2f)]
    private float previewCoreHitRadiusMultiplier = 0.85f;

    // Particle travel time from spawn ring to the core (seconds)
    [SerializeField, Range(0.5f, 2.5f)]
    private float previewTravelTimeSeconds = 1.1f;

    // Material template using shader "MASSIVE/UI/CircleIcon" (ASSIGN IN INSPECTOR)
    [SerializeField] private Material circleIconMaterialTemplate;

    // Optional fallback sprite (circle) if shader fails (prevents squares)
    [SerializeField] private Sprite circleSpriteFallback;


    private bool _gameplayEnabled = false;

    private bool _instructionsPreviewActive;

public void SetGameplayEnabled(bool enabled)
    {
        _gameplayEnabled = enabled;

        // When leaving gameplay (intro/outro), remove any live projectile visuals.
        if (!enabled)
            ClearAllProjectiles();
    }

    private void ClearAllProjectiles()
    {
        for (int i = 0; i < _projectiles.Count; i++)
            CleanupProjectile(_projectiles[i]);
        _projectiles.Clear();
    }



    // -------------------- Runtime state --------------------

    private int _lightTeamParticlesCaptured;
    private int _darkTeamParticlesCaptured;

    private float _timeRemaining;
    private bool _started;

    private float _halfMinDimension;
    private float _orbitRadius;
    private int _maxDotsPerRay;

    private float _spawnTimer;

    private readonly HashSet<int> _onTimePlayerIds = new HashSet<int>();

    // Global multipliers driven by NovaMinigameTransition
    private float _globalIconsScale01  = 1f;
    private float _globalLabelsAlpha01 = 1f;
    private float _globalRayDotsScale01 = 0f; // default hidden, like current behavior


    // -------------------- Internal types --------------------

    private class ParticipantState
    {
        public PlayerControllerScript controller;
        public Player rewiredPlayer;

        public float totalCapturedMass;

        public RectTransform icon;
        public float angleRad;
        public Vector2 outwardDir;

        public List<RectTransform> rayDots = new List<RectTransform>();

        // Late join penalty
        public bool isLate;
        public float penaltyRemaining;
        public float penaltyDuration;
        public float joinScale01 = 1f;

        public TMP_Text label;
    }

    private class CoreParticle
    {
        public RectTransform root;

        public List<RectTransform> subRects = new List<RectTransform>();
        public List<bool> subAlive = new List<bool>();
        public List<float> subShrink = new List<float>(); // -1 idle, 0..1 shrinking

        public int originalSubCount;
        public ParticipantState lastHitBy;

        public Vector2 position;   // base position
        public Vector2 direction;  // toward core
        public float speed;

        public bool consumed;
        public bool missed;

        public float spinAngle;
        public float spinSpeed;
        public float noiseSeed;

        public bool exploded;      // prevent duplicate explosion on same particle
    }

    private struct BurstDot
    {
        public RectTransform rt;
        public Vector2 vel;
        public float age;
        public float life;
    }

    private readonly List<ParticipantState> _participants = new List<ParticipantState>();
    private readonly List<CoreParticle> _particles = new List<CoreParticle>();
    private readonly List<BurstDot> _burstDots = new List<BurstDot>();

    private class ProjectileRay
{
    public ParticipantState shooter;
    public Vector2 origin;   // playArea/local space
    public Vector2 dir;      // normalized, frozen at fire time
    public float wavePos;    // dot-index position (0..dots.Count)
    public List<RectTransform> dots = new List<RectTransform>();
}

private readonly List<ProjectileRay> _projectiles = new List<ProjectileRay>();


    // -------------------- Lifecycle --------------------

    /// <summary>
    /// Called by AnomalyManager immediately after prefab instantiation.
    /// Initializes timers, caches, participants, and pushes score UI to 00/00.
    /// </summary>
void OnEnable()
{
    // Force rebuild of cached materials so masking picks up the current shader.
    if (_iconBaseMaterial != null) Destroy(_iconBaseMaterial);
    if (_rayDotMaterial != null) Destroy(_rayDotMaterial);
    _iconBaseMaterial = null;
    _rayDotMaterial = null;
}

public void SetOnTimeParticipants(IReadOnlyList<PlayerControllerScript> onTimeParticipants)
{
    _onTimePlayerIds.Clear();
    if (onTimeParticipants == null) return;

    foreach (var p in onTimeParticipants)
    {
        if (p != null)
            _onTimePlayerIds.Add(p.playerID);
    }
}




    public override void Init(AnomalyContext context)
    {
        base.Init(context);

        _timeRemaining = (overrideDuration > 0f)
            ? overrideDuration
            : (Context.duration > 0f ? Context.duration : 5f);

        _lightTeamParticlesCaptured = 0;
        _darkTeamParticlesCaptured = 0;
        PushScoreToUI();

        RecomputeOrbitRadius();
        BuildParticipants();

        ApplyLateJoinSetup();


        _spawnTimer = 0f;
    }

    /// <summary>
    /// Called by AnomalyManager after Init(). Starts the minigame update loop.
    /// </summary>
    public override void Begin()
    {
        base.Begin();
        _started = true;
    }

    /// <summary>
    /// Main per-frame update: orbit UI, inputs, particle sim, burst FX, and end timer.
    /// </summary>
private void Update()
{
    if (!_started) return;

    if (_orbitRadius <= 0f)
        RecomputeOrbitRadius();

    float dt =
        (_instructionsPreviewActive && previewUseUnscaledTime)
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

    // Keep orbit placement running so transition scaling looks correct
    UpdateParticipantOrbitUI_VisualOnly(dt);

    // During gameplay, full sim runs
    if (_gameplayEnabled)
    {
        UpdateLateJoinPenalties(dt);
        HandleSwordInput();
        UpdateProjectiles(dt);  // <-- ADD THIS
        UpdateParticles(dt);

        _timeRemaining -= dt;
        if (_timeRemaining <= 0f)
            EndMinigame();
    }

    else
    {
        // During outro, keep particles moving + shrinking (no spawn, no hits)
        if (_outroDissolveActive)
            UpdateParticlesOutro(dt);
    }

    // Let burst FX finish during outro too
    UpdateBurstDots(dt);
}

private void UpdateParticlesOutro(float dt)
{
    if (particlesRoot == null || coreRect == null) return;

    Vector2 corePos = coreRect.anchoredPosition;

    for (int i = _particles.Count - 1; i >= 0; i--)
    {
        var p = _particles[i];

        // Keep moving toward core
        if (!p.consumed)
        {
            p.position += p.direction * (p.speed * dt);

            // spin
            p.spinAngle += p.spinSpeed * dt;
            if (p.root != null)
                p.root.localRotation = Quaternion.AngleAxis(p.spinAngle, Vector3.forward);

            // vibration + apply position
            if (p.root != null)
            {
                float tNoise = Time.time * particleVibrationFrequency + p.noiseSeed;
                float nx = (Mathf.PerlinNoise(tNoise, 0f) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(0f, tNoise) - 0.5f) * 2f;
                Vector2 vib = new Vector2(nx, ny) * particleVibrationAmplitude;
                p.root.anchoredPosition = p.position + vib;
            }
        }

        // shrink anim (same as normal)
        bool anyAlive = false;
        for (int s = 0; s < p.subRects.Count; s++)
        {
            if (!p.subAlive[s]) continue;

            float t = p.subShrink[s];
            if (t >= 0f)
            {
                t += dt / Mathf.Max(0.001f, subShrinkDuration);
                float clamped = Mathf.Clamp01(t);
                float eased = Mathf.SmoothStep(1f, 0f, clamped);
                p.subRects[s].localScale = Vector3.one * eased;

                if (clamped >= 1f)
                {
                    p.subAlive[s] = false;
                    p.subShrink[s] = -1f;
                    p.subRects[s].localScale = Vector3.zero;
                }
                else
                {
                    p.subShrink[s] = t;
                }
            }

            if (p.subAlive[s]) anyAlive = true;
        }

        // Once fully shrunk, remove
        if (!anyAlive)
        {
            if (p.root != null) Destroy(p.root.gameObject);
            _particles.RemoveAt(i);
        }
    }
}




    public void SetIconsScale(float s)
    {
        _globalIconsScale01 = Mathf.Clamp01(s);
        ApplyParticipantVisuals();
    }

    public void SetLabelsAlpha(float a)
    {
        _globalLabelsAlpha01 = Mathf.Clamp01(a);
        ApplyParticipantVisuals();
    }

    public void SetRayDotsScale01(float s01)
    {
        _globalRayDotsScale01 = Mathf.Clamp01(s01);
        ApplyParticipantVisuals();
    }



    // -------------------- Orbit / rays --------------------

    /// <summary>
    /// Recomputes orbit radius and max ray dots based on playAreaRect.
    /// Call whenever layout/scale changes.
    /// </summary>
    private void RecomputeOrbitRadius()
    {
        if (playAreaRect == null)
            return;

        var rect = playAreaRect.rect;
        _halfMinDimension = Mathf.Min(rect.width, rect.height) * 0.5f;
        _orbitRadius = _halfMinDimension * orbitRadiusFactor;

        float maxRayLength = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height);
        float spacing = Mathf.Max(0.001f, rayDotSpacing);
        _maxDotsPerRay = Mathf.Clamp(Mathf.CeilToInt(maxRayLength / spacing), 3, 200);
    }

    /// <summary>
    /// Updates icon orbit positions and ray dot placement; advances projectile waves.
    /// </summary>
  
private void UpdateParticipantOrbitUI_VisualOnly(float dt)
{
    if (coreRect == null || playersRoot == null || _participants.Count == 0)
        return;

    Vector2 corePos = coreRect.anchoredPosition;
    float angSpeedRad = angularSpeedDegreesPerSecond * Mathf.Deg2Rad;

    foreach (var ps in _participants)
    {
        if (ps.icon == null)
            continue;

        // Only rotate in response to input during gameplay.
        if (_gameplayEnabled && ps.rewiredPlayer != null)
        {
            float h = ps.rewiredPlayer.GetAxis("MoveH");
            ps.angleRad += h * angSpeedRad * dt;
            if (ps.angleRad > Mathf.PI) ps.angleRad -= Mathf.PI * 2f;
            if (ps.angleRad < -Mathf.PI) ps.angleRad += Mathf.PI * 2f;
        }

        Vector2 dir = new Vector2(Mathf.Sin(ps.angleRad), Mathf.Cos(ps.angleRad));
        Vector2 iconPos = corePos + dir * _orbitRadius;
        ps.icon.anchoredPosition = iconPos;

        Vector2 fromCore = iconPos - corePos;
        float dCore = fromCore.magnitude;
        ps.outwardDir = (dCore < 1e-3f) ? Vector2.up : (fromCore / dCore);

        if (ps.rayDots != null && ps.rayDots.Count > 0)
        {
            PositionRayDots(ps);
        }
    }
}  
  
    // private void UpdateParticipantOrbitUI()
    // {
    //     if (coreRect == null || playersRoot == null || _participants.Count == 0)
    //         return;

    //     Vector2 corePos = coreRect.anchoredPosition;
    //     float angSpeedRad = angularSpeedDegreesPerSecond * Mathf.Deg2Rad;
    //     float dt = Time.deltaTime;

    //     foreach (var ps in _participants)
    //     {
    //         if (ps.rewiredPlayer == null || ps.icon == null)
    //             continue;

    //         float h = ps.rewiredPlayer.GetAxis("MoveH");
    //         ps.angleRad += h * angSpeedRad * dt;

    //         if (ps.angleRad > Mathf.PI) ps.angleRad -= Mathf.PI * 2f;
    //         if (ps.angleRad < -Mathf.PI) ps.angleRad += Mathf.PI * 2f;

    //         // 0 rad = up, clockwise positive
    //         Vector2 dir = new Vector2(Mathf.Sin(ps.angleRad), Mathf.Cos(ps.angleRad));
    //         Vector2 iconPos = corePos + dir * _orbitRadius;
    //         ps.icon.anchoredPosition = iconPos;

    //         Vector2 fromCore = iconPos - corePos;
    //         float dCore = fromCore.magnitude;
    //         ps.outwardDir = (dCore < 1e-3f) ? Vector2.up : (fromCore / dCore);

    //         if (ps.rayDots != null && ps.rayDots.Count > 0)
    //         {
    //             PositionRayDots(ps);
    //             UpdateProjectileWaves(ps, dt);
    //         }
    //     }
    // }

    /// <summary>
    /// Places ray dots along the outward direction from the player's icon, clipped to playAreaRect.
    /// </summary>
    private void PositionRayDots(ParticipantState ps)
    {
        int count = ps.rayDots.Count;
        if (count == 0 || playAreaRect == null)
            return;

        Vector2 origin = ps.icon.anchoredPosition;
        Vector2 outward = ps.outwardDir;
        float spacing = Mathf.Max(0.001f, rayDotSpacing);
        float maxDist = ComputeMaxRayDistanceToRect(origin, outward);

        for (int i = 0; i < count; i++)
        {
            float d = spacing * (i + 1);
            if (maxDist > 0f && d <= maxDist)
            {
                ps.rayDots[i].anchoredPosition = origin + outward * d;
                if (!ps.rayDots[i].gameObject.activeSelf) ps.rayDots[i].gameObject.SetActive(true);
            }
            else
            {
                if (ps.rayDots[i].gameObject.activeSelf) ps.rayDots[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Returns the distance to the edge of playAreaRect along a ray starting at origin with direction dir.
    /// Used to hide ray dots past the minigame bounds.
    /// </summary>
    private float ComputeMaxRayDistanceToRect(Vector2 origin, Vector2 dir)
    {
        Rect rect = playAreaRect.rect;
        float tMin = 0f;
        float tMax = float.PositiveInfinity;
        const float eps = 1e-5f;

        if (Mathf.Abs(dir.x) < eps)
        {
            if (origin.x < rect.xMin || origin.x > rect.xMax) return 0f;
        }
        else
        {
            float tx1 = (rect.xMin - origin.x) / dir.x;
            float tx2 = (rect.xMax - origin.x) / dir.x;
            float txMin = Mathf.Min(tx1, tx2);
            float txMax = Mathf.Max(tx1, tx2);
            tMin = Mathf.Max(tMin, txMin);
            tMax = Mathf.Min(tMax, txMax);
        }

        if (Mathf.Abs(dir.y) < eps)
        {
            if (origin.y < rect.yMin || origin.y > rect.yMax) return 0f;
        }
        else
        {
            float ty1 = (rect.yMin - origin.y) / dir.y;
            float ty2 = (rect.yMax - origin.y) / dir.y;
            float tyMin = Mathf.Min(ty1, ty2);
            float tyMax = Mathf.Max(ty1, ty2);
            tMin = Mathf.Max(tMin, tyMin);
            tMax = Mathf.Min(tMax, tyMax);
        }

        if (tMax < tMin || tMax <= 0f) return 0f;
        return tMax;
    }

    // -------------------- Input / waves --------------------

    /// <summary>
    /// Adds a new projectile wave on Sword press for each participating player.
    /// The actual hits occur when the wave passes over a subparticle.
    /// </summary>
private void HandleSwordInput()
{
    foreach (var ps in _participants)
    {
        if (ps.rewiredPlayer == null) continue;

        // Late join: cannot fire until penalty ends
        if (ps.isLate && ps.penaltyRemaining > 0f)
            continue;

        if (ps.rewiredPlayer.GetButtonDown("Sword"))
        {
            FireProjectile(ps);
        }
    }
}



    /// <summary>
    /// Advances projectile waves, scales ray dots to show wave motion, and performs wave/subparticle collision checks.
    /// </summary>
private void FireProjectile(ParticipantState shooter)
{
    if (shooter == null || shooter.icon == null)
        return;

    Vector2 origin = shooter.icon.anchoredPosition;
    Vector2 dir = shooter.outwardDir;
    if (dir.sqrMagnitude < 1e-6f)
        dir = Vector2.up;
    dir.Normalize();

    float maxDist = ComputeMaxRayDistanceToRect(origin, dir);
    if (maxDist <= 0.001f)
        return;

    float spacing = Mathf.Max(0.001f, rayDotSpacing);

    int dotCount = Mathf.FloorToInt(maxDist / spacing);
    dotCount = Mathf.Clamp(dotCount, 6, projectileMaxDots);

    RectTransform parent = (projectilesRoot != null) ? projectilesRoot : playersRoot;
    if (parent == null) return;

    var pr = new ProjectileRay
    {
        shooter = shooter,
        origin = origin,
        dir = dir,
        wavePos = 0f
    };

    // Create dots once, fixed in space along the frozen ray
    for (int i = 0; i < dotCount; i++)
    {
        var dot = CreateRayDot(parent);
        dot.anchoredPosition = origin + dir * (spacing * (i + 1));
        dot.localScale = Vector3.zero; // hidden until the wave passes
        pr.dots.Add(dot);
    }

    _projectiles.Add(pr);
}

private void UpdateProjectiles(float dt)
{
    for (int i = _projectiles.Count - 1; i >= 0; i--)
    {
        var pr = _projectiles[i];

        pr.wavePos += projectileSpeed * dt;

        // Finished when wave head passes the last dot
        if (pr.wavePos >= pr.dots.Count - 1)
        {
            CleanupProjectile(pr);
            _projectiles.RemoveAt(i);
            continue;
        }

        ApplyProjectileWaveVisual(pr);
        TryHitParticleAlongProjectile(pr);
    }
}

private void ApplyProjectileWaveVisual(ProjectileRay pr)
{
    int count = pr.dots.Count;

    for (int i = 0; i < count; i++)
    {
        float scale = 0f; // hidden unless it's part of the wave tail/head

        float offset = i - pr.wavePos; // tail behind, head ahead
        if (offset >= 0f && offset <= 1f)
        {
            float t = 1f - offset;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            scale = rayDotBaseScale * (1f + projectileScaleIncrease * eased);
        }
        else if (offset < 0f && offset >= -5f)
        {
            float t = (offset + 5f) / 5f;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            scale = rayDotBaseScale * (1f + projectileScaleIncrease * eased);
        }

        if (pr.dots[i] != null)
            pr.dots[i].localScale = Vector3.one * scale;
    }
}

private void TryHitParticleAlongProjectile(ProjectileRay pr)
{
    if (_particles.Count == 0 || pr == null || pr.shooter == null)
        return;

    float spacing = Mathf.Max(0.001f, rayDotSpacing);
    Vector2 wavePos2D = pr.origin + pr.dir * (pr.wavePos * spacing);

    foreach (var p in _particles)
    {
        if (p.consumed || p.missed) continue;

        for (int s = 0; s < p.subRects.Count; s++)
        {
            if (!p.subAlive[s]) continue;
            if (p.subShrink[s] >= 0f) continue;

            Vector2 rootPos = (p.root != null) ? p.root.anchoredPosition : p.position;
            Vector2 subCenter = rootPos + (Vector2)p.subRects[s].anchoredPosition;

            float visualRadius = (p.subRects[s].sizeDelta.x * p.subRects[s].localScale.x) * 0.5f;
            float hitRadius = visualRadius + projectileHitPadding;

            if ((subCenter - wavePos2D).sqrMagnitude <= hitRadius * hitRadius)
            {
                BeginSubparticleShrink(p, s, pr.shooter);
                return;
            }
        }
    }
}

private void CleanupProjectile(ProjectileRay pr)
{
    if (pr == null) return;

    for (int i = 0; i < pr.dots.Count; i++)
    {
        if (pr.dots[i] != null)
            Destroy(pr.dots[i].gameObject);
    }
    pr.dots.Clear();
}

    // -------------------- Particles --------------------

    /// <summary>
    /// Spawns new particles, moves/spins/vibrates them, updates shrink animations, and cleans up finished particles.
    /// </summary>
    private void UpdateParticles(float dt)
    {
        if (particlesRoot == null || coreRect == null || playAreaRect == null)
            return;

        Vector2 corePos = coreRect.anchoredPosition;

        // spawn
        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            SpawnCoreParticle(corePos);
            float rate = Mathf.Max(0.01f, spawnRatePerSecond + UnityEngine.Random.Range(-spawnRateJitter, spawnRateJitter));
            _spawnTimer = 1f / rate;
        }

        // update
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];

            if (!p.consumed && !p.missed)
            {
                Vector2 prevPos = p.position;
                Vector2 nextPos = prevPos + p.direction * (p.speed * dt);

                // robust: did we cross within the hit radius at any point this frame?
                float r = coreHitRadius;
                bool hitCoreThisFrame = SqDistPointSegment(corePos, prevPos, nextPos) <= (r * r);

                if (hitCoreThisFrame)
                {
                    // Snap to core and trigger the existing miss/capture logic
                    p.position = corePos;

                    if (p.lastHitBy == null)
                    {
                        p.missed = true;
                        BeginShrinkAllSub(p);
                    }
                    else
                    {
                        p.speed = 0f;
                    }
                }
                else
                {
                    p.position = nextPos;
                }


                // spin
                p.spinAngle += p.spinSpeed * dt;
                if (p.root != null)
                    p.root.localRotation = Quaternion.AngleAxis(p.spinAngle, Vector3.forward);
            }

            // vibration + position apply
            if (p.root != null)
            {
                float tNoise = Time.time * particleVibrationFrequency + p.noiseSeed;
                float nx = (Mathf.PerlinNoise(tNoise, 0f) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(0f, tNoise) - 0.5f) * 2f;
                Vector2 vib = new Vector2(nx, ny) * particleVibrationAmplitude;
                p.root.anchoredPosition = p.position + vib;
            }

            // shrink anim
            bool anyAlive = false;
            for (int s = 0; s < p.subRects.Count; s++)
            {
                if (!p.subAlive[s]) continue;

                float t = p.subShrink[s];
                if (t >= 0f)
                {
                    t += dt / Mathf.Max(0.001f, subShrinkDuration);
                    float clamped = Mathf.Clamp01(t);
                    float eased = Mathf.SmoothStep(1f, 0f, clamped);
                    p.subRects[s].localScale = Vector3.one * eased;

                    if (clamped >= 1f)
                    {
                        // Compute explosion center BEFORE we disable/zero this sub:
                        Vector2 rootPos = (p.root != null) ? p.root.anchoredPosition : p.position;
                        Vector2 hitPos = rootPos + (Vector2)p.subRects[s].anchoredPosition;

                        p.subAlive[s] = false;
                        p.subShrink[s] = -1f;
                        p.subRects[s].localScale = Vector3.zero;

                        // If that was the LAST alive sub and this wasn't a miss, explode once.
                        if (!p.exploded && !p.missed)
                        {
                            bool anyAliveAfter = false;
                            for (int k = 0; k < p.subAlive.Count; k++)
                            {
                                if (p.subAlive[k]) { anyAliveAfter = true; break; }
                            }

                            if (!anyAliveAfter)
                            {
                                p.exploded = true;
                                SpawnMiniExplosion(hitPos);
                            }
                        }
                    }
                    else
                    {
                        p.subShrink[s] = t;
                    }
                }

                if (p.subAlive[s]) anyAlive = true;
            }

            // consumed (award score)
            if (!anyAlive && !p.consumed && !p.missed)
            {
                p.consumed = true;
                AwardParticleMass(p);
            }

            // cleanup
            if ((p.consumed || p.missed) && !HasVisibleSub(p))
            {
                if (p.root != null) Destroy(p.root.gameObject);
                _particles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Returns true if any subparticle is still alive (visible) for a particle.
    /// </summary>
    private bool HasVisibleSub(CoreParticle p)
    {
        for (int i = 0; i < p.subRects.Count; i++)
            if (p.subAlive[i]) return true;
        return false;
    }

    /// <summary>
    /// Samples a spawn angle biased toward left/right (high |cos|), with a non-zero floor weight.
    /// </summary>
    private float SampleSpawnAngleBiased()
    {
        for (int k = 0; k < spawnBiasAttempts; k++)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float side = Mathf.Abs(Mathf.Cos(a));
            float w = spawnMinWeight + (1f - spawnMinWeight) * Mathf.Pow(side, spawnSideExponent);
            if (UnityEngine.Random.value < w)
                return a;
        }
        return UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// Spawns a procedural 1–3 subparticle blob outside playAreaRect and targets it toward the core.
    /// Also builds glue bridges between subs.
    /// </summary>
    private void SpawnCoreParticle(Vector2 corePos)
    {
        if (particlesRoot == null || playAreaRect == null)
            return;

        int subCount = UnityEngine.Random.Range(1, 4);

        float angle = SampleSpawnAngleBiased();
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        Rect rect = playAreaRect.rect;
        float halfDiag = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.5f;
        float spawnRadius = halfDiag + subparticleRadius * 2f;
        Vector2 spawnPos = corePos + dir * spawnRadius;

        float speedMult = 1f + UnityEngine.Random.Range(-particleSpeedVariance, particleSpeedVariance);
        float speed = Mathf.Max(0.01f, particleSpeed * speedMult);

        var p = new CoreParticle
        {
            originalSubCount = subCount,
            position = spawnPos,
            direction = (corePos - spawnPos).normalized,
            speed = speed,
            consumed = false,
            missed = false,
            lastHitBy = null,
            spinAngle = UnityEngine.Random.Range(0f, 360f),
            spinSpeed = UnityEngine.Random.Range(-particleSpinSpeed, particleSpinSpeed),
            noiseSeed = UnityEngine.Random.value * 1000f,
            exploded = false
        };

        var rootGO = new GameObject("CoreParticle", typeof(RectTransform));
        var rt = rootGO.GetComponent<RectTransform>();
        rt.SetParent(particlesRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = spawnPos;
        rt.localRotation = Quaternion.AngleAxis(p.spinAngle, Vector3.forward);
        p.root = rt;

        // material for subs (shared)
        EnsureRayDotMaterial();
        Material subMat = _rayDotMaterial;


        Vector2 forward = p.direction.normalized;
        Vector2 normal = new Vector2(-forward.y, forward.x);
        float r = subparticleRadius;
        float spacing = r * subSpacingFactor;

        List<Vector2> offsets = new List<Vector2>(subCount);

        for (int i = 0; i < subCount; i++)
        {
            var go = new GameObject("Sub", typeof(RectTransform), typeof(Image));
            var srt = go.GetComponent<RectTransform>();
            srt.SetParent(p.root, false);
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(r * 2f, r * 2f);
            srt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.maskable = true;

            if (subMat != null)
            {
                img.material = subMat;
                img.color = Color.white;
                img.sprite = null; // shader handles the circle
            }
            else
            {
                // Fallback: use a sprite if provided, so you don't get squares
                img.material = null;
                img.sprite = circleSpriteFallback;
                img.color = Color.black;
            }


            Vector2 offset = Vector2.zero;

            if (subCount == 1)
            {
                offset = Vector2.zero;
            }
            else if (subCount == 2)
            {
                float sign = (i == 0) ? -1f : 1f;
                offset = normal * (sign * spacing);
            }
            else
            {
                float triRad = spacing;
                if (i == 0) offset = forward * triRad;
                else
                {
                    float sideSign = (i == 1) ? -1f : 1f;
                    Vector2 baseDir = -forward * (triRad * 0.4f);
                    offset = baseDir + normal * (sideSign * triRad);
                }
            }

            srt.anchoredPosition = offset;
            offsets.Add(offset);

            p.subRects.Add(srt);
            p.subAlive.Add(true);
            p.subShrink.Add(-1f);
        }

        // glue bridges
        if (subCount == 2)
        {
            AddBridgeDots(p, subMat, offsets[0], offsets[1]);
        }
        else if (subCount == 3)
        {
            AddBridgeDots(p, subMat, offsets[0], offsets[1]);
            AddBridgeDots(p, subMat, offsets[1], offsets[2]);
            AddBridgeDots(p, subMat, offsets[2], offsets[0]);
        }

        _particles.Add(p);
    }

    /// <summary>
    /// Adds small bridge dots between two subparticle offsets to simulate “glue”.
    /// Uses a mid-pinched radius profile (smallest at midpoint, larger toward ends).
    /// </summary>
    private void AddBridgeDots(CoreParticle p, Material subMat, Vector2 a, Vector2 b)
    {
        int n = Mathf.Max(1, bridgeDotsPerConnection);

        float endR = subparticleRadius * bridgeDotRadiusFactor;
        float midR = subparticleRadius * (bridgeDotRadiusFactor * 0.45f);

        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)(n + 1);
            Vector2 pos = Vector2.Lerp(a, b, t);

            float u = Mathf.Abs(t - 0.5f) * 2f; // 0 at midpoint, 1 at ends
            float rr = Mathf.Lerp(midR, endR, Mathf.SmoothStep(0f, 1f, u));
            float diameter = rr * 2f;

            var go = new GameObject("Bridge", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(p.root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            if (subMat != null) { img.material = subMat; img.color = Color.white; }
            else img.color = Color.black;
        }
    }

    /// <summary>
    /// Starts shrinking a single subparticle for a particle (at most once per sub).
    /// Also records who last hit the particle for scoring.
    /// </summary>
    private void BeginSubparticleShrink(CoreParticle p, int subIndex, ParticipantState hitter)
    {
        if (subIndex < 0 || subIndex >= p.subRects.Count) return;
        if (!p.subAlive[subIndex]) return;
        if (p.subShrink[subIndex] >= 0f) return;

        p.subShrink[subIndex] = 0f;
        p.lastHitBy = hitter;
    }

    /// <summary>
    /// Starts shrinking all remaining subs (used when particle hits core and is “missed”).
    /// Clears lastHitBy so misses never award score.
    /// </summary>
    private void BeginShrinkAllSub(CoreParticle p)
    {
        for (int s = 0; s < p.subRects.Count; s++)
        {
            if (!p.subAlive[s]) continue;
            if (p.subShrink[s] >= 0f) continue;
            p.subShrink[s] = 0f;
        }
        p.lastHitBy = null;
    }

    // -------------------- Burst FX --------------------

    /// <summary>
    /// Spawns a small burst of circles (10–15) outward from a point; each shrinks quickly to 0.
    /// </summary>
    private void SpawnMiniExplosion(Vector2 center)
    {
        if (particlesRoot == null) return;

        int count = UnityEngine.Random.Range(explosionCountMin, explosionCountMax + 1);
        float r = subparticleRadius * explosionDotRadiusFactor;
        float diameter = r * 2f;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("BurstDot", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(particlesRoot, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.anchoredPosition = center;

            var img = go.GetComponent<Image>();
            if (_rayDotMaterial != null) { img.material = _rayDotMaterial; img.color = Color.white; }
            else img.color = Color.black;

            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float spd = explosionSpeed * UnityEngine.Random.Range(0.7f, 1.3f);

            _burstDots.Add(new BurstDot
            {
                rt = rt,
                vel = dir * spd,
                age = 0f,
                life = explosionLifetime * UnityEngine.Random.Range(0.85f, 1.15f),
            });
        }
    }

    /// <summary>
    /// Updates burst dots: moves them outward and scales them smoothly down to 0, then destroys.
    /// </summary>
    private void UpdateBurstDots(float dt)
    {
        for (int i = _burstDots.Count - 1; i >= 0; i--)
        {
            var b = _burstDots[i];
            b.age += dt;

            if (b.rt == null || b.age >= b.life)
            {
                if (b.rt != null) Destroy(b.rt.gameObject);
                _burstDots.RemoveAt(i);
                continue;
            }

            b.rt.anchoredPosition += b.vel * dt;

            float t = Mathf.Clamp01(b.age / b.life);
            float s = Mathf.SmoothStep(1f, 0f, t);
            b.rt.localScale = Vector3.one * s;

            _burstDots[i] = b;
        }
    }

    // -------------------- Scoring --------------------

    /// <summary>
    /// Awards mass and increments the “particles captured” counters for UI.
    /// </summary>
    private void AwardParticleMass(CoreParticle p)
    {
        if (!_scoringEnabled)
        return;

        if (p.lastHitBy == null) return;

        float mass = massPerSubparticle * p.originalSubCount;
        p.lastHitBy.totalCapturedMass += mass;

        int teamId = p.lastHitBy.controller.teamID;
        if (teamId == lightTeamIndex) _lightTeamParticlesCaptured++;
        else if (teamId == darkTeamIndex) _darkTeamParticlesCaptured++;

        PushScoreToUI();
    }

    /// <summary>
    /// Writes the particle capture counts to the scene-side AnomalyUIController (Option A).
    /// </summary>
    private void PushScoreToUI()
    {
        var ui = (Context.manager != null) ? Context.manager.ui : null;
        if (ui == null) return;

        ui.SetParticleCounts(_lightTeamParticlesCaptured, _darkTeamParticlesCaptured);
    }

    // -------------------- Participants --------------------

    /// <summary>
    /// Builds participant list from Context.participants and spawns their icon + ray dots.
    /// </summary>
    private void BuildParticipants()
    {
        _participants.Clear();

        if (Context.participants == null)
            return;

        var lightTeam = new List<ParticipantState>();
        var darkTeam  = new List<ParticipantState>();

        foreach (var p in Context.participants)
        {
            if (p == null) continue;

            var ps = new ParticipantState
            {
                controller = p,
                rewiredPlayer = ReInput.players.GetPlayer(p.playerID),
                totalCapturedMass = 0f,
                angleRad = 0f
            };

            _participants.Add(ps);

            if (p.teamID == lightTeamIndex) lightTeam.Add(ps);
            else if (p.teamID == darkTeamIndex) darkTeam.Add(ps);
        }

        lightTeam.Sort((a, b) => a.controller.playerID.CompareTo(b.controller.playerID));
        darkTeam.Sort((a, b) => a.controller.playerID.CompareTo(b.controller.playerID));

        AssignSpawnAngles(lightTeam, darkTeam);

        foreach (var ps in _participants)
        {
            ps.icon = CreateIcon(ps);
            ps.icon.gameObject.SetActive(true);
            
            // Cache label once
            ps.label = ps.icon.GetComponentInChildren<TMP_Text>(true);

            ps.rayDots = new List<RectTransform>();
            int count = _maxDotsPerRay > 0 ? _maxDotsPerRay : 16;
            for (int i = 0; i < count; i++)
            {
                var dot = CreateRayDot(playersRoot);
                ps.rayDots.Add(dot);
            }
        }
    }

    /// <summary>
    /// Assigns the initial orbit angles for players (2-player and 4-player layouts supported).
    /// </summary>
    private void AssignSpawnAngles(List<ParticipantState> lightTeam, List<ParticipantState> darkTeam)
    {
        int total = lightTeam.Count + darkTeam.Count;

        if (lightTeam.Count == 1 && darkTeam.Count == 1 && total == 2)
        {
            lightTeam[0].angleRad = DegToRad(270f);
            darkTeam[0].angleRad  = DegToRad(90f);
            return;
        }

        if (lightTeam.Count == 2 && darkTeam.Count == 2 && total == 4)
        {
            lightTeam[0].angleRad = DegToRad(315f);
            lightTeam[1].angleRad = DegToRad(225f);
            darkTeam[0].angleRad  = DegToRad(45f);
            darkTeam[1].angleRad  = DegToRad(135f);
            return;
        }

        var all = new List<ParticipantState>();
        all.AddRange(lightTeam);
        all.AddRange(darkTeam);

        if (all.Count == 0) return;

        float step = 360f / all.Count;
        float startDeg = 270f;
        for (int i = 0; i < all.Count; i++)
            all[i].angleRad = DegToRad(startDeg + step * i);
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    private float DegToRad(float deg) => deg * Mathf.Deg2Rad;

    /// <summary>
    /// Creates a procedural circle icon + P# label for a player (uses CircleIcon shader).
    /// </summary>
    private RectTransform CreateIcon(ParticipantState ps)
    {
        var go = new GameObject($"Icon_P{ps.controller.playerID}", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(playersRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(iconDiameter, iconDiameter);
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();

        if (_iconBaseMaterial == null)
        {
            var shader = Shader.Find(IconShaderName);
            if (shader != null) _iconBaseMaterial = new Material(shader);
        }

        Material iconMatInstance = null;
        if (_iconBaseMaterial != null)
        {
            iconMatInstance = new Material(_iconBaseMaterial);
            img.material = iconMatInstance;
        }

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;

        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        if (iconLabelFont != null) tmp.font = iconLabelFont;
        tmp.fontSize = iconLabelFontSize;
        tmp.text = "P" + ps.controller.playerID;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        bool isLight = (ps.controller.teamID == lightTeamIndex);
        bool isDark  = (ps.controller.teamID == darkTeamIndex);

        if (iconMatInstance != null)
        {
            if (isLight)
            {
                iconMatInstance.SetColor("_FillColor", lightFillColor);
                iconMatInstance.SetColor("_OutlineColor", lightOutlineColor);
                iconMatInstance.SetFloat("_OutlineWidth", iconOutlineWidth);
            }
            else if (isDark)
            {
                iconMatInstance.SetColor("_FillColor", darkFillColor);
                iconMatInstance.SetColor("_OutlineColor", darkOutlineColor);
                iconMatInstance.SetFloat("_OutlineWidth", iconOutlineWidth);
            }
            else
            {
                iconMatInstance.SetColor("_FillColor", darkFillColor);
                iconMatInstance.SetColor("_OutlineColor", darkOutlineColor);
                iconMatInstance.SetFloat("_OutlineWidth", iconOutlineWidth);
            }
        }

        if (isLight) tmp.color = lightTextColor;
        else if (isDark) tmp.color = darkTextColor;
        else tmp.color = darkTextColor;

        return rt;
    }

    /// <summary>
    /// Creates one dot used in the player's dotted ray (CircleIcon shader with zero outline).
    /// </summary>
    private RectTransform CreateRayDot(RectTransform parent)
    {
        var go = new GameObject("RayDot", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(rayDotSize, rayDotSize);
        rt.localScale = Vector3.zero; // instead of base scale
        

        var img = go.GetComponent<Image>();

        if (_rayDotMaterial == null)
        {
            var shader = Shader.Find(IconShaderName);
            if (shader != null)
            {
                _rayDotMaterial = new Material(shader);
                _rayDotMaterial.SetColor("_FillColor", rayDotColor);
                _rayDotMaterial.SetColor("_OutlineColor", rayDotColor);
                _rayDotMaterial.SetFloat("_OutlineWidth", 0.0f);
            }
        }

        if (_rayDotMaterial != null)
        {
            img.material = _rayDotMaterial;
            img.color = Color.white;
            img.maskable = true;

        }
        else
        {
            img.color = rayDotColor;
        }

        return rt;
    }

    /// <summary>
/// Called at the start of outro. Stops scoring, stops spawning, and starts shrinking all live subs.
/// Particles continue moving until they finish shrinking.
/// </summary>
public void BeginOutroDissolve()
{
    _outroDissolveActive = true;
    _scoringEnabled = false;

    // Start shrinking every live sub immediately
    for (int i = 0; i < _particles.Count; i++)
    {
        var p = _particles[i];
        if (p.consumed) continue;

        for (int s = 0; s < p.subRects.Count; s++)
        {
            if (!p.subAlive[s]) continue;
            if (p.subShrink[s] >= 0f) continue;

            p.subShrink[s] = 0f; // begin shrink animation
        }

        // During outro we don't want a "miss" to clear attribution etc.
        // Just let them shrink out.
    }
}


    // -------------------- End game --------------------

    /// <summary>
    /// Computes winner/score, cleans up runtime objects, and calls Complete(result).
    /// </summary>
private void EndMinigame()
{
    if (IsFinished) return;

    // stop sim/input immediately; transition will handle visuals
    SetGameplayEnabled(false);

    var teamToMass = new Dictionary<int, float>();
    ParticipantState best = null;

    foreach (var ps in _participants)
    {
        int team = ps.controller.teamID;
        if (!teamToMass.ContainsKey(team)) teamToMass[team] = 0f;
        teamToMass[team] += ps.totalCapturedMass;

        if (best == null || ps.totalCapturedMass > best.totalCapturedMass)
            best = ps;
    }

    float totalMass = 0f;
    foreach (var kv in teamToMass) totalMass += kv.Value;

    bool success = totalMass >= minTotalMassForSuccess;

    int winningTeam = -1;
    float winningMass = 0f;
    foreach (var kv in teamToMass)
    {
        if (kv.Value > winningMass)
        {
            winningMass = kv.Value;
            winningTeam = kv.Key;
        }
    }

    var result = new AnomalyResult
    {
        success = success,
        winningPlayer = best != null ? best.controller : null,
        winningTeamIndex = winningTeam,
        score = winningMass
    };

    Complete(result);
}


// ====================
// Instructions Preview
// ====================

/// <summary>
/// Starts an endless, no-input, no-score preview suitable for the Instructions screen.
/// Spawns only incoming particles and keeps them looping.
/// </summary>
public void BeginInstructionsPreview()
{
    _instructionsPreviewActive = true;
    // Ensure we don't carry any runtime junk if this object gets re-enabled.
    ClearAllProjectiles();
    ClearAllParticlesAndBursts();

    // Hide gameplay UI roots (no players/rays/projectiles in the preview).
    if (playersRoot != null) playersRoot.gameObject.SetActive(false);
    if (projectilesRoot != null) projectilesRoot.gameObject.SetActive(false);

    // Prevent score/UI writes.
    _scoringEnabled = false;

    // Start "gameplay" updates so particles spawn/move.
    _gameplayEnabled = true;

    // Force the loop to never end.
    _timeRemaining = float.PositiveInfinity;

    // Mark started so Update() runs.
    _started = true;

    if (previewAutoScale)
        ApplyPreviewAutoScale();


    // Make sure play area sizing is computed.
    RecomputeOrbitRadius();

    // IMPORTANT: avoid spawning a new Material every particle when there are no ray dots.
    EnsureRayDotMaterial();

    // Spawn immediately.
    _spawnTimer = 0f;
}

private void ClearAllParticlesAndBursts()
{
    for (int i = _particles.Count - 1; i >= 0; i--)
    {
        var p = _particles[i];
        if (p.root != null)
            Destroy(p.root.gameObject);
    }
    _particles.Clear();

    for (int i = _burstDots.Count - 1; i >= 0; i--)
    {
        var b = _burstDots[i];
        if (b.rt != null)
            Destroy(b.rt.gameObject);
    }
    _burstDots.Clear();
}

/// <summary>
/// In normal gameplay this gets created when ray dots are created.
/// In preview mode we might never create ray dots, so we must create it once here
/// to prevent per-particle material allocation.
/// </summary>
private void EnsureRayDotMaterial()
{
    if (_rayDotMaterial != null) return;

    if (circleIconMaterialTemplate != null)
    {
        _rayDotMaterial = new Material(circleIconMaterialTemplate);
    }
    else
    {
        var shader = Shader.Find(IconShaderName);
        if (shader == null)
        {
            Debug.LogError($"[NovaCoreMinigame] Shader not found: {IconShaderName}. " +
                           "Assign 'circleIconMaterialTemplate' to avoid squares.", this);
            return;
        }

        _rayDotMaterial = new Material(shader);
    }

    _rayDotMaterial.SetColor("_FillColor", rayDotColor);
    _rayDotMaterial.SetColor("_OutlineColor", rayDotColor);
    _rayDotMaterial.SetFloat("_OutlineWidth", 0.0f);
}


private static float SqDistPointSegment(Vector2 p, Vector2 a, Vector2 b)
{
    Vector2 ab = b - a;
    float ab2 = ab.sqrMagnitude;
    if (ab2 < 1e-8f) return (p - a).sqrMagnitude;

    float t = Vector2.Dot(p - a, ab) / ab2;
    t = Mathf.Clamp01(t);

    Vector2 closest = a + ab * t;
    return (p - closest).sqrMagnitude;
}

private void ApplyPreviewAutoScale()
{
    if (playAreaRect == null || coreRect == null)
        return;

    // Ensure layout has run so rect sizes are valid
    Canvas.ForceUpdateCanvases();

    Rect pr = playAreaRect.rect;
    float minDim = Mathf.Min(pr.width, pr.height);
    if (minDim <= 1f) return;

    // 1) Subparticle size (visual readability)
    float r = Mathf.Clamp(minDim * previewSubRadiusPercent, 2f, 18f);
    subparticleRadius = r;

    // 2) Core hit radius based on core visual size
    float coreVisualRadius = Mathf.Min(coreRect.rect.width, coreRect.rect.height) * 0.5f;
    coreHitRadius = Mathf.Max(coreVisualRadius * previewCoreHitRadiusMultiplier, r * 2f);

    // 3) Particle speed so it reaches the core in ~previewTravelTimeSeconds
    float halfDiag = Mathf.Sqrt(pr.width * pr.width + pr.height * pr.height) * 0.5f;
    float spawnRadius = halfDiag + r * 2f;

    float t = Mathf.Max(0.01f, previewTravelTimeSeconds);
    particleSpeed = Mathf.Clamp(spawnRadius / t, 60f, 1400f);

    // Optional: make vibration scale with size (keeps it subtle at any resolution)
    particleVibrationAmplitude = Mathf.Clamp(r * 0.12f, 0.5f, 6f);

    // Optional: spawn rate scales a bit with box size (keeps it lively but not insane)
    float targetRate = Mathf.Clamp(minDim / 140f, 2f, 7f);
    spawnRatePerSecond = targetRate;
    spawnRateJitter = targetRate * 0.25f;
}

private void ApplyLateJoinSetup()
{
    bool hasOnTimeInfo = _onTimePlayerIds.Count > 0;

    foreach (var ps in _participants)
    {
        if (ps.controller == null) continue;

        bool onTime = !hasOnTimeInfo || _onTimePlayerIds.Contains(ps.controller.playerID);

        if (onTime || lateJoinPenaltySeconds <= 0f)
        {
            ps.isLate = false;
            ps.penaltyDuration = 0f;
            ps.penaltyRemaining = 0f;
            ps.joinScale01 = 1f;

            if (ps.label != null)
                ps.label.gameObject.SetActive(true);
        }
        else
        {
            ps.isLate = true;
            ps.penaltyDuration = Mathf.Max(0.001f, lateJoinPenaltySeconds);
            ps.penaltyRemaining = ps.penaltyDuration;
            ps.joinScale01 = 0f;

            if (ps.label != null)
                ps.label.gameObject.SetActive(false);
        }
    }

    ApplyParticipantVisuals();
}

private void ApplyParticipantVisuals()
{
    float rayScale = rayDotBaseScale * _globalRayDotsScale01;

    foreach (var ps in _participants)
    {
        // Icon scale = transition scale * late-join scale
        if (ps.icon != null)
        {
            float s = _globalIconsScale01 * Mathf.Clamp01(ps.joinScale01);
            ps.icon.localScale = Vector3.one * s;
        }

        // Label: hidden until penalty ends
        if (ps.label != null)
        {
            bool shouldShow = !ps.isLate || ps.penaltyRemaining <= 0f;

            if (ps.label.gameObject.activeSelf != shouldShow)
                ps.label.gameObject.SetActive(shouldShow);

            var c = ps.label.color;
            c.a = shouldShow ? _globalLabelsAlpha01 : 0f;
            ps.label.color = c;
        }

        // Aim rays (static dotted rays)
        if (ps.rayDots != null)
        {
            for (int i = 0; i < ps.rayDots.Count; i++)
                if (ps.rayDots[i] != null)
                    ps.rayDots[i].localScale = Vector3.one * rayScale;
        }
    }
}

private void UpdateLateJoinPenalties(float dt)
{
    bool changed = false;

    foreach (var ps in _participants)
    {
        if (!ps.isLate) continue;
        if (ps.penaltyRemaining <= 0f) continue;

        ps.penaltyRemaining -= dt;
        if (ps.penaltyRemaining < 0f) ps.penaltyRemaining = 0f;

        float t01 = 1f - (ps.penaltyRemaining / ps.penaltyDuration);
        t01 = Mathf.Clamp01(t01);

        float eased = (lateJoinScaleEase != null) ? lateJoinScaleEase.Evaluate(t01) : t01;
        ps.joinScale01 = Mathf.Clamp01(eased);

        changed = true;
    }

    if (changed)
        ApplyParticipantVisuals();
}










}
