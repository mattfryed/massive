using System.Collections;
using TMPro;
using UnityEngine;

namespace Massive.PowerUps.UI
{
    [DisallowMultipleComponent]
    public class PowerUpToastController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform root;      // the black box (panel) rect
        [SerializeField] private TMP_Text label;          // TMP text
        [Tooltip("Optional: toggle this object off/on to retrigger your existing text animation script.")]
        [SerializeField] private GameObject textAnimatorRoot;

        [Header("Timing")]
        public float introSeconds = 0.18f;
        public float holdSeconds  = 0.90f;
        public float outroSeconds = 0.18f;

        [Header("Scale")]
        public Vector3 scaleHidden = new Vector3(0.001f, 0.001f, 1f);
        public Vector3 scaleShown  = Vector3.one;

        [Header("Easing")]
        public AnimationCurve introEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve outroEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Coroutine _routine;

        private void Reset()
        {
            root = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            if (!root) root = GetComponent<RectTransform>();
            root.localScale = scaleHidden;
        }

        public void Show(string text, float intro, float hold, float outro)
        {
            introSeconds = Mathf.Max(0f, intro);
            holdSeconds  = Mathf.Max(0f, hold);
            outroSeconds = Mathf.Max(0f, outro);

            if (label) label.text = text;

            // retrigger existing TMP animation if it runs on enable
            if (textAnimatorRoot != null)
            {
                textAnimatorRoot.SetActive(false);
                textAnimatorRoot.SetActive(true);
            }
            else
            {
                // fallback: try common method names without hard dependency
                // (does nothing if the method doesn't exist)
                SendMessage("Play", SendMessageOptions.DontRequireReceiver);
                SendMessage("Restart", SendMessageOptions.DontRequireReceiver);
            }

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // Intro
            yield return ScaleRoutine(scaleHidden, scaleShown, introSeconds, introEase);

            // Hold
            if (holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(holdSeconds);

            // Outro
            yield return ScaleRoutine(scaleShown, scaleHidden, outroSeconds, outroEase);

            Destroy(gameObject);
        }

        IEnumerator ScaleRoutine(Vector3 a, Vector3 b, float seconds, AnimationCurve ease)
        {
            if (seconds <= 0f)
            {
                root.localScale = b;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                float e = (ease != null) ? ease.Evaluate(u) : u;
                root.localScale = Vector3.LerpUnclamped(a, b, e);
                yield return null;
            }

            root.localScale = b;
        }
    }
}
