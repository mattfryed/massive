using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlayerNuggetsMesh : MonoBehaviour
{
    [Header("Counts / Size")]
    public int maxNuggets = 2000;
    public int visibleCount = 800;
    public float dotSize = 0.04f;   // world diameter of each dot

    [Header("Shape")]
    public float blobRadius = 0.6f;    // set from controller (≈ baseRadius)
    public float packRadius = 0.56f;   // working radius for seeds (slightly smaller)

    [Header("Motion")]
    public Vector2 velocityWS;     // XZ (x,z)
    public float jitterAmp = 0.12f;
    public float jitterFreq = 6.0f;
    public float bounceDamp = 0.65f;

    [Header("Mass feel")]
    public float centerSpring = 18f;   // pull toward seeded rest (mass cohesion)
    public float radialDamping = 2.0f;  // damps outward radial velocity
    public float tangentialBias = 0.65f; // 0..1, more = slide around rim not into it
    public float rimSoftness = 0.08f; // inner soft wall thickness (in world units)
    public float rimRepel = 28f;   // strength of soft wall repulsion
    public float pullAccel = 8f;    // forward bunching (lower than before)
    public float sloshFactor = 0.05f; // smaller; we’ll project out radial
    public float spinAccel = 2.0f;  // gentle swirl
    public float drag = 1.8f;

    [Header("Collision with Rim")]
    public float outlineHalf = 0.03f;   // set from controller
    public float dotRadius = 0.02f;   // set from controller = dotSize*0.5

    Mesh _mesh;
    Vector3[] _verts;
    Vector2[] _uvs;
    int[] _tris;

    // per-dot sim state (local XZ plane, y=0)
    Vector2[] _pos, _vel, _seed;

    void Awake()
    {
        _mesh = new Mesh { name = "NuggetsMesh" };
        GetComponent<MeshFilter>().sharedMesh = _mesh;

        _verts = new Vector3[maxNuggets * 4];
        _uvs = new Vector2[maxNuggets * 4];
        _tris = new int[maxNuggets * 6];

        _pos = new Vector2[maxNuggets];
        _vel = new Vector2[maxNuggets];
        _seed = new Vector2[maxNuggets];

        // Blue-noise-ish disk (Vogel spiral)
        float golden = 2.39996323f;
        for (int i = 0; i < maxNuggets; i++)
        {
            float r = Mathf.Sqrt((i + 0.5f) / maxNuggets) * packRadius;
            float a = i * golden;
            _seed[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

            // start at seed (inside)
            _pos[i] = _seed[i];
            _vel[i] = Vector2.zero;
        }

        if (visibleCount <= 0) visibleCount = 500;
    }

    public void SetDotColor(Color c)
    {
        var mr = GetComponent<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", c);
        mr.SetPropertyBlock(mpb);
    }

    void LateUpdate()
    {
        int n = Mathf.Clamp(visibleCount, 0, maxNuggets);
        if (n == 0) { _mesh.Clear(); return; }

        float dt = Time.deltaTime;
        float t = Time.time;
        Vector2 v = velocityWS;

        float hs = dotSize * 0.5f;
        float keepR = Mathf.Max(0f, blobRadius - outlineHalf - dotRadius - 0.002f); // keep dot centers inside this radius

        int vi = 0, ti = 0;
        for (int i = 0; i < n; i++)
        {

            // --- build helpers ---
            float vLen = velocityWS.magnitude;
            Vector2 fwd = vLen > 1e-3f ? (velocityWS / vLen) : Vector2.zero;
            Vector2 p = _pos[i];
            float r = p.magnitude;
            Vector2 nrm = (r > 1e-6f) ? (p / r) : Vector2.right;

            // 1) Target around seed with small inertial slosh, BUT remove outward radial component
            Vector2 rawSlosh = -sloshFactor * velocityWS;
            Vector2 sloshTangential = rawSlosh - Vector2.Dot(rawSlosh, nrm) * nrm;  // no outward push
            Vector2 target = _seed[i] + sloshTangential
                           + jitterAmp * new Vector2(
                                Mathf.Sin(13.1f * _seed[i].x + jitterFreq * t),
                                Mathf.Sin(11.9f * _seed[i].y + 1.3f * jitterFreq * t));

            // 2) Cohesion spring toward target (mass stays blob-like)
            Vector2 acc = (target - p) * centerSpring;

            // 3) Forward bunching (keep modest so we don’t form a hard arc)
            acc += fwd * pullAccel;

            // 4) Gentle swirl around center to feel liquidy
            Vector2 tangential = new Vector2(-nrm.y, nrm.x);
            float spinSign = Mathf.Sign(Vector2.Dot(p, tangential));
            acc += tangential * (spinAccel * spinSign);

            // 5) Soft rim repulsion (inner “wall” of thickness rimSoftness)
            keepR = Mathf.Max(0f, blobRadius - outlineHalf - dotRadius - 0.002f);
            float dToWall = keepR - r;
            if (dToWall < rimSoftness) // near the wall
            {
                float k = 1f - Mathf.Clamp01(dToWall / rimSoftness); // 0 at safe, 1 at at/over rim
                acc += (-nrm) * (rimRepel * k);

                // remove outward radial velocity to prevent skating along the wall
                float vOut = Vector2.Dot(_vel[i], nrm);
                if (vOut > 0f) _vel[i] -= nrm * (vOut * (radialDamping));
            }

            // 6) Integrate
            _vel[i] += acc * dt;
            _vel[i] *= Mathf.Exp(-drag * dt);
            _pos[i] += _vel[i] * dt;

            // 7) Hard safety (in case any dot still escapes due to dt spikes)
            r = _pos[i].magnitude;
            if (r > keepR)
            {
                _pos[i] = (_pos[i] / r) * keepR;
                // reflect only the outward component a bit, but keep most tangential motion
                float vOut = Vector2.Dot(_vel[i], _pos[i].normalized);
                if (vOut > 0f) _vel[i] -= _pos[i].normalized * vOut * 0.8f;
            }

            // 8) Write the quad on the local XY plane (z = 0). We'll rotate to XZ in world.
            float cx = _pos[i].x;
            float cy = _pos[i].y;

            // vertices (XY plane)
            _verts[vi + 0] = new Vector3(cx - hs, cy - hs, 0f);
            _verts[vi + 1] = new Vector3(cx - hs, cy + hs, 0f);
            _verts[vi + 2] = new Vector3(cx + hs, cy + hs, 0f);
            _verts[vi + 3] = new Vector3(cx + hs, cy - hs, 0f);

            // UVs for round disc (shader clips by uv circle)
            _uvs[vi + 0] = new Vector2(0f, 0f);
            _uvs[vi + 1] = new Vector2(0f, 1f);
            _uvs[vi + 2] = new Vector2(1f, 1f);
            _uvs[vi + 3] = new Vector2(1f, 0f);

            // triangles
            _tris[ti + 0] = vi + 0; _tris[ti + 1] = vi + 1; _tris[ti + 2] = vi + 2;
            _tris[ti + 3] = vi + 0; _tris[ti + 4] = vi + 2; _tris[ti + 5] = vi + 3;

            vi += 4; ti += 6;
        }

        _mesh.Clear();
        _mesh.SetVertices(_verts, 0, vi);
        _mesh.SetUVs(0, _uvs, 0, vi);
        _mesh.SetTriangles(_tris, 0, ti, 0);

        // generous bounds so culling never clips
        float R = (blobRadius + dotSize) * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(R * 2f, 0.5f, R * 2f));
    }
}