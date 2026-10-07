using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickupToastSystem : MonoBehaviour
    {
        private static readonly List<PowerUpPickupToastSystem> systems = new();
        public static PowerUpPickupToastSystem Instance => ForScene(SceneManager.GetActiveScene());
        public static PowerUpPickupToastSystem ForScene(Scene scene)
        {
            foreach (var system in systems) if (system && system.gameObject.scene == scene) return system;
            // Also recover when entering Play Mode without a domain/scene reload.
            if (scene.IsValid() && scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects())
                {
                    var system = root.GetComponentInChildren<PowerUpPickupToastSystem>(true);
                    if (!system) continue;
                    systems.Add(system);
                    return system;
                }
            return null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => systems.Clear();

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
    var settings = PowerUpSettings.Current;
    var prefab = settings ? settings.toasts.prefab : toastPrefab;
    if (!prefab || (settings && !settings.toasts.enabled)) yield break;

    Camera cam = cameraOverride ? cameraOverride : Camera.main;

    // Spawn far away so it won't be seen
    Vector3 far = new Vector3(99999f, 99999f, 99999f);
    var toast = Instantiate(prefab, far, Quaternion.identity, parent ? parent : transform);

    // Run once to force TMP + layout + materials to initialize
    toast.Play(prewarmText, 0f, 0.01f, 0f, cam);

    // Wait one frame so TMP/layout actually execute
    yield return null;

    if (toast) Destroy(toast.gameObject);
}


        private void Awake()
        {
            if (!systems.Contains(this)) systems.Add(this);
        }

        private void OnDestroy() => systems.Remove(this);

        public void Show(PowerUpDefinition def, Vector3 pickupWorldPos)
        {
            Show(def, pickupWorldPos, null);
        }

        /// <summary>Use the same toast with an explicit owner for isolated demonstrations.</summary>
        public PowerUpPickupToast Show(PowerUpDefinition def, Vector3 pickupWorldPos, Transform owner)
        {
            var settings = PowerUpSettings.Current;
            var tuning = settings ? settings.toasts : null;
            var prefab = tuning != null ? tuning.prefab : toastPrefab;
            if (!prefab || (tuning != null && !tuning.enabled)) return null;

            Camera cam = cameraOverride ? cameraOverride : Camera.main;

            string title =
                (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                    ? def.displayName
                    : (def != null ? def.name : "Power Up");

            string desc =
                (def != null) ? def.description : "";

            // 2-line label (desc optional)
            string text =
                string.IsNullOrWhiteSpace(desc) || (tuning != null && !tuning.showDescription)
                    ? title
                    : $"{title}\n<size={(tuning != null ? tuning.descriptionPercent : 70):0}%>{desc}</size>";

            Vector3 pos = pickupWorldPos + Vector3.up * (tuning != null ? tuning.worldYLift : worldYLift);

            if (cam)
            {
                Vector3 up = cam.transform.up;
                up.y = 0f;
                if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
                up.Normalize();
                pos += up * (tuning != null ? tuning.screenUpOffset : screenUpOffsetWorld);
            }

            var toast = Instantiate(prefab, pos, Quaternion.identity, owner ? owner : (parent ? parent : transform));
            if (tuning != null) toast.ApplySettings(tuning);
            toast.Play(text, tuning != null ? tuning.introSeconds : introSeconds,
                tuning != null ? tuning.totalSeconds : totalSeconds, tuning != null ? tuning.outroSeconds : outroSeconds, cam);
            return toast;
        }

    }
}
