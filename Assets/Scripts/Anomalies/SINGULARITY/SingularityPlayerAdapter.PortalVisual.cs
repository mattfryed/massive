using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityPlayerAdapter
    {
        private Vector2 portalVisualDirection = Vector2.right;
        private float portalVisualScale = 1f, portalVisualStretch = 1f;
        private bool portalVisualActive;
        private static readonly int PortalCenterId = Shader.PropertyToID("_SingularityPortalCenter");
        private static readonly int PortalShapeId = Shader.PropertyToID("_SingularityPortalShape");

        public bool HasPortalVisual => portalVisualActive;
        public float PortalVisualScale => portalVisualScale;
        public float PortalVisualStretch => portalVisualStretch;

        /// <summary>Presentation only: deform body, nuggets and mapped effects together,
        /// without changing the player's authored transform, mass or collider size.</summary>
        public void SetPortalVisual(Vector2 chartDirection, float uniformScale, float stretch)
        {
            portalVisualDirection = Finite(chartDirection.x) && Finite(chartDirection.y) && chartDirection.sqrMagnitude > .00001f
                ? chartDirection.normalized : Vector2.right;
            portalVisualScale = Finite(uniformScale) ? Mathf.Clamp(uniformScale, .001f, 4f) : 1f;
            portalVisualStretch = Finite(stretch) ? Mathf.Clamp(stretch, 1f, 8f) : 1f;
            portalVisualActive = true;
        }

        public void ClearPortalVisual()
        {
            portalVisualActive = false;
            portalVisualScale = portalVisualStretch = 1f;
            portalVisualDirection = Vector2.right;
        }

        private Vector3 DeformPortalPoint(Vector3 chartLocal)
        {
            if (!portalVisualActive || surface == null) return chartLocal;
            Vector3 center = surface.transform.InverseTransformPoint(transform.position);
            Vector2 offset = new Vector2(chartLocal.x - center.x, surface.LoopDelta(center.z, chartLocal.z));
            offset = DeformPortalOffset(offset, portalVisualDirection, portalVisualScale, portalVisualStretch);
            return new Vector3(center.x + offset.x, chartLocal.y, center.z + offset.y);
        }

        public static Vector2 DeformPortalOffset(Vector2 offset, Vector2 direction, float scale, float stretch)
        {
            Vector2 along = direction * Vector2.Dot(offset, direction);
            return (along * stretch + (offset - along) / Mathf.Sqrt(stretch)) * scale;
        }

        private void ApplyPortalVisualProperties(MaterialPropertyBlock properties)
        {
            Vector3 center = surface.transform.InverseTransformPoint(transform.position);
            properties.SetVector(PortalCenterId, new Vector4(center.x, center.y, center.z, portalVisualActive ? 1f : 0f));
            properties.SetVector(PortalShapeId, new Vector4(portalVisualDirection.x, portalVisualDirection.y,
                portalVisualScale, portalVisualStretch));
        }
    }
}
