using System;
using System.Collections;
using UnityEngine;
using Rigidbody = UnityEngine.Rigidbody;
using Massive.Player;      // PlayerAttackController
using Massive.PowerUps;    // PlayerPowerUpController + PowerUpInputState

/// <summary>
/// Optional hook component for death/respawn presentation (GPU-driven dissolve, etc).
/// Assign a MonoBehaviour that implements this interface to `lifeFx`.
/// </summary>
public interface IPlayerLifeFx
{
    IEnumerator PlayDeath(PlayerControllerScript player, float duration);
    IEnumerator PlayRespawn(PlayerControllerScript player, float duration);

    /// <summary>
    /// Force a hard visible/hidden state (used at the end of dissolve).
    /// Implementations may enable/disable render drivers here.
    /// </summary>
    void SetVisibleInstant(PlayerControllerScript player, bool visible);
}

[DisallowMultipleComponent]
public class PlayerControllerScript : MonoBehaviour
{
    private Rewired.Player rewiredPlayer;

    [Header("Identity")]
    public int playerID;
    public int teamID;

    [Header("Gameplay Rig Root (Anomalies / Minigames)")]
    [SerializeField] private Transform gameplayRigRoot; // assign in prefab if you can
    [SerializeField] private string gameplayRigRootName = "GameplayRigRoot";

    private bool _worldGameplaySuppressed;


    [Header("Movement")]
    [SerializeField] private float movePower = 10f;

    [Tooltip("Scale movement while an attack stage is active (1 = no slowdown).")]
    [SerializeField, Range(0.1f, 1.0f)] private float attackingMoveScale = 0.6f;

    [Tooltip("Ignore tiny stick noise before applying force.")]
    [SerializeField, Range(0.05f, 0.6f)] private float moveDeadzone = 0.15f;

    [Tooltip("Movement multiplier while shielding.")]
    [SerializeField, Range(0.05f, 1f)] private float shieldMoveMultiplier = 0.4f;

    [Header("Movement Feel (Turn On A Dime)")]
    [SerializeField] private float maxMoveSpeed = 7.5f;     // XZ max speed (set to your current “feels right” top speed)
    [SerializeField] private float lateralFriction = 35f;   // higher = less drift / sharper turns
    [SerializeField] private float reverseBrake = 45f;      // extra braking when input is opposite current velocity
    [SerializeField, Range(-1f, 1f)] private float reverseDotThreshold = 0.0f; // dot < 0 means reversing
    [SerializeField] private float idleBrake = 8f;          // braking when stick released
    [SerializeField] private bool clampSpeed = true;


    private Rigidbody rb;
    private Vector3 spawnAnchorWS;

    [Header("Scene / Refs")]
    [Tooltip("Leave empty to auto-find TEAM 1 / TEAM 2 objects by name.")]
    public GameObject goalZone;

    [Tooltip("Small mass blob projectile used for hit/score VFX (NOT the player blob).")]
    public GameObject massBlobPrefab;

    public PlayerVisualController visualsController; // assign in inspector
    [SerializeField] private PlayerNuggetsGPU nuggetsGPU; // assign or auto-find

    [Tooltip("Optional: visual sword root (melee is handled elsewhere).")]
    public GameObject sword;

    [Tooltip("Shield GameObject (rotated + toggled by this controller).")]
    public GameObject shield;

    private GameObject sm; // SFX module
    private GameObject stunEffect;

    [Header("Attack System")]
    [Tooltip("New combo/attack driver. Required for melee.")]
    public PlayerAttackController attackController;

    [Header("Power-Ups")]
    public PlayerPowerUpController powerUps; // assign or auto-find

    [Header("Shield Ability (Timed)")]
    [SerializeField] private Massive.Player.PlayerShieldAbility shieldAbility;


    [Header("Aim (Power-up routing)")]
    [SerializeField, Range(0.05f, 0.6f)] private float aimDeadzone = 0.18f;
    private Vector3 lastStickAimWS = Vector3.right;

    [Header("Mass v2 (Carry Cap + Overflow → Team Score)")]
    [Range(0f, 1f)]
    public float massScore = 0.5f;

    public float massScoreMin = 0f;
    public float massScoreMax = 1f;

    [SerializeField, Range(0f, 1f)] private float spawnMassScore01 = 0.5f;

    [Tooltip("Mass gained by attacker per melee hit (0..1 massScore space).")]
    [SerializeField] private float massGainPerHit = 0.08f;

    [Tooltip("Mass lost by victim per melee hit (0..1 massScore space).")]
    [SerializeField] private float massLossPerHit = 0.08f;

    [Tooltip("Continuous drain while shield is held (massScore units per second).")]
    [SerializeField] private float shieldDrainPerSecond01 = 0.004f;

    [Tooltip("Minimum seconds between valid Shrink() applications (prevents multi-hit spam).")]
    [SerializeField] private float hitShrinkCooldownSeconds = 0.10f;

    [Header("Mass → Nuggets")]
    public int minNuggets = 5;
    public int midNuggets = 50;
    public int maxNuggets = 300;

    [Header("Overflow Scoring")]
    [SerializeField] private bool overflowScoresToTeam = true;

    [Tooltip("Team score (0..1) gained per 1.0 overflow mass. Example: overflow 0.08 with k=0.25 => +0.02 score.")]
    [SerializeField, Range(0f, 2f)] private float overflowScorePerMass = 0.25f;

