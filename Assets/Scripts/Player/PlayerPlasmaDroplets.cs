using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerPlasmaDroplets : MonoBehaviour
{
    [Header("Links")]
    public Material dropletMat;      // Use the shader below ("MASSIVE/PlasmaDroplet")
    public Mesh quadMesh;            // A unit quad (or built-in quad)
    public float sphereRadius = 0.5f; // matches your sphere visual radius in object scale

    [Header("Mass Mapping")]
    [Range(0,1)] public float massFill = 0.25f;   // set from gameplay (0..1)
    public int minDroplets = 6;
    public int maxDroplets = 60;
    public Vector2 dropletSizeRange = new Vector2(0.025f, 0.08f); // in world units

    [Header("Inertia / Motion")]
    public float springK = 8f;       // pulls toward center
    public float damping = 7f;       // velocity damping
    public float biasTowardVel = 2.5f; // pushes toward velocity direction
    public float jitter = 0.4f;      // small random motion

    [Header("Team Look")]
    public bool teamWhite = true;    // true: white droplets; false: black droplets with white outline
    [Range(0.5f, 2f)] public float outlinePx = 1f;

    Rigidbody rb;
    struct Drop { public Vector3 pos, vel; public float size; public float seed; }
    List<Drop> drops = new List<Drop>(128);
    Matrix4x4[] matrices = new Matrix4x4[1023]; // instancing batch

    static readonly int _ColorMain   = Shader.PropertyToID("_ColorMain");
    static readonly int _UseOutline  = Shader.PropertyToID("_UseOutline");
    static readonly int _OutlinePx   = Shader.PropertyToID("_OutlinePx");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!quadMesh)
        {
            // Build a quick unit quad if none
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        }
        Prime();
    }

    void Prime()
    {
        drops.Clear();
        int target = Mathf.RoundToInt(Mathf.Lerp(minDroplets, maxDroplets, massFill));
        for (int i = 0; i < target; i++) drops.Add(Spawn());
    }

    Drop Spawn()
    {
        // Size (optionally bias slightly larger at higher counts later if desired)
        float tSize = Random.value;
        float size  = Mathf.Lerp(dropletSizeRange.x, dropletSizeRange.y, tSize);

        // Radius biased toward center (u^1.7) within ~45% of shell radius
        float u = Random.value;
        float radiusBias = Mathf.Pow(u, 1.7f) * (sphereRadius * 0.45f);

        // Random direction, place on that radius
        Vector3 p = Random.onUnitSphere * radiusBias;

        return new Drop {
            pos  = p,
            vel  = Vector3.zero,
            size = size,
            seed = Random.value * 1000f
        };
    }


void Update()
{
    // Resize to mass level (add/remove)
    int want = Mathf.RoundToInt(Mathf.Lerp(minDroplets, maxDroplets, massFill));
    if (want > drops.Count) { for (int i = drops.Count; i < want; i++) drops.Add(Spawn()); }
    else if (want < drops.Count) { drops.RemoveRange(want, drops.Count - want); }

    // Motion inputs
    // Use rb.velocity (Unity 2021+) instead of linearVelocity unless you’re on Unity 6
    Vector3 vel = rb ? rb.linearVelocity : Vector3.zero;
    float spd = vel.magnitude;
    Vector3 vdir = spd > 0.0001f ? vel / spd : Vector3.forward;

    float dt = Time.deltaTime;

    // Compute a world-space clamp bound that respects current non-uniform scale
    // (use the smallest axis so the cluster always fits the shell)
    Vector3 s = transform.lossyScale;
    float worldRadius = sphereRadius * Mathf.Min(s.x, Mathf.Min(s.y, s.z));
    float bound = worldRadius * 0.98f; // never touch shell
    float bias = biasTowardVel * Mathf.SmoothStep(0f, 1f, spd / 10f) * Mathf.Lerp(0.25f, 1.25f, massFill);

    // --- integrate all droplets first ---
    for (int i = 0; i < drops.Count; i++)
    {
        var d = drops[i];

        // Spring to center + bias in velocity direction + light jitter (Perlin)
        Vector3 acc = (-springK * d.pos) + (bias * vdir) + (jitter * new Vector3(
            Mathf.PerlinNoise(d.seed,             Time.time * 1.7f) - 0.5f,
            Mathf.PerlinNoise(d.seed + 77.2f,     Time.time * 2.1f) - 0.5f,
            Mathf.PerlinNoise(d.seed + 321.7f,    Time.time * 1.3f) - 0.5f));

        d.vel += acc * dt;
        d.vel *= Mathf.Clamp01(1f - damping * dt);
        d.pos += d.vel * dt;

        // Hard clamp inside sphere
        float r = d.pos.magnitude;
        if (r + d.size > bound)
        {
            Vector3 n = d.pos.normalized;
            d.pos = n * (bound - d.size);
            // reflect a bit
            d.vel = Vector3.Reflect(d.vel, -n) * 0.25f;
        }

        drops[i] = d;
    }

    // --- simple separation pass (1–2 iterations) after integration ---
    const int SEP_ITERS = 2;
    const float SEPARATION = 1.0f; // 1.0 = exactly sum of radii; >1 gives more gap
    for (int it = 0; it < SEP_ITERS; it++)
    {
        for (int a = 0; a < drops.Count; a++)
        {
            for (int b = a + 1; b < drops.Count; b++)
            {
                var da = drops[a];
                var db = drops[b];

                float minDist = (da.size + db.size) * SEPARATION;
                Vector3 delta = db.pos - da.pos;
                float dist = delta.magnitude;

                // BUGFIX: need braces so both statements run when dist < eps
                if (dist < 1e-5f) { delta = Random.onUnitSphere * 0.001f; dist = 0.001f; }

                if (dist < minDist)
                {
                    Vector3 n = delta / dist;
                    float push = (minDist - dist) * 0.5f;

                    // push both away from each other
                    da.pos -= n * push;
                    db.pos += n * push;

                    // keep inside shell (USE OUTER 'bound' — do not re-declare it)
                    if (da.pos.magnitude + da.size > bound) da.pos = da.pos.normalized * (bound - da.size);
                    if (db.pos.magnitude + db.size > bound) db.pos = db.pos.normalized * (bound - db.size);

                    drops[a] = da; drops[b] = db;
                }
            }
        }
    }

    // --- render ---
    if (!dropletMat || drops.Count == 0) return;

    // Team coloring: white inside vs black w/ white outline
    // (Enable GPU Instancing on the material in the inspector)
    if (teamWhite)
    {
        dropletMat.SetColor(_ColorMain, Color.white);
        dropletMat.SetFloat(_UseOutline, 0f);
    }
    else
    {
        dropletMat.SetColor(_ColorMain, Color.black);
        dropletMat.SetFloat(_UseOutline, 1f);
        dropletMat.SetFloat(_OutlinePx, outlinePx);
    }

    int batched = 0;
    var trs = transform.localToWorldMatrix;
    for (int i = 0; i < drops.Count; i++)
    {
        var d = drops[i];
        // camera-facing billboard (facing handled in the droplet shader)
        matrices[batched++] = trs * Matrix4x4.TRS(d.pos, Quaternion.identity, Vector3.one * d.size);
        if (batched == 1023 || i == drops.Count - 1)
        {
            Graphics.DrawMeshInstanced(quadMesh, 0, dropletMat, matrices, batched,
                null, UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer);
            batched = 0;
        }
    }
}

}
