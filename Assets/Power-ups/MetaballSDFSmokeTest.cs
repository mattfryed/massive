using UnityEngine;

[ExecuteAlways]
public class MetaballSDFSmokeTest : MonoBehaviour
{
    public MetaballSDFInstance sdf;

    [Range(0.01f, 0.49f)] public float radiusOS = 0.22f;
    public Vector3 centerOS = Vector3.zero;
    public bool animate = true;

    void OnEnable()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        Apply();
    }

    void OnValidate()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        Apply();
    }

    void Update()
    {
        Apply();
    }

    void Apply()
    {
        if (!sdf) return;

        float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

        Vector3 c = centerOS;
        if (animate)
            c += new Vector3(Mathf.Sin(t) * 0.15f, 0f, Mathf.Cos(t) * 0.15f);

        sdf.Clear();
        sdf.AddBall(c, radiusOS);
        sdf.Apply();
    }
}