    [Header("Overflow VFX")]
    [SerializeField] private bool spawnOverflowScoreVFX = true;
    [SerializeField, Range(0, 10)] private int overflowVfxMaxBlobsPerEvent = 3;

    [Header("Legacy Deposit Helpers (kept for compatibility; ScoreSphere legacy mode should be OFF)")]
    [SerializeField] private float legacyGoalShrink01 = 0.0045f;

    [Header("Life FX Hook")]
    [Tooltip("Assign PlayerLifeFx_DissolveGPU (or any IPlayerLifeFx).")]
    [SerializeField] private MonoBehaviour lifeFx;
    private IPlayerLifeFx _lifeFx;

    [Header("Respawn")]
    [SerializeField] private Transform respawnPointOverride;
    [SerializeField] private float deathFxSeconds = 0.25f;
    [SerializeField] private float respawnDelaySeconds = 0.5f;
    [SerializeField] private float respawnFxSeconds = 0.25f;

    
    
[Header("Match Start Spawn")]
[SerializeField] private bool playMatchSpawnOnSceneLoad = true;
[SerializeField] private float matchSpawnFxSeconds = -1f; // <0 => use respawnFxSeconds
[SerializeField] private bool showPlayerIdToastOnMatchStart = true;
[SerializeField] private float playerIdToastLifetime = 1.25f;

private bool _matchSpawning;






    [Tooltip("After respawn, ignore Shrink() hits for this many seconds.")]
    [SerializeField] private float respawnInvulnSeconds = 0.6f;

    [Tooltip("Helps avoid respawning on top of someone.")]
    [SerializeField] private float respawnCheckRadius = 1.0f;

    [SerializeField] private LayerMask respawnBlockMask = ~0;

    // ===== Stun =====
    [Header("Stun (Shield Parry) - Visual/Feel")]
    [SerializeField] private float stunNuggetDragMaxMul = 10f;
    [SerializeField] private float stunNuggetRampUpSeconds = 0.08f;
    [SerializeField] private float stunNuggetRampDownSeconds = 0.12f;
    [SerializeField] private float stunBlobJitterMul = 1.0f;

    [Header("Stun Knockback")]
    [SerializeField] private bool stunAppliesKnockback = true;

    // Interpreted as "meters/second of knockback at full strength"
    [SerializeField] private float stunKnockbackVelocity = 4.0f;

    // Absolute safety clamp for planar speed during the kick
    [SerializeField] private float stunMaxPlanarSpeed = 8.0f;

    // Prevent multiple knockback kicks from stacking if stun is triggered repeatedly
    [SerializeField] private float stunKnockbackLockout = 0.15f;

    private float _nextAllowedStunKickTime = -Mathf.Infinity;


    private float _baseNuggetDrag = -1f;
    private Coroutine _stunRoutine;


    // ===== State =====
    [Header("Stun")]
    public float stunTime = 1.25f;
    private bool isStunned = false;

    public bool IsStunned => isStunned;


    public bool isActive = true;
    [SerializeField] private float idleTime = 60f;
    public float timeSinceLastActivity;
    public float lastActivityTime;

    // Input snapshots (legacy exposed)
    public float moveHorizontal;
    public float moveVertical;
    public Vector3 movement;
    public bool didPlayerTapActionThisFrame = false;
    public bool shieldOn = false;

    public bool temporarilyEliminated = false;
    private float externalStunUntil = -Mathf.Infinity;
    public bool IsExternallyStunned => Time.time < externalStunUntil;
    public void ExternalStun(float seconds)
    {
        externalStunUntil = Mathf.Max(externalStunUntil, Time.time + Mathf.Max(0f, seconds));
    }

    // Team score refs
    private ScoreSphereScript _teamScoreSphere;
    private GameObject _teamScoreTarget;

    // Colliders cache
    private Collider[] _allColliders;

    // Timers
    private float _invulnUntil = -Mathf.Infinity;
    private float _timeOfLastShrink = -999f;

    private Coroutine _deathRoutine;

    // Events
    public event Action<PlayerControllerScript> DeathStarted;
    public event Action<PlayerControllerScript> DeathHidden;
    public event Action<PlayerControllerScript> RespawnStarted;
    public event Action<PlayerControllerScript> RespawnCompleted;

    public bool IsInvulnerable => Time.time < _invulnUntil;

private void OnEnable()
{
    if (!playMatchSpawnOnSceneLoad) return;

    // DO NOT call ForceHiddenForSpawn() here.
    // It can run before PlayerLifeFx_DissolveGPU.Awake caches default radii,
    // causing _baseRadius0/_dotRadius0 to become 0 => "1px dot" player.

    // Safe: hide render drivers without modifying baseRadius/dotRadius.
    if (!visualsController) visualsController = GetComponent<PlayerVisualController>();
    if (!nuggetsGPU) nuggetsGPU = GetComponentInChildren<PlayerNuggetsGPU>(true);

    if (visualsController) visualsController.enabled = false;
    if (nuggetsGPU) nuggetsGPU.enabled = false;
}



private void EnsureLifeFxCached()
{
    if (_lifeFx != null) return;

    if (lifeFx == null) lifeFx = GetComponent<PlayerLifeFx_DissolveGPU>();
    _lifeFx = lifeFx as IPlayerLifeFx;
}

private void ForceHiddenForSpawn()
{
    // Lock movement/interaction without marking player “inactive”
    _matchSpawning = true;
    lastActivityTime = Time.time;
    isActive = true;

    // Disable collisions + physics
    SetCollidersEnabled(false);

    if (rb != null)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.Sleep();
    }

