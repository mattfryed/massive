using System;
using UnityEngine;

namespace Massive.Emergence
{
    public interface IEmergenceSimulation : IDisposable
    {
        int Count { get; }
        Vector2 HalfSize { get; }
        float Age { get; }
        int BodyCount { get; }
        int ProductiveBodies { get; }
        int Consumed { get; }
        int Replenished { get; }
        int Secreted { get; }
        int Disruptions { get; }
        float StoredMaterial { get; }
        float ExportedMaterial { get; }
        int PendingCount { get; }
        int EmissionSlots { get; set; }
        EmergenceBud[] RewardBuds { get; }
        void CancelRewardBuds();
        // Explicit diagnostics only on the GPU backend: this property synchronizes with the GPU.
        EmergenceSimulation.Particle[] Particles { get; }
        void Step(float dt, bool allowSecretion = true);
        bool TryDequeue(out EmergenceSimulation.Secretion value);
    }
}
