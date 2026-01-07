using UnityEngine;
 
[RequireComponent(typeof(MetaballSDFInstance))]
[RequireComponent(typeof(MetaballManifest))]

public class PowerupDecoherenceVisual : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float cycleSeconds = 1.15f;
    [SerializeField, Range(0.05f, 0.6f)] private float snapBackPortion = 0.22f;

    [Header("Shape")]
    [SerializeField] private float baseRadius = 0.23f;
    [SerializeField] private float splitRadius = 0.18f;
    // [SerializeField] private float bridgeRadiusMax = 0.20f;
    // [SerializeField] private float bridgeRadiusMin = 0.07f;
    [SerializeField] private float maxSeparation = 0.42f;

    [Header("Direction")]
    [SerializeField] private float directionSlerp = 18f;

    [Header("Connector Chain")] 
[SerializeField, Range(1, 24)] private int connectorCount = 7;     // how many balls between the lobes
[SerializeField] private float connectorRadiusNearLobe = 0.16f;    // thicker near the big blobs
[SerializeField] private float connectorRadiusMid = 0.06f;         // thinnest point in the middle
[SerializeField] private float connectorStress = 0.85f;            // 0..1 extra thinning when stretched

    private MetaballSDFInstance _sdf;
    private Vector3 _dirCurrent;
    private Vector3 _dirNext;

    private float _tCycle;

    private MetaballManifest _manifest;

    private static Vector3 RandomDirectionXZ()
{
    float a = Random.value * Mathf.PI * 2f;
    return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
}

    private void Awake()
    {
        _sdf = GetComponent<MetaballSDFInstance>();
    _manifest = GetComponent<MetaballManifest>();
_dirCurrent = RandomDirectionXZ();
_dirNext = RandomDirectionXZ();
    }

    private void Update()
    {
        _tCycle += Time.deltaTime / Mathf.Max(0.001f, cycleSeconds);

        if (_tCycle >= 1f)
        {
            _tCycle -= 1f;
            _dirNext = RandomDirectionXZ();
        }

        _dirCurrent = Vector3.Slerp(_dirCurrent, _dirNext, 1f - Mathf.Exp(-directionSlerp * Time.deltaTime));

        float sep01 = Separation01(_tCycle, snapBackPortion);
        float sep = maxSeparation * sep01;

        // Two split blobs
        Vector3 a = _dirCurrent * (sep * 0.5f);
        Vector3 b = -_dirCurrent * (sep * 0.5f);

        float lobeR = Mathf.Lerp(baseRadius, splitRadius, sep01);

        _manifest.Clear();
        _manifest.AddBall(a, lobeR);
        _manifest.AddBall(b, lobeR);

        int n = Mathf.Clamp(connectorCount, 1, MetaballSDFInstance.MaxBalls - 2);
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)(n + 1);
            Vector3 p = Vector3.Lerp(a, b, u);

            float mid = 1f - Mathf.Abs(2f * u - 1f);
            float neck = Mathf.SmoothStep(0f, 1f, mid);

            float r = Mathf.Lerp(connectorRadiusNearLobe, connectorRadiusMid, neck);
            r *= Mathf.Lerp(1f, 1f - connectorStress, sep01);

            _manifest.AddBall(p, r);
        }

        _manifest.Apply();


    }

    // Ease out to max, then snap back quickly
    private static float Separation01(float t, float snapPortion)
    {
        float splitPortion = 1f - snapPortion;
        if (t <= splitPortion)
        {
            float x = Mathf.Clamp01(t / splitPortion);
            // easeOutCubic
            return 1f - Mathf.Pow(1f - x, 3f);
        }
        else
        {
            float x = Mathf.Clamp01((t - splitPortion) / snapPortion);
            // snapBack: fast easeIn
            float back = Mathf.Pow(x, 2.6f);
            return 1f - back;
        }
    }
}