using Massive.Scoring;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Swept metaball shot. The shared base owns damage/reflection; this class owns travel and retirement.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class RangedDroneProjectile : EnemyProjectileBase
    {
        public enum ShotPhase { Flying, Impact, Expiring }
        public Renderer volume;
        public LayerMask collisionMask = ~0;
        [Header("Resonance passage")]
        [Tooltip("Fraction of normal bullet speed while passing through Resonance. 0.75 = 75%. Overlapping colliders do not stack.")]
        [Range(.05f, 1f)] public float resonanceSpeedMultiplier = .75f;
        [Header("Travel and retirement")]
        [Min(.01f)] public float maxTravelDistance = 6.75f;
        [Min(.05f)] public float expirySeconds = .35f;
        [Min(.05f)] public float impactSeconds = .4f;
        [Tooltip("Hold the packet against the visible outline before it splinters.")]
        [Range(0f, .15f)] public float contactHoldSeconds = .06f;
        [Range(4, 20)] public int impactFragments = 10;
        [Min(0f)] public float impactSpread = .48f;
        public ShotPhase Phase { get; private set; }
        public float PhaseAge { get; private set; }
        public float DistanceTravelled { get; private set; }
        public bool IsPaused => (Owner && !Owner.IsDead && Owner.IsPaused) || (director &&
            (!director.isActiveAndEnabled ||
             (director.waitForScoring && (!MatchScoreService.Instance || !MatchScoreService.Instance.IsScoringOpen || !MatchScoreService.Instance.IsChainClockRunning)) ||
             (director.anomalyManager && director.anomalyManager.IsAnomalyRunning && director.spawnProfile && director.spawnProfile.freezeExistingEnemiesDuringAnomalies)));
        public float ActiveAge { get; private set; }
        private Rigidbody body;
        private SphereCollider sphere;
        private EnemyDirector director;
        private ArenaBoundsFromVectorGrid arena;
        private MaterialPropertyBlock properties;
        private PlayerVisualController impactPlayer;
        private Vector3 contactTowardPlayer, initialVolumeScale;
        private float volumeScale, smoothUnion, surfaceEpsilon, reflectedAt = -1f;
        private bool initialized;
        private readonly Vector4[] balls = new Vector4[48];
        private readonly Vector3[] fragmentDirections = new Vector3[20];
        private readonly float[] fragmentSizes = new float[20];
        private readonly RaycastHit[] hits = new RaycastHit[48];
        private readonly Collider[] overlaps = new Collider[32];
        private static readonly int BallsId = Shader.PropertyToID("_Balls"), CountId = Shader.PropertyToID("_BallCount");

        protected override void Awake()
        {
            useRigidbody = false; base.Awake();
            body = GetComponent<Rigidbody>(); sphere = GetComponent<SphereCollider>();
            body.isKinematic = true; body.useGravity = false; sphere.isTrigger = true;
            properties = new MaterialPropertyBlock();
            if (volume)
            {
                initialVolumeScale = volume.transform.localScale;
                volumeScale = Mathf.Abs(volume.transform.lossyScale.x);
                smoothUnion = volume.sharedMaterial.GetFloat("_SmoothK");
                surfaceEpsilon = volume.sharedMaterial.GetFloat("_SurfaceEps");
            }
            var random = new System.Random(GetInstanceID());
            for (int i = 0; i < fragmentDirections.Length; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float height = (float)random.NextDouble() * 1.4f - .7f;
                fragmentDirections[i] = new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle)).normalized
                    * Mathf.Lerp(.55f, 1f, (float)random.NextDouble());
                fragmentSizes[i] = Mathf.Lerp(.042f, .075f, (float)random.NextDouble());
            }
        }
        public override void Init(EnemyBase owner, Vector3 directionWS)
        {
            useRigidbody = false; base.Init(owner, directionWS);
            director = owner ? owner.Director : null; arena = director ? director.arenaBounds : null;
            ActiveAge = PhaseAge = DistanceTravelled = 0f; reflectedAt = -1f; initialized = true;
            Phase = ShotPhase.Flying; impactPlayer = null; sphere.enabled = true;
            if (owner && owner.TryGetComponent<RangedDroneController>(out var ranged))
                maxTravelDistance = ranged.firingRange * ranged.projectileRangeMultiplier;
            UpdateVisual();
        }
        protected override void Update() { } // Base Update uses wall-time lifetime and unswept motion.
        private void LateUpdate()
        {
            if (!initialized || IsPaused) return;
            if (Phase == ShotPhase.Impact && PhaseAge <= contactHoldSeconds) AlignPlayerContact();
            UpdateVisual();
        }
        private void FixedUpdate()
        {
            if (!initialized || IsPaused) return;
            float delta = Time.fixedDeltaTime; ActiveAge += delta;
            if (Phase != ShotPhase.Flying)
            {
                PhaseAge += delta;
                float duration = Phase == ShotPhase.Impact ? contactHoldSeconds + impactSeconds : expirySeconds;
                if (PhaseAge >= duration) { initialized = false; Destroy(gameObject); }
                return;
            }
            if (lifetimeSeconds > 0f && ActiveAge >= lifetimeSeconds) { Retire(ShotPhase.Expiring); return; }
            float radius = sphere.radius * Mathf.Abs(transform.lossyScale.x);
            int count = Physics.OverlapSphereNonAlloc(body.position, radius, overlaps, collisionMask, QueryTriggerInteraction.Collide);
            bool insideResonance = false;
            for (int i = 0; i < count; i++)
            {
                if (IsResonance(overlaps[i])) { insideResonance = true; continue; }
                if (CanHit(overlaps[i])) { Resolve(overlaps[i]); if (Phase != ShotPhase.Flying || reflectedAt == ActiveAge) return; }
            }
            if (count == overlaps.Length) { Retire(ShotPhase.Expiring); return; }
            float remaining = Mathf.Max(0f, maxTravelDistance - DistanceTravelled);
            if (remaining <= .00001f) { Retire(ShotPhase.Expiring); return; }
            float freeDistance = Mathf.Max(0f, speed) * delta;
            if (freeDistance <= .00001f) return;
            count = Physics.SphereCastNonAlloc(body.position, radius, TravelDirection, hits, Mathf.Min(freeDistance, remaining), collisionMask, QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity; Collider contact = null;
            float resonanceEntry = insideResonance ? 0f : freeDistance;
            for (int i = 0; i < count; i++)
            {
                if (IsResonance(hits[i].collider)) { resonanceEntry = Mathf.Min(resonanceEntry, hits[i].distance); continue; }
                if (hits[i].distance < nearest && CanHit(hits[i].collider)) { nearest = hits[i].distance; contact = hits[i].collider; }
            }
            if (count == hits.Length) { Retire(ShotPhase.Expiring); return; }
            // Spend the part of this step before entry at full speed. Re-query overlaps every step
            // so exiting, disabling or rebuilding a field cannot leave a stale slowdown behind.
            float distance = Mathf.Min(remaining, resonanceEntry + (freeDistance - resonanceEntry) * Mathf.Clamp(resonanceSpeedMultiplier, .05f, 1f));
            if (contact && nearest <= distance) { Move(nearest); Resolve(contact); return; }
            Vector3 next = body.position + TravelDirection * distance;
            if (arena && !arena.ContainsWorldPoint(next, radius))
            {
                Vector3 edge = arena.ClampWorldPointInside(next, radius);
                DistanceTravelled += Vector3.Distance(body.position, edge); body.position = edge;
                Retire(ShotPhase.Expiring); return;
            }
            Move(distance);
            if (distance >= remaining - .00001f) Retire(ShotPhase.Expiring);
        }
        private void Move(float distance)
        {
            DistanceTravelled += distance;
            // Present the swept contact this frame; interpolating here leaves the visual behind the hit.
            body.position += TravelDirection * distance;
        }
        private bool CanHit(Collider other)
        {
            if (!other || !other.enabled || other.transform.IsChildOf(transform)) return false;
            if (IsResonance(other)) return false;
            var player = other.GetComponentInParent<PlayerControllerScript>();
            if (player)
            {
                if (!player.isActiveAndEnabled || (Owner ? !Owner.SharesSimulationWith(player) : player.IsPseudoPlayer) || player.IsSpawning || player.IsMatchInputLocked || player.IsInvulnerable) return false;
                if (player == ReflectedBy && ActiveAge - reflectedAt < .15f) return false;
                if (Owner && Owner.OwnerTeamId >= 0 && player.teamID == Owner.OwnerTeamId && !ReflectedBy) return false;
                if (other.CompareTag("Shield")) return player.shieldOn;
                if (other.GetComponentInParent<PlayerMelee>()) return false;
                return other.CompareTag("Player") && !player.shieldOn;
            }
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy && Owner && !Owner.SharesSimulationWith(enemy)) return false;
            if (enemy == Owner && enemy) return ReflectedBy && !enemy.IsDead && !enemy.IsPaused;
            if (enemy && other.isTrigger) return false;
            if (other.GetComponentInParent<EnemyProjectileBase>()) return false;
            if (other.GetComponent<VectorGridGPU>() || other.bounds.max.y < body.position.y - .05f) return false;
            int layer = other.gameObject.layer;
            return !other.isTrigger || layer == 9 || layer == 11;
        }
        private static bool IsResonance(Collider collider) => collider && collider.GetComponentInParent<Massive.Resonance.ResonanceSegment>();
        private void Resolve(Collider other)
        {
            var previousReflector = ReflectedBy;
            Vector3 previousDirection = TravelDirection;
            base.OnTriggerEnter(other);
            if (ImpactResolved) return;
            if (ReflectedBy && (ReflectedBy != previousReflector || Vector3.Dot(previousDirection, TravelDirection) < .999f))
            { reflectedAt = ActiveAge; transform.rotation = Quaternion.LookRotation(TravelDirection, Vector3.up); return; }
            if (!other.GetComponentInParent<PlayerControllerScript>()) Retire(ShotPhase.Impact);
        }
        protected override void OnImpactResolved(Collider other)
        {
            var player = other.GetComponentInParent<PlayerControllerScript>();
            if (player)
            {
                impactPlayer = player.visualsController;
                contactTowardPlayer = player.transform.position - body.position; contactTowardPlayer.y = 0f;
                contactTowardPlayer.Normalize();
            }
            Retire(ShotPhase.Impact); AlignPlayerContact();
        }
        private void AlignPlayerContact()
        {
            if (!impactPlayer) return;
            Vector3 edge = impactPlayer.OutlinePointTowards(impactPlayer.transform.position - contactTowardPlayer * 10f);
            body.position = edge - contactTowardPlayer * (volumeScale * .07f);
        }
        // The explicit sweep owns contacts, including overlaps, to prevent duplicate damage and tunnelling.
        protected override void OnTriggerEnter(Collider other) { }
        protected override void OnCollisionEnter(Collision collision) { }
        private void Retire(ShotPhase phase)
        {
            if (Phase != ShotPhase.Flying) return;
            Phase = phase; PhaseAge = 0f; sphere.enabled = false;
            UpdateVisual();
        }
        private void UpdateVisual()
        {
            if (!volume) return;
            transform.rotation = Quaternion.LookRotation(TravelDirection, Vector3.up);
            bool splinter = Phase == ShotPhase.Impact && PhaseAge > contactHoldSeconds;
            float t = splinter ? Mathf.Clamp01((PhaseAge - contactHoldSeconds) / Mathf.Max(.01f, impactSeconds))
                : Phase == ShotPhase.Expiring ? Mathf.Clamp01(PhaseAge / Mathf.Max(.01f, expirySeconds)) : 0f;
            // Ease-out shrink: most contraction happens early, settling gently to zero.
            float shrink = (1f - t) * (1f - t);
            float expansion = splinter ? Mathf.Max(1f, (impactSpread * 2f + .3f) / Mathf.Max(.01f, volumeScale)) : 1f;
            volume.transform.localScale = initialVolumeScale * expansion;
            int count = splinter ? Mathf.Clamp(impactFragments, 4, 20) : 4;
            float emerge = Mathf.SmoothStep(0f, 1f, ActiveAge / .08f);
            for (int i = 0; i < count; i++)
            {
                Vector3 p; float radius;
                if (splinter)
                {
                    float spread = 1f - (1f - t) * (1f - t);
                    p = fragmentDirections[i] * (impactSpread * spread / Mathf.Max(.01f, volumeScale));
                    radius = fragmentSizes[i] * shrink;
                }
                else
                {
                    float phase = ActiveAge * 17f + i * 2.1f;
                    p = i == 0 ? Vector3.zero : new Vector3(Mathf.Sin(phase) * .018f, Mathf.Cos(phase * 1.3f) * .018f, -.13f * i) * emerge;
                    radius = .14f * (1f - i * .14f) * Mathf.Lerp(.6f, 1f, emerge) * shrink;
                }
                balls[i] = new Vector4(p.x / expansion, p.y / expansion, p.z / expansion, radius / expansion);
            }
            // The union radius must shrink too or overlapping zero-radius lobes leave a residual blob.
            properties.SetFloat("_SmoothK", Mathf.Max(.00001f, smoothUnion * shrink / expansion));
            properties.SetFloat("_SurfaceEps", Mathf.Max(.0001f, surfaceEpsilon * Mathf.Max(.05f, shrink) / expansion));
            properties.SetVectorArray(BallsId, balls); properties.SetInt(CountId, count); volume.SetPropertyBlock(properties);
        }
    }
}
