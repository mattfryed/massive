using Massive.PowerUps;
using UnityEngine;

namespace Massive.Demonstrations
{
    /// <summary>Frames real-size actors in a remote demonstration stage without scaling gameplay.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerDemoView : MonoBehaviour
    {
        public Camera screenCamera;
        public Vector3 lowerLeft;
        public Vector3 upperRight;
        public GameObject captionPrefab;
        public string caption;
        private Camera view;
        private GameObject title;
        private void Awake() { view = GetComponent<Camera>(); }
        private void Start()
        {
            if (!captionPrefab || string.IsNullOrEmpty(caption)) return;
            title = Instantiate(captionPrefab, transform);
            title.name = caption + " — permanent title";
            title.GetComponent<PowerUpPickupToast>()?.ShowPersistent(caption, screenCamera);
            PositionTitle();
        }
        private void OnDestroy() { if (title) Destroy(title); }
        private void LateUpdate() { RefreshRect(); PositionTitle(); }
        public void RefreshRect()
        {
            if (!view) view = GetComponent<Camera>();
            if (!screenCamera) return;
            var a = screenCamera.WorldToViewportPoint(lowerLeft);
            var b = screenCamera.WorldToViewportPoint(upperRight);
            var r = screenCamera.rect;
            view.rect = new Rect(r.x + Mathf.Min(a.x,b.x) * r.width, r.y + Mathf.Min(a.y,b.y) * r.height,
                Mathf.Abs(b.x-a.x) * r.width, Mathf.Abs(b.y-a.y) * r.height);
        }
        private void PositionTitle()
        {
            if (title) title.transform.position = new Vector3((lowerLeft.x+upperRight.x)*.5f,27.2f,lowerLeft.z-.42f);
        }
    }
}
