using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MetaballSDFInstance))]
public class MetaballManifest : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private bool startHidden = true;

    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(0.001f)] private float inDuration = 0.30f;
    [SerializeField, Min(0.001f)] private float outDuration = 0.22f;
    [SerializeField, Min(0f)] private float stagger = 0.03f;
    [SerializeField, Min(0f)] private float jitter = 0.015f;
    [SerializeField] private bool reverseOrderOnOut = true;

    [Header("Shape Feel")]
    [SerializeField, Range(0f, 2f)] private float overshoot = 1.15f; // 0 = no overshoot
    [SerializeField, Min(0f)] private float minRadiusToEmit = 0.0005f;

    private enum State { Hidden, In, Shown, Out }

    private MetaballSDFInstance _sdf;
    private float _t;
    private int _idx;
    private int _lastFrameCount = 1;
    private State _state = State.Shown;

    private int MaxBalls => Mathf.Max(1, MetaballSDFInstance.MaxBalls);

private void Awake()
{
    _sdf = GetComponent<MetaballSDFInstance>();
    if (startHidden) SetHiddenInstant();
}


    public void SetHiddenInstant() { _state = State.Hidden; _t = 0f; }
    public void SetShownInstant()  { _state = State.Shown;  _t = 0f; }

    public void PlayIn()  { _state = State.In;  _t = 0f; }
    public void PlayOut() { _state = State.Out; _t = 0f; }

    public float EstimatedOutTime()
    {
        int n = Mathf.Clamp(_lastFrameCount, 1, MaxBalls);
        return (n - 1) * stagger + outDuration + jitter;
    }

    // Call instead of _sdf.Clear()
    public void Clear()
    {
        TickTime();
        _idx = 0;
        _sdf.Clear();
    }

    // Call instead of _sdf.AddBall()
    public void AddBall(Vector3 posOS, float radiusOS)
    {
        int i = _idx++;
        float m = Evaluate(i);
        float r = radiusOS * m;

        if (r > minRadiusToEmit)
            _sdf.AddBall(posOS, r);
    }

    // Call instead of _sdf.Apply()
    public void Apply()
    {
        _lastFrameCount = Mathf.Max(1, _idx);
        _sdf.Apply();

        // optional terminal snap
        float maxIn  = (MaxBalls - 1) * stagger + inDuration + jitter;
        float maxOut = (MaxBalls - 1) * stagger + outDuration + jitter;

        if (_state == State.In  && _t >= maxIn)  _state = State.Shown;
        if (_state == State.Out && _t >= maxOut) _state = State.Hidden;
    }

    private void TickTime()
    {
        if (_state != State.In && _state != State.Out) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _t += dt;
    }

    private float Evaluate(int index)
    {
        if (_state == State.Hidden) return 0f;
        if (_state == State.Shown)  return 1f;

        int i = Mathf.Clamp(index, 0, MaxBalls - 1);

        // deterministic per-ball jitter (no per-frame Random)
        float j = HashSigned01(i, GetInstanceID()) * jitter;

        if (_state == State.In)
        {
            float delay = i * stagger + j;
            float u = Mathf.Clamp01((_t - delay) / Mathf.Max(0.0001f, inDuration));
            return EaseOutBack(u, overshoot);
        }
        else // Out
        {
            int count = Mathf.Clamp(_lastFrameCount, 1, MaxBalls);
            int order = reverseOrderOnOut ? (count - 1 - i) : i;
            order = Mathf.Clamp(order, 0, count - 1);

            float delay = order * stagger + j;
            float u = Mathf.Clamp01((_t - delay) / Mathf.Max(0.0001f, outDuration));
            return 1f - EaseInBack(u, overshoot);
        }
    }

    private static float HashSigned01(int i, int seed)
    {
        unchecked
        {
            uint h = (uint)(i * 374761393) ^ (uint)seed;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            float u = (h & 0x00FFFFFF) / 16777215f; // 0..1
            return u * 2f - 1f; // -1..1
        }
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        t = Mathf.Clamp01(t);
        if (overshoot <= 0.0001f) return t * t * (3f - 2f * t); // smoothstep
        float s = 1.70158f * overshoot;
        t -= 1f;
        return (t * t * ((s + 1f) * t + s) + 1f);
    }

    private static float EaseInBack(float t, float overshoot)
    {
        t = Mathf.Clamp01(t);
        if (overshoot <= 0.0001f) return t * t * (3f - 2f * t);
        float s = 1.70158f * overshoot;
        return t * t * ((s + 1f) * t - s);
    }
}
