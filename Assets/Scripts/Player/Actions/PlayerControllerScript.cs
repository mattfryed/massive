using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rigidbody = UnityEngine.Rigidbody;
using Massive.Player; // <-- new: for PlayerAttackController

public class PlayerControllerScript : MonoBehaviour
{
    private Rewired.Player player;
    

    [Header("Identity")]
    public int playerID;
    public int teamID;

    [Header("Movement")]
    public float movePower = 10f;
    private Rigidbody rb;
    private Vector3 startingPosition;

    [Tooltip("Scale movement while an attack stage is active (1 = no slowdown).")]
    [Range(0.1f, 1.0f)] public float attackingMoveScale = 0.6f;

    [Header("Scene / Refs")]
    public GameObject goalZone;
    private GameObject sm; // SFX module
    public GameObject massBlobPrefab;
    public GameObject explosionPrefab;
    public GameObject respawnPrefab;
    public PlayerVisualController visualsController; // assign in inspector

    [Tooltip("Optional: visual sword root (not used to gate gameplay anymore).")]
    public GameObject sword;

    public GameObject shield;
    private GameObject stunEffect;

    [Header("Attack System")]
    [Tooltip("New combo/attack driver. Required for melee.")]
    public PlayerAttackController attackController;

    [Header("Mass & Size Tuning")]
    private float timeUntilNextShrink = .1f;
    private float timeOfLastShrink = 0f;
    private float shieldSlowdownFactor = .3f;
    private float massAddedOnGrow = .1f;
    private float massRemovedOnShrink = .05f;
    private float massRemovedOnGoalShrink = .0045f;
    private float sizeChangeOnHit = .15f;
    private float sizeChangeOnGrow = .2f;
    private float sizeChangeOnShrink = .15f;
    private float sizeChangeOnGoalHit = .01f;
    private float maxScale = 3.0f;
    public float stunTime = 1.25f;
    private bool isStunned = false;
    private float minScale = .5f;
    private float timeToReturn = 5f;

    // Amount of “massScore” to gain/lose per hit
[SerializeField] private float massGainPerHit  = 0.08f; // tweak to taste
[SerializeField] private float massLossPerHit  = 0.08f;

    [Header("Mass → Nuggets")]
[Range(0f, 1f)]
public float massScore = 0.5f;       // 0 = min, 0.5 = start, 1 = max

public float massScoreMin = 0f;
public float massScoreMax = 1f;

// Visual nugget anchors
public int minNuggets = 5;
public int midNuggets = 50;
public int maxNuggets = 300;

// Movement slowdown: how much heavier you get at max mass
public float baseRBMass = 1f;        // mass at 0
public float extraRBMassAtMax = 2f;  // so mass goes 1 → 3

[SerializeField] private PlayerNuggetsGPU nuggetsGPU; // assign in Inspector or via GetComponentInChildren



    [Header("State & Activity")]
    private GameObject dm;
    public bool isActive = true;
    private float idleTime = 60f;
    public float timeSinceLastActivity;
    public float lastActivityTime;

    // Input snapshots
    public float moveHorizontal;
    public float moveVertical;
    public Vector3 movement;
    private Quaternion lookRotation;
    public bool didPlayerTapActionThisFrame = false;
    public bool shieldOn = false;

    private GameObject gameplayObjects;

    // Convenience properties
    public Vector2 CurrentInput2D => new Vector2(moveHorizontal, moveVertical);
    public Vector2 CurrentPlanarVelocity => rb ? new Vector2(rb.linearVelocity.x, rb.linearVelocity.z) : Vector2.zero;
    public Vector2 CurrentFacing => (CurrentInput2D.sqrMagnitude > 0.0001f) ? CurrentInput2D.normalized : CurrentPlanarVelocity.normalized;

    private void Awake()
    {
        player = Rewired.ReInput.players.GetPlayer(playerID);
        lastActivityTime = Time.time;
        gameplayObjects = GameObject.FindWithTag("GameplayObjects");


    // Keep visual scale constant
    transform.localScale = Vector3.one;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        startingPosition = transform.position;
        dm = GameObject.FindWithTag("GameManager");
        sm = transform.Find("SfxModule")?.gameObject;
        stunEffect = transform.Find("StunnedEffect")?.gameObject;

        goalZone = (teamID == 1) ? GameObject.Find("TEAM 1") : GameObject.Find("TEAM 2");

    // Starting “life” at 50% → 50 nuggets
    massScore = 0.5f;
    UpdateMassAndNuggets();

        // Sanity checks
        if (!attackController)
            Debug.LogWarning($"[{name}] PlayerAttackController not assigned. Attacks will not trigger.");
        if (!shield)
            Debug.LogWarning($"[{name}] Shield reference not assigned.");
    }

