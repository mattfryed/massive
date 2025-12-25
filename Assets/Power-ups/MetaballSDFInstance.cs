using UnityEngine;

[DisallowMultipleComponent]
public class MetaballSDFInstance : MonoBehaviour
{
    public const int MaxBalls = 32;

    [SerializeField] private Renderer targetRenderer;

    private readonly Vector4[] _balls = new Vector4[MaxBalls];
    private int _count;
    private MaterialPropertyBlock _mpb;

    public int Count => _count;

    private void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>();
        _mpb ??= new MaterialPropertyBlock();
    }

    public void Clear()
    {
        _count = 0;
    }

    public void AddBall(Vector3 centerOS, float radius)
    {
        if (_count >= MaxBalls) return;
        _balls[_count] = new Vector4(centerOS.x, centerOS.y, centerOS.z, radius);
        _count++;
    }

    public void SetBall(int index, Vector3 centerOS, float radius)
    {
        if (index < 0 || index >= MaxBalls) return;
        _balls[index] = new Vector4(centerOS.x, centerOS.y, centerOS.z, radius);
        _count = Mathf.Max(_count, index + 1);
    }

    public void Apply()
    {
        if (!targetRenderer) return;

        _mpb.Clear();
        _mpb.SetInt("_BallCount", _count);
        _mpb.SetVectorArray("_Balls", _balls);
        targetRenderer.SetPropertyBlock(_mpb);
    }
}