    if (attackController != null) attackController.enabled = false;
    if (shield) shield.SetActive(false);
    if (sword) sword.SetActive(false);

    // Hide visuals
    if (_lifeFx != null) _lifeFx.SetVisibleInstant(this, false);
}

private IEnumerator MatchSpawnRoutine()
{
    _matchSpawning = true;

    // Keep “cabinet empty” logic from thinking this player is inactive
    lastActivityTime = Time.time;
    isActive = true;

    // Lock gameplay like your death routine does (but WITHOUT temporarilyEliminated)
    SetCollidersEnabled(false);

    if (rb != null)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.Sleep();
    }

    if (attackController != null) attackController.enabled = false;

    if (_lifeFx != null) _lifeFx.SetVisibleInstant(this, false);
    else
    {
        if (visualsController) visualsController.enabled = false;
        if (nuggetsGPU) nuggetsGPU.enabled = false;
    }

    // Spawn position: use your existing anchor logic
    Vector3 spawnPos = respawnPointOverride ? respawnPointOverride.position : spawnAnchorWS;
    spawnPos.y = transform.position.y;

    TeleportTo(spawnPos);

    // Snap visuals child so first render frame is correct (same fix you used in respawn)
    if (visualsController != null && visualsController.visuals != null)
        visualsController.visuals.position = spawnPos;

    // Optional “PLAYER X” toast during spawn
