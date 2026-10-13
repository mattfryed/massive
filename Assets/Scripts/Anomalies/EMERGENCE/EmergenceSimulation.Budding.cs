using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceSimulation
    {
        public int EmissionSlots { get; set; } = 32;
        public EmergenceBud[] RewardBuds { get; } = new EmergenceBud[EmergenceBud.Capacity];
        int AttachedBudCount { get { int n = 0; foreach (var b in RewardBuds) if (b.phase == 1 || b.phase == 2 || b.phase == 4) n++; return n; } }
        bool HasBud(int root)
        {
            foreach (var b in RewardBuds) if ((b.phase == 1 || b.phase == 2) && Root((int)b.anchor) == root) return true;
            return false;
        }
        public void CancelRewardBuds()
        {
            for (int i = 0; i < RewardBuds.Length; i++) if (RewardBuds[i].phase == 1)
            { var b = RewardBuds[i]; b.phase = 2; b.cancelFrom = b.Progress; b.retractElapsed = 0; RewardBuds[i] = b; }
        }
        void StartRewardBud(int root)
        {
            if (queued + AttachedBudCount >= Mathf.Min(EmissionSlots, 32)) return;
            int slot = -1, anchor = -1; float outermost = -1;
            for (int j = 0; j < RewardBuds.Length; j++) if (RewardBuds[j].phase == 0) { slot = j; break; }
            if (slot < 0) return;
            Vector2 center = centers[root] / sizes[root];
            for (int i = 0; i < bodyParticles; i++) if (Root(i) == root)
            {
                var p = Particles[i]; if (p.disruption > 0) return;
                float distance = (p.position - center).sqrMagnitude;
                if (p.species == 2 && p.maturity >= profile.maturationSeconds && distance > outermost) { anchor = i; outermost = distance; }
            }
            if (anchor < 0) return;
            var particle = Particles[anchor]; Vector2 outward = particle.position - center;
            if (outward.sqrMagnitude < .000001f) outward = particle.velocity.sqrMagnitude > .000001f ? -particle.velocity : Vector2.right;
            RewardBuds[slot] = new EmergenceBud { serial = RewardBuds[slot].serial + 1, phase = 1, anchor = (uint)anchor, root = (uint)root,
                position = particle.position, velocity = particle.velocity, direction = outward.normalized, duration = profile.budWindupSeconds,
                cost = profile.materialPerNugget, radius = particle.radius };
            secretionGate = profile.fieldSecretionInterval;
        }
        void AdvanceRewardBuds(float dt)
        {
            for (int slot = 0; slot < RewardBuds.Length; slot++)
            {
                var b = RewardBuds[slot]; if (!b.Attached) continue;
                var anchor = Particles[b.anchor]; int root = Root((int)b.anchor);
                b.position = anchor.position; b.velocity = anchor.velocity; b.root = (uint)root;
                if (b.phase == 1)
                {
                    bool valid = viable[root] && anchor.maturity >= profile.maturationSeconds && groupWaste[root] >= b.cost;
                    for (int i = 0; valid && i < bodyParticles; i++) if (Root(i) == root && (Particles[i].disruption > 0 || Particles[i].cooldown > 0)) valid = false;
                    // When two budding bodies merge, only the earliest slot keeps its claim on their waste.
                    for (int i = 0; valid && i < slot; i++) if (RewardBuds[i].phase == 1 && Root((int)RewardBuds[i].anchor) == root) valid = false;
                    if (!valid) { b.phase = 2; b.cancelFrom = b.Progress; b.retractElapsed = 0; }
                    else
                    {
                        b.elapsed = Mathf.Min(b.duration, b.elapsed + dt);
                        if (b.elapsed >= b.duration && queued < pending.Length)
                        {
                            float fraction = b.cost / groupWaste[root];
                            for (int i = 0; i < bodyParticles; i++) if (Root(i) == root)
                            { var p = Particles[i]; p.waste *= 1 - fraction; p.cooldown = profile.secretionInterval; Particles[i] = p; }
                            groupWaste[root] -= b.cost;
                            pending[(readIndex + queued++) % pending.Length] = new Secretion { position = b.position, velocity = b.velocity, budSlot = slot, budSerial = b.serial };
                            Secreted++; ExportedMaterial += b.cost; b.phase = 3;
                        }
                    }
                }
                else { b.retractElapsed = Mathf.Min(profile.budRetractSeconds, b.retractElapsed + dt); if (b.retractElapsed >= profile.budRetractSeconds) b.phase = 4; }
                RewardBuds[slot] = b;
            }
        }
    }
}
