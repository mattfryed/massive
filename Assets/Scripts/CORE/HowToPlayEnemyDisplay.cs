using Massive.Enemies;
using UnityEngine;

/// <summary>Keeps the instruction pane's enemy models isolated from gameplay.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10500)]
public sealed class HowToPlayEnemyDisplay : MonoBehaviour
{
    [Header("Stationary attack previews")]
    [SerializeField] private DroneVisuals rangedVisuals;
    [Tooltip("Canonical gameplay prefab supplies the charge, burst, projectile and recovery settings.")]
    [SerializeField] private RangedDroneController rangedGameplay;
    [Tooltip("Clip projectile travel to the model area; native speed, scale and expiry remain unchanged.")]
    [Min(.1f), SerializeField] private float rangedShotTravel = 1.4f;
    [SerializeField] private SeekerVisuals seekerVisuals;
    [Tooltip("Canonical gameplay prefab supplies charge, thrust and recovery timing.")]
    [SerializeField] private SeekerController seekerGameplay;
    [SerializeField] private TMPro.TMP_Text seekerTitle;
    [Min(.01f), SerializeField] private float seekerTitleClearance = .15f;
    [Tooltip("Only the regular Dyson Sphere; the amplified variant keeps its idle presentation.")]
    [SerializeField] private DysonSphereRepulsorController attackingDyson;
    private DroneVisuals[] drones;
    private EnemySpawnPresentation rangedSpawn, seekerSpawn, dysonSpawn;
    private EnemyBase rangedEnemy;
    private Transform rangedShots;
    private readonly Vector3[] titleCorners = new Vector3[4];
    private float seekerAge, dysonAge;
    private float rangedPhaseAge, rangedShotPulse;
    private int rangedBurstShots;
    private bool presentationHeld;
    public SeekerController.AttackPhase SeekerPhase { get; private set; }
    public DysonSphereRepulsorController.AttackPhase DysonPhase { get; private set; }
    public int SeekerThrusts { get; private set; }
    public int DysonBursts { get; private set; }
    public float SeekerTrailMinZ { get; private set; }
    public RangedDroneController.AttackPhase RangedPhase { get; private set; }
    public int RangedBursts { get; private set; }
    public int RangedShots { get; private set; }

    public void SetPresentationHeld(bool held) => presentationHeld = held;

    private void Awake()
    {
        // These prefab instances retain their native geometry and idle visuals.
        // Dyson's controller also drives its shell/core animation, so keep it alive
        // while suppressing its movement, targeting, damage and global registration.
        foreach (var enemy in GetComponentsInChildren<EnemyBase>(true))
        {
            enemy.ConfigureDemonstration(transform, null);
            enemy.HoldPosition = true;
            enemy.AttacksEnabled = false;
            enemy.enabled = false;
        }
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        // Carrier-mounted decorative drones stay in their existing docked state.
        drones = System.Array.FindAll(GetComponentsInChildren<DroneVisuals>(true), drone => drone.GetComponent<EnemyBase>() != null);
        if (rangedVisuals)
        {
            rangedSpawn = rangedVisuals.GetComponentInParent<EnemySpawnPresentation>();
            rangedEnemy = rangedVisuals.GetComponent<EnemyBase>();
        }
        if (seekerVisuals) seekerSpawn = seekerVisuals.GetComponentInParent<EnemySpawnPresentation>();
        if (attackingDyson) dysonSpawn = attackingDyson.GetComponentInParent<EnemySpawnPresentation>();
    }

    private void OnEnable()
    {
        seekerAge = dysonAge = 0f; SeekerThrusts = DysonBursts = 0;
        rangedPhaseAge = rangedShotPulse = 0f; rangedBurstShots = RangedBursts = RangedShots = 0;
        RangedPhase = RangedDroneController.AttackPhase.Seeking;
        SeekerPhase = SeekerController.AttackPhase.Seeking;
        DysonPhase = DysonSphereRepulsorController.AttackPhase.Spawning;
    }

    private void Update()
    {
        foreach (var drone in drones) if (drone) drone.SetPresentationThrust(!presentationHeld);
        UpdateRanged(); UpdateSeeker(); UpdateDyson();
    }