if (showPlayerIdToastOnMatchStart)
    Massive.Players.UI.PlayerIdToastSystem.Instance?.ShowForPlayer(this, playerIdToastLifetime);


    // Reset mass now, but rebuild AFTER form finishes (same as respawn)
    massScore = spawnMassScore01;

    float fx = (matchSpawnFxSeconds >= 0f) ? matchSpawnFxSeconds : respawnFxSeconds;

    AudioSystem.I?.Play(AudioEventId.Player_Spawn, transform.position);

    if (_lifeFx != null) yield return _lifeFx.PlayRespawn(this, fx);
    else if (fx > 0f) yield return new WaitForSeconds(fx);

    SyncNuggetsGeometryFromVisuals();
    UpdateMassAndNuggets(forceRebuild: true);

    // Re-enable gameplay
    if (rb != null)
    {
        rb.isKinematic = false;
        rb.WakeUp();
    }

    SetCollidersEnabled(true);

    _invulnUntil = Time.time + Mathf.Max(0f, respawnInvulnSeconds);

    if (attackController != null) attackController.enabled = true;

    _matchSpawning = false;
}


    private void Awake()
    {
        rewiredPlayer = Rewired.ReInput.players.GetPlayer(playerID);

        rb = GetComponent<Rigidbody>();
        if (!rb) Debug.LogWarning($"[{name}] No Rigidbody found on Player root.", this);

        sm = FindUnderRigOrRoot("SfxModule")?.gameObject;
        stunEffect = FindUnderRigOrRoot("StunnedEffect")?.gameObject;

        if (!shieldAbility)
            shieldAbility = GetComponent<Massive.Player.PlayerShieldAbility>();

        if (!powerUps) powerUps = GetComponent<PlayerPowerUpController>();
        if (!visualsController) visualsController = GetComponent<PlayerVisualController>();
        if (!nuggetsGPU) nuggetsGPU = GetComponentInChildren<PlayerNuggetsGPU>(true);

        if (lifeFx == null) lifeFx = GetComponent<PlayerLifeFx_DissolveGPU>();
        _lifeFx = lifeFx as IPlayerLifeFx;

        if (nuggetsGPU != null)
        _baseNuggetDrag = nuggetsGPU.drag;

        
        // Determine respawn anchor. If you assign respawnPointOverride, that becomes authoritative.
        spawnAnchorWS = respawnPointOverride ? respawnPointOverride.position : transform.position;



        _allColliders = GetComponentsInChildren<Collider>(true);

        lastActivityTime = Time.time;

        // Safety warning for the exact mistake you hit earlier:
        if (massBlobPrefab != null && massBlobPrefab.GetComponent<PlayerVisualController>() != null)
            Debug.LogWarning($"[{name}] massBlobPrefab looks like a Player blob object. Assign the SmallMassBlob prefab instead.", this);
    }

    private void Start()
    {
        if (goalZone == null)
            goalZone = (teamID == 1) ? GameObject.Find("TEAM 1") : GameObject.Find("TEAM 2");

        CacheTeamScoreRefs();

        // Initialize aim from visual if possible
        if (visualsController != null && visualsController.visuals != null)
        {
            var f = visualsController.visuals.right;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f) lastStickAimWS = f.normalized;
        }


                // original behavior
                massScore = spawnMassScore01;
                UpdateMassAndNuggets(forceRebuild: true);
            
    }

    public void BeginMatchStartSpawn()
{
    if (!gameObject.activeInHierarchy) return;
    if (_matchSpawning) return;

    EnsureLifeFxCached();
    ForceHiddenForSpawn();
    StartCoroutine(MatchSpawnRoutine());
}


    private void CacheTeamScoreRefs()
    {
        _teamScoreSphere = null;
        _teamScoreTarget = null;

        if (!goalZone) return;

        _teamScoreSphere = goalZone.GetComponentInChildren<ScoreSphereScript>(true);

        var scoreSphereT = goalZone.transform.Find("Score Sphere");
        _teamScoreTarget = scoreSphereT ? scoreSphereT.gameObject : null;
    }

    private void Update()
    {

        if (_matchSpawning)
        {
            // Don’t read inputs while spawning; also don’t go “inactive”
            lastActivityTime = Time.time;
            timeSinceLastActivity = 0f;
            isActive = true;
            return;
        }
        
        if (temporarilyEliminated)
        {
            moveHorizontal = 0f;
            moveVertical = 0f;
            movement = Vector3.zero;
            didPlayerTapActionThisFrame = false;
            shieldOn = false;

            timeSinceLastActivity = Time.time - lastActivityTime;
            isActive = false;
            return;
        }

        if (isStunned || IsExternallyStunned)
        {
            // Hard input lock
            movement = Vector3.zero;
            shieldOn = false;
            didPlayerTapActionThisFrame = false;

            // Optional: keep visuals stable while stunned
            if (visualsController != null)
            {
                visualsController.SetMoveInput(Vector2.zero);
                if (rb != null)
                    visualsController.velocityWS = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            }

            return;
        }

        if (_worldGameplaySuppressed)
            {
                // Clear world-driven state
                moveHorizontal = 0f;
                moveVertical = 0f;
                movement = Vector3.zero;
                shieldOn = false;
                didPlayerTapActionThisFrame = false;

                // Still keep inactivity bookkeeping correct
                timeSinceLastActivity = Time.time - lastActivityTime;
                isActive = timeSinceLastActivity <= idleTime;
                return;
            }



        // Input
        moveHorizontal = rewiredPlayer.GetAxis("MoveH");
        moveVertical = rewiredPlayer.GetAxis("MoveV");
        movement = new Vector3(moveHorizontal, 0f, moveVertical);
        float moveDz2 = moveDeadzone * moveDeadzone;
        bool moveActive = movement.sqrMagnitude > moveDz2;


        // Aim memory for power-ups
        float dz2 = aimDeadzone * aimDeadzone;
        if (movement.sqrMagnitude >= dz2)
            lastStickAimWS = movement.normalized;

        bool shieldDown = rewiredPlayer.GetButtonDown("Shield");

        if (shieldAbility != null)
        {
            if (shieldDown)
                shieldAbility.TryActivate();

            shieldOn = shieldAbility.IsActive;
        }
        else
        {
            // Legacy fallback if the ability isn't present
            shieldOn = rewiredPlayer.GetButton("Shield");
        }


        bool attackDown = rewiredPlayer.GetButtonDown("Sword");
        bool attackHeld = rewiredPlayer.GetButton("Sword");
        bool attackUp   = rewiredPlayer.GetButtonUp("Sword");

        didPlayerTapActionThisFrame = attackDown;

        // Visuals bridge
        if (visualsController != null)
        {
            visualsController.SetMoveInput(new Vector2(movement.x, movement.z));
            if (rb != null) visualsController.velocityWS = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        }

        // Feed stick to attack system for swipe direction
        if (attackController != null)
            attackController.ExternalMoveInput = new Vector2(movement.x, movement.z);

        // Power-up routing (can consume Sword)
        bool consumedAttack = false;
        if (powerUps != null)
        {
            Vector3 aimDir = lastStickAimWS;
            aimDir.y = 0f;

            if (aimDir.sqrMagnitude < 0.0001f) aimDir = Vector3.right;
            else aimDir.Normalize();

            var puInput = new PowerUpInputState
            {
                shieldHeld = shieldOn,
                attackDown = attackDown,
                attackHeld = attackHeld,
                attackUp   = attackUp,
                moveInput  = new Vector2(movement.x, movement.z),
                aimDirWS   = aimDir
            };

            consumedAttack = powerUps.HandleInput(puInput);
        }

        // Attack trigger
        if (!consumedAttack && attackDown && attackController != null)
        {
            attackController.RegisterAttackPress();

            if (!shieldOn && !attackController.IsAttacking)
                attackController.BeginAttack();

            lastActivityTime = Time.time;
        }

        if (attackDown || attackHeld || shieldDown || shieldOn || moveActive)
        lastActivityTime = Time.time;


        timeSinceLastActivity = Time.time - lastActivityTime;
        isActive = timeSinceLastActivity <= idleTime;
    }

    private void FixedUpdate()
    {
        if (_worldGameplaySuppressed) return;

        if (temporarilyEliminated || isStunned || IsExternallyStunned) return;
        if (!rb) return;

        float moveMul = 1f;

        if (attackController != null && attackController.IsAttacking)
            moveMul *= attackingMoveScale;

        bool canShield = !(attackController != null && attackController.IsAttacking) && !IsExternallyStunned;
        if (shieldOn && canShield)
            moveMul *= shieldMoveMultiplier;

        if (powerUps != null)
            moveMul *= powerUps.MovementMultiplier * powerUps.MovementMultiplierWhileCharging;

        float dz2 = moveDeadzone * moveDeadzone;
        // if (movement.sqrMagnitude > dz2)
        //     rb.AddForce(movement * movePower * moveMul, ForceMode.Force);

        // --- Movement (arcade traction) ---
        Vector3 input = movement;
        input.y = 0f;

        Vector3 vel = rb.linearVelocity;
        Vector3 velXZ = new Vector3(vel.x, 0f, vel.z);

        // Deadzone + rescale so you still get full strength at the rim
        float inputMag = input.magnitude;
        float dz = moveDeadzone;

        bool hasInput = inputMag > dz;
        Vector3 inputDir = Vector3.zero;
        float input01 = 0f;

        if (hasInput)
        {
            inputDir = input / Mathf.Max(0.0001f, inputMag);
            input01  = Mathf.InverseLerp(dz, 1f, Mathf.Clamp01(inputMag));
        }

        // 1) Traction: kill sideways velocity relative to desired input direction
        if (hasInput && velXZ.sqrMagnitude > 0.0001f)
        {
            // velocity component NOT aligned with stick direction
            Vector3 lateralVel = velXZ - Vector3.Project(velXZ, inputDir);

            // Acceleration mode = consistent “feel” regardless of mass changes
            rb.AddForce(-lateralVel * lateralFriction, ForceMode.Acceleration);

            // 2) Extra brake when reversing direction
            float speed = velXZ.magnitude;
            if (speed > 0.001f)
            {
                float dot = Vector3.Dot(velXZ / speed, inputDir); // -1..1
                if (dot < reverseDotThreshold)
                    rb.AddForce(-velXZ * reverseBrake, ForceMode.Acceleration);
            }
        }
        else
        {
            // 3) Brake when no input (optional)
            rb.AddForce(-velXZ * idleBrake, ForceMode.Acceleration);
        }

        // 4) Your existing propulsion (keeps “heavy blob” mass effect)
        if (hasInput)
        {
            // Use direction * input01, so tiny-stick still moves but with controlled ramp
            Vector3 drive = inputDir * (input01 * movePower * moveMul);
            rb.AddForce(drive, ForceMode.Force);
        }

        // 5) Clamp XZ speed so you can crank responsiveness without raising top speed
        if (clampSpeed)
        {
            Vector3 v2 = rb.linearVelocity;
            Vector3 v2xz = new Vector3(v2.x, 0f, v2.z);

            float max = Mathf.Max(0.1f, maxMoveSpeed * moveMul);
            float max2 = max * max;

            if (v2xz.sqrMagnitude > max2)
            {
                v2xz = v2xz.normalized * max;
                rb.linearVelocity = new Vector3(v2xz.x, v2.y, v2xz.z);
            }
        }


        // Shield visuals + drain
        if (shield != null)
        {
            if (shieldOn && canShield)
            {
                Vector3 face = (movement.sqrMagnitude > 0.001f) ? movement : lastStickAimWS;
                face.y = 0f;
                if (face.sqrMagnitude < 0.0001f) face = Vector3.right;

                shield.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
                shield.SetActive(true);

                // if (shieldDrainPerSecond01 > 0f)
                // {
                //     float drain = shieldDrainPerSecond01 * Time.fixedDeltaTime;
                //     ApplyExternalMassDelta(-drain, allowDeath: true);
                // }
            }
            else
            {
                shield.SetActive(false);
            }
        }
    }

    // ===== SFX =====
    public void playSFX(string sfxName)
    {
        if (sm != null) sm.GetComponent<SfxPlayerScript>()?.SafePlay(sfxName);
    }

    // ===== Mass / Score =====
    private void GainMass_WithOverflowScore(float delta01, bool allowOverflowScore)
    {
        if (delta01 <= 0f) return;

        float cap = Mathf.Max(massScoreMin, massScoreMax);
        float before = massScore;
        float after = before + delta01;

        float overflow = 0f;

        if (after > cap)
        {
            overflow = after - cap;
            massScore = cap;
        }
        else
        {
            massScore = after;
        }

        // Update only clamps + (optional) dotcount; safe during normal play
        UpdateMassAndNuggets(forceRebuild: false);

        if (!allowOverflowScore || !overflowScoresToTeam || overflow <= 0f) return;

        float scoreDelta01 = overflow * overflowScorePerMass;
        if (!Mathf.Approximately(scoreDelta01, 0f))
        {
            if (_teamScoreSphere != null) _teamScoreSphere.AddScore01(scoreDelta01);
            else if (goalZone != null) goalZone.BroadcastMessage("AddScore01", scoreDelta01, SendMessageOptions.DontRequireReceiver);
        }

        if (spawnOverflowScoreVFX)
            EmitOverflowScoreBlobs(overflow);
    }

    private void EmitOverflowScoreBlobs(float overflowMass01)
    {
        if (!massBlobPrefab || _teamScoreTarget == null) return;

        float denom = Mathf.Max(0.0001f, massGainPerHit);
        int count = Mathf.Clamp(Mathf.RoundToInt(overflowMass01 / denom), 1, overflowVfxMaxBlobsPerEvent);

        for (int i = 0; i < count; i++)
            EjectBlob(_teamScoreTarget);
    }

    private void SyncNuggetsGeometryFromVisuals()
    {
        if (nuggetsGPU == null) return;
        if (visualsController == null) return;

        // PlayerNuggetsGPU uses its own baseRadius/outlineHalf when rebuilding seeds/buffers.
        nuggetsGPU.baseRadius = visualsController.baseRadius;
        nuggetsGPU.outlineHalf = visualsController.outlineHalf;
    }

    private int ComputeNuggetCountFromMass01()
    {
        massScore = Mathf.Clamp(massScore, massScoreMin, massScoreMax);

        float t = (massScoreMax <= massScoreMin) ? 0f : Mathf.InverseLerp(massScoreMin, massScoreMax, massScore);

        int nuggetCount;
        if (t <= 0.5f)
        {
            float tt = t / 0.5f;
            nuggetCount = Mathf.RoundToInt(Mathf.Lerp(minNuggets, midNuggets, tt));
        }
        else
        {
            float tt = (t - 0.5f) / 0.5f;
            nuggetCount = Mathf.RoundToInt(Mathf.Lerp(midNuggets, maxNuggets, tt));
        }

        return Mathf.Clamp(nuggetCount, Mathf.Min(minNuggets, maxNuggets), Mathf.Max(minNuggets, maxNuggets));
    }

    private void UpdateMassAndNuggets(bool forceRebuild)
    {
        massScore = Mathf.Clamp(massScore, massScoreMin, massScoreMax);

        int nuggetCount = ComputeNuggetCountFromMass01();

        if (nuggetsGPU != null)
        {
            if (forceRebuild)
            {
                // Force a buffer rebuild even if the computed count happens to match,
                // by toggling count briefly in the same frame.
                int bump = (nuggetCount < maxNuggets) ? nuggetCount + 1 : nuggetCount - 1;
                nuggetsGPU.SetDotCount(bump);
            }

            nuggetsGPU.SetDotCount(nuggetCount);
        }
    }

    // ===== Combat API (called by PlayerMelee / hazards) =====
    public void Grow()
    {
        if (temporarilyEliminated) return;
        GainMass_WithOverflowScore(massGainPerHit, allowOverflowScore: true);
    }

    public void Shrink(GameObject hitSource)
    {
        if (temporarilyEliminated) return;
        if (IsInvulnerable) return;

        if (Time.time - _timeOfLastShrink < hitShrinkCooldownSeconds)
            return;

        massScore -= massLossPerHit;
        UpdateMassAndNuggets(forceRebuild: false);

        if (hitSource != null)
        {
            for (int i = 0; i < 5; i++)
                EjectBlob(hitSource);
        }

        if (massScore <= massScoreMin)
            Die();

        _timeOfLastShrink = Time.time;
    }

    public void ApplyExternalMassDelta(float delta, bool allowDeath = true)
    {
        if (temporarilyEliminated) return;

        if (delta >= 0f)
        {
            GainMass_WithOverflowScore(delta, allowOverflowScore: true);
            return;
        }

        massScore += delta;
        UpdateMassAndNuggets(forceRebuild: false);

        if (allowDeath && massScore <= massScoreMin)
            Die();
    }

