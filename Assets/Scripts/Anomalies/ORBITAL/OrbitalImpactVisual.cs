using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Orbital
{
    [DisallowMultipleComponent]
    public sealed class OrbitalImpactVisual : MonoBehaviour
    {
        public Material lineMaterial;
        [Range(4, 64)] public int capacity = 24;
        [Min(.05f)] public float flashSeconds = .34f;
        public bool useUnscaledTime;
        private sealed class Flash
        {
            public GameObject root;
            public LineRenderer orbit, arrow, electron;
            public float age;
            public Vector3 contact, direction, arrowCenter, arrowTip, arrowLeft, arrowRight;
        }
        private Flash[] pool;
        private int next;
        public int FlashesShown { get; private set; }

        private void Awake()
        {
            pool = new Flash[Mathf.Clamp(capacity, 4, 64)];
            for (int i = 0; i < pool.Length; i++)
            {
                var root = new GameObject("Electron impact " + i);
                root.transform.SetParent(transform, false);
                var f = new Flash { root = root };
                // The wider ribbon includes a soft halo; the shader keeps its bright center narrow.
                f.orbit = MakeLine(root.transform, "Sampled orbital vector", 97, .09f);
                f.arrow = MakeLine(root.transform, "Impulse arrowhead", 3, .16f);
                f.arrow.numCornerVertices = 0; f.arrow.numCapVertices = 0;
                f.electron = MakeLine(root.transform, "Electron flash", 2, .21f);
                var electronStyle = new MaterialPropertyBlock();
                electronStyle.SetFloat("_CoreFraction", 1f);
                electronStyle.SetFloat("_GlowStrength", 0f);
                f.electron.SetPropertyBlock(electronStyle);
                f.orbit.startColor = f.orbit.endColor = new Color(.8f, .57f, 1f, 1f);
                f.arrow.startColor = f.arrow.endColor = new Color(1f, .91f, .54f, 1f);
                f.electron.startColor = f.electron.endColor = new Color(1f, 1f, .92f, 1f);
                root.SetActive(false); pool[i] = f;
            }
        }

        private LineRenderer MakeLine(Transform parent, string label, int count, float width)
        {
            var go = new GameObject(label); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.useWorldSpace = true;
            line.positionCount = count; line.widthMultiplier = width;
            line.numCapVertices = 3; line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }

        public void Show(Vector3 center, Vector3 contact, Vector3 direction, float aspect)
        {
            if (pool == null || lineMaterial == null) return;
            Flash f = pool[next++ % pool.Length];
            Vector3 a = contact - center; a.y = 0f;
            if (a.sqrMagnitude < .01f) a = Vector3.forward * .1f;
            Vector3 b = direction * (a.magnitude * aspect);
            Vector3 planeCenter = new Vector3(center.x, Mathf.Max(.18f, contact.y), center.z);
            for (int i = 0; i < f.orbit.positionCount; i++)
            {
                float theta = i * Mathf.PI * 2f / (f.orbit.positionCount - 1);
                f.orbit.SetPosition(i, planeCenter + a * Mathf.Cos(theta) + b * Mathf.Sin(theta));
            }
            // The ellipse passes through the contact; its tangent there is precisely the impulse direction.
            Vector3 p = planeCenter + a;
            Vector3 tip = p + direction * .55f;
            Vector3 side = Vector3.Cross(Vector3.up, direction) * .15f;
            f.contact = p; f.direction = direction;
            f.arrowCenter = tip - direction * .15f;
            f.arrowTip = tip; f.arrowLeft = tip - direction * .23f + side; f.arrowRight = tip - direction * .23f - side;
            f.age = 0f; f.root.SetActive(true); SetSize(f, 0f); FlashesShown++;
        }

        private static void SetSize(Flash f, float size)
        {
            f.orbit.widthMultiplier = .09f * size;
            f.arrow.widthMultiplier = .16f * size;
            f.electron.widthMultiplier = .21f * size;
            f.orbit.enabled = f.arrow.enabled = f.electron.enabled = size > .0001f;
            f.arrow.SetPosition(0, Vector3.Lerp(f.arrowCenter, f.arrowLeft, size));
            f.arrow.SetPosition(1, Vector3.Lerp(f.arrowCenter, f.arrowTip, size));
            f.arrow.SetPosition(2, Vector3.Lerp(f.arrowCenter, f.arrowRight, size));
            f.electron.SetPosition(0, f.contact - f.direction * (.012f * size));
            f.electron.SetPosition(1, f.contact + f.direction * (.012f * size));
        }

        private void Update()
        {
            if (pool == null) return;
            foreach (Flash f in pool)
            {
                if (!f.root.activeSelf) continue;
                f.age += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float phase = Mathf.Clamp01(f.age / Mathf.Max(.05f, flashSeconds));
                float size = Mathf.SmoothStep(0f, 1f, phase / .2f) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, phase)));
                SetSize(f, size);
                if (phase >= 1f) f.root.SetActive(false);
            }
        }
        private void OnDisable() { if (pool != null) foreach (Flash f in pool) if (f.root) f.root.SetActive(false); }
    }
}
