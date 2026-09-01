using UnityEngine;

/// <summary>
/// GPU-driven bubbling core for NOVA.
/// Adds "reveal" control so you can animate intro/outro without scaling the object:
/// - Active particle count ramps 0..particleCount
/// - Simulation core radius ramps 0..coreRadius (collapse/expand)
/// - Optional render fade/size ramp
/// </summary>
[ExecuteAlways]
public class NovaCoreGPU : MonoBehaviour
{
    [Header("Assets")]
    public ComputeShader simCompute;       // NovaCoreSim.compute
    public Material      instanceMaterial; // Using MASSIVE/NovaCoreInstanced
    public Mesh          quadMesh;         // simple quad (e.g. built-in quad)

    [Header("Particles (MAX / BASE)")]
    [Tooltip("Maximum particle capacity. Reveal controls how many are active.")]
    public int   particleCount = 512;
    [Tooltip("Base core radius at full reveal. Reveal scales this down to 0.")]
    public float coreRadius    = 1.0f;

    [Header("Forces")]
    public float centerSpring     = 40f;
    public float drag             = 4f;
    public float glueRadius       = 0.6f;
    public float glueStrength     = 20f;
    public float repelRadius      = 0.25f;
    public float repelStrength    = 80f;
    public float jitterStrength   = 10f;

    [Header("Rendering")]
    public Color particleColor    = Color.white;
    public float particleSize     = 0.15f;
    public float softness         = 1.0f;
    public Bounds drawBounds      = new Bounds(Vector3.zero, Vector3.one * 10f);

    [Header("Reveal")]
    [Range(0f, 1f)]
    public float reveal01 = 1f;

    [Tooltip("If true, alpha scales with reveal (nice fade-in).")]
    public bool fadeWithReveal = true;

    [Tooltip("If true, particle size scales with reveal (nice grow-in).")]
    public bool sizeWithReveal = true;

    int _kernel;
    ComputeBuffer _positionsBuffer;
    ComputeBuffer _velocitiesBuffer;
    ComputeBuffer _argsBuffer;
    bool _initialized;

    // cached base values (from inspector)
    int   _maxCount;
    float _baseRadius;
    float _baseSize;
    Color _baseColor;

    // cached runtime
    int _activeCountCached = -1;
    float _prevReveal = -1f;
    readonly uint[] _args = new uint[5];

    static readonly int ID_Positions      = Shader.PropertyToID("_Positions");
    static readonly int ID_Velocities     = Shader.PropertyToID("_Velocities");
    static readonly int ID_ParticleCount  = Shader.PropertyToID("_ParticleCount");
    static readonly int ID_Dt             = Shader.PropertyToID("_Dt");
    static readonly int ID_Time           = Shader.PropertyToID("_Time");
    static readonly int ID_CenterSpring   = Shader.PropertyToID("_CenterSpring");
    static readonly int ID_Drag           = Shader.PropertyToID("_Drag");
    static readonly int ID_GlueRadius     = Shader.PropertyToID("_GlueRadius");
    static readonly int ID_GlueStrength   = Shader.PropertyToID("_GlueStrength");
    static readonly int ID_RepelRadius    = Shader.PropertyToID("_RepelRadius");
    static readonly int ID_RepelStrength  = Shader.PropertyToID("_RepelStrength");
    static readonly int ID_JitterStrength = Shader.PropertyToID("_JitterStrength");
    static readonly int ID_CoreRadius     = Shader.PropertyToID("_CoreRadius");

    void OnEnable()
    {
        InitIfNeeded();
        // Force apply reveal on enable.
        _prevReveal = -1f;
    }

    void OnDisable() => ReleaseBuffers();
    void OnDestroy() => ReleaseBuffers();

    /// <summary>
    /// Called by your transition to drive intro/outro.
    /// </summary>
    public void SetReveal01(float t)
    {
        reveal01 = Mathf.Clamp01(t);
    }

