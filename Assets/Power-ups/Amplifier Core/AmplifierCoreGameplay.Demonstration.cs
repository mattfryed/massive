using UnityEngine;
namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreGameplay
    {
        // Explicit runtime policy; authored gameplay cores retain normal match rules.
        public Transform DemonstrationRoot { get; private set; }
        public void ConfigureDemonstration(Transform scope, float? respawnDelay = null)
        {
            DemonstrationRoot = scope;
            if (scope && respawnDelay.HasValue) respawnDelaySeconds = Mathf.Max(0f, respawnDelay.Value);
        }
    }
}
