using UnityEngine;
using System.Collections.Generic;

public class NovaEntryBeadRing : MonoBehaviour
{
    [Header("Refs")]
    public Transform ringCenter;              // usually the star transform
    public Mesh quadMesh;                     // use a Quad mesh
    public Material beadMaterial;             // MASSIVE/UnlitCircle3D
    public Camera mainCam;                    // optional (for sanity); not required if flat on XZ

    [Header("Ring")]
    [Range(16, 256)] public int beadCount = 96;
    public float ringRadius = 4.5f;
    public float dotSize = 0.15f;             // world units (radius-ish visual)
    public float ringRotationDegPerSec = 8f;

    [Header("Intro Expand")]
    public float expandDuration = 0.35f;
    [Tooltip("Per-dot delay variance (seconds).")]
    public float expandDelayJitter = 0.06f;
    [Tooltip("Per-dot duration variance (seconds).")]
    public float expandDurationJitter = 0.08f;
    public AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Pulse Wave")]
    public bool pulseEnabled = true;
    public float pulseDotsPerSec = 18f;       // how fast the pulse travels around the ring
    public float pulseScaleIncrease = 2f;     // like your ray projectile
    [Range(0, 12)] public int pulseTailDots = 5;
    [Range(0, 6)]  public int pulseHeadDots = 1;

    // internal
    class Bead
    {
        public Transform t;
        public float baseAngle;      // radians
        public float delay;
        public float dur;
    }

    readonly List<Bead> _beads = new();
    float _time;
    float _pulseHead;
    float _ringRot;

    void OnEnable()
    {
        if (ringCenter == null) ringCenter = transform;
        if (quadMesh == null) quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        if (mainCam == null) mainCam = Camera.main;

        BuildIfNeeded();
        ResetAnim();
    }

    void OnDisable()
    {
        // keep beads around (no destroy) so re-enabling is cheap
    }

    void BuildIfNeeded()
    {
        if (_beads.Count == beadCount) return;

        // rebuild if count changed
        foreach (var b in _beads)
            if (b?.t != null) Destroy(b.t.gameObject);
        _beads.Clear();

        for (int i = 0; i < beadCount; i++)
        {
            var go = new GameObject($"Bead_{i:000}");
            go.transform.SetParent(transform, false);

            // Quad on XZ plane facing +Y (camera looks down -Y)
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = quadMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = beadMaterial;

            _beads.Add(new Bead
            {
                t = go.transform,
                baseAngle = (Mathf.PI * 2f) * (i / (float)beadCount),
                delay = 0f,
                dur = expandDuration
            });
        }
    }

    void ResetAnim()
    {
        _time = 0f;
        _pulseHead = 0f;
        _ringRot = 0f;

        for (int i = 0; i < _beads.Count; i++)
        {
            _beads[i].delay = Random.Range(0f, expandDelayJitter);
            _beads[i].dur = Mathf.Max(0.05f, expandDuration + Random.Range(-expandDurationJitter, expandDurationJitter));
            _beads[i].t.localPosition = Vector3.zero;
            _beads[i].t.localScale = Vector3.one * dotSize;
        }
    }

    void Update()
    {
        if (ringCenter == null) return;

        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        _time += dt;

        // ring rotation
        _ringRot += ringRotationDegPerSec * dt;
        float rotRad = _ringRot * Mathf.Deg2Rad;

        // pulse head advances around indices
        if (pulseEnabled)
            _pulseHead += pulseDotsPerSec * dt;

        // place beads
        for (int i = 0; i < _beads.Count; i++)
        {
            var b = _beads[i];

            // expansion t per bead
            float u = Mathf.Clamp01((_time - b.delay) / Mathf.Max(0.001f, b.dur));
            float radial = expandCurve.Evaluate(u) * ringRadius;

            // angle + ring rotation
            float a = b.baseAngle + rotRad;
            float x = Mathf.Cos(a) * radial;
            float z = Mathf.Sin(a) * radial;

            // local position around ringCenter
            b.t.position = ringCenter.position + new Vector3(x, 0f, z);

            // scale pulse
            float s = dotSize;
            if (pulseEnabled && u >= 1f) // only pulse after beads finished expanding
            {
                // distance in bead index space, wrap-around
                float head = _pulseHead % _beads.Count;
                float d = Mathf.Abs(i - head);
                d = Mathf.Min(d, _beads.Count - d); // wrap

                float scaleMul = 1f;

                // head + one dot ahead
                if (d <= pulseHeadDots)
                {
                    float tHead = 1f - (d / Mathf.Max(1f, pulseHeadDots));
                    float eased = Mathf.SmoothStep(0f, 1f, tHead);
                    scaleMul = 1f + pulseScaleIncrease * eased;
                }
                // tail behind
                else if (d <= pulseTailDots)
                {
                    float tTail = 1f - ((d - pulseHeadDots) / Mathf.Max(1f, pulseTailDots - pulseHeadDots));
                    float eased = Mathf.SmoothStep(0f, 1f, tTail);
                    scaleMul = 1f + pulseScaleIncrease * 0.65f * eased;
                }

                s = dotSize * scaleMul;
            }

            b.t.localScale = Vector3.one * s;
        }
    }
}
