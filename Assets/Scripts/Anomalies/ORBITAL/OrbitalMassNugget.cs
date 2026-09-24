using UnityEngine;

namespace Massive.Orbital
{
    /// <summary>Pooled shared pickup behavior; nugglets override size and reward in their prefab.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class OrbitalMassNugget : MatterNuggetScript
    {
        protected override void FinishDespawn() { gameObject.SetActive(false); }
    }
}