    void InitIfNeeded()
    {
        if (_initialized) return;

        if (simCompute == null || instanceMaterial == null || quadMesh == null)
        {
            Debug.LogWarning("[NovaCoreGPU] Missing references.", this);
            return;
        }

        _maxCount   = Mathf.Max(1, particleCount);
        _baseRadius = Mathf.Max(0.0001f, coreRadius);
        _baseSize   = Mathf.Max(0.0001f, particleSize);
        _baseColor  = particleColor;

        _kernel = simCompute.FindKernel("CSMain");

        _positionsBuffer  = new ComputeBuffer(_maxCount, sizeof(float) * 2);
        _velocitiesBuffer = new ComputeBuffer(_maxCount, sizeof(float) * 2);

        // Init particles inside base radius (we’ll collapse them via reveal when needed)
        ResetParticles(_baseRadius);

        // Bind buffers to compute
        simCompute.SetBuffer(_kernel, ID_Positions, _positionsBuffer);
        simCompute.SetBuffer(_kernel, ID_Velocities, _velocitiesBuffer);

        // Bind to material
        instanceMaterial.SetBuffer(ID_Positions, _positionsBuffer);

        // Setup indirect args buffer (instance count updated per-frame)
        _args[0] = quadMesh.GetIndexCount(0);
        _args[1] = 0; // active instances (set in Update)
        _args[2] = quadMesh.GetIndexStart(0);
        _args[3] = quadMesh.GetBaseVertex(0);
        _args[4] = 0;

        _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(_args);

        _initialized = true;
    }

    void ResetParticles(float radius)
    {
        radius = Mathf.Max(0.0001f, radius);

        var positions  = new Vector2[_maxCount];
        var velocities = new Vector2[_maxCount];

        for (int i = 0; i < _maxCount; i++)
        {
            float r = radius * 0.4f * Mathf.Sqrt(Random.value);
            float a = Random.value * Mathf.PI * 2f;
            positions[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            velocities[i] = Vector2.zero;
        }

        _positionsBuffer.SetData(positions);
        _velocitiesBuffer.SetData(velocities);
    }

    void ReleaseBuffers()
    {
        if (_positionsBuffer != null) { _positionsBuffer.Release(); _positionsBuffer = null; }
        if (_velocitiesBuffer != null) { _velocitiesBuffer.Release(); _velocitiesBuffer = null; }
        if (_argsBuffer != null) { _argsBuffer.Release(); _argsBuffer = null; }
        _initialized = false;
        _activeCountCached = -1;
    }

    void Update()
    {
        if (!_initialized)
        {
            InitIfNeeded();
            if (!_initialized) return;
        }

        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

        float reveal = Mathf.Clamp01(reveal01);

        // If reveal crosses from 0 → >0, reseed into tiny radius so it grows nicely.
        if (_prevReveal <= 0.0001f && reveal > 0.0001f)
        {
            ResetParticles(_baseRadius * reveal);
        }
        _prevReveal = reveal;

        int activeCount = Mathf.RoundToInt(_maxCount * reveal);
        activeCount = Mathf.Clamp(activeCount, 0, _maxCount);

        // Update args instance count only when it changes.
        if (activeCount != _activeCountCached)
        {
            _activeCountCached = activeCount;
            _args[1] = (uint)activeCount;
            _argsBuffer.SetData(_args);
        }

        // If nothing active, skip sim + draw.
        if (activeCount <= 0 || reveal <= 0.0001f)
            return;

        // Simulation radius collapses/expands with reveal (nice "zoom into core" effect)
        float simRadius = Mathf.Max(0.0001f, _baseRadius * reveal);

        // Set sim parameters
        simCompute.SetInt(ID_ParticleCount, activeCount);
        simCompute.SetFloat(ID_Dt, dt);
        simCompute.SetFloat(ID_Time, Time.time);
        simCompute.SetFloat(ID_CenterSpring, centerSpring * reveal);
        simCompute.SetFloat(ID_Drag, drag * reveal);
        simCompute.SetFloat(ID_GlueRadius, glueRadius * reveal);
        simCompute.SetFloat(ID_GlueStrength, glueStrength * reveal);
        simCompute.SetFloat(ID_RepelRadius, repelRadius * reveal);
        simCompute.SetFloat(ID_RepelStrength, repelStrength * reveal);
        simCompute.SetFloat(ID_JitterStrength, jitterStrength * reveal);
        simCompute.SetFloat(ID_CoreRadius, simRadius * reveal);

        int threadGroups = Mathf.CeilToInt(activeCount / 128f);
        if (threadGroups > 0)
            simCompute.Dispatch(_kernel, threadGroups, 1, 1);

        // Render params (optional fade/size with reveal)
        Color c = _baseColor;
        if (fadeWithReveal) c.a *= reveal;
        instanceMaterial.SetColor("_Color", c);

        float size = _baseSize;
        if (sizeWithReveal) size *= Mathf.Lerp(0.25f, 1f, reveal);
        instanceMaterial.SetFloat("_ParticleSize", size);

        instanceMaterial.SetFloat("_Softness", softness);

        Bounds worldBounds = new Bounds(transform.position + drawBounds.center, drawBounds.size);

        Graphics.DrawMeshInstancedIndirect(
            quadMesh,
            0,
            instanceMaterial,
            worldBounds,
            _argsBuffer,
            0,
            null,
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }
}
