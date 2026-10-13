using System;
using UnityEngine;

namespace Massive.Emergence
{
    /// <summary>Fixed-step, planar active matter. Budgets belong to particles, never transient group IDs.</summary>
    public sealed partial class EmergenceSimulation : IEmergenceSimulation
    {
        public struct Particle
        {
            public Vector2 position, velocity;
            public int species;
            public float radius, reserve, waste, maturity, disruption, cooldown, returnIn;
            // Phase 0: normal/hidden; 1: approaching a catalyst; 2: shrinking; 3: growing.
            public int feedingPhase, feedingTarget;
            public float transition, visualScale;
            public Vector2 feedingStart;
            public bool Alive => returnIn <= 0;
        }
        public struct Secretion { public Vector2 position, velocity; }
        public readonly Particle[] Particles;
        public readonly Vector2 HalfSize;
        public int Count => Particles.Length;
        Vector2 IEmergenceSimulation.HalfSize => HalfSize;
        Particle[] IEmergenceSimulation.Particles => Particles;
        public void Dispose() { }
        public int BodyCount { get; private set; }
        public int ProductiveBodies { get; private set; }
        public int Consumed { get; private set; }
        public int Replenished { get; private set; }
        public int Secreted { get; private set; }
        public float ExportedMaterial { get; private set; }
        public float Age { get; private set; }
        public int PairChecks { get; private set; }
        public int Disruptions { get; private set; }
        public float StoredMaterial { get { float sum = 0; foreach (var p in Particles) sum += p.reserve + p.waste; return sum; } }
        public int PendingCount => queued;

        readonly EmergenceProfile profile;
        readonly System.Random random;
        readonly int bodyParticles, nutrientEnd, columns, rows;
        readonly float cellSize;
        readonly int[] heads, next, parents, sizes, motors, catalysts, structures, neighbours;
        readonly float[] groupReserve, groupWaste;
        readonly bool[] viable, edible;
        readonly Vector2[] forces, centers, velocities;
        readonly Secretion[] pending = new Secretion[16];
        int queued, readIndex;
        float recognitionIn, secretionGate;
        readonly float maximumRadius, contactGap, coreRangeMultiplier;

