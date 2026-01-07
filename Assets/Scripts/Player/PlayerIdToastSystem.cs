using System.Collections;
using UnityEngine;

namespace Massive.Players.UI
{
    [DisallowMultipleComponent]
    public class PlayerIdToastSystem : MonoBehaviour
    {
        public static PlayerIdToastSystem Instance { get; private set; }

        [Header("Prefab")]
        [SerializeField] private PlayerIdToast toastPrefab;

        [Header("Placement")]
        [SerializeField] private float worldYLift = 0.25f;
        [SerializeField] private float screenUpOffsetWorld = 0.45f;
        [SerializeField] private Transform parent; // optional: GameplayObjects root

        [Header("Timings")]
        [SerializeField] private float introSeconds = 0.14f;
        [SerializeField] private float totalSeconds = 1.25f;
        [SerializeField] private float outroSeconds = 0.14f;

        [Header("Camera")]
        [SerializeField] private Camera cameraOverride;

        [Header("Team Styling (optional)")]
        [SerializeField] private bool styleByTeam = true;
        [SerializeField] private Color team1Panel = Color.black;
        [SerializeField] private Color team1Text  = Color.white;
        [SerializeField] private Color team2Panel = Color.white;
        [SerializeField] private Color team2Text  = Color.black;

        [Header("Prewarm (optional)")]
        [SerializeField] private bool prewarmOnStart = true;
        [SerializeField] private string prewarmText = "P4";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate PlayerIdToastSystem on '{name}'. Destroying duplicate component.", this);
                Destroy(this); // NOT Destroy(gameObject)
                return;
            }

            Instance = this;
        }


        private void Start()
        {
            if (prewarmOnStart)
                StartCoroutine(PrewarmRoutine());
        }

        IEnumerator PrewarmRoutine()
        {
            if (!toastPrefab) yield break;

            Camera cam = cameraOverride ? cameraOverride : Camera.main;
            Vector3 far = new Vector3(99999f, 99999f, 99999f);

            var toast = Instantiate(toastPrefab, far, Quaternion.identity, parent);
            toast.Play(null, prewarmText, 0f, 0.01f, 0f, cam);

            yield return null;

            if (toast) Destroy(toast.gameObject);
        }

        public void ShowForPlayer(PlayerControllerScript player, float overrideTotalSeconds = -1f)
        {
            if (!toastPrefab || !player) return;

            Camera cam = cameraOverride ? cameraOverride : Camera.main;

            // Display 1-based player label even though Rewired IDs are 0-based.
            string text = $"Player {player.playerID + 1}";

            Vector3 offset = Vector3.up * worldYLift;

            if (cam)
            {
                Vector3 up = cam.transform.up;
                up.y = 0f;
                if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
                up.Normalize();

                offset += up * screenUpOffsetWorld;
            }

            Vector3 pos = player.transform.position + offset;

            var toast = Instantiate(toastPrefab, pos, Quaternion.identity, parent);

            // IMPORTANT: make follow use the SAME offset so LateUpdate doesn't wipe it out
            toast.SetFollowOffset(offset);

            float t = (overrideTotalSeconds > 0f) ? overrideTotalSeconds : totalSeconds;
            toast.Play(player.transform, text, introSeconds, t, outroSeconds, cam);

        }

        private void OnDestroy()
{
    if (Instance == this) Instance = null;
}

    }
}
