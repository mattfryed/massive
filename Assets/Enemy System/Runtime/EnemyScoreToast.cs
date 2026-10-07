using System.Collections.Generic;
using Massive.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Enemies
{
    /// <summary>Scene-owned reusable labels for accepted score awards and actual pickup mass restored.</summary>
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
        public float MassRestored { get; private set; }
        public static int ActiveCount { get { int n = 0; foreach (var t in pool) if (t != null && t.gameObject.activeSelf) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pool.Clear();

        public static EnemyScoreToast Show(EnemyScoreToast prefab, ScoreAwardResult award, Scene scene)
        {
            if (prefab == null || prefab.label == null || !award.accepted || award.finalMilliElectronVolts <= 0) return null;
            return ShowValue(prefab, ScoreText(award.finalMilliElectronVolts), award.finalMilliElectronVolts, 0f, award.worldPosition, scene);
        }

        public static EnemyScoreToast ShowMass(EnemyScoreToast prefab, float restored, float range, Vector3 position, Scene scene)
        {
            if (restored <= 0f || range <= 0f) return null;
            string percent = (restored / range * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return ShowValue(prefab, "+" + percent + "% MASS", 0L, restored, position, scene);
        }

        public static EnemyScoreToast ShowPickup(EnemyScoreToast prefab, ScoreAwardResult award,
            float restored, float range, Vector3 position, Scene scene)
        {
            if (!award.accepted || award.finalMilliElectronVolts <= 0)
                return ShowMass(prefab, restored, range, position, scene);

            string text = ScoreText(award.finalMilliElectronVolts) + " SCORE";
            if (restored > 0f && range > 0f)
            {
                string percent = (restored / range * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                text += "\n<size=80%>+" + percent + "% MASS</size>";
            }
            return ShowValue(prefab, text, award.finalMilliElectronVolts, restored, position, scene);
        }

        private static string ScoreText(long value)
        {
            var display = EnergyScoreFormatter.GetDisplayValue(value);
            string amount = EnergyScoreFormatter.FormatValue(value, 1, display.fractionalThousandths == 0 ? 0 : 3);
            if (display.fractionalThousandths != 0) amount = amount.TrimEnd('0').TrimEnd('.');
            return "+" + amount + " " + display.unitLabel;
        }

        private static EnemyScoreToast ShowValue(EnemyScoreToast prefab, string text, long score, float mass, Vector3 position, Scene scene)
        {
            if (prefab == null || prefab.label == null) return null;
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
            toast.Amount = score; toast.MassRestored = mass;
            toast.age = 0f; toast.startedAt = Time.unscaledTime; toast.view = Camera.main;
            if (toast.view == null) toast.view = FindFirstObjectByType<Camera>();
            toast.up = toast.view != null ? toast.view.transform.up : Vector3.forward;
            toast.origin = position + Vector3.up * .2f + toast.up * .25f;
            toast.transform.position = toast.origin;
            toast.label.text = text;
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
