using Massive.Scoring;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Finite-lived enemy loot using the existing body collection, mass, score and receipt path.</summary>
    public sealed class EnemyLootPickup : MatterNuggetScript
    {
        [Min(0f)] public float personalMultiplierGain;
        private EnemyBase source;
        private EnemyDirector director;
        private Transform simulationRoot;
        private PlayerControllerScript demonstrationCollector;
        private bool isolated, paused, savedKinematic;
        private Rigidbody lootBody;
        private Vector3 savedVelocity;
        private ParticleSystem[] systems;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float clock;
        private static readonly int PreviewTime = Shader.PropertyToID("_PreviewTime");

        public void Bind(EnemyBase owner)
        {
            source = owner; director = owner.Director; simulationRoot = owner.SimulationRoot;
            isolated = simulationRoot;
        }
        // Explicit opt-in: ordinary isolated enemy loot remains noncollectible.
        public void ConfigureDemonstration(PlayerControllerScript collector)
        {
            if (!collector || collector.ParticipatesInMatch || !collector.SimulationRoot) return;
            demonstrationCollector = collector; simulationRoot = collector.SimulationRoot; isolated = true;
        }
        protected override void Awake()
        {
            base.Awake(); lootBody = GetComponent<Rigidbody>(); systems = GetComponentsInChildren<ParticleSystem>(true);
            renderers = GetComponentsInChildren<ParticleSystemRenderer>(true); block = new MaterialPropertyBlock();
        }
        private bool ShouldPause => source && source.IsPaused || director && (!director.isActiveAndEnabled || director.timelinePaused ||
            director.anomalyManager && director.anomalyManager.IsAnomalyRunning && director.spawnProfile && director.spawnProfile.freezeExistingEnemiesDuringAnomalies) ||
            !isolated && MatchScoreService.Instance && (!MatchScoreService.Instance.IsScoringOpen || !MatchScoreService.Instance.IsChainClockRunning);
        private bool SyncPause()
        {
            bool next = ShouldPause;
            if (next == paused) return paused;
            paused = next;
            if (paused)
            {
                savedKinematic = lootBody.isKinematic; savedVelocity = lootBody.linearVelocity;
                if (!lootBody.isKinematic) lootBody.linearVelocity = Vector3.zero;
                lootBody.isKinematic = true;
                foreach (var ps in systems) ps.Pause(false);
            }
            else
            {
                lootBody.isKinematic = savedKinematic;
                if (!savedKinematic) lootBody.linearVelocity = savedVelocity;
                foreach (var ps in systems) ps.Play(false);
            }
            return paused;
        }
        protected override void Update()
        {
            if (isolated && !simulationRoot) { Destroy(gameObject); return; }
            if (SyncPause()) return;
            base.Update(); clock += Time.deltaTime;
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block); block.SetFloat(PreviewTime, clock); renderer.SetPropertyBlock(block);
            }
        }
        protected override void FixedUpdate() { if (!SyncPause()) base.FixedUpdate(); }
        protected override void OnTriggerEnter(Collider other) { if ((!isolated || demonstrationCollector) && !ShouldPause) base.OnTriggerEnter(other); }
        protected override void OnTriggerStay(Collider other) { if ((!isolated || demonstrationCollector) && !ShouldPause) base.OnTriggerStay(other); }
        protected override bool CanCollectPlayer(PlayerControllerScript player) => isolated
            ? player == demonstrationCollector && simulationRoot && player.SimulationRoot == simulationRoot
            : base.CanCollectPlayer(player);
        protected override double ApplyPersonalMultiplierReward(PlayerControllerScript player)
        {
            var scores = MatchScoreService.Instance;
            if (player != demonstrationCollector && (!scores || !scores.IsScoringOpen || !scores.IsChainClockRunning || scores.IsBonusScoringOnly)) return 0;
            var chain = player.GetComponent<PlayerScoreChain>();
            return chain ? chain.AddMultiplier(personalMultiplierGain) : 0;
        }
    }
}
