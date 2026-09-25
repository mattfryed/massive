using UnityEngine;

namespace Massive.Singularity
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class SingularityCameraFraming : MonoBehaviour
    {
        public SingularitySurface surface;
        [Min(0f)] public float padding = 1.5f;
        [Tooltip("Leave enabled while adjusting the shape; disable for manual camera exploration.")]
        public bool autoFrame = true;
        [Tooltip("Optional minimum half-height for scene HUD/goal clearance. Zero preserves surface-only framing.")]
        [Min(0f)] public float minimumOrthographicSize;
        private Camera view;
        private int revision = -1;
        private float maximumZ;

        private void OnEnable() { view = GetComponent<Camera>(); revision = -1; }
        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (!autoFrame || !surface) return;
            if (!view) view = GetComponent<Camera>();
            if (revision != surface.Revision)
            {
                maximumZ = 0f;
                for (int i = 0; i < 256; i++)
                    maximumZ = Mathf.Max(maximumZ, Mathf.Abs(surface.Evaluate(0f, surface.LoopLength * i / 256f).z));
                revision = surface.Revision;
            }
            float xScale = surface.transform.TransformVector(Vector3.right).magnitude;
            float zScale = surface.transform.TransformVector(Vector3.forward).magnitude;
            view.orthographic = true;
            view.orthographicSize = Mathf.Max(minimumOrthographicSize, Mathf.Max(maximumZ * zScale + padding,
                (surface.Width * .5f * xScale + padding) / Mathf.Max(.1f, view.aspect)));
            transform.position = surface.transform.position + surface.transform.up * 50f;
            transform.rotation = Quaternion.LookRotation(-surface.transform.up, surface.transform.forward);
        }
    }
}