        public EmergenceSimulation(EmergenceProfile settings, Vector2 halfSize, int seed)
        {
            if (!settings || settings.ConfigurationError() != null) throw new ArgumentException(settings ? settings.ConfigurationError() : "Missing ecology profile.");
            profile = settings; HalfSize = new Vector2(Mathf.Max(2, halfSize.x), Mathf.Max(2, halfSize.y));
            random = new System.Random(seed); bodyParticles = settings.BodyParticleCount;
            nutrientEnd = bodyParticles + settings.species[EmergenceProfile.NutrientSpecies].count;
            maximumRadius = settings.MaximumRadius; contactGap = settings.particleGap; coreRangeMultiplier = settings.coreRangeMultiplier;
            Particles = new Particle[settings.ParticleCount];
            int n = Particles.Length;
            next = new int[n]; parents = new int[n]; sizes = new int[n]; motors = new int[n]; catalysts = new int[n]; structures = new int[n];
            neighbours = new int[n];
            groupReserve = new float[n]; groupWaste = new float[n]; viable = new bool[n];
            edible = new bool[n];
            forces = new Vector2[n]; centers = new Vector2[n]; velocities = new Vector2[n];
            cellSize = Mathf.Max((2 * maximumRadius + contactGap) * coreRangeMultiplier, Mathf.Max(settings.interactionRadius, Mathf.Max(settings.connectionRadius, settings.feedingRadius)));
            columns = Mathf.CeilToInt(HalfSize.x * 2 / cellSize) + 1; rows = Mathf.CeilToInt(HalfSize.y * 2 / cellSize) + 1;
            heads = new int[columns * rows];
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            int type = 0, typeEnd = settings.species[0].count;
            for (int i = 0; i < n; i++)
            {
                while (i >= typeEnd && type < EmergenceProfile.SpeciesCount - 1) typeEnd += settings.species[++type].count;
                var s = settings.species[type];
                var p = new Particle { species = type, radius = s.diameter * .5f * Range(1 - s.sizeVariation, 1 + s.sizeVariation), velocity = Disk() * settings.initialSpeed, visualScale = 1, feedingTarget = -1 };
                if (!TryFindFree(p.radius, settings.initialCoverage, 4096, out p.position))
                    throw new ArgumentException("Particle sizes/counts do not fit the starting area without overlap. Reduce population or diameter, or increase coverage.");
                Particles[i] = p;
                AddToGrid(i);
            }
            BuildGrid(); Recognize();
        }
        float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        Vector2 Disk() { float a = Range(0, Mathf.PI * 2); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Sqrt(Range(0, 1)); }
        int Cell(Vector2 p) => Mathf.Clamp((int)((p.y + HalfSize.y) / cellSize), 0, rows - 1) * columns + Mathf.Clamp((int)((p.x + HalfSize.x) / cellSize), 0, columns - 1);
        void BuildGrid()
        {
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            for (int i = 0; i < Particles.Length; i++)
            {
                next[i] = -1; if (Particles[i].Alive) AddToGrid(i);
            }
        }
        int Root(int i) { while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; } return i; }
        void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) parents[b] = a; }
        void Recognize()
        {
            Array.Clear(sizes, 0, sizes.Length); Array.Clear(motors, 0, motors.Length); Array.Clear(catalysts, 0, catalysts.Length);
            Array.Clear(structures, 0, structures.Length); Array.Clear(viable, 0, viable.Length);
            for (int i = 0; i < bodyParticles; i++) parents[i] = i;
            Array.Clear(neighbours, 0, neighbours.Length);
            // Count local support first; only supported particles can connect into a body.
            for (int i = 0; i < bodyParticles; i++)
            {
                CellRange(Particles[i].position, profile.connectionRadius, out int x0, out int x1, out int y0, out int y1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    for (int j = heads[y * columns + x]; j >= 0; j = next[j])
                        if (j > i && j < bodyParticles && Connected(i, j)) { neighbours[i]++; neighbours[j]++; }
            }
            for (int i = 0; i < bodyParticles; i++)
            {
                if (neighbours[i] < profile.minimumLocalNeighbours) continue;
                int c = Cell(Particles[i].position), x = c % columns, y = c / columns;
                for (int yy = Mathf.Max(0, y - 1); yy <= Mathf.Min(rows - 1, y + 1); yy++)
                    for (int xx = Mathf.Max(0, x - 1); xx <= Mathf.Min(columns - 1, x + 1); xx++)
                        for (int j = heads[yy * columns + xx]; j >= 0; j = next[j])
                            if (j > i && j < bodyParticles && neighbours[j] >= profile.minimumLocalNeighbours && Connected(i, j)) Join(i, j);
            }
            for (int i = 0; i < bodyParticles; i++)
            { int r = Root(i); sizes[r]++; if (Particles[i].species == 0) structures[r]++; else if (Particles[i].species == 1) motors[r]++; else catalysts[r]++; }
            BodyCount = 0;
            for (int i = 0; i < bodyParticles; i++)
            {
                viable[i] = sizes[i] >= profile.minimumBody && structures[i] >= sizes[i] * profile.minimumStructureFraction && motors[i] >= sizes[i] * profile.minimumMotorFraction && catalysts[i] > 0;
                if (viable[i]) BodyCount++;
            }
        }
        bool Connected(int i, int j)
        {
            float reach = Mathf.Min(profile.connectionRadius, (Particles[i].radius + Particles[j].radius) * profile.connectionSizeMultiplier);
            return (Particles[i].position - Particles[j].position).sqrMagnitude < reach * reach;
        }

        public void Step(float dt, bool allowSecretion = true)
        {
            if (dt <= 0 || dt > .05f) throw new ArgumentOutOfRangeException(nameof(dt), "Use fixed substeps of at most 50 ms.");
            Age += dt; secretionGate = Mathf.Max(0, secretionGate - dt); PairChecks = 0;
            for (int i = bodyParticles; i < nutrientEnd; i++) edible[i] = Particles[i].Alive && Particles[i].feedingPhase == 0;
            AdvanceNutrientTransitions(dt);
            BuildGrid();
            for (int i = bodyParticles; i < nutrientEnd; i++)
            {
                var p = Particles[i]; if (p.Alive || p.feedingPhase != 0) continue;
                p.returnIn -= dt;
                if (p.Alive)
                {
                    // Uniform external nutrient supply; delay a return if no nonoverlapping space is found.
                    if (TryFindFree(p.radius, 1, 48, out var point))
                    { p.position = point; p.velocity = Vector2.zero; p.returnIn = 0; p.feedingPhase = 3; p.transition = p.visualScale = 0; p.feedingTarget = -1; Replenished++; Particles[i] = p; AddToGrid(i); }
                    else p.returnIn = dt;
                }
                Particles[i] = p;
            }
            recognitionIn -= dt;
            if (recognitionIn <= 0) { Recognize(); recognitionIn = profile.recognitionInterval; }
            Array.Clear(groupReserve, 0, groupReserve.Length); Array.Clear(groupWaste, 0, groupWaste.Length);
            Array.Clear(centers, 0, centers.Length); Array.Clear(velocities, 0, velocities.Length);
            for (int i = 0; i < bodyParticles; i++)
            {
                var p = Particles[i]; int r = Root(i);
                p.disruption = Mathf.Max(0, p.disruption - dt); p.cooldown = Mathf.Max(0, p.cooldown - dt);
                p.maturity = viable[r] && p.disruption <= 0 ? Mathf.Min(profile.maturationSeconds, p.maturity + dt) : 0;
                if (p.species == 2 && p.maturity >= profile.maturationSeconds)
                { float processed = Mathf.Min(p.reserve, profile.processingPerSecond * dt); p.reserve -= processed; p.waste += processed; }
                groupReserve[r] += p.reserve; groupWaste[r] += p.waste; centers[r] += p.position; velocities[r] += p.velocity; Particles[i] = p;
            }
            float radius = profile.interactionRadius, radius2 = radius * radius, feed2 = profile.feedingRadius * profile.feedingRadius;
            float coreReach = (2 * maximumRadius + contactGap) * coreRangeMultiplier;
            float queryRadius2 = Mathf.Max(radius2, Mathf.Max(coreReach * coreReach, feed2));
            for (int i = 0; i < Particles.Length; i++)
            {
                var p = Particles[i]; forces[i] = Vector2.zero; if (!p.Alive) continue;
                var species = profile.species[p.species];
                Vector2 noise = Disk() * profile.agitation * species.agitationMultiplier;
                float forceX = noise.x, forceY = noise.y;
                int root = p.species == 1 ? Root(i) : 0;
                float powered = p.species == 1 ? Mathf.Lerp(profile.unfedMotorResponse, 1, Mathf.Clamp01(groupReserve[root] / Mathf.Max(1, catalysts[root] * profile.motorFuelPerCatalyst))) : 1;
                float response = profile.force * species.forceMultiplier * powered;
                int cell = Cell(p.position), cx = cell % columns, cy = cell / columns;
                int y0 = Mathf.Max(0, cy - 1), y1 = Mathf.Min(rows - 1, cy + 1), x0 = Mathf.Max(0, cx - 1), x1 = Mathf.Min(columns - 1, cx + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        for (int j = heads[y * columns + x]; j >= 0; j = next[j])
                        {
                            if (i == j || !Particles[j].Alive) continue;
                            var q = Particles[j]; float dx = q.position.x - p.position.x, dy = q.position.y - p.position.y;
                            float d2 = dx * dx + dy * dy;
                            if (d2 >= queryRadius2) continue;
                            float coreRange = (p.radius + q.radius + contactGap) * coreRangeMultiplier;
                            if (d2 >= radius2 && d2 >= coreRange * coreRange && d2 >= feed2) continue; PairChecks++;
                            if (p.species == 2 && q.species == 3 && edible[j] && q.feedingPhase == 0 && d2 < feed2 && p.maturity >= profile.maturationSeconds && p.reserve + 1 <= profile.reservePerCatalyst)
                            {
                                q.returnIn = profile.nutrientReturnSeconds; q.velocity = Vector2.zero;
                                q.feedingPhase = 1; q.feedingTarget = i; q.feedingStart = q.position; q.transition = 0;
                                Particles[j] = q; p.reserve++; Consumed++; continue;
                            }
                            float d = Mathf.Sqrt(Mathf.Max(d2, .000001f));
                            if (d2 < .000001f) { dx = i < j ? .001f : -.001f; dy = 0; }
                            float f = 0;
                            if (d < profile.separation) f = -profile.repulsion * (1 - d / profile.separation);
                            else if (d < radius)
                            {
                                float band = 1 - Mathf.Abs(2 * (d - profile.separation) / (radius - profile.separation) - 1);
                                f = profile.attraction[p.species * EmergenceProfile.SpeciesCount + q.species] * response * band;
                            }
                            float inverseDistance = 1 / d;
                            if (d < coreRange)
                            {
                                float compression = 1 - d / coreRange;
                                float closingSpeed = Mathf.Max(0, ((p.velocity.x - q.velocity.x) * dx + (p.velocity.y - q.velocity.y) * dy) * inverseDistance);
                                f -= profile.coreRepulsion * compression * compression + profile.coreDamping * closingSpeed * compression;
                            }
                            float acceleration = f * inverseDistance;
                            forceX += dx * acceleration; forceY += dy * acceleration;
                        }
                Particles[i] = p; forces[i] = Vector2.ClampMagnitude(new Vector2(forceX, forceY), profile.maximumForce);
            }
            // All forces use the same positions. Integrate once; spacing is entirely force-based.
            // The grid is rebuilt at the next tick, instead of maintained during every move.
            for (int i = 0; i < Particles.Length; i++)
            {
                var p = Particles[i]; if (!p.Alive) continue;
                var s = profile.species[p.species];
                float damp = Mathf.Exp(-profile.drag * s.dragMultiplier * dt);
                p.velocity = Vector2.ClampMagnitude((p.velocity + forces[i] * dt) * damp, profile.speedLimit * s.speedMultiplier);
                MoveWithinBounds(ref p, dt); Particles[i] = p;
            }
            ProductiveBodies = 0;
            for (int r = 0; r < bodyParticles; r++)
            {
                if (!viable[r]) continue;
                bool mature = false, cooling = false;
                for (int i = 0; i < bodyParticles; i++) if (Root(i) == r && Particles[i].species == 2)
                { mature |= Particles[i].maturity >= profile.maturationSeconds; cooling |= Particles[i].cooldown > 0 || Particles[i].disruption > 0; }
                if (mature && groupReserve[r] > 0) ProductiveBodies++;
                if (!allowSecretion || !mature || cooling || secretionGate > 0 || queued == pending.Length || groupWaste[r] < profile.materialPerNugget) continue;
                pending[(readIndex + queued++) % pending.Length] = new Secretion { position = centers[r] / sizes[r], velocity = velocities[r] / sizes[r] };
                float fraction = profile.materialPerNugget / groupWaste[r];
                for (int i = 0; i < bodyParticles; i++) if (Root(i) == r)
                { var p = Particles[i]; p.waste *= 1 - fraction; p.cooldown = profile.secretionInterval; Particles[i] = p; }
                Secreted++; ExportedMaterial += profile.materialPerNugget; secretionGate = profile.fieldSecretionInterval;
            }
        }
        public bool TryDequeue(out Secretion value)
        { if (queued == 0) { value = default; return false; } value = pending[readIndex]; readIndex = (readIndex + 1) % pending.Length; queued--; return true; }

        public int Disturb(Vector2 origin, float radius, float impulse, float disruption, Vector2 drift)
        {
            if (radius <= 0) return 0;
            int affected = 0; float r2 = radius * radius;
            for (int i = 0; i < Particles.Length; i++)
            {
                var p = Particles[i]; if (!p.Alive) continue; Vector2 d = p.position - origin; float ds = d.sqrMagnitude;
                if (ds >= r2) continue;
                float weight = 1 - Mathf.Sqrt(ds) / radius;
                Vector2 direction = ds > .000001f ? d.normalized : Vector2.right;
                p.velocity = Vector2.ClampMagnitude(p.velocity + (direction * impulse + drift) * weight, profile.speedLimit * 2);
                if (disruption > 0 && i < bodyParticles) { p.disruption = Mathf.Max(p.disruption, disruption); p.maturity = 0; }
                Particles[i] = p; affected++;
            }
            if (disruption > 0 && affected > 0) Disruptions++;
            return affected;
        }

        public void DisturbParticle(int index, Vector2 impulse, float disruption)
        {
            var p = Particles[index]; if (!p.Alive) return;
            p.velocity = Vector2.ClampMagnitude(p.velocity + impulse, profile.speedLimit * 2);
            if (index < bodyParticles && disruption > 0) { p.disruption = Mathf.Max(p.disruption, disruption); p.maturity = 0; Disruptions++; }
            Particles[index] = p;
        }
    }
}
