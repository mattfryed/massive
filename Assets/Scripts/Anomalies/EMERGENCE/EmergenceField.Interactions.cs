using Massive.Player;
using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceField
    {
        sealed class PlayerBinding
        {
            public readonly PlayerControllerScript player;
            public readonly Rigidbody body;
            public readonly PlayerAttackController attack;
            public readonly PlayerRepulsorAOE repulsor;
            public readonly Collider sword;
            public readonly bool[] swordHit, pulseHit;
            public AttackStage stage;
            public float time;
            public bool pulse;
            public uint swordSequence, pulseSequence;
            public PlayerBinding(PlayerControllerScript owner, int count)
            {
                player = owner; body = owner.GetComponent<Rigidbody>(); attack = owner.GetComponent<PlayerAttackController>();
                repulsor = owner.GetComponentInChildren<PlayerRepulsorAOE>(true);
                var melee = owner.GetComponentInChildren<PlayerMelee>(true);
                var window = owner.GetComponentInChildren<AttackHitboxWindow>(true);
                sword = melee ? melee.GetComponent<Collider>() : (window ? window.GetComponent<Collider>() : null);
                swordHit = new bool[count]; pulseHit = new bool[count];
            }
        }
        void ApplyPlayerInteractions(float dt)
        {
            if (bindings == null) return;
            if (Simulation is EmergenceGpuSimulation gpu) { ApplyGpuInteractions(gpu, dt); return; }
            var cpu = (EmergenceSimulation)Simulation;
            foreach (var b in bindings)
            {
                if (b == null || !b.player || !b.player.isActiveAndEnabled || b.player.IsPseudoPlayer || b.player.temporarilyEliminated || b.player.IsMatchInputLocked) continue;
                Vector2 origin = PlanePosition(b.player.transform.position);
                float radius = PlayerScaleAdjuster.BodyRadiusOf(b.player) + .28f;
                WakeContacts += cpu.Disturb(origin, radius, profile.wakeStrength * dt, 0, b.body ? PlaneVector(b.body.linearVelocity) * dt * 2 : Vector2.zero);
                if (b.attack && (b.stage != b.attack.CurrentStage || b.attack.TimeInStage < b.time))
                { System.Array.Clear(b.swordHit, 0, b.swordHit.Length); b.stage = b.attack.CurrentStage; }
                b.time = b.attack ? b.attack.TimeInStage : 0;
                bool pulse = b.repulsor && b.repulsor.IsPulseActive;
                if (pulse && !b.pulse) System.Array.Clear(b.pulseHit, 0, b.pulseHit.Length);
                b.pulse = pulse;
                bool sword = b.attack && b.attack.IsAttacking && b.stage != null && b.stage.StageType != AttackStageType.FinisherRepulsor && b.sword && b.sword.enabled && b.sword.gameObject.activeInHierarchy;
                if (!sword && !pulse) continue;
                Bounds swordBounds = sword ? b.sword.bounds : default;
                swordBounds.Expand(profile.MaximumRadius * 2);
                Vector2 pulseOrigin = pulse ? PlanePosition(b.repulsor.OriginWorld) : origin;
                float pulseR2 = pulse ? b.repulsor.RadiusWorld * b.repulsor.RadiusWorld : 0;
                for (int i = 0; i < Simulation.Particles.Length; i++)
                {
                    var p = Simulation.Particles[i]; if (!p.Alive) continue;
                    Vector2 d = p.position - pulseOrigin;
                    if (pulse && !b.pulseHit[i] && d.sqrMagnitude <= pulseR2)
                    { cpu.DisturbParticle(i, d.normalized * profile.repulsorImpulse, profile.disruptedSeconds); b.pulseHit[i] = true; RepulsorContacts++; }
                    if (!sword || b.swordHit[i]) continue;
                    Vector3 world = WorldPosition(p.position); world.y = swordBounds.center.y;
                    if (swordBounds.Contains(world) && (b.sword.ClosestPoint(world) - world).sqrMagnitude <= p.radius * p.radius)
                    { cpu.DisturbParticle(i, PlaneVector(b.attack.CurrentAttackVisualDirectionWS) * profile.attackImpulse, profile.disruptedSeconds); b.swordHit[i] = true; AttackContacts++; }
                }
            }
        }
    }
}
