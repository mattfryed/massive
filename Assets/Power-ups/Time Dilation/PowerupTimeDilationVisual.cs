using UnityEngine;

[RequireComponent(typeof(MetaballSDFInstance))]
public class PowerupTimeDilationVisual : MonoBehaviour
{
    [Header("Dash Motion")]
    [SerializeField] private Vector3 dashAxis = Vector3.right;
    [SerializeField] private float dashAmplitude = 0.30f;
    [SerializeField] private float dashFrequency = 10.0f;   // high = “buzz/dash”

    [Header("Buzz")]
    [SerializeField] private float jitterAmp = 0.02f;
    [SerializeField] private float jitterFreq = 24f;

    [Header("Main + Ghosts")]
    [SerializeField] private float mainRadius = 0.11f;
    [SerializeField, Range(1, 16)] private int ghostCount = 8;
    [SerializeField] private float ghostRadiusFalloff = 0.55f;
    [SerializeField] private float sampleInterval = 0.015f;

    private MetaballSDFInstance _sdf;
    private Vector3[] _history;
    private float _sampleTimer;

    private void Awake()
    {
        _sdf = GetComponent<MetaballSDFInstance>();
        _history = new Vector3[Mathf.Max(1, ghostCount)];
        for (int i = 0; i < _history.Length; i++) _history[i] = Vector3.zero;
        dashAxis = dashAxis.sqrMagnitude < 0.0001f ? Vector3.right : dashAxis.normalized;
        dashAxis.y = 0f;
if (dashAxis.sqrMagnitude < 0.0001f) dashAxis = Vector3.right;
dashAxis.Normalize();
    }

    private void Update()
    {
        float t = Time.time;

        // Fast back-and-forth dash
        float s = Mathf.Sin(t * Mathf.PI * 2f * dashFrequency);
        Vector3 p = dashAxis * (s * dashAmplitude);

        // Buzz jitter (deterministic-ish)
        Vector3 j = new Vector3(
            Mathf.Sin(t * jitterFreq * 1.13f),
            Mathf.Sin(t * jitterFreq * 0.97f + 1.7f),
            Mathf.Sin(t * jitterFreq * 1.21f + 3.1f)
        ) * jitterAmp;

        Vector3 current = p + j;

        // Sample history for ghost trail
        _sampleTimer += Time.deltaTime;
        if (_sampleTimer >= sampleInterval)
        {
            _sampleTimer = 0f;
            for (int i = _history.Length - 1; i > 0; i--)
                _history[i] = _history[i - 1];
            _history[0] = current;
        }

        _sdf.Clear();

        // Main particle
        _sdf.AddBall(current, mainRadius);

        // Ghosts (hard falloff, still crisp)
        for (int g = 0; g < _history.Length; g++)
        {
            float k = (g + 1f) / (_history.Length + 1f);
            float r = Mathf.Lerp(mainRadius, mainRadius * ghostRadiusFalloff, k);
            _sdf.AddBall(_history[g], r);
        }

        _sdf.Apply();

        // Optional: tiny spin to keep it “alive”
        transform.localRotation = Quaternion.Euler(t * 20f, t * 60f, 0f);
        
    }
}