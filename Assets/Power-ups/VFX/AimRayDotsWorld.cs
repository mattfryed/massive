using System.Collections.Generic;
using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class AimRayDotsWorld : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField, Range(4, 256)] private int maxDots = 48;
        [SerializeField] private float startRadiusWorld = 0.85f; // NEW: first dot distance from origin
        [SerializeField] private float spacing = 0.35f;
        [SerializeField] private float yLift = 0.03f;

        [Header("Dot Size")]
        [SerializeField] private float dotRadiusWorld = 0.10f;

        [Header("Ease In/Out")]
        [Tooltip("Higher = snappier. ~10-18 feels good.")]
        [SerializeField] private float easeSpeed = 14f;

        [Tooltip("If true, also fade alpha with scale.")]
        [SerializeField] private bool fadeAlphaWithScale = true;

        [Header("Material")]
        [SerializeField] private Material dotMaterial; // optional. If null, we create an Unlit/Transparent material.

        class Dot
        {
            public Transform t;
            public MeshRenderer r;
            public MaterialPropertyBlock mpb;

            public float current01; // 0..1
            public float target01;  // 0..1
            public Vector3 targetPosWS;
            public bool active;      // logical active (wants to be shown)
        }

        private readonly List<Dot> _dots = new();
        private bool _initialized;
public void SetTuning(
    float startRadiusWorld,
    float dotRadiusWorld,
    float spacingWorld,
    float yLiftWorld,
    float easeSpeed,
    bool fadeAlphaWithScale)
{
    this.startRadiusWorld = Mathf.Max(0f, startRadiusWorld);
    this.dotRadiusWorld   = Mathf.Max(0.001f, dotRadiusWorld);
    this.spacing          = Mathf.Max(0.01f, spacingWorld);
    this.yLift            = yLiftWorld;

    this.easeSpeed = Mathf.Max(0.01f, easeSpeed);
    this.fadeAlphaWithScale = fadeAlphaWithScale;
}

        private void EnsurePool()
        {
            if (_initialized) return;
            _initialized = true;

            if (!dotMaterial)
            {
                // Prefer transparent so alpha fade works.
                var s = Shader.Find("Unlit/Transparent");
                if (!s) s = Shader.Find("Unlit/Color"); // fallback (alpha fade may not work as expected)
                dotMaterial = new Material(s);

                // Try setting color if shader supports it.
                if (dotMaterial.HasProperty("_Color"))
                    dotMaterial.SetColor("_Color", Color.white);
            }

            for (int i = 0; i < maxDots; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"RayDot_{i:00}";
                go.transform.SetParent(transform, false);

                var col = go.GetComponent<Collider>();
                if (col) Destroy(col);

                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = dotMaterial;

                go.SetActive(false);

                _dots.Add(new Dot
                {
                    t = go.transform,
                    r = r,
                    mpb = new MaterialPropertyBlock(),
                    current01 = 0f,
                    target01 = 0f,
                    targetPosWS = Vector3.zero,
                    active = false
                });
            }
        }

        private void Update()
        {
            if (!_initialized) return;

            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, easeSpeed) * dt);

            float diameter = Mathf.Max(0.0001f, dotRadiusWorld * 2f);

            for (int i = 0; i < _dots.Count; i++)
            {
                var d = _dots[i];
                if (d.t == null) continue;

                // Smooth current toward target
                d.current01 = Mathf.Lerp(d.current01, d.target01, k);

                // Toggle active state based on visibility
                bool shouldBeActiveGO = d.current01 > 0.002f || d.target01 > 0.002f;

                if (shouldBeActiveGO && !d.t.gameObject.activeSelf)
                    d.t.gameObject.SetActive(true);

                if (!shouldBeActiveGO && d.t.gameObject.activeSelf)
                {
                    d.t.gameObject.SetActive(false);
                    continue;
                }

                if (!d.t.gameObject.activeSelf)
                    continue;

                // Follow target position
                d.t.position = d.targetPosWS;

                // Scale ease
                float s = diameter * d.current01;
                d.t.localScale = new Vector3(s, s, s);

                // Optional alpha fade (if shader supports _Color)
                if (fadeAlphaWithScale && d.r != null)
                {
                    if (d.r.sharedMaterial != null && d.r.sharedMaterial.HasProperty("_Color"))
                    {
                        // Fade more aggressively near end so "death" feels softer
                        float a = Mathf.Clamp01(d.current01);
                        a = a * a; // ease

                        d.mpb.Clear();
                        d.mpb.SetColor("_Color", new Color(1f, 1f, 1f, a));
                        d.r.SetPropertyBlock(d.mpb);
                    }
                }
            }
        }

        public void Hide()
        {
            EnsurePool();
            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].active = false;
                _dots[i].target01 = 0f; // ease out
            }
        }

        /// <summary>
        /// Draw a dotted ray in world space along dirWS, clamped to maxDistance.
        /// dotCount is the desired count; we also clamp by maxDistance/spacing.
        /// </summary>
public void SetRay(Vector3 originWS, Vector3 dirWS, float blockDistance, int dotCount)
{
    EnsurePool();

    dirWS.y = 0f;
    if (dirWS.sqrMagnitude < 0.0001f) { Hide(); return; }
    dirWS.Normalize();

    int desiredCount = Mathf.Clamp(dotCount, 0, maxDots);

    // Desired ray length is determined ONLY by dot count + fixed spacing
    float desiredRayEnd = startRadiusWorld + spacing * desiredCount;

    // blockDistance comes from the spherecast (or max range if unblocked).
    // Clamp the visible end to the blocker.
    float visibleEnd = blockDistance > 0.0001f ? Mathf.Min(desiredRayEnd, blockDistance) : desiredRayEnd;

    // Helper: instant off (for blocked dots)
    void InstantOff(Dot d)
    {
        d.active = false;
        d.target01 = 0f;
        d.current01 = 0f;
        if (d.t && d.t.gameObject.activeSelf)
            d.t.gameObject.SetActive(false);
    }

    // 1) Dots within desiredCount: place them at fixed spacing
    for (int i = 0; i < desiredCount; i++)
    {
        var d = _dots[i];

        float dist = startRadiusWorld + spacing * (i + 1);

        // 2) If blocked: everything at/after the contact point is instantly OFF
        // Use a tiny epsilon so we don't flicker at the boundary.
        if (dist > visibleEnd - 0.001f)
        {
            InstantOff(d);
            continue;
        }

        Vector3 p = originWS + dirWS * dist;
        p.y = originWS.y + yLift;

        d.targetPosWS = p;
        d.active = true;
        d.target01 = 1f; // ease in
    }

    // 3) Dots beyond desiredCount: ease out (normal “dying”)
    for (int i = desiredCount; i < _dots.Count; i++)
    {
        var d = _dots[i];
        d.active = false;
        d.target01 = 0f; // ease out
        // NOTE: we do NOT force current01 to 0 here, so it eases out naturally.
    }
}



        // Optional runtime tuning from the VFX module/ability
        public void SetStartRadius(float rWorld) => startRadiusWorld = Mathf.Max(0f, rWorld);
        public void SetDotRadius(float rWorld) => dotRadiusWorld = Mathf.Max(0.001f, rWorld);

        public float Spacing => spacing;
        public int MaxDots => maxDots;
        public float StartRadiusWorld => startRadiusWorld;
        public float DotRadiusWorld => dotRadiusWorld;
    }
}
