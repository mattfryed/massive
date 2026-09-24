using System.Collections.Generic;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Orbital
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class OrbitalProbabilityCloud : MonoBehaviour
    {
        public OrbitalCloudVisual cloud;
        public OrbitalImpactVisual impacts;
        public OrbitalMassNugget nuggetPrefab;
        public MatchScoreService scoreService;
        public AnomalyManager anomalyManager;
        [Tooltip("Independent encounters/second at maximum density. At 2.4, one-second probability is 91%.")]
        [Min(0f)] public float peakStrikesPerSecond = 2.4f;
        public Vector2 kickSpeed = new Vector2(2.8f, 4.6f);
        [Min(.1f)] public float maxPlanarSpeed = 10f;
        [Range(8, 128)] public int nuggetCapacity = 64;
        public Vector2 nuggetSpeed = new Vector2(2f, 4f);
        [Min(1f)] public float nuggetLifetime = 18f;
        public bool waitForMatch = true;
        public bool fixedRandomSeed;
        public int randomSeed = 6109;

        private readonly Collider[] overlaps = new Collider[512];
        private readonly HashSet<Rigidbody> visited = new HashSet<Rigidbody>();
        private OrbitalMassNugget[] nuggets;
        private System.Random random;
        private int nextNugget;
        public int TotalStrikes { get; private set; }
        public int PlayerStrikes { get; private set; }
        public int CoreStrikes { get; private set; }
        public int EnemyStrikes { get; private set; }
        public int NuggetsReleased { get; private set; }
        public Vector3 LastContact { get; private set; }
        public Vector3 LastDirection { get; private set; }
        public bool IsHazardActive => isActiveAndEnabled && Time.timeScale > 0f && cloud != null && cloud.isActiveAndEnabled &&
            (!waitForMatch || (scoreService != null && scoreService.IsScoringOpen && scoreService.IsChainClockRunning)) &&
            (anomalyManager == null || !anomalyManager.IsAnomalyRunning);

        private void Awake()
        {
            if (!cloud) cloud = GetComponent<OrbitalCloudVisual>();
            if (!impacts) impacts = GetComponent<OrbitalImpactVisual>();
            random = new System.Random(fixedRandomSeed ? randomSeed : System.Guid.NewGuid().GetHashCode());
            nuggets = new OrbitalMassNugget[Mathf.Clamp(nuggetCapacity, 8, 128)];
            if (!cloud || !impacts || !nuggetPrefab)
            { Debug.LogError("[ORBITAL] Assign cloud, impact visuals and mass nugget prefab.", this); enabled = false; }
        }

        public float DensityAt(Vector3 position)
        {
            if (!cloud) return 0f;
            Vector3 local = cloud.transform.InverseTransformPoint(position); local.y = 0f;
            return cloud.DensityAtNormalized(local / Mathf.Max(.1f, cloud.radius));
        }

        private void FixedUpdate()
        {
            if (!IsHazardActive) return;
            cloud.AdvanceFormation(Time.fixedDeltaTime);
            visited.Clear();
            float radius = cloud.OuterRadius * Mathf.Max(Mathf.Abs(cloud.transform.lossyScale.x), Mathf.Abs(cloud.transform.lossyScale.z));
            int count = Physics.OverlapSphereNonAlloc(cloud.transform.position, radius + 1f, overlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider shape = overlaps[i];
                Rigidbody body = shape.attachedRigidbody;
                if (!body || body.isKinematic || body.gameObject.scene != gameObject.scene || visited.Contains(body)) continue;
                var player = body.GetComponent<PlayerControllerScript>();
                var core = body.GetComponent<AmplifierCoreGameplay>();
                var enemy = body.GetComponent<EnemyBase>();
                if (player && (!player.isActiveAndEnabled || player.IsPseudoPlayer || player.temporarilyEliminated || player.IsMatchInputLocked)) continue;
                if (core && (!core.isActiveAndEnabled || core.IsPresentationOnly || core.IsCaptured)) continue;
                if (enemy && (!enemy.isActiveAndEnabled || enemy.IsDead || enemy.IsPaused)) continue;
                if (!player && !core && !enemy) continue;
                // Ignore weapon/sensor volumes. Trigger-bodied enemies expose their body through EnemyHurtbox.
                if (shape.isTrigger && !(enemy && shape.GetComponent<EnemyHurtbox>())) continue;
                visited.Add(body);
                Vector3 direction = OrbitalDensity.SampleDirection(body.worldCenterOfMass - cloud.transform.position, random);
                Vector3 contact = shape.ClosestPoint(body.worldCenterOfMass - direction * (shape.bounds.extents.magnitude + 1f));
                float density = DensityAt(contact);
                if (random.NextDouble() >= OrbitalDensity.StrikeProbability(density, peakStrikesPerSecond, Time.fixedDeltaTime)) continue;
                ApplyStrike(body, player, core, enemy, contact, direction);
            }
        }

        private void ApplyStrike(Rigidbody body, PlayerControllerScript player, AmplifierCoreGameplay core, EnemyBase enemy,
            Vector3 contact, Vector3 direction)
        {
            float speed = Mathf.Lerp(kickSpeed.x, kickSpeed.y, (float)random.NextDouble());
            Vector3 velocity = body.linearVelocity;
            Vector3 desired = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z) + direction * speed, maxPlanarSpeed);
            body.AddForce(desired - new Vector3(velocity.x, 0f, velocity.z), ForceMode.VelocityChange);
            if (player) { player.ProtectActionMomentum(.2f); player.ExternalStun(.10f); PlayerStrikes++; }
            if (core) { core.SetAttractionExcitement(1f); CoreStrikes++; }
            if (enemy) EnemyStrikes++;
            impacts.Show(cloud.transform.position, contact, direction, Mathf.Lerp(.65f, 1.1f, (float)random.NextDouble()));
            ReleaseNugget(contact);
            LastContact = contact; LastDirection = direction; TotalStrikes++;
        }

        private void ReleaseNugget(Vector3 contact)
        {
            int index = nextNugget++ % nuggets.Length;
            for (int i = 0; i < nuggets.Length; i++)
            {
                int candidate = (index + i) % nuggets.Length;
                if (!nuggets[candidate] || !nuggets[candidate].gameObject.activeSelf) { index = candidate; break; }
            }
            if (!nuggets[index]) nuggets[index] = Instantiate(nuggetPrefab, transform);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector3 velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
                Mathf.Lerp(nuggetSpeed.x, nuggetSpeed.y, (float)random.NextDouble());
            contact.y = 0f;
            nuggets[index].EjectSmoothly(contact, velocity, nuggetLifetime);
            NuggetsReleased++;
        }
        private void OnDisable()
        {
            visited.Clear();
            if (nuggets != null) foreach (var nugget in nuggets) if (nugget) nugget.gameObject.SetActive(false);
        }
    }
}
