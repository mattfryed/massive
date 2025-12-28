using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickupToast : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform scaleRoot;            // what scales in/out (typically ToastRoot)
        [SerializeField] private TMP_Text label;                 // TMP label to set
        [SerializeField] private TMPTextTransition textAnim;     // your existing text animator
        [SerializeField] private RectTransform layoutRoot;       // set to Panel rect (or ToastRoot)

        [Header("World-Space Canvas (required for TMP UGUI / Image)")]
        [SerializeField] private bool autoSetupWorldCanvas = true;

        [Tooltip("If empty, we auto-grab/add a Canvas on the scaleRoot object.")]
        [SerializeField] private Canvas canvas;

        [SerializeField] private CanvasScaler canvasScaler;

        [Tooltip("World-space canvases are usually scaled down a lot (0.005–0.03 typical).")]
        [SerializeField] private float worldScale = 0.012f;

        [SerializeField] private bool overrideSorting = true;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 5000;

        [Tooltip("Higher = crisper but heavier. 10 is a good start.")]
        [SerializeField] private float dynamicPixelsPerUnit = 10f;

        [Header("Auto Size (optional code-based sizing)")]
        [Tooltip("If set, we resize this rect to match label preferred size + padding.")]
        [SerializeField] private RectTransform panelRect;

        [SerializeField] private Vector2 panelPadding = new Vector2(24f, 12f);

        [Tooltip("0 = unlimited. Otherwise wraps/clamps preferred width calc.")]
        [SerializeField] private float maxWidth = 0f;

        [Header("Billboard")]
        [SerializeField] private bool billboardToCamera = true;

        [Header("Scale")]
        [SerializeField] private Vector3 hiddenScale = Vector3.zero;
        [SerializeField] private Vector3 shownScale = Vector3.one;

        [Header("Easing")]
        [SerializeField] private AnimationCurve introEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve outroEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Camera _cam;
        private Coroutine _routine;

        private void Awake()
        {
            if (!scaleRoot) scaleRoot = transform;
            ApplyScaleInstant(hiddenScale);
        }

        public void Play(string text, float introSeconds, float totalSeconds, float outroSeconds, Camera camOverride = null)
        {
            _cam = camOverride ? camOverride : (Camera.main ? Camera.main : FindFirstCamera());

            if (autoSetupWorldCanvas)
                EnsureWorldCanvas();

            if (label) label.text = text;
            if (label) label.ForceMeshUpdate(true, true);

            // Optional: code-driven panel size (works even if layout components are misconfigured)
            if (panelRect && label != null)
                AutoSizePanelToLabel();

            Canvas.ForceUpdateCanvases();
            if (!layoutRoot && scaleRoot) layoutRoot = scaleRoot as RectTransform;
            if (layoutRoot) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(introSeconds, totalSeconds, outroSeconds));
        }

        private static Camera FindFirstCamera()
        {
#if UNITY_2023_1_OR_NEWER
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam) return cam;
#endif
            var cams = Camera.allCameras;
            return (cams != null && cams.Length > 0) ? cams[0] : null;
        }

        private void EnsureWorldCanvas()
        {
            var rootGO = (scaleRoot != null) ? scaleRoot.gameObject : gameObject;

            if (!canvas) canvas = rootGO.GetComponent<Canvas>();
            if (!canvas) canvas = rootGO.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;

            if (overrideSorting)
            {
                canvas.overrideSorting = true;
                if (!string.IsNullOrEmpty(sortingLayerName))
                    canvas.sortingLayerName = sortingLayerName;
                canvas.sortingOrder = sortingOrder;
            }

            if (!canvasScaler) canvasScaler = rootGO.GetComponent<CanvasScaler>();
            if (!canvasScaler) canvasScaler = rootGO.AddComponent<CanvasScaler>();

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasScaler.dynamicPixelsPerUnit = Mathf.Max(0.01f, dynamicPixelsPerUnit);

            // We don’t want this world UI to block gameplay raycasts
            var gr = rootGO.GetComponent<GraphicRaycaster>();
            if (gr) gr.enabled = false;
        }

        private void AutoSizePanelToLabel()
        {
            Vector2 pref = (maxWidth > 0f)
                ? label.GetPreferredValues(label.text, maxWidth, 9999f)
                : label.GetPreferredValues(label.text);

            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pref.x + panelPadding.x);
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, pref.y + panelPadding.y);
        }

private void LateUpdate()
{
    if (!billboardToCamera) return;

    var cam = _cam ? _cam : Camera.main;
    if (!cam) return;

    // For ortho cameras: use a constant billboard (no “toCam” tilt).
    if (cam.orthographic)
    {
        // Try this first:
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

        // If it still appears mirrored/upside-down, flip 180 on Y:
        // transform.rotation *= Quaternion.Euler(0f, 180f, 0f);

        // Or flip 180 on Z:
        // transform.rotation *= Quaternion.Euler(0f, 0f, 180f);
        return;
    }

    // Perspective fallback
    Vector3 toCam = cam.transform.position - transform.position;
    if (toCam.sqrMagnitude > 0.0001f)
        transform.rotation = Quaternion.LookRotation(toCam.normalized, cam.transform.up);
}


        private IEnumerator Run(float introSeconds, float totalSeconds, float outroSeconds)
        {
            introSeconds = Mathf.Max(0f, introSeconds);
            outroSeconds = Mathf.Max(0f, outroSeconds);
            totalSeconds = Mathf.Max(0f, totalSeconds);

            float holdSeconds = Mathf.Max(0f, totalSeconds - introSeconds - outroSeconds);

            if (textAnim) textAnim.PlayIn();

            yield return Scale(hiddenScale, shownScale, introSeconds, introEase);

            if (holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(holdSeconds);

            if (textAnim) textAnim.PlayOut();

            yield return Scale(shownScale, hiddenScale, outroSeconds, outroEase);

            Destroy(gameObject);
        }

        private void ApplyScaleInstant(Vector3 factor)
        {
            if (!scaleRoot) return;
            float ws = Mathf.Max(0.0001f, worldScale);
            scaleRoot.localScale = factor * ws;
        }

        private IEnumerator Scale(Vector3 from, Vector3 to, float seconds, AnimationCurve ease)
        {
            if (!scaleRoot) yield break;

            float ws = Mathf.Max(0.0001f, worldScale);
            from *= ws;
            to *= ws;

            if (seconds <= 0f)
            {
                scaleRoot.localScale = to;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                float e = (ease != null) ? ease.Evaluate(u) : u;

                scaleRoot.localScale = Vector3.LerpUnclamped(from, to, e);
                yield return null;
            }

            scaleRoot.localScale = to;
        }
    }
}
