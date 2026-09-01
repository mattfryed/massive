using UnityEngine;

[DisallowMultipleComponent]
public class GridTuningApplier : MonoBehaviour
{
    public VectorGridGPU grid;                 // assign or auto-find
    public GridTuningProfile tuning;
    [Range(0f,1f)] public float blend = 1f;    // 0=no effect, 1=fully apply
    public int priority = 0;                   // higher wins when multiple are active
    public bool applyEveryFrame = true;        // true = live drive (for anims); false = apply on enable/changes

    // Optional live modifiers (e.g., scale spring by speed)
    [Header("Live Modifiers (optional)")]
    public bool scaleSpringBySpeed = false;
    public float springPerUnitSpeed = 0.15f;   // add to springK per 1 m/s
    public Rigidbody rbForSpeed;               // if null, tries to find one

    void Reset()
    {
        if (!grid) grid = FindFirstObjectByType<VectorGridGPU>();
        if (!rbForSpeed) rbForSpeed = GetComponent<Rigidbody>();
    }

    void OnEnable() { ApplyNow(); }
    void Update() { if (applyEveryFrame) ApplyNow(); }

    public void ApplyNow()
    {
        if (!grid || !tuning || blend <= 0f) return;

        // Read current grid values (so we can blend)
        // NOTE: we read from fields directly; adjust names if yours differ.
        float springK = Mathf.Lerp(grid.SpringK, tuning.springK, blend);
        float damping = Mathf.Lerp(grid.Damping, tuning.damping, blend);

        float maxSpeed       = Mathf.Lerp(GetPrivate(grid, "_maxSpeed", tuning.maxSpeed), tuning.maxSpeed, blend);
        int   falloffMode    = (blend >= 1f) ? tuning.falloffMode : GetIntPrivate(grid, "_falloffMode", tuning.falloffMode);
        float falloffExp     = Mathf.Lerp(GetPrivate(grid, "_falloffExp", tuning.falloffExp), tuning.falloffExp, blend);
        float innerFrac      = Mathf.Lerp(GetPrivate(grid, "_innerFrac", tuning.innerFrac), tuning.innerFrac, blend);
        float sharpness      = Mathf.Lerp(GetPrivate(grid, "_sharpness", tuning.sharpness), tuning.sharpness, blend);
        float weightCap      = Mathf.Lerp(GetPrivate(grid, "_weightCap", tuning.weightCap), tuning.weightCap, blend);
        float crowdStiffness = Mathf.Lerp(GetPrivate(grid, "_crowdStiffness", tuning.crowdStiffness), tuning.crowdStiffness, blend);
        bool  pinEdges       = (blend >= 0.5f) ? tuning.pinEdges : GetBoolPrivate(grid, "_pinEdges", tuning.pinEdges);
        bool  hardCutoff     = (blend >= 0.5f) ? tuning.hardCutoff : GetBoolPrivate(grid, "_hardCutoff", tuning.hardCutoff);
        float cutoffSmooth   = Mathf.Lerp(GetPrivate(grid, "_cutoffSmooth", tuning.cutoffSmooth), tuning.cutoffSmooth, blend);
        float edgeFeatherCells = Mathf.Lerp(GetPrivate(grid, "_edgeFeatherCells", tuning.edgeFeatherCells), tuning.edgeFeatherCells, blend);
        float edgeFeatherExp   = Mathf.Lerp(GetPrivate(grid, "_edgeFeatherExp", tuning.edgeFeatherExp), tuning.edgeFeatherExp, blend);

        // Optional live tweak: add spring from speed
        if (scaleSpringBySpeed)
        {
            if (!rbForSpeed) rbForSpeed = GetComponent<Rigidbody>();
            float spd = rbForSpeed ? rbForSpeed.linearVelocity.magnitude : 0f;
            springK += springPerUnitSpeed * spd;
        }

        // Push into the grid via its public fields + uniform setters
        grid.SpringK = springK;
        grid.Damping = damping;

        // These next calls assume you set the compute uniforms each Update() in VectorGridGPU.
        // We just set the backing fields so your Update() passes them along.
        SetPrivate(grid, "_maxSpeed", maxSpeed);
        SetPrivate(grid, "_falloffMode", falloffMode);
        SetPrivate(grid, "_falloffExp", falloffExp);
        SetPrivate(grid, "_innerFrac", innerFrac);
        SetPrivate(grid, "_sharpness", sharpness);
        SetPrivate(grid, "_weightCap", weightCap);
        SetPrivate(grid, "_crowdStiffness", crowdStiffness);
        SetPrivate(grid, "_pinEdges", pinEdges);
        SetPrivate(grid, "_hardCutoff", hardCutoff);
        SetPrivate(grid, "_cutoffSmooth", cutoffSmooth);
        SetPrivate(grid, "_edgeFeatherCells", edgeFeatherCells);
        SetPrivate(grid, "_edgeFeatherExp", edgeFeatherExp);
    }

    // --- tiny reflection helpers so we don't modify VectorGridGPU ---
    float GetPrivate(object o, string field, float def)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(o);
        return def;
    }
    int GetIntPrivate(object o, string field, int def)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(o);
        return def;
    }
    bool GetBoolPrivate(object o, string field, bool def)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(o);
        return def;
    }
    void SetPrivate(object o, string field, float v)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(float)) f.SetValue(o, v);
    }
    void SetPrivate(object o, string field, int v)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(int)) f.SetValue(o, v);
    }
    void SetPrivate(object o, string field, bool v)
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        if (f != null && f.FieldType == typeof(bool)) f.SetValue(o, v);
    }
}
