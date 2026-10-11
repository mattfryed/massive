using Massive.Enemies;
using Massive.Levels;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Adapts the folded surface to shared placement and exterior masking.
    /// The disabled grids carry dimensions only; SingularityGridRenderer remains the renderer.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-250)]
    public sealed class SingularityArenaDomains : MonoBehaviour
    {
        public SingularitySurface surface;
        public VectorGridGPU frontDomain;
        public VectorGridGPU projectedDomain;
        public EnemyArenaLayout enemies;

        void OnEnable() => Refresh();
        void Update() => Refresh();
        public void Refresh()
        {
            if (!surface) return;
            Configure(frontDomain, new Vector2(surface.Width, surface.FrontHeight));
            Configure(projectedDomain, new Vector2(surface.Width, surface.ProjectedHeight));
            if (enemies) enemies.gameplayHeight = surface.transform.position.y;
        }
        void Configure(VectorGridGPU domain, Vector2 size)
        {
            if (!domain) return;
            domain.transform.SetPositionAndRotation(surface.transform.position, surface.transform.rotation * Quaternion.Euler(90, 0, 0));
            domain.size = size;
            domain.GetComponent<ArenaBoundsFromVectorGrid>().RefreshNow(false);
        }
    }
}
