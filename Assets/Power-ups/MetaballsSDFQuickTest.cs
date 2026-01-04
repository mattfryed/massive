using UnityEngine;

public class MetaballSDFQuickTest : MonoBehaviour
{
    public MetaballSDFInstance sdf;

    void Start()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        sdf.Clear();
        sdf.AddBall(Vector3.zero, 0.2f);
        sdf.Apply();
    }
}