    private void Update()
    {
        // Rewired input
        moveHorizontal = player.GetAxis("MoveH");
        moveVertical   = player.GetAxis("MoveV");
        movement       = new Vector3(moveHorizontal, 0f, moveVertical);
        shieldOn       = player.GetButton("Shield");
        didPlayerTapActionThisFrame = player.GetButtonDown("Sword");

        // Visuals bridge (for trails, facing, etc.)
        if (visualsController != null)
        {
            var stick = new Vector2(movement.x, movement.z);
            visualsController.SetMoveInput(stick);
            if (rb != null) visualsController.velocityWS = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        }

        if (attackController != null) {
        // Feed stick to attack system for swipe direction
        attackController.ExternalMoveInput = new Vector2(movement.x, movement.z);
        }


        // Attack trigger: hand off to new attack system (no manual sword toggling / dash)

        if (didPlayerTapActionThisFrame && attackController != null) {
            // Always tell the attack system about the press (for combo buffering)
            attackController.RegisterAttackPress();

            // Start an attack if we're not shielding and not already mid-attack
            if (!shieldOn && !attackController.IsAttacking) {
                attackController.BeginAttack();
            }
            lastActivityTime = Time.time;
        }


        // Basic mass sanity
        if (rb.mass < .1f) rb.mass = 1f;

        if (didPlayerTapActionThisFrame || shieldOn)
            lastActivityTime = Time.time;

        timeSinceLastActivity = Time.time - lastActivityTime;
        isActive = timeSinceLastActivity <= idleTime;
    }

    void FixedUpdate()
    {
        if (isStunned || temporarilyEliminated) return;

        // Movement force (scaled if attacking)
        float attackScale = (attackController != null && attackController.IsAttacking) ? attackingMoveScale : 1f;
        float speedScale  = shieldSlowdownFactor;

        // Apply steering (reduced when shield is held; additional reduction while attacking)
        if (Mathf.Abs(movement.magnitude) > .15f)
        {
            rb.AddForce(movement * movePower * speedScale * attackScale);
        }

        // Shield behavior (can’t raise while attacking)
        if (shieldOn && !(attackController != null && attackController.IsAttacking))
        {
            if (movement != Vector3.zero)
                lookRotation = Quaternion.LookRotation(movement.normalized);

            if (shield != null)
            {
                shield.transform.rotation = lookRotation;
                shield.SetActive(true);
            }

            shieldSlowdownFactor = .4f;

            // Continuous tiny drain while shielding

if (massScore > massScoreMin)
{
    massScore -= massRemovedOnGoalShrink / 6f; // small drain per frame
    UpdateMassAndNuggets();
}
        }
        else
        {
            shieldSlowdownFactor = 1f;
            if (shield != null) shield.SetActive(false);
        }
    }

    // ===== Gameplay Effects (called from PlayerMelee / collisions / goals) =====

    public void playSFX(string sfxName)
    {
        if (sm != null) sm.GetComponent<SfxPlayerScript>()?.SafePlay(sfxName);
        else Debug.Log("SFX Module not found");
    }

private void OnCollisionEnter(Collision collision)
{
    if (!visualsController) return;

    float relSpeed = collision.relativeVelocity.magnitude;

    const float minImpulseSpeed = 0.1f;
    const float maxImpulseSpeed = 8f;

    float velImpulse = Mathf.InverseLerp(minImpulseSpeed, maxImpulseSpeed, relSpeed);

    // Extra impulse from attack stage, independent of physics velocity
    float attackImpulse = 0f;
    if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
    {
        var s = attackController.CurrentStage;
        float baseDashSpeed = (s.Duration > 0.001f) ? (s.TravelDistance / s.Duration) : 0f;
        const float dashRefSpeed = 8f; // tune to taste
        attackImpulse = Mathf.Clamp01(baseDashSpeed / dashRefSpeed);
    }

    // Combine, then clamp
    float hitStrength = Mathf.Clamp01(velImpulse + attackImpulse);

    if (hitStrength > 0.01f)
    {
        // Use contact point so we can drive directional ripples later
        var contact = (collision.contactCount > 0) ? collision.GetContact(0) : default;
        visualsController.OnHit(hitStrength, contact.point);
    }

    Debug.Log($"Collision with {collision.gameObject.name}, relSpeed={relSpeed}, hitStrength={hitStrength}");
}

private void OnCollisionStay(Collision collision)
{
    if (!visualsController) return;

    // Filter if you only want walls here:
    // if (!collision.gameObject.CompareTag("Wall")) return;

    // Use max penetration-ish contact as “how much” we’re pressing
    ContactPoint best = collision.GetContact(0);
    float maxSep = best.separation;

    for (int i = 1; i < collision.contactCount; i++)
    {
        var cp = collision.GetContact(i);
        if (cp.separation < maxSep)  // more negative = deeper
        {
            maxSep = cp.separation;
            best   = cp;
        }
    }

    // Map separation (usually <= 0) to a 0..1 "press" value.
    // 0 = just touching, 1 = deeply overlapped.
    float press = 0f;
    if (maxSep < 0f)
    {
        const float maxPenetration = 0.2f; // tune
        press = Mathf.Clamp01(-maxSep / maxPenetration);
    }

    // Combine with impact-based info if you like, or keep it separate.
    if (press > 0.01f)
    {
        visualsController.OnContact(best.point, best.normal, press);
    }
}




public void Shrink(GameObject hitSource)
{
    if (Time.time - timeOfLastShrink <= timeUntilNextShrink)
        return;

    // Defender loses mass
    massScore -= massLossPerHit;
    UpdateMassAndNuggets();

    // Existing visuals / blob ejections
    if (hitSource != null)
    {
        for (int i = 0; i < 5; i++)
            EjectBlob(hitSource);
    }

    // “Death” condition
    if (massScore <= massScoreMin)
    {
        temporarilyEliminated = true;

        if (explosionPrefab)
        {
            var exp = Instantiate(explosionPrefab);
            exp.transform.position = transform.position;
        }

        playSFX("diedSFX");

        // Reset mass for next life
        massScore = 0.5f;
        UpdateMassAndNuggets();

        RespawnEffect();
        Invoke(nameof(Return), timeToReturn);

        transform.position = new Vector3(1200f, 1200f, 1200f);

        if (goalZone) goalZone.BroadcastMessage("LoseScore", teamID);
    }

    timeOfLastShrink = Time.time;
}

