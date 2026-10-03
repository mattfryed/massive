using UnityEngine;
using Massive.Demonstrations;

namespace Massive.Orbital
{
    /// <summary>Presentation-only loop: no player registration, collisions, pickups or scoring.</summary>
    [DisallowMultipleComponent]
    public sealed class OrbitalInstructionsDemo : MonoBehaviour
    {
        public OrbitalCloudVisual cloud;
        public OrbitalImpactVisual impacts;
        public Transform playerDisplay;
        public PlayerVisualController playerVisual;
        public Transform nuggletDisplay;
        public PlayerDemoView viewport;
        public Transform panelFrame;
        public Vector2 viewportSize = new Vector2(7.37f, 3.9f);
        [Min(3.5f)] public float loopSeconds = 4.2f;

        private static readonly Vector3 KickDirection = new Vector3(1f, 0f, -.12f).normalized;
        private Camera previewCamera;
        private Vector3 playerHome, nuggletScale, contactLocal;
        private float age;
        private bool struck;
        public int DemonstratedHits { get; private set; }

        private void Awake()
        {
            if (playerDisplay) playerHome = playerDisplay.localPosition;
            if (nuggletDisplay) nuggletScale = nuggletDisplay.localScale;
            if (viewport) previewCamera = viewport.GetComponent<Camera>();
        }

        private void OnEnable()
        {
            age = 0f; struck = false;
            if (playerDisplay) playerDisplay.localPosition = playerHome;
            if (nuggletDisplay) nuggletDisplay.gameObject.SetActive(false);
            if (impacts) impacts.enabled = true;
            if (previewCamera) previewCamera.enabled = false;
        }

        private void Update()
        {
            if (!cloud || !impacts || !playerDisplay || !playerVisual) return;
            float dt = Time.unscaledDeltaTime;
            age += dt;
            if (age >= Mathf.Max(3.5f, loopSeconds))
            {
                age %= Mathf.Max(3.5f, loopSeconds);
                struck = false;
            }

            Vector3 previous = playerDisplay.position;
            float sinceHit = age - 1.1f;
            float recoil = sinceHit < 0f ? 0f : 1f - Mathf.Exp(-6f * sinceHit);
            float returnHome = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.45f, 3.45f, age));
            playerDisplay.localPosition = playerHome + KickDirection * (1.15f * recoil * (1f - returnHome));
            Vector3 velocity = dt > 0f ? (playerDisplay.position - previous) / dt : Vector3.zero;
            playerVisual.velocityWS = new Vector2(velocity.x, velocity.z);
            playerVisual.SetAimDirection(transform.TransformDirection(KickDirection));

            if (!struck && sinceHit >= 0f)
            {
                struck = true; DemonstratedHits++;
                contactLocal = playerHome - KickDirection * .5f;
                Vector3 contact = transform.TransformPoint(contactLocal);
                impacts.Show(cloud.transform.position, contact, transform.TransformDirection(KickDirection), .8f);
                playerVisual.OnHit(.45f, contact);
                if (nuggletDisplay) nuggletDisplay.gameObject.SetActive(true);
            }
            if (nuggletDisplay && nuggletDisplay.gameObject.activeSelf)
            {
                float size = Mathf.SmoothStep(0f, 1f, sinceHit / .18f) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.05f, 1.4f, sinceHit)));
                nuggletDisplay.localScale = nuggletScale * size;
                nuggletDisplay.localPosition = contactLocal + new Vector3(.2f, 0f, -.65f) * (1f - Mathf.Exp(-2f * sinceHit));
                if (sinceHit >= 1.4f) nuggletDisplay.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (!viewport || !panelFrame || !previewCamera) return;
            if (!viewport.screenCamera) viewport.screenCamera = Camera.main;
            if (!viewport.screenCamera) return;
            // The remote, full-size stage is rendered only inside the panel's camera viewport.
            // Derive its screen rectangle from the frame so resizing retains the crop and border.
            Vector3 half = new Vector3(viewportSize.x, viewportSize.y, 0f) * .5f;
            viewport.lowerLeft = panelFrame.TransformPoint(-half);
            viewport.upperRight = panelFrame.TransformPoint(half);
            viewport.RefreshRect();
            previewCamera.depth = viewport.screenCamera.depth + 1f;
            previewCamera.enabled = true;
        }

        private void OnDisable()
        {
            if (playerDisplay) playerDisplay.localPosition = playerHome;
            if (playerVisual) playerVisual.velocityWS = Vector2.zero;
            if (nuggletDisplay) nuggletDisplay.gameObject.SetActive(false);
            if (impacts) impacts.enabled = false;
            if (previewCamera) previewCamera.enabled = false;
        }
    }
}
