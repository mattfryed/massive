using UnityEngine;

[DisallowMultipleComponent]
public class GridForceSource : MonoBehaviour
{
    public enum Mode { Radial, Directional }

    public VectorGridGPU grid;         // assign (or will auto-find in parents)
    public Mode mode = Mode.Radial;
    public float radius = 2.0f;
    public float strength = 8.0f;
    public Vector3 direction = Vector3.right; // used if Directional
    public bool persistent = true;     // stays every frame

    void Reset()
    {
        if (grid == null) grid = GetComponentInParent<VectorGridGPU>();
    }

    void LateUpdate()
    {
        if (grid == null) return;
        Vector3 p = transform.position;
        var local = grid.transform.InverseTransformPoint(p);

        VectorGridGPU.Force f = (mode == Mode.Radial)
            ? VectorGridGPU.MakeRadial(local, radius, strength)
            : VectorGridGPU.MakeDirectional(local, radius, direction, strength);

        if (persistent) grid.AddForce(f);
        else grid.SetForces(stackalloc VectorGridGPU.Force[] { f }); // single-shot
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(Vector3.zero, radius);
        if (mode == Mode.Directional)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Vector3.zero, direction.normalized * radius);
        }
    }
#endif
}