    private void UpdateRanged()
    {
        if (!rangedVisuals || !rangedGameplay || !rangedGameplay.projectilePrefab) return;
        bool ready = !presentationHeld && (!rangedSpawn || rangedSpawn.IsComplete);
        if (rangedShots) rangedShots.gameObject.SetActive(!presentationHeld);
        if (ready)
        {
            float dt = Time.deltaTime;
            rangedPhaseAge += dt;
            rangedShotPulse = Mathf.MoveTowards(rangedShotPulse, 0f, dt / .3f);
            switch (RangedPhase)
            {
                case RangedDroneController.AttackPhase.Seeking:
                    if (rangedPhaseAge >= .6f) EnterRanged(RangedDroneController.AttackPhase.Charging);
                    break;
                case RangedDroneController.AttackPhase.Charging:
                    if (rangedPhaseAge >= Mathf.Max(.05f, rangedGameplay.chargeSeconds))
                    {
                        rangedBurstShots = 0; RangedBursts++;
                        EnterRanged(RangedDroneController.AttackPhase.Bursting); FireRanged();
                    }
                    break;
                case RangedDroneController.AttackPhase.Bursting:
                    if (rangedBurstShots < rangedGameplay.shotsPerBurst &&
                        rangedPhaseAge >= rangedBurstShots * Mathf.Max(.05f, rangedGameplay.shotInterval)) FireRanged();
                    if (rangedBurstShots >= rangedGameplay.shotsPerBurst && rangedPhaseAge >=
                        Mathf.Max(rangedGameplay.burstSeconds, rangedGameplay.shotsPerBurst * rangedGameplay.shotInterval))
                        EnterRanged(RangedDroneController.AttackPhase.Cooldown);
                    break;
                case RangedDroneController.AttackPhase.Cooldown:
                    if (rangedPhaseAge >= Mathf.Max(.05f, rangedGameplay.cooldownSeconds))
                        EnterRanged(RangedDroneController.AttackPhase.Charging);
                    break;
            }
        }
        bool charging = RangedPhase == RangedDroneController.AttackPhase.Charging;
        rangedVisuals.SetRangedPresentationState(charging,
            charging ? rangedPhaseAge / Mathf.Max(.05f, rangedGameplay.chargeSeconds) : 0f, rangedShotPulse);
    }

    private void EnterRanged(RangedDroneController.AttackPhase phase) { RangedPhase = phase; rangedPhaseAge = 0f; }

    private void FireRanged()
    {
        if (!rangedShots)
        {
            rangedShots = new GameObject("Ranged Drone preview shots").transform;
            rangedShots.SetParent(transform, false);
        }
        var model = rangedVisuals.transform;
        Vector3 muzzle = model.TransformPoint(Vector3.forward * rangedGameplay.muzzleDistance);
        var shot = Instantiate(rangedGameplay.projectilePrefab, muzzle, model.rotation);
        shot.transform.SetParent(rangedShots, true);
        shot.collisionMask = 0;
        shot.Init(rangedEnemy, model.forward);
        shot.maxTravelDistance = rangedShotTravel;
        var body = shot.GetComponent<Rigidbody>();
        if (body) body.detectCollisions = false;
        rangedBurstShots++; RangedShots++; rangedShotPulse = 1f;
    }

