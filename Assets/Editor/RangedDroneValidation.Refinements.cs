#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Massive.Enemies;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class RangedDroneValidation
{
    private static IEnumerator ProjectileRefinementChecks()
    {
        var shot = Object.Instantiate(ranged.projectilePrefab, new Vector3(1.5f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
        shot.Init(enemy, Vector3.right); float mass = player.massScore;
        while (shot && shot.Phase == RangedDroneProjectile.ShotPhase.Flying) yield return null;
        Check(shot && shot.Phase == RangedDroneProjectile.ShotPhase.Impact && !shot.GetComponent<Collider>().enabled,
            "Player contact retires gameplay collision while retaining the visible packet");
        yield return null;
        Vector3 outline = player.visualsController.OutlinePointTowards(player.transform.position + Vector3.left * 10f);
        Check(Vector3.Distance(outline, shot.transform.position) < .07f,
            "Impact head overlaps the rendered player outline, including its damage dent");
        Capture("ranged-contact.png", player.transform, shot.transform);
        float afterHit = player.massScore;
        Check(Mathf.Abs(mass - afterHit - shot.damageToPlayerMass01) < .001f, "A visible impact applies player damage exactly once");
        director.enabled = false; float age = shot.PhaseAge;
        float until = Time.time + .2f; while (Time.time < until) yield return null;
        Check(shot && shot.IsPaused && shot.PhaseAge == age, "Impact animation pauses with the Director");
        director.enabled = true;
        while (shot && shot.PhaseAge < .14f) yield return null;
        var block = new MaterialPropertyBlock(); shot.volume.GetPropertyBlock(block);
        var fragments = block.GetVectorArray("_Balls").Take(block.GetInt("_BallCount")).ToArray();
        Check(fragments.Length == shot.impactFragments && fragments.Max(b => b.x) - fragments.Min(b => b.x) > .08f
            && fragments.Max(b => b.z) - fragments.Min(b => b.z) > .08f,
            "Impact splinters into independently scattered metaballs");
        float firstRadius = fragments[0].w;
        Capture("ranged-impact.png", player.transform, shot.transform);
        while (shot && shot.PhaseAge < .3f) yield return null;
        shot.volume.GetPropertyBlock(block);
        Check(block.GetVectorArray("_Balls")[0].w < firstRadius * .4f, "Impact fragments visibly shrink while spreading");
        while (shot) yield return null;
        Check(Mathf.Approximately(afterHit, player.massScore), "Impact remnants cannot damage the player a second time");

        // Outside the arena fixture so its edge cannot pre-empt the requested distance cutoff.
        var missed = Object.Instantiate(ranged.projectilePrefab, new Vector3(100f, 0f, 100f), Quaternion.identity);
        missed.Init(null, Vector3.forward); missed.lifetimeSeconds = 0f;
        missed.maxTravelDistance = ranged.firingRange * ranged.projectileRangeMultiplier;
        while (missed.Phase == RangedDroneProjectile.ShotPhase.Flying) yield return null;
        Check(missed.Phase == RangedDroneProjectile.ShotPhase.Expiring && Mathf.Abs(missed.DistanceTravelled - 6.75f) < .001f
            && Mathf.Abs(missed.transform.position.z - 106.75f) < .002f && !missed.GetComponent<Collider>().enabled,
            "A missed shot stops precisely at 6.75 units and disables damage");
        Vector3 end = missed.transform.position;
        while (missed && missed.PhaseAge < missed.expirySeconds * .45f) yield return null;
        missed.volume.GetPropertyBlock(block);
        Check(missed.transform.position == end && block.GetVectorArray("_Balls")[0].w < .14f * .4f,
            "Range expiry holds position and eases the metaball radii toward zero");
        Capture("ranged-expiring.png", missed.transform);
        while (missed) yield return null;
        Check(!missed, "Range-expired shot cleans up after its shrink animation");
    }

    private static IEnumerator SwarmRefinementChecks(GameObject normalPrefab)
    {
        float detection = drone.detectionRange, disengage = drone.disengageRange;
        Place(player, new Vector3(7f, 0f, 0f));
        var normal = Object.Instantiate(normalPrefab, new Vector3(-1.2f, 0f, -2f), Quaternion.identity).GetComponent<DroneController>();
        normal.spawnGraceSeconds = 0f;
        normal.detectionRange = normal.disengageRange = .1f;
        drone.detectionRange = drone.disengageRange = .1f;
        drone.enabled = true; drone.idleSpeed = .85f; Place(drone, new Vector3(1.2f, 0f, -2f));
        drone.GetComponent<Rigidbody>().rotation = Quaternion.identity;
        // Warm up after the pursuing-to-idle transition, then sample each actual physics step.
        float until = Time.time + 1f; while (Time.time < until) yield return null;
        var left = normal.gameObject.AddComponent<DroneIdleTurnProbe>();
        var right = drone.gameObject.AddComponent<DroneIdleTurnProbe>();
        until = Time.time + 5f; while (Time.time < until) yield return null;
        Check(left.samples > 100 && right.samples > 100 && left.totalTurn > 15f && right.totalTurn > 15f,
            "Both Drone variants exercise active idle swarm turns");
        Check(left.maxSpeed <= normal.swarmTurnSpeed + 1f && right.maxSpeed <= drone.swarmTurnSpeed + 1f,
            $"Idle turns remain bounded (normal {left.maxSpeed:F1}, ranged {right.maxSpeed:F1} degrees/second)");
        Check(left.maxAcceleration <= normal.swarmTurnAcceleration + 8f && right.maxAcceleration <= drone.swarmTurnAcceleration + 8f,
            $"Idle heading speed changes smoothly (normal {left.maxAcceleration:F1}, ranged {right.maxAcceleration:F1} degrees/second squared)");
        Object.Destroy(left); Object.Destroy(right); Object.Destroy(normal.gameObject);
        drone.enabled = false; drone.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        drone.detectionRange = detection; drone.disengageRange = disengage;
        Place(drone, Vector3.zero); Place(player, new Vector3(3f, 0f, 0f));
        yield return null;
    }
}

// Editor-only fixture; measures consecutive physics headings independently of render/editor frame rate.
[DefaultExecutionOrder(10000)]
public sealed class DroneIdleTurnProbe : MonoBehaviour
{
    public int samples;
    public float maxSpeed, maxAcceleration, totalTurn;
    private float previousYaw, previousSpeed;
    private Rigidbody body;
    private void Awake() { body = GetComponent<Rigidbody>(); previousYaw = body.rotation.eulerAngles.y; }
    private void FixedUpdate()
    {
        float yaw = body.rotation.eulerAngles.y;
        float speed = Mathf.DeltaAngle(previousYaw, yaw) / Time.fixedDeltaTime;
        if (samples > 1) maxAcceleration = Mathf.Max(maxAcceleration, Mathf.Abs(speed - previousSpeed) / Time.fixedDeltaTime);
        if (samples > 0) { maxSpeed = Mathf.Max(maxSpeed, Mathf.Abs(speed)); totalTurn += Mathf.Abs(speed) * Time.fixedDeltaTime; }
        previousYaw = yaw; previousSpeed = speed; samples++;
    }
}
#endif
