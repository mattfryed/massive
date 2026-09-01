using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)] // after forces have been fed
public class GridTuningMixer : MonoBehaviour
{
    [System.Serializable]
    public struct Contribution
    {
        public float blend;        // 0..1
        public float springK;
        public float damping;
        public int   falloffMode;
        public float falloffExp;
        public float sharpness;
        public float maxSpeed;
    }

    public VectorGridGPU grid;

    // base snapshot from the grid on enable
    float baseSpringK, baseDamping, baseFallExp, baseSharp, baseMax;
    int   baseFallMode;

    readonly List<Contribution> _contribs = new();

    void OnEnable()
    {
        if (!grid) grid = FindFirstObjectByType<VectorGridGPU>();

        baseSpringK = grid ? grid.SpringK : 3f;
        baseDamping = grid ? grid.Damping : 0.97f;

        // If you store these privately on VectorGridGPU, you can reflect them; otherwise keep sensible defaults:
        baseFallMode = 3;   // Gaussian
        baseFallExp  = 1.25f;
        baseSharp    = 3.0f;
        baseMax      = 12f;
    }

    public void Add(Contribution c) => _contribs.Add(c);

    void LateUpdate()
    {
        if (!grid) return;

        float springK = baseSpringK;
        float damping = baseDamping;
        int   fall    = baseFallMode;
        float fexp    = baseFallExp;
        float sharp   = baseSharp;
        float maxSpd  = baseMax;

        // Simple “strongest takes precedence” blend (highest blend wins)
        float best = -1f;
        for (int i = 0; i < _contribs.Count; i++)
        {
            var c = _contribs[i];
            if (c.blend <= best) continue;
            best   = c.blend;
            springK = Mathf.Lerp(baseSpringK, c.springK, c.blend);
            damping = Mathf.Lerp(baseDamping, c.damping, c.blend);
            fall    = (c.blend >= 1f) ? c.falloffMode : baseFallMode; // switch at full blend
            fexp    = Mathf.Lerp(baseFallExp, c.falloffExp, c.blend);
            sharp   = Mathf.Lerp(baseSharp,    c.sharpness, c.blend);
            maxSpd  = Mathf.Lerp(baseMax,      c.maxSpeed,  c.blend);
        }

        // Push back into the grid (your Update() already forwards these to the compute)
        grid.SpringK = springK;
        grid.Damping = damping;

        // If your VectorGridGPU exposes backing fields, set them; otherwise add a small Apply() method there.
        SetPrivate(grid, "_falloffMode", fall);
        SetPrivate(grid, "_falloffExp",  fexp);
        SetPrivate(grid, "_sharpness",   sharp);
        SetPrivate(grid, "_maxSpeed",    maxSpd);

        _contribs.Clear(); // important: contributions are per-frame
    }

    // light reflection helpers (same pattern you used before)
    static void SetPrivate(object o, string field, float v)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(float)) f.SetValue(o, v);
    }
    static void SetPrivate(object o, string field, int v)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(int)) f.SetValue(o, v);
    }
}
