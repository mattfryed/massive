using Massive.Demonstrations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Emergence
{
    /// <summary>A small, seeded close-up using the production ecology and reward visuals.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EmergenceInstructionsDemo : MonoBehaviour
    {
        public EmergenceProfile ecology;
        public Material particleMaterial;
        [Tooltip("Matter nugglet, matter nugget, amplifier nugglet, amplifier nugget. The demo cycles through all four.")]
        public EmergenceMassNugget[] rewardPrefabs;
        public PlayerDemoView viewport;
        public Transform panelFrame;
        public Vector2 viewportSize = new Vector2(7.37f, 3.9f);
        [Range(1, 2)] public int organismCount = 2;
        [Min(5)] public float loopSeconds = 8;
        [Range(.2f, 1)] public float transitionSeconds = .6f;
        [Min(.5f)] public float feedingLeadIn = 1;
        [Min(.5f)] public float rewardHoldSeconds = 2.5f;

        public int CompletedLoops { get; private set; }
        public int RewardsReleased { get; private set; }
        public int BudsStarted { get; private set; }
        public int Consumed => consumedBeforeLoop + (simulation?.Consumed ?? 0);
        public int ParticleCount => simulation?.Count ?? 0;
        public int BodyCount => simulation?.BodyCount ?? 0;
        public float LoopAge => simulation?.Age ?? 0;
        public float VisibleScale { get; private set; }
        public int ActiveRewards { get { int n = 0; foreach (var reward in rewards) if (reward.pickup) n++; return n; } }
        public Camera PreviewCamera => previewCamera;

        struct Reward
        {
            public EmergenceMassNugget pickup;
            public ParticleSystem[] particles;
            public uint serial;
            public int slot;
            public float size, age;
            public bool released;
            public Vector2 point, velocity;
        }
        readonly Reward[] rewards = new Reward[2];
        readonly uint[] seenBuds = new uint[EmergenceBud.Capacity];
        EmergenceProfile settings;
        EmergenceSimulation simulation;
        Camera previewCamera;
        Mesh mesh, previousMesh;
        Vector3[] vertices;
        Color[] colors;
        float accumulator;
        int releasedThisLoop, consumedBeforeLoop;
        const float Step = 1f / 60;

        void OnEnable() { if (Application.isPlaying) Prepare(); }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void LateUpdate()
        {
            if (simulation == null || !viewport || !panelFrame || !previewCamera) return;
            if (!viewport.screenCamera) viewport.screenCamera = Camera.main;
            if (!viewport.screenCamera) return;
            Vector3 half = new Vector3(viewportSize.x, viewportSize.y, 0) * .5f;
            viewport.lowerLeft = panelFrame.TransformPoint(-half); viewport.upperRight = panelFrame.TransformPoint(half);
            viewport.RefreshRect(); previewCamera.depth = viewport.screenCamera.depth + 1;
            previewCamera.enabled = true;
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }

        public bool Prepare()
        {
            if (simulation != null) return true;
            if (!ecology || !particleMaterial || !viewport || rewardPrefabs == null || rewardPrefabs.Length != 4) return false;
            foreach (var prefab in rewardPrefabs) if (!prefab) return false;
            previewCamera = viewport.GetComponent<Camera>(); previewCamera.enabled = false;
            // Clone configuration. The saved gameplay profile is never tuned by the demonstration.
            settings = Instantiate(ecology); settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backend = EmergenceBackend.CPUReference;
            int colonies = Mathf.Clamp(organismCount, 1, 2);
            int[] perColony = { 32, 16, 8, 80, 12 };
            for (int i = 0; i < perColony.Length; i++) settings.species[i].count = perColony[i] * colonies;
            CompletedLoops = RewardsReleased = BudsStarted = consumedBeforeLoop = 0;
            BeginLoop(); BuildMesh(); RenderParticles(); return true;
        }

        void BeginLoop()
        {
            ClearRewards(); simulation?.Dispose();
            simulation = new EmergenceSimulation(settings, new Vector2(3, 2), 1729);
            int colonies = Mathf.Clamp(organismCount, 1, 2);
            int[] local = new int[EmergenceProfile.SpeciesCount];
            for (int index = 0; index < simulation.Count; index++)
            {
                var p = simulation.Particles[index];
                int i = local[p.species]++, side = i % colonies, n = i / colonies;
                float angle = n * 2.39996323f + p.species * 1.2f;
                float radius = Mathf.Sqrt((n + .5f) / (settings.species[p.species].count / (float)colonies)) * (p.species < 3 ? .33f : 1.25f);
                Vector2 center = colonies == 1 ? Vector2.zero : new Vector2(side == 0 ? -1 : 1, side == 0 ? .2f : -.2f);
                p.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                // Begin with mature, previously fed colonies so the instructional event is immediate.
                // Their subsequent movement, feeding, bud validation and material debit are all simulated.
                if (p.species < 3) p.maturity = settings.maturationSeconds;
                if (p.species == 2) { p.reserve = 2; p.waste = settings.materialPerNugget / 8f * 1.2f; }
                simulation.Particles[index] = p;
            }
            releasedThisLoop = 0; System.Array.Clear(seenBuds, 0, seenBuds.Length);
        }

        public void Advance(float deltaTime)
        {
            if (!isActiveAndEnabled || !Prepare()) return;
            accumulator = Mathf.Min(accumulator + Mathf.Clamp(deltaTime, 0, .1f), Step * 6);
            while (accumulator >= Step)
            {
                simulation.EmissionSlots = Mathf.Clamp(organismCount, 1, 2) - releasedThisLoop;
                simulation.Step(Step, simulation.Age >= feedingLeadIn);
                VisibleScale = Mathf.SmoothStep(0, 1, Mathf.Clamp01(simulation.Age / transitionSeconds)) *
                    Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Max(5, loopSeconds) - simulation.Age) / transitionSeconds));
                UpdateBuds(); ReleaseRewards(); AnimateRewards(Step);
                if (simulation.Age >= Mathf.Max(5, loopSeconds))
                {
                    consumedBeforeLoop += simulation.Consumed; CompletedLoops++; BeginLoop(); VisibleScale = 0;
                }
                accumulator -= Step;
            }
            RenderParticles();
        }

        void UpdateBuds()
        {
            for (int slot = 0; slot < simulation.RewardBuds.Length; slot++)
            {
                var bud = simulation.RewardBuds[slot];
                if (!bud.Attached || seenBuds[slot] == bud.serial) continue;
                for (int i = 0; i < rewards.Length; i++)
                {
                    if (rewards[i].pickup) continue;
                    var prefab = rewardPrefabs[BudsStarted % rewardPrefabs.Length];
                    var pickup = Instantiate(prefab, transform);
                    pickup.name = "Demonstration " + prefab.name;
                    // Prefabs are inactive. Disable authority before activation; nothing registers as a live pickup.
                    foreach (var behaviour in pickup.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                    foreach (var collider in pickup.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    var body = pickup.GetComponent<Rigidbody>(); body.detectCollisions = false;
                    foreach (var t in pickup.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
                    pickup.BeginAttachedPresentation(WorldPoint(bud.position));
                    var particles = pickup.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var particle in particles) { var main = particle.main; main.useUnscaledTime = true; particle.Pause(false); }
                    rewards[i] = new Reward { pickup = pickup, particles = particles, slot = slot, serial = bud.serial,
                        size = Mathf.Abs(prefab.transform.localScale.x) / Mathf.Max(.0001f, Mathf.Abs(rewardPrefabs[1].transform.localScale.x)) };
                    seenBuds[slot] = bud.serial; BudsStarted++; break;
                }
            }
        }

        void ReleaseRewards()
        {
            while (simulation.TryDequeue(out var emission))
            {
                for (int i = 0; i < rewards.Length; i++)
                {
                    var r = rewards[i];
                    if (!r.pickup || r.slot != emission.budSlot || r.serial != emission.budSerial || r.released) continue;
                    var bud = simulation.RewardBuds[r.slot];
                    // TryDequeue frees the bud's phase, but retains its completed pose and identity.
                    r.point = bud.Tip(settings, r.size); r.velocity = emission.velocity + bud.direction * settings.nuggetEjectionSpeed;
                    r.released = true; r.age = 0; r.pickup.UseDetachedPreviewAppearance();
                    rewards[i] = r; releasedThisLoop++; RewardsReleased++; break;
                }
            }
        }

        void AnimateRewards(float dt)
        {
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i]; if (!r.pickup) continue;
                float scale;
                if (r.released)
                {
                    r.age += dt; r.point += r.velocity * dt; r.velocity *= Mathf.Exp(-.8f * dt);
                    scale = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(rewardHoldSeconds, rewardHoldSeconds + transitionSeconds, r.age));
                }
                else
                {
                    var bud = simulation.RewardBuds[r.slot]; r.point = bud.Tip(settings, r.size); scale = bud.Attached ? bud.Scale(settings) : 0;
                }
                r.pickup.UpdateAttachedPresentation(WorldPoint(r.point), scale * VisibleScale);
                foreach (var particle in r.particles) { particle.Simulate(dt, false, false, false); particle.Pause(false); }
                if ((r.released && scale <= 0) || (!r.released && !simulation.RewardBuds[r.slot].Attached))
                { DestroyReward(r.pickup); r = default; }
                rewards[i] = r;
            }
        }
        Vector3 WorldPoint(Vector2 point) => transform.TransformPoint(new Vector3(point.x, .015f, point.y));

        void BuildMesh()
        {
            var filter = GetComponent<MeshFilter>(); previousMesh = filter.sharedMesh;
            mesh = new Mesh { name = "EMERGENCE instructions ecology", hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic();
            vertices = new Vector3[simulation.Count * 4]; colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length]; var triangles = new int[simulation.Count * 6];
            for (int i = 0; i < simulation.Count; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles; filter.sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>(); renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        void RenderParticles()
        {
            if (!mesh) return;
            for (int i = 0; i < simulation.Count; i++)
            {
                var p = simulation.Particles[i]; float radius = p.radius * p.visualScale * VisibleScale;
                if (p.species < 3)
                {
                    float pulse = 0;
                    foreach (var bud in simulation.RewardBuds) if (bud.Attached)
                    {
                        float weight = Mathf.Clamp01(1 - Vector2.Distance(p.position, bud.position) / settings.budPulseRadius);
                        pulse += bud.Pulse(settings) * weight * weight;
                    }
                    radius *= 1 + Mathf.Clamp(pulse, -1, 1) * settings.budPulseStrength;
                }
                Color color = settings.species[p.species].color; color.a = 1;
                int v = i * 4;
                vertices[v] = new Vector3(p.position.x - radius, 0, p.position.y - radius);
                vertices[v + 1] = new Vector3(p.position.x + radius, 0, p.position.y - radius);
                vertices[v + 2] = new Vector3(p.position.x + radius, 0, p.position.y + radius);
                vertices[v + 3] = new Vector3(p.position.x - radius, 0, p.position.y + radius);
                for (int j = 0; j < 4; j++) colors[v + j] = color;
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        }
        void ClearRewards()
        {
            for (int i = 0; i < rewards.Length; i++)
            { DestroyReward(rewards[i].pickup); rewards[i] = default; }
        }
        static void DestroyReward(EmergenceMassNugget pickup)
        {
            if (!pickup) return;
            pickup.gameObject.SetActive(false); pickup.ReleasePreviewMaterials(); DestroyOwned(pickup.gameObject);
        }
        void Release()
        {
            if (previewCamera) previewCamera.enabled = false;
            ClearRewards(); simulation?.Dispose(); simulation = null; accumulator = 0;
            var filter = GetComponent<MeshFilter>(); if (filter && filter.sharedMesh == mesh) filter.sharedMesh = previousMesh;
            DestroyOwned(mesh); mesh = null; previousMesh = null; DestroyOwned(settings); settings = null;
        }
        static void DestroyOwned(Object value)
        { if (value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    }
}
