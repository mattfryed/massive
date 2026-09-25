using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityAmplifierAdapter
    {
        private Transform portalDeformation, portalOriginalParent;
        private int portalOriginalSibling;
        private Vector2 portalVisualDirection = Vector2.right;
        private float portalVisualScale = 1f, portalVisualStretch = 1f;
        private bool portalVisualActive;
        public bool HasPortalVisual => portalVisualActive;
        public float PortalVisualScale => portalVisualScale;
        public float PortalVisualStretch => portalVisualStretch;

        public void SetPortalVisual(Vector2 chartDirection, float uniformScale, float stretch)
        {
            if (!IsRenderingOnSurface) return;
            if (portalDeformation == null)
            {
                // Deform above the lifecycle-owned root, never multiplicatively
                // edit its scale. This also preserves the independently breathing
                // shell and inner core, including a lifecycle interruption.
                portalOriginalParent = visualRoot.parent;
                portalOriginalSibling = visualRoot.GetSiblingIndex();
                var holder = new GameObject("Portal Deformation (runtime)");
                holder.hideFlags = HideFlags.DontSave;
                portalDeformation = holder.transform;
                portalDeformation.SetParent(transform, false);
                visualRoot.SetParent(portalDeformation, false);
                visualRoot.localPosition = Vector3.zero;
            }
            portalVisualDirection = Finite(chartDirection.x) && Finite(chartDirection.y) && chartDirection.sqrMagnitude > .00001f
                ? chartDirection.normalized : Vector2.right;
            portalVisualScale = Finite(uniformScale) ? Mathf.Clamp(uniformScale, .001f, 4f) : 1f;
            portalVisualStretch = Finite(stretch) ? Mathf.Clamp(stretch, 1f, 8f) : 1f;
            portalVisualActive = true;
            RefreshPresentation();
        }

        private void ApplyPortalPose(Vector3 position, Quaternion frame, Quaternion originalRotation)
        {
            // Local X is the stretch axis. The child's compensating rotation keeps
            // its resting orientation; rotating the transit direction never spins
            // the colorful texture or changes authored lifecycle values.
            Quaternion direction = Quaternion.Euler(0f,
                -Mathf.Atan2(portalVisualDirection.y, portalVisualDirection.x) * Mathf.Rad2Deg, 0f);
            portalDeformation.SetPositionAndRotation(position, surface.transform.rotation * frame * direction);
            float cross = portalVisualScale / Mathf.Sqrt(portalVisualStretch);
            portalDeformation.localScale = new Vector3(portalVisualScale * portalVisualStretch, cross, cross);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.Inverse(portalDeformation.rotation) * originalRotation;
        }

        public void ClearPortalVisual()
        {
            portalVisualActive = false;
            portalVisualScale = portalVisualStretch = 1f;
            if (portalDeformation == null) return;
            if (visualRoot != null && visualRoot.parent == portalDeformation)
            {
                visualRoot.SetParent(portalOriginalParent, false);
                visualRoot.SetSiblingIndex(portalOriginalSibling);
                visualRoot.localPosition = originalVisualPosition;
                visualRoot.localRotation = originalVisualRotation;
            }
            var holder = portalDeformation.gameObject;
            portalDeformation = null;
            if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder);
            RefreshPresentation();
        }
    }
}