    private void UpdateSeeker()
    {
        if (!seekerVisuals || !seekerGameplay) return;
        bool ready = !presentationHeld && (!seekerSpawn || seekerSpawn.IsComplete);
        if (ready) seekerAge += Time.deltaTime;
        float charge = Mathf.Max(.05f, seekerGameplay.chargeSeconds);
        float compression = Mathf.Max(.02f, seekerGameplay.compressionSeconds);
        float speed = Mathf.Max(.1f, seekerGameplay.lungeSpeed);
        float thrust = Mathf.Max(.01f, seekerGameplay.lungeDistance / speed);
        float glide = Mathf.Max(.02f, seekerGameplay.missGlideSeconds);
        float cooldown = Mathf.Max(.05f, seekerGameplay.cooldownSeconds);
        float t = Mathf.Repeat(Mathf.Max(0f, seekerAge - .6f), charge + compression + thrust + glide + cooldown);
        var phase = SeekerController.AttackPhase.Seeking;
        float phaseAge = 0f, charge01 = 0f, compression01 = 0f, travelSpeed = 0f;
        if (ready && seekerAge >= .6f)
        {
            if (t < charge + compression)
            {
                phase = SeekerController.AttackPhase.Charging; phaseAge = t;
                charge01 = Mathf.Clamp01(t / charge); compression01 = Mathf.Clamp01((t - charge) / compression);
            }
            else if ((t -= charge + compression) < thrust)
            { phase = SeekerController.AttackPhase.Firing; phaseAge = t; travelSpeed = speed; }
            else if ((t -= thrust) < glide)
            {
                phase = SeekerController.AttackPhase.Gliding; phaseAge = t;
                travelSpeed = speed * seekerGameplay.missGlideSpeedFraction * Mathf.Pow(1f - t / glide, 2f);
            }
            else { phase = SeekerController.AttackPhase.Cooldown; phaseAge = t - glide; }
        }
        if (phase == SeekerController.AttackPhase.Firing && SeekerPhase != phase) SeekerThrusts++;
        SeekerPhase = phase;
        SeekerTrailMinZ = float.NegativeInfinity;
        if (seekerTitle)
        {
            seekerTitle.rectTransform.GetWorldCorners(titleCorners);
            foreach (var corner in titleCorners) SeekerTrailMinZ = Mathf.Max(SeekerTrailMinZ, corner.z + seekerTitleClearance);
        }
        seekerVisuals.SetPresentationState(phase, phaseAge, charge01, compression01,
            seekerVisuals.transform.forward * travelSpeed, SeekerTrailMinZ);
    }

    private void UpdateDyson()
    {
        if (!attackingDyson) return;
        bool ready = !presentationHeld && (!dysonSpawn || dysonSpawn.IsComplete);
        if (ready) dysonAge += Time.deltaTime;
        float charge = Mathf.Max(.05f, attackingDyson.chargeSeconds);
        float expand = Mathf.Max(.02f, attackingDyson.expansionSeconds);
        float hold = Mathf.Max(0f, attackingDyson.panelHoldSeconds);
        float recover = Mathf.Max(.05f, attackingDyson.returnSeconds);
        float cooldown = Mathf.Max(.05f, attackingDyson.cooldownSeconds);
        float t = Mathf.Repeat(Mathf.Max(0f, dysonAge - .6f), charge + expand + hold + recover + cooldown);
        var phase = DysonSphereRepulsorController.AttackPhase.Spawning;
        if (ready && dysonAge >= .6f)
        {
            if (t < charge) phase = DysonSphereRepulsorController.AttackPhase.Charge;
            else if ((t -= charge) < expand) phase = DysonSphereRepulsorController.AttackPhase.Expand;
            else if ((t -= expand) < hold) phase = DysonSphereRepulsorController.AttackPhase.Hold;
            else if ((t -= hold) < recover) phase = DysonSphereRepulsorController.AttackPhase.Return;
            else { t -= recover; phase = DysonSphereRepulsorController.AttackPhase.Cooldown; }
        }
        if (phase == DysonSphereRepulsorController.AttackPhase.Expand && DysonPhase != phase) DysonBursts++;
        DysonPhase = phase;
        attackingDyson.SetPresentationState(phase, t, presentationHeld ? 0f : Time.deltaTime);
    }

    private void OnDisable()
    {
        if (drones != null) foreach (var drone in drones) if (drone) drone.SetPresentationThrust(false);
        if (rangedVisuals) rangedVisuals.ClearRangedPresentationState();
        if (rangedShots) { rangedShots.gameObject.SetActive(false); Destroy(rangedShots.gameObject); rangedShots = null; }
        if (seekerVisuals) seekerVisuals.ClearPresentationState();
        if (attackingDyson) attackingDyson.ClearPresentationState();
    }
}
