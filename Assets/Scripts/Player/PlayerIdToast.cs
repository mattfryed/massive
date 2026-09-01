using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Massive.Players.UI
{
    [DisallowMultipleComponent]
    public class PlayerIdToast : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform scaleRoot;     // scales in/out (ToastRoot)
        [SerializeField] private TMP_Text label;          // "P1", "P2"...
        [SerializeField] private Image panelImage;        // optional background panel

        [Header("Follow")]
        [SerializeField] private bool followTarget = true;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 0.25f, 0f);
        private Transform _target;

        [Header("World-Space Canvas")]
        [SerializeField] private bool autoSetupWorldCanvas = true;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private float worldScale = 0.012f;

        [SerializeField] private bool overrideSorting = true;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 5200;
        [SerializeField] private float dynamicPixelsPerUnit = 10f;

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

        public void SetStyle(Color panel, Color text)
        {
            if (panelImage) panelImage.color = panel;
            if (label) label.color = text;
        }

        public void Play(
            Transform target,
            string text,
            float introSeconds,
            float totalSeconds,
            float outroSeconds,
            Camera camOverride = null)
        {
            _target = target;
            _cam = camOverride ? camOverride : (Camera.main ? Camera.main : FindFirstCamera());

            if (autoSetupWorldCanvas)
                EnsureWorldCanvas();

            if (label)
            {
                label.text = text;
                label.ForceMeshUpdate(true, true);
            }

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(introSeconds, totalSeconds, outroSeconds));
        }

        private void LateUpdate()
        {
            if (followTarget && _target)
                transform.position = _target.position + followOffset;

            if (!billboardToCamera) return;

            var cam = _cam ? _cam : Camera.main;
            if (!cam) return;

            if (cam.orthographic)
            {
                transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                return;
            }

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

            yield return Scale(hiddenScale, shownScale, introSeconds, introEase);

            if (holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(holdSeconds);

            yield return Scale(shownScale, hiddenScale, outroSeconds, outroEase);

            Destroy(gameObject);
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

            // don't block gameplay raycasts
            var gr = rootGO.GetComponent<GraphicRaycaster>();
            if (gr) gr.enabled = false;
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

        private static Camera FindFirstCamera()
        {
#if UNITY_2023_1_OR_NEWER
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam) return cam;
#endif
            var cams = Camera.allCameras;
            return (cams != null && cams.Length > 0) ? cams[0] : null;
        }

            public void SetFollowOffset(Vector3 offsetWS)
        {
            followOffset = offsetWS;
        }
    }


}