public void GrowScaled(float scale01)
{
    if (temporarilyEliminated) return;

    float s = Mathf.Clamp01(scale01);
    if (s <= 0f) return;

    GainMass_WithOverflowScore(massGainPerHit * s, allowOverflowScore: true);
}

public void ShrinkScaled(GameObject hitSource, float scale01)
{
    if (temporarilyEliminated) return;
    if (IsInvulnerable) return;

    float s = Mathf.Clamp01(scale01);
    if (s <= 0f) return;

    if (Time.time - _timeOfLastShrink < hitShrinkCooldownSeconds)
        return;

    float loss = massLossPerHit * s;
    massScore -= loss;
    UpdateMassAndNuggets(forceRebuild: false);

    // VFX: eject fewer blobs proportional to the scaled hit
    if (hitSource != null)
    {
        const int maxBlobs = 5;
        int num = Mathf.Clamp(Mathf.RoundToInt(maxBlobs * s), 1, maxBlobs);

        for (int i = 0; i < num; i++)
            EjectBlob(hitSource);
    }

    if (massScore <= massScoreMin)
        Die();

    _timeOfLastShrink = Time.time;
}


    // Legacy deposit helpers (kept for compatibility)
    public void ShrinkSlow(GameObject target)
    {
        if (temporarilyEliminated) return;
        if (massScore <= massScoreMin) return;

        massScore -= legacyGoalShrink01;
        UpdateMassAndNuggets(forceRebuild: false);

        if (target != null) EjectBlob(target);

        if (massScore <= massScoreMin)
            Die();
    }

    public bool GoalShrink()
    {
        if (temporarilyEliminated) return false;
        if (massScore <= massScoreMin) return false;

        massScore -= legacyGoalShrink01;
        UpdateMassAndNuggets(forceRebuild: false);

        var scoreSphere = goalZone ? goalZone.transform.Find("Score Sphere") : null;
        if (scoreSphere) EjectBlob(scoreSphere.gameObject);

        if (massScore <= massScoreMin)
            Die();

        return true;
    }

    // ===== Death / Respawn =====
    private void Die()
    {
        if (temporarilyEliminated) return;
        if (_deathRoutine != null) return;

        temporarilyEliminated = true;
        isActive = false;

        CancelInvoke();
        isStunned = false;
        externalStunUntil = -Mathf.Infinity;

        if (stunEffect) stunEffect.GetComponent<ParticleSystem>()?.Stop();

        if (shield) shield.SetActive(false);
        if (sword) sword.SetActive(false);

        // Apply team respawn penalty (ScoreSphereScript clamps at 0)
        if (_teamScoreSphere != null) _teamScoreSphere.LoseScore(teamID);
        else if (goalZone != null) goalZone.BroadcastMessage("LoseScore", teamID, SendMessageOptions.DontRequireReceiver);

        // playSFX("diedSFX");
        AudioSystem.I?.Play(AudioEventId.Player_Death, transform.position);

        // Disable collisions immediately so we can't keep interacting while dissolving
        SetCollidersEnabled(false);

        // Freeze RB
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.Sleep();
        }

        // Also prevent attack controller from continuing to apply motion during death/respawn
        if (attackController != null) attackController.enabled = false;

        _deathRoutine = StartCoroutine(DeathRespawnRoutine());
    }

    private IEnumerator DeathRespawnRoutine()
    {
        DeathStarted?.Invoke(this);

        // --- Death FX (shrink/dissolve) ---
        if (_lifeFx != null) yield return _lifeFx.PlayDeath(this, deathFxSeconds);
        else if (deathFxSeconds > 0f) yield return new WaitForSeconds(deathFxSeconds);

        // --- Hard hide (prevents the “tiny dot” artifact) ---
        if (_lifeFx != null) _lifeFx.SetVisibleInstant(this, false);
        else
        {
            if (visualsController) visualsController.enabled = false;
            if (nuggetsGPU) nuggetsGPU.enabled = false;
        }

        DeathHidden?.Invoke(this);

        // --- Respawn delay ---
        if (respawnDelaySeconds > 0f)
            yield return new WaitForSeconds(respawnDelaySeconds);

        // --- Teleport while still hidden ---
        Vector3 basePos = respawnPointOverride ? respawnPointOverride.position : spawnAnchorWS;
        basePos.y = transform.position.y;

        Vector3 respawnPos = FindSafeRespawnPosition(basePos);

        TeleportTo(respawnPos);

        // Critical: snap visuals child immediately so the first render frame is correct
        if (visualsController != null && visualsController.visuals != null)
            visualsController.visuals.position = respawnPos;

        // Reset mass now (but rebuild nugget buffers AFTER form finishes)
        massScore = spawnMassScore01;

        RespawnStarted?.Invoke(this);

        AudioSystem.I?.Play(AudioEventId.Player_Spawn, transform.position);

        // --- Respawn FX (form) ---
        if (_lifeFx != null) yield return _lifeFx.PlayRespawn(this, respawnFxSeconds);
        else if (respawnFxSeconds > 0f) yield return new WaitForSeconds(respawnFxSeconds);

        // Ensure nugget sim has sane geometry before rebuilding seed buffers
        SyncNuggetsGeometryFromVisuals();

        // Now rebuild nuggets for spawn mass with a healthy radius (prevents the “1 nugget” look)
        UpdateMassAndNuggets(forceRebuild: true);

        // Re-enable gameplay
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }

        SetCollidersEnabled(true);

        temporarilyEliminated = false;
        lastActivityTime = Time.time;
        isActive = true;

        _invulnUntil = Time.time + Mathf.Max(0f, respawnInvulnSeconds);

        if (attackController != null) attackController.enabled = true;

        RespawnCompleted?.Invoke(this);
        _deathRoutine = null;
    }

    private void TeleportTo(Vector3 pos)
    {
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        transform.position = pos;
        Physics.SyncTransforms();
    }

    private Vector3 FindSafeRespawnPosition(Vector3 basePos)
    {
        // First try the exact anchor
        if (IsRespawnSpotClear(basePos))
            return basePos;

        // Otherwise search nearby
        const int tries = 12;
        const float searchRadius = 2.5f;

        for (int i = 0; i < tries; i++)
        {
            Vector2 o = UnityEngine.Random.insideUnitCircle * searchRadius;
            Vector3 p = basePos + new Vector3(o.x, 0f, o.y);

            if (IsRespawnSpotClear(p))
                return p;
        }

        return basePos;
    }

    private bool IsRespawnSpotClear(Vector3 pos)
    {
        var hits = Physics.OverlapSphere(pos, respawnCheckRadius, respawnBlockMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!h || !h.enabled) continue;
            if (rb != null && h.attachedRigidbody == rb) continue; // self

            if (h.CompareTag("Player"))
                return false;

            if (h.attachedRigidbody != null)
                return false;
        }
        return true;
    }

