using UnityEngine;

[ExecuteAlways]
public class ArenaBoundaryCollidersFromVectorGrid : MonoBehaviour
{
    [SerializeField] private VectorGridGPU grid;
    [SerializeField] private ArenaBoundsFromVectorGrid boundsProvider;

    [Header("Walls")]
    [Tooltip("If true, wall thickness defaults to the grid's thick border stroke (borderWidthWorld * borderWidthMul).")]
    public bool useGridBorderThickness = true;

    [Tooltip("Wall thickness in world units (used if useGridBorderThickness is false).")]
    [Min(0.001f)] public float wallThicknessWorld = 0.25f;

    [Tooltip("Wall height along grid local +Z (world units). Make big enough to catch all blob colliders.")]
    [Min(0.1f)] public float wallHeightWorld = 5f;

    [Tooltip("If true, places walls entirely OUTSIDE the playable rect. If false, walls straddle the edge (half inside / half outside).")]
    public bool placeOutside = true;

    [Tooltip("If true, colliders are triggers (for OOB events). If false, they are solid walls.")]
    public bool isTrigger = false;

    const string LEFT  = "ArenaWall_Left";
    const string RIGHT = "ArenaWall_Right";
    const string BOT   = "ArenaWall_Bottom";
    const string TOP   = "ArenaWall_Top";

    void Reset()
    {
        if (!grid) grid = GetComponent<VectorGridGPU>();
        if (!boundsProvider) boundsProvider = GetComponent<ArenaBoundsFromVectorGrid>();
    }

    void OnEnable() => Rebuild();
    void Update()
    {
        // keep it simple: rebuild in edit mode so inspector tweaks reflect instantly
        if (!Application.isPlaying) Rebuild();
    }

    void OnValidate() => Rebuild();

    public void Rebuild()
    {
        if (!grid) return;

        Vector2 halfLocal = grid.size * 0.5f;

        // Determine wall thickness: border stroke (world) or manual
        float tWorld = wallThicknessWorld;
        if (useGridBorderThickness)
        {
            // Your render code uses: halfW = 0.5 * borderWidthWorld * borderWidthMul
            // so full stroke width is borderWidthWorld * borderWidthMul.
            tWorld = Mathf.Max(0.001f, grid.boundary.borderWidthWorld * grid.boundary.borderWidthMul);
        }

        // Convert world units to local units (per-axis) to survive scaled transforms
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(1e-6f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(1e-6f, Mathf.Abs(ls.y));
        float sz = Mathf.Max(1e-6f, Mathf.Abs(ls.z));

        float tLocalX = tWorld / sx;
        float tLocalY = tWorld / sy;
        float hLocalZ = wallHeightWorld / sz;

        // Where to place walls relative to the edge
        float offsetX = halfLocal.x + (placeOutside ? tLocalX * 0.5f : 0f);
        float offsetY = halfLocal.y + (placeOutside ? tLocalY * 0.5f : 0f);

        // Sizes (local units)
        float spanX = (halfLocal.x * 2f) + (placeOutside ? tLocalX * 2f : 0f);
        float spanY = (halfLocal.y * 2f) + (placeOutside ? tLocalY * 2f : 0f);

        MakeWall(LEFT,  new Vector3(-offsetX, 0f, 0f), new Vector3(tLocalX, spanY, hLocalZ));
        MakeWall(RIGHT, new Vector3( offsetX, 0f, 0f), new Vector3(tLocalX, spanY, hLocalZ));
        MakeWall(BOT,   new Vector3(0f, -offsetY, 0f), new Vector3(spanX, tLocalY, hLocalZ));
        MakeWall(TOP,   new Vector3(0f,  offsetY, 0f), new Vector3(spanX, tLocalY, hLocalZ));
    }

    void MakeWall(string name, Vector3 localPos, Vector3 localSize)
    {
        Transform t = transform.Find(name);
        GameObject go = t ? t.gameObject : new GameObject(name);

        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var col = go.GetComponent<BoxCollider>();
        if (!col) col = go.AddComponent<BoxCollider>();

        col.isTrigger = isTrigger;
        col.center = Vector3.zero;
        col.size = new Vector3(
            Mathf.Max(0.001f, localSize.x),
            Mathf.Max(0.001f, localSize.y),
            Mathf.Max(0.001f, localSize.z)
        );
    }
}
