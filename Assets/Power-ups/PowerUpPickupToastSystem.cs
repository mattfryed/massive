using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickupToastSystem : MonoBehaviour
    {
        public static PowerUpPickupToastSystem Instance { get; private set; }

        [Header("Prefab")]
        [SerializeField] private PowerUpPickupToast toastPrefab;

        [Header("Placement")]
        [Tooltip("Lift in world Y so it doesn't z-fight or get swallowed by floor.")]
        [SerializeField] private float worldYLift = 0.25f;

        [Tooltip("Offset along camera-up projected onto XZ (for top-down this makes it 'above' the icon on screen).")]
        [SerializeField] private float screenUpOffsetWorld = 0.45f;

        [SerializeField] private Transform parent; // optional: e.g. GameplayObjects

        [Header("Timings")]
        [SerializeField] private float introSeconds = 0.16f;
        [SerializeField] private float totalSeconds = 1.10f; // total lifetime from pickup moment
        [SerializeField] private float outroSeconds = 0.16f;

        [Header("Camera")]
        [SerializeField] private Camera cameraOverride;

        [SerializeField] private bool prewarmOnStart = true;
[SerializeField] private string prewarmText = "PARTICLE ACCELERATOR"; // longest expected label

private void Start()
{
    if (prewarmOnStart)
        StartCoroutine(PrewarmRoutine());
}

private System.Collections.IEnumerator PrewarmRoutine()
{
    if (!toastPrefab) yield break;

    Camera cam = cameraOverride ? cameraOverride : Camera.main;

    // Spawn far away so it won't be seen
    Vector3 far = new Vector3(99999f, 99999f, 99999f);
    var toast = Instantiate(toastPrefab, far, Quaternion.identity, parent);

    // Run once to force TMP + layout + materials to initialize
    toast.Play(prewarmText, 0f, 0.01f, 0f, cam);

    // Wait one frame so TMP/layout actually execute
    yield return null;

    if (toast) Destroy(toast.gameObject);
}


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Show(PowerUpDefinition def, Vector3 pickupWorldPos)
        {
            if (!toastPrefab) return;

            Camera cam = cameraOverride ? cameraOverride : Camera.main;

            // text from definition
            string text =
                (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                    ? def.displayName
                    : (def != null ? def.name : "Power Up");

            Vector3 pos = pickupWorldPos + Vector3.up * worldYLift;

            // Screen-up offset (for your top-down camera this prevents it overlapping the icon)
            if (cam)
            {
                Vector3 up = cam.transform.up;
                up.y = 0f;
                if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
                up.Normalize();
                pos += up * screenUpOffsetWorld;
            }

            var toast = Instantiate(toastPrefab, pos, Quaternion.identity, parent);
            toast.Play(text, introSeconds, totalSeconds, outroSeconds, cam);
        }
    }
}
