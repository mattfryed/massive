using System.Collections.Generic;
using Massive.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Enemies
{
    /// <summary>Small, scene-owned, reusable labels. The score service is the only source of displayed amounts.</summary>
    public sealed class EnemyScoreToast : MonoBehaviour
    {
        public TMP_Text label;
        [Min(.1f)] public float lifetime = .8f;
        [Min(0f)] public float riseDistance = .35f;
        private const int Capacity = 24;
        private static readonly List<EnemyScoreToast> pool = new List<EnemyScoreToast>(Capacity);
        private float age, startedAt;
        private Vector3 origin, up;
        private Camera view;
        public long Amount { get; private set; }
        public static int ActiveCount { get { int n = 0; foreach (var t in pool) if (t != null && t.gameObject.activeSelf) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pool.Clear();

        public static EnemyScoreToast Show(EnemyScoreToast prefab, ScoreAwardResult award, Scene scene)
        {
            if (prefab == null || prefab.label == null || !award.accepted || award.finalMilliElectronVolts <= 0) return null;
            EnemyScoreToast toast = null, oldest = null;
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                var t = pool[i];
                if (t == null) { pool.RemoveAt(i); continue; }
                if (!t.gameObject.activeSelf) toast = t;
                if (oldest == null || t.startedAt < oldest.startedAt) oldest = t;
            }
            if (toast == null && pool.Count < Capacity)
            { toast = Instantiate(prefab); pool.Add(toast); }
            if (toast == null) toast = oldest;
            if (toast == null) return null;
            if (toast.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(toast.gameObject, scene);
            toast.lifetime = prefab.lifetime; toast.riseDistance = prefab.riseDistance;
            toast.Amount = award.finalMilliElectronVolts;
            toast.age = 0f; toast.startedAt = Time.unscaledTime; toast.view = Camera.main;
            if (toast.view == null) toast.view = FindFirstObjectByType<Camera>();
            toast.up = toast.view != null ? toast.view.transform.up : Vector3.forward;
            toast.origin = award.worldPosition + Vector3.up * .2f + toast.up * .25f;
            toast.transform.position = toast.origin;
            var display = EnergyScoreFormatter.GetDisplayValue(toast.Amount);
            string amount = EnergyScoreFormatter.FormatValue(toast.Amount, 1, display.fractionalThousandths == 0 ? 0 : 3);
            if (display.fractionalThousandths != 0) amount = amount.TrimEnd('0').TrimEnd('.');
            toast.label.text = "+" + amount + " " + display.unitLabel;
            toast.label.color = Color.white; toast.gameObject.SetActive(true); toast.FaceCamera();
            return toast;
        }
        private void FaceCamera() { if (view != null) transform.rotation = view.transform.rotation; }
        private void LateUpdate()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Mathf.Max(.1f, lifetime));
            transform.position = origin + up * riseDistance * t; FaceCamera();
            if (label != null) label.alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.45f, 1f, t));
            if (t >= 1f) gameObject.SetActive(false);
        }
        private void OnDestroy() => pool.Remove(this);
    }
}
