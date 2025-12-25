using UnityEngine;

[RequireComponent(typeof(MetaballSDFInstance))]
public class PowerupParticleAcceleratorVisual : MonoBehaviour
{
    [Header("Coil")]
    [SerializeField, Range(8, 28)] private int segments = 20;
    [SerializeField] private float ringRadius = 0.34f;
    [SerializeField] private float ringBallRadius = 0.085f;
    [SerializeField] private float ringSquashY = 0.72f;

    [Header("Charge Wave")]
    [SerializeField] private float waveSpeed = 1.2f;  // cycles/sec
    [SerializeField] private float waveWidth = 2.6f;  // in segments
    [SerializeField] private float waveAmp = 0.05f;

    [Header("Center Particle")]
    [SerializeField] private float centerRadius = 0.10f;
    [SerializeField] private float centerPulseAmp = 0.06f;
    [SerializeField] private float centerYOffset = 0.02f;

    [SerializeField] private float orbitRadius = 0.12f;
    [SerializeField] private float orbitBallRadius = 0.035f;
    [SerializeField] private int orbiters = 3;
    [SerializeField] private float orbitSpeed = 3.5f;

    [Header("Output Stream (Perpendicular)")]
    [SerializeField] private Vector3 streamDirOS = Vector3.up; // NOTE: top-down camera won't "see" a Y stream as a streak
    [SerializeField] private float streamHalfLength = 0.45f;   // must stay within [-0.5, 0.5] object space
    [SerializeField, Range(3, 24)] private int streamCount = 10;
    [SerializeField] private float streamSpacing = 0.09f;
    [SerializeField] private float streamSpeed = 0.35f;        // units/sec in object space
    [SerializeField] private float streamRadiusHead = 0.06f;
    [SerializeField] private float streamRadiusTail = 0.02f;
    [SerializeField] private bool streamBothDirections = false;

    [Header("Coil Feel")]
    [SerializeField] private float windings = 4f;
    [SerializeField] private float coilHeight = 0.06f;

    private MetaballSDFInstance _sdf;

    private void Awake()
    {
        _sdf = GetComponent<MetaballSDFInstance>();
    }

    private void Update()
    {
        float t = Time.time;

        // 0..1 charge envelope
        float charge01 = 0.5f + 0.5f * Mathf.Sin(t * 2.0f);

        // Budget balls (shader supports 32)
        int maxBalls = MetaballSDFInstance.MaxBalls;

        int desiredStream = Mathf.Clamp(streamCount, 0, 24);
        int desiredOrbiters = Mathf.Max(0, orbiters);
        int desiredSegments = Mathf.Clamp(segments, 8, 28);

        // Reserve: center(1) + orbiters + stream (+ stream reverse if enabled)
        int streamMultiplier = streamBothDirections ? 2 : 1;
        int reserved = 1 + desiredOrbiters + desiredStream * streamMultiplier;

        // Whatever remains goes to the coil segments
        int seg = Mathf.Clamp(desiredSegments, 0, Mathf.Max(0, maxBalls - reserved));

        // If seg got clamped too low, clamp stream instead (prefer keeping coil readable)
        if (seg < 8)
        {
            seg = Mathf.Clamp(desiredSegments, 8, 28); // restore target
            int remainingForStream = maxBalls - (1 + desiredOrbiters + seg);
            int streamPossible = Mathf.Max(0, remainingForStream / streamMultiplier);
            desiredStream = Mathf.Min(desiredStream, streamPossible);
        }

        _sdf.Clear();

        // ---- Coil metaballs around a ring (local XZ plane) ----
        // waveCenter in "segment index space"
        float waveCenter = (t * waveSpeed) * seg;

        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;

            float x = Mathf.Cos(a) * ringRadius;
            float z = Mathf.Sin(a) * ringRadius;
            float y = Mathf.Sin(a * windings + t * 2.0f) * coilHeight; // animated coil ripple

            Vector3 p = new Vector3(x, y * ringSquashY, z);

            // index-distance around ring (wrap)
            float diff = Mathf.Abs(i - (waveCenter % seg));
            diff = Mathf.Min(diff, seg - diff);

            float w = Mathf.Exp(-(diff * diff) / (2f * waveWidth * waveWidth));
            float r = ringBallRadius + (w * waveAmp * charge01);

            _sdf.AddBall(p, r);
        }

        // ---- Center particle ----
        Vector3 centerPos = new Vector3(0f, centerYOffset, 0f);
        _sdf.AddBall(centerPos, centerRadius + centerPulseAmp * charge01);

        // ---- Orbiting “charge dots” (XZ) ----
        for (int j = 0; j < desiredOrbiters; j++)
        {
            float aj = (j / (float)desiredOrbiters) * Mathf.PI * 2f + t * orbitSpeed;
            Vector3 op = new Vector3(Mathf.Cos(aj), 0f, Mathf.Sin(aj)) * orbitRadius;
            op += centerPos;
            _sdf.AddBall(op, orbitBallRadius);
        }

        // ---- Output stream ----
        Vector3 dir = (streamDirOS.sqrMagnitude < 0.0001f) ? Vector3.up : streamDirOS.normalized;

        float len = Mathf.Clamp(streamHalfLength, 0.05f, 0.49f);
        float total = 2f * len;
        float travel = Mathf.Repeat(t * streamSpeed, streamSpacing);

        for (int i = 0; i < desiredStream; i++)
        {
            float k = (desiredStream <= 1) ? 0f : i / (float)(desiredStream - 1);
            float r = Mathf.Lerp(streamRadiusHead, streamRadiusTail, k);

            float s = -len + i * streamSpacing + travel;
            s = Mathf.Repeat(s + len, total) - len;

            _sdf.AddBall(centerPos + dir * s, r);

            if (streamBothDirections)
                _sdf.AddBall(centerPos - dir * s, r);
        }

        // APPLY MUST BE LAST (after all balls added)
        _sdf.Apply();

        // Optional: slow rotation for “device” read (mostly around Y for top-down)
        transform.localRotation = Quaternion.Euler(0f, t * 35f, 0f);
    }
}