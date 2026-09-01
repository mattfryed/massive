using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlayerArc : MonoBehaviour
{
    public Material mat;                   // flat color (team-dependent)
    public float arcRadiusOffset = 0.12f;  // outside the outline
    public float arcHalfAngle = 22f;       // degrees
    public AnimationCurve widthBySpeed =
        AnimationCurve.EaseInOut(0, 0.06f, 10, 0.12f);

    Mesh _mesh; Vector3[] _v; int[] _i;

    void Awake()
    {
        _mesh = new Mesh { name = "PlayerArc" };
        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    public void Rebuild(Vector2 vel, Vector3 centerWS, float bodyR)
    {
        int seg = 12;
        Ensure(seg);

        float speed = vel.magnitude;
        if (speed < 0.02f) { _mesh.Clear(); return; }

        float W = widthBySpeed.Evaluate(speed);
        float ang0 = -arcHalfAngle * Mathf.Deg2Rad, ang1 = +arcHalfAngle * Mathf.Deg2Rad;
        float heading = Mathf.Atan2(vel.y, vel.x);
        float R = bodyR + arcRadiusOffset;

        int vi = 0, ii = 0;
        for (int s = 0; s < seg; s++)
        {
            float a0 = Mathf.Lerp(ang0, ang1, (float)s / seg);
            float a1 = Mathf.Lerp(ang0, ang1, (float)(s + 1) / seg);
            float t0 = 1f - Mathf.Abs(Mathf.Lerp(-1, 1, (float)s / seg));
            float t1 = 1f - Mathf.Abs(Mathf.Lerp(-1, 1, (float)(s + 1) / seg));
            float w0 = W * (0.6f + 0.4f * t0);
            float w1 = W * (0.6f + 0.4f * t1);

            Vector2 n0 = new(Mathf.Cos(heading + a0), Mathf.Sin(heading + a0));
            Vector2 n1 = new(Mathf.Cos(heading + a1), Mathf.Sin(heading + a1));

            Vector3 c0 = centerWS + new Vector3(n0.x, n0.y, 0) * R;
            Vector3 c1 = centerWS + new Vector3(n1.x, n1.y, 0) * R;
            Vector3 t0n = new Vector3(-n0.y, n0.x, 0);
            Vector3 t1n = new Vector3(-n1.y, n1.x, 0);

            Vector3 l0 = c0 + t0n * (w0 * 0.5f);
            Vector3 r0 = c0 - t0n * (w0 * 0.5f);
            Vector3 l1 = c1 + t1n * (w1 * 0.5f);
            Vector3 r1 = c1 - t1n * (w1 * 0.5f);

            _v[vi + 0] = l0; _v[vi + 1] = r0; _v[vi + 2] = l1; _v[vi + 3] = r1;
            _i[ii + 0] = vi + 0; _i[ii + 1] = vi + 2; _i[ii + 2] = vi + 1;
            _i[ii + 3] = vi + 2; _i[ii + 4] = vi + 3; _i[ii + 5] = vi + 1;

            vi += 4; ii += 6;
        }

        _mesh.Clear();
        _mesh.vertices = _v;
        _mesh.triangles = _i;
        _mesh.RecalculateBounds();
    }

    void Ensure(int seg)
    {
        int needV = seg * 4, needI = seg * 6;
        if (_v == null || _v.Length != needV) _v = new Vector3[needV];
        if (_i == null || _i.Length != needI) _i = new int[needI];
    }
}