private void SetCollidersEnabled(bool enabled)
{
    if (_allColliders == null) return;

    foreach (var c in _allColliders)
    {
        if (c == null) continue;

        if (enabled)
        {
            // IMPORTANT:
            // Don't globally enable melee hitboxes.
            // PlayerMelee gates its collider to the activation window.
            if (c.TryGetComponent<PlayerMelee>(out _))
            {
                c.enabled = false;
                continue;
            }
        }

        c.enabled = enabled;
    }
}


    // ===== Mass blob VFX =====
    private void EjectBlob(GameObject newTarget)
    {
        if (!massBlobPrefab) return;

        GameObject newBlob = Instantiate(massBlobPrefab);
        newBlob.transform.position = transform.position;

        var smb = newBlob.GetComponent<SmallMassBlobScript>();
        if (smb) smb.target = newTarget;
    }

    // ===== Stun =====
    public void Stun(Vector3 shieldPosition, float strength01 = 1f)
    {
        if (temporarilyEliminated) return;

        float s = Mathf.Clamp01(strength01);
        float duration = stunTime * s;
        if (duration <= 0.0001f) return;

        if (_stunRoutine != null)
            StopCoroutine(_stunRoutine);

        _stunRoutine = StartCoroutine(StunRoutine(shieldPosition, duration, s));
    }

    private IEnumerator StunRoutine(Vector3 shieldPosition, float duration, float s)
    {
        isStunned = true;

        if (stunEffect)
        {
            stunEffect.SetActive(true);
            stunEffect.GetComponent<ParticleSystem>()?.Play();
        }

        playSFX("StunnedSFX");

        // Cancel any in-progress attack sequence (prevents “ghost” attack motion)
        if (attackController != null)
            attackController.CancelAttack(); // :contentReference[oaicite:10]{index=10}

        
        // shieldAbility?.ForceStopShield();

        if (shield) shield.SetActive(false);
        if (sword) sword.SetActive(false);

        // Knockback away from shield impact point
        Vector3 dir = transform.position - shieldPosition;
        dir.y = 0f;
        dir = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : Vector3.right;

        if (rb != null && stunAppliesKnockback && Time.time >= _nextAllowedStunKickTime)
        {
            _nextAllowedStunKickTime = Time.time + stunKnockbackLockout;

            // Optional but recommended: don't compound existing lateral speed
            Vector3 v = rb.linearVelocity;
            Vector3 planarV = new Vector3(v.x, 0f, v.z);
            rb.linearVelocity = v - planarV;

            // Predictable, mass-independent kick:
            float kick = stunKnockbackVelocity * s;
            rb.AddForce(dir * kick, ForceMode.VelocityChange);

            // Safety clamp so you can never get launched across the map
            Vector3 v2 = rb.linearVelocity;
            Vector3 planar2 = new Vector3(v2.x, 0f, v2.z);
            float mag = planar2.magnitude;
            if (mag > stunMaxPlanarSpeed && mag > 0.0001f)
            {
                planar2 = (planar2 / mag) * stunMaxPlanarSpeed;
                rb.linearVelocity = new Vector3(planar2.x, v2.y, planar2.z);
            }
        }


        // Blob jitter (re-using your Particle Accelerator style external modifier)
        if (visualsController != null)
            visualsController.SetExternalChargeJitter01(Mathf.Clamp01(stunBlobJitterMul * s));
        // (This ultimately drives the amp*sin/cos render jitter):contentReference[oaicite:12]{index=12}

        // Nuggets drag ramp
        if (nuggetsGPU != null && _baseNuggetDrag <= 0f)
            _baseNuggetDrag = nuggetsGPU.drag;

        float maxMul = Mathf.Lerp(1f, stunNuggetDragMaxMul, s);

        float t0 = Time.time;
        float tEnd = t0 + duration;

        float rampUp = Mathf.Min(stunNuggetRampUpSeconds, duration * 0.45f);
        float rampDown = Mathf.Min(stunNuggetRampDownSeconds, duration * 0.45f);

        while (Time.time < tEnd)
        {
            float t = Time.time - t0;
            float mul;

            if (t < rampUp)
            {
                float u = t / Mathf.Max(0.0001f, rampUp);
                mul = Mathf.Lerp(1f, maxMul, Mathf.SmoothStep(0f, 1f, u));
            }
            else if (t > duration - rampDown)
            {
                float u = (t - (duration - rampDown)) / Mathf.Max(0.0001f, rampDown);
                mul = Mathf.Lerp(maxMul, 1f, Mathf.SmoothStep(0f, 1f, u));
            }
            else
            {
                mul = maxMul;
            }

            if (nuggetsGPU != null && _baseNuggetDrag > 0f)
                nuggetsGPU.drag = _baseNuggetDrag * mul; // affects _Drag fed to compute

            yield return null;
        }

        UnStun();

        _stunRoutine = null;
    }

    private void UnStun()
    {
        isStunned = false;

        if (stunEffect)
            stunEffect.GetComponent<ParticleSystem>()?.Stop();

        // Reset visuals
        if (visualsController != null)
            visualsController.SetExternalChargeJitter01(0f);

        // Reset nuggets drag
        if (nuggetsGPU != null && _baseNuggetDrag > 0f)
            nuggetsGPU.drag = _baseNuggetDrag;
    }

    private Transform FindUnderRigOrRoot(string childName)
{
    if (gameplayRigRoot == null && !string.IsNullOrEmpty(gameplayRigRootName))
        gameplayRigRoot = transform.Find(gameplayRigRootName);

    if (gameplayRigRoot != null)
    {
        var t = gameplayRigRoot.Find(childName);
        if (t != null) return t;
    }

    return transform.Find(childName);
}