    void RespawnEffect()
    {
        if (!respawnPrefab || !gameplayObjects) return;
        var re = Instantiate(respawnPrefab, gameplayObjects.transform);
        re.transform.position = startingPosition;
    }

    void Return()
    {
        transform.position = startingPosition;
        temporarilyEliminated = false;
    }

    void UnStun()
    {
        isStunned = false;
        if (stunEffect) stunEffect.GetComponent<ParticleSystem>()?.Stop();
    }

    public void Stun(Vector3 shieldPosition)
    {
        Debug.Log($"Player {playerID} stunned!");
        playSFX("StunnedSFX");

        Vector3 knockDirection = transform.position - shieldPosition;
        rb.AddForce(knockDirection.normalized * movePower * 30f);

        if (shield) shield.SetActive(false);
        if (sword)  sword.SetActive(false); // purely visual; collider is managed by PlayerMelee

        isStunned = true;
        if (stunEffect) stunEffect.GetComponent<ParticleSystem>()?.Play();
        Invoke(nameof(UnStun), stunTime);
    }

    public void SwordClash()
    {
        // Small knockback away from the sword's visual position (if assigned)
        Vector3 from = sword ? sword.transform.position : (transform.position - transform.forward);
        Vector3 direction = transform.position - from;
        rb.AddForce(direction * 200f);
    }

public void ShrinkSlow(GameObject target)
{
    if (massScore <= massScoreMin) return;

    massScore -= massRemovedOnGoalShrink;
    UpdateMassAndNuggets();
    EjectBlob(target);
}

public bool GoalShrink()
{
    if (massScore <= massScoreMin) return false;

    massScore -= massRemovedOnGoalShrink;
    UpdateMassAndNuggets();

    var scoreSphere = goalZone.transform.Find("Score Sphere");
    if (scoreSphere) EjectBlob(scoreSphere.gameObject);

    return true;
}

public void Grow()
{
    if (massScore >= massScoreMax) return;

    // Attacker gains mass
    massScore += massGainPerHit;
    UpdateMassAndNuggets();
}

    void UpdateMassAndNuggets()
{
    // Clamp score
    massScore = Mathf.Clamp(massScore, massScoreMin, massScoreMax);

    float t = (massScoreMax <= massScoreMin)
        ? 0f
        : Mathf.Clamp01((massScore - massScoreMin) / (massScoreMax - massScoreMin));

    // 1) Map to Rigidbody mass: base → base+extra
    if (rb)
    {
        float targetMass = baseRBMass + t * extraRBMassAtMax;
        rb.mass = targetMass;
    }

    // 2) Map to nugget count with mid anchor
    int nuggetCount;
    if (t <= 0.5f)
    {
        // 0 → minNuggets, 0.5 → midNuggets
        float tt = t / 0.5f;
        nuggetCount = Mathf.RoundToInt(Mathf.Lerp(minNuggets, midNuggets, tt));
    }
    else
    {
        // 0.5 → midNuggets, 1.0 → maxNuggets
        float tt = (t - 0.5f) / 0.5f;
        nuggetCount = Mathf.RoundToInt(Mathf.Lerp(midNuggets, maxNuggets, tt));
    }

    nuggetCount = Mathf.Clamp(nuggetCount, minNuggets, maxNuggets);

    if (nuggetsGPU)
        nuggetsGPU.SetDotCount(nuggetCount);
}



    void EjectBlob(GameObject newTarget)
    {
        if (!massBlobPrefab) return;

        GameObject newBlob = Instantiate(massBlobPrefab);
        newBlob.transform.position = transform.position;
        var smb = newBlob.GetComponent<SmallMassBlobScript>();
        if (smb) smb.target = newTarget;
    }

    // ===== Flags retained for compatibility =====
    public bool gamepadMode = false;
    public bool temporarilyEliminated = false;
}
