using UnityEngine;

namespace Massive.Orbital
{
    /// <summary>Direct editor launches use the same level identity as launches through the carousel.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class OrbitalLevelContext : MonoBehaviour
    {
        public LevelDefinition level;
        private void Awake()
        {
            GameFlowContext.EnsureExists();
            if (level != null) GameFlowContext.Instance.SelectLevel(level);
        }
    }
}
