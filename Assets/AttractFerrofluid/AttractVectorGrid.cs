using UnityEngine;

namespace Massive.AttractStudy
{
    /// <summary>Presentation-only adaptation of the gameplay grid for ATTRACT's animated backdrop.</summary>
    [DisallowMultipleComponent]
    public sealed class AttractVectorGrid : MonoBehaviour
    {
        public VectorGridGPU grid;
        public Camera viewCamera;
        public Transform attractor;
        [Min(0f)] public float attractionRadius = 20f;
        [Min(0f)] public float attractionStrength = 16f;

        void OnEnable()
        {
            if (grid == null) grid = GetComponent<VectorGridGPU>();
            if (viewCamera == null) viewCamera = Camera.main;
            EnsureCoverage();
        }

        void LateUpdate()
        {
            EnsureCoverage();
            if (grid == null || !grid.isActiveAndEnabled || attractor == null) return;
            float scale = Mathf.Max(.0001f, grid.transform.lossyScale.x);
            grid.AddForce(VectorGridGPU.MakeRadial(
                grid.transform.InverseTransformPoint(attractor.position),
                attractionRadius / scale, attractionStrength / scale));
        }

        /// <summary>
        /// Extend the lattice, without changing cell spacing, when the camera requires it.
        /// Overscan keeps the boundary outside the view while the original animation tilts/rotates.
        /// Grow in chunks and never shrink during a run, avoiding continuous buffer rebuilds.
        /// </summary>
        public void EnsureCoverage()
        {
            if (grid == null || viewCamera == null) return;
            grid.SetPresentationIgnoresBounds(true);
            var plane = new Plane(grid.transform.forward, grid.transform.position);
            float needed = 0f;
            for (int i = 0; i < 4; i++)
            {
                var ray = viewCamera.ViewportPointToRay(new Vector3(i & 1, (i >> 1) & 1, 0));
                if (!plane.Raycast(ray, out float distance)) continue;
                var local = grid.transform.InverseTransformPoint(ray.GetPoint(distance));
                needed = Mathf.Max(needed, Mathf.Abs(local.x), Mathf.Abs(local.y));
            }
            // Eight world units of reserve also cover the displacement and a frame of rotation.
            float halfExtent = needed + 8f / Mathf.Max(.0001f, grid.transform.lossyScale.x);
            float side = Mathf.Ceil(2f * halfExtent * grid.MinimumPresentationScale / 8f) * 8f;
            // The authored grid is already oversized. A changed display may require more.
            if (side > Mathf.Min(grid.size.x, grid.size.y))
                grid.size = Vector2.one * side;
        }
    }
}