public void SetWorldGameplaySuppressed(bool suppressed)
{
    // Only do work if state actually changes
    if (_worldGameplaySuppressed == suppressed)
        return;

    _worldGameplaySuppressed = suppressed;

    // Resolve rig root if needed
    if (gameplayRigRoot == null && !string.IsNullOrEmpty(gameplayRigRootName))
        gameplayRigRoot = transform.Find(gameplayRigRootName);

    // Make sure we have refs (safe even if already assigned)
    if (!visualsController) visualsController = GetComponent<PlayerVisualController>();
    if (!nuggetsGPU) nuggetsGPU = GetComponentInChildren<PlayerNuggetsGPU>(true);

    // Toggle subtree (visuals, sword, shield, VFX, etc.)
    if (gameplayRigRoot != null)
        gameplayRigRoot.gameObject.SetActive(!suppressed);

    // Disable root-level controllers that can still affect gameplay even if visuals are hidden.
    if (attackController != null) attackController.enabled = !suppressed;
    if (powerUps != null) powerUps.enabled = !suppressed;
    if (visualsController != null) visualsController.enabled = !suppressed;
    if (shieldAbility != null) shieldAbility.enabled = !suppressed;

    // If your nuggets driver is NOT under GameplayRigRoot in some prefabs,
    // this ensures it follows suppression too (harmless if it *is* under the root).
    if (nuggetsGPU != null) nuggetsGPU.enabled = !suppressed;

    // Prevent physics interactions while suppressed (NOVA)
    SetCollidersEnabled(!suppressed);

    // Stop drift when suppressing
    if (suppressed && rb != null)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    // ✅ KEY: when returning from suppression, re-sync nugget geometry + force a rebuild
    // This is the same fix you already rely on after respawn to prevent the "single dot" look.
    if (!suppressed)
    {
        SyncNuggetsGeometryFromVisuals();
        UpdateMassAndNuggets(forceRebuild: true);
    }
}





}
