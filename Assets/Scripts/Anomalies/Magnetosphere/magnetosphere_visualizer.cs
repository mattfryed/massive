using UnityEngine;
using System.Runtime.InteropServices;

public class MagnetosphereVisualizer : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader magnetosphereCompute;
    
    [Header("Particle Settings")]
    [SerializeField] private int particleCount = 50000;
    [SerializeField] private float particleSize = 0.02f;
    [SerializeField] private float particleDamping = 0.5f;
    [SerializeField] private float velocityLimit = 5.0f;
    
    [Header("Magnetosphere Settings")]
    [SerializeField] private float dipoleMoment = 10.0f;
    [SerializeField] private float magnetosphereRadius = 5.0f;
    [SerializeField] private Vector3 dipolePosition = Vector3.zero;
    
    [Header("Storm Settings")]
    [SerializeField] private float stormInterval = 8.0f;
    [SerializeField] private float stormDuration = 2.0f;
    [SerializeField] private float stormIntensity = 3.0f;
    
    [Header("Visual Settings")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private bool drawFieldLines = true;
    [SerializeField] private int fieldLineCount = 20;
    [SerializeField] private Color fieldLineColor = new Color(0.3f, 0.6f, 1.0f, 0.3f);

    [Header("Orientation")]
[SerializeField] private Vector3 dipoleAxis = Vector3.forward; // <-- set to Forward for side-on view

[Header("Field Line Rendering (Runtime)")]
[SerializeField] private bool renderFieldLinesRuntime = true;
[SerializeField] private Material fieldLineMaterial;
[SerializeField] private float fieldLineWidth = 0.03f;
[SerializeField] private int fieldLineSegments = 128;

private GameObject fieldLinesRoot;
private LineRenderer[] fieldLines;


    
    // Particle structure (must match compute shader)
    [StructLayout(LayoutKind.Sequential)]
    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float charge;
        public float energy;
        public float lifetime;
        public float maxLifetime;
    }
    
    private ComputeBuffer particleBuffer;
    private ComputeBuffer argsBuffer;
    private Mesh particleMesh;
    private Bounds bounds;
    
    private int initKernel;
    private int updateKernel;
    private int applyStormKernel;
    
    private float simulationTime;
    private float lastStormTime;
    private float currentStormTime;
    private Vector3 currentStormDirection;
    private bool stormActive;
    
    private void Start()
    {
        InitializeCompute();
        CreateParticleMesh();
        InitializeParticles();
        BuildOrRebuildFieldLines();

        
        bounds = new Bounds(dipolePosition, Vector3.one * magnetosphereRadius * 10);
        lastStormTime = -stormInterval;
        currentStormDirection = Vector3.forward;
    }
    
    private void InitializeCompute()
    {
        if (magnetosphereCompute == null)
        {
            Debug.LogError("MagnetosphereCompute shader is not assigned!");
            enabled = false;
            return;
        }
        
        // Get kernel indices
        initKernel = magnetosphereCompute.FindKernel("InitParticles");
        updateKernel = magnetosphereCompute.FindKernel("UpdateParticles");
        applyStormKernel = magnetosphereCompute.FindKernel("ApplyStorm");
        
        // Create particle buffer
        int stride = Marshal.SizeOf(typeof(Particle));
        particleBuffer = new ComputeBuffer(particleCount, stride);
        
        // Bind buffer to all kernels
        magnetosphereCompute.SetBuffer(initKernel, "particles", particleBuffer);
        magnetosphereCompute.SetBuffer(updateKernel, "particles", particleBuffer);
        magnetosphereCompute.SetBuffer(applyStormKernel, "particles", particleBuffer);
        
        // Set static parameters
        magnetosphereCompute.SetInt("particleCount", particleCount);
        magnetosphereCompute.SetFloat("particleDamping", particleDamping);
        magnetosphereCompute.SetFloat("velocityLimit", velocityLimit);
        magnetosphereCompute.SetFloat("dipoleMoment", dipoleMoment);
        magnetosphereCompute.SetFloat("magnetosphereRadius", magnetosphereRadius);
        magnetosphereCompute.SetVector("dipolePosition", dipolePosition);
        
        // Create args buffer for instanced rendering
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        dipoleAxis = (dipoleAxis == Vector3.zero) ? Vector3.up : dipoleAxis.normalized;
        magnetosphereCompute.SetVector("dipoleAxis", dipoleAxis);

    }

    private void BuildOrRebuildFieldLines()
{
    if (!renderFieldLinesRuntime || fieldLineCount <= 0) return;

    if (fieldLineMaterial == null)
        fieldLineMaterial = new Material(Shader.Find("Sprites/Default"));

    if (fieldLinesRoot != null) Destroy(fieldLinesRoot);
    fieldLinesRoot = new GameObject("MagnetosphereFieldLines");
    fieldLinesRoot.transform.SetParent(transform, false);

    fieldLines = new LineRenderer[fieldLineCount];

    var axis = (dipoleAxis == Vector3.zero ? Vector3.up : dipoleAxis.normalized);
    var rot = Quaternion.FromToRotation(Vector3.up, axis);

    // Choose "L-shell" values (outermost line reaches ~magnetosphereRadius)
    float Lmin = magnetosphereRadius * 0.25f;
    float Lmax = magnetosphereRadius * 0.95f;

    float thetaMin = 0.12f;              // avoid singularities near poles
    float thetaMax = Mathf.PI - 0.12f;

    for (int i = 0; i < fieldLineCount; i++)
    {
        float azimuth = (i / (float)fieldLineCount) * Mathf.PI * 2f;
        float t = (fieldLineCount == 1) ? 0f : i / (float)(fieldLineCount - 1);
        float L = Mathf.Lerp(Lmin, Lmax, t);

        var go = new GameObject($"FieldLine_{i:00}");
        go.transform.SetParent(fieldLinesRoot.transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = fieldLineMaterial;
        lr.widthMultiplier = fieldLineWidth;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.startColor = fieldLineColor;
        lr.endColor = fieldLineColor;

        lr.positionCount = fieldLineSegments;

        for (int s = 0; s < fieldLineSegments; s++)
        {
            float u = (fieldLineSegments == 1) ? 0f : s / (float)(fieldLineSegments - 1);
            float theta = Mathf.Lerp(thetaMin, thetaMax, u);

            // Dipole field line equation (in dipole-local spherical coords):
            // r = L * sin^2(theta)
            float sinT = Mathf.Sin(theta);
            float r = L * sinT * sinT;

            // Convert to Cartesian in dipole-local space (axis = +Y)
            float x = r * sinT * Mathf.Cos(azimuth);
            float y = r * Mathf.Cos(theta);
            float z = r * sinT * Mathf.Sin(azimuth);

            Vector3 world = dipolePosition + rot * new Vector3(x, y, z);
            lr.SetPosition(s, world);
        }

        fieldLines[i] = lr;
    }
}

    
    private void CreateParticleMesh()
    {
        // Create a simple quad mesh for particles
        particleMesh = new Mesh();
        particleMesh.name = "ParticleQuad";
        
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0)
        };
        
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
        
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        
        particleMesh.vertices = vertices;
        particleMesh.triangles = triangles;
        particleMesh.uv = uv;
        particleMesh.RecalculateNormals();
        particleMesh.RecalculateBounds();
    }
    
    private void InitializeParticles()
    {
        magnetosphereCompute.SetFloat("time", Random.value * 1000f);
        int threadGroups = Mathf.CeilToInt(particleCount / 256f);
        magnetosphereCompute.Dispatch(initKernel, threadGroups, 1, 1);
    }
    
    private void Update()
    {
        if (particleBuffer == null || particleMaterial == null) return;
        
        simulationTime += Time.deltaTime;
        
        // Storm management
        if (simulationTime - lastStormTime > stormInterval)
        {
            StartNewStorm();
        }
        
        if (stormActive)
        {
            currentStormTime += Time.deltaTime;
            if (currentStormTime > stormDuration)
            {
                stormActive = false;
            }
        }
        
        UpdateCompute();
        RenderParticles();
    }
    
    private void UpdateCompute()
    {
        // Update dynamic parameters
        magnetosphereCompute.SetFloat("time", simulationTime);
        magnetosphereCompute.SetFloat("deltaTime", Time.deltaTime);
        magnetosphereCompute.SetVector("stormDirection", currentStormDirection);
        magnetosphereCompute.SetFloat("stormIntensity", stormIntensity);
        magnetosphereCompute.SetFloat("stormActive", stormActive ? 1f : 0f);
        
        int threadGroups = Mathf.CeilToInt(particleCount / 256f);
        
        // Apply storm if active
        if (stormActive)
        {
            magnetosphereCompute.Dispatch(applyStormKernel, threadGroups, 1, 1);
        }
        
        // Update particles
        magnetosphereCompute.Dispatch(updateKernel, threadGroups, 1, 1);
    }
    
    private void RenderParticles()
    {
        // Setup material for instanced rendering
        particleMaterial.SetBuffer("particles", particleBuffer);
        particleMaterial.SetFloat("_ParticleSize", particleSize);
        particleMaterial.SetVector("_DipolePosition", dipolePosition);
        
        // Setup args buffer
        uint[] args = new uint[5]
        {
            particleMesh.GetIndexCount(0),
            (uint)particleCount,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };
        argsBuffer.SetData(args);
        
        // Draw instanced
        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            particleMaterial,
            bounds,
            argsBuffer,
            castShadows: UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows: false,
            layer: gameObject.layer
        );
    }
    
    private void StartNewStorm()
    {
        // Random direction on XZ plane
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        currentStormDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        
        stormActive = true;
        currentStormTime = 0f;
        lastStormTime = simulationTime;
        
        Debug.Log($"Cosmic storm incoming from direction: {currentStormDirection}");
    }
    
    // Public method to manually trigger storms from gameplay
    public void TriggerStorm(Vector3 direction)
    {
        currentStormDirection = new Vector3(direction.x, 0, direction.z).normalized;
        if (currentStormDirection == Vector3.zero)
        {
            currentStormDirection = Vector3.forward;
        }
        
        stormActive = true;
        currentStormTime = 0f;
        lastStormTime = simulationTime;
        
        Debug.Log($"Manual storm triggered from direction: {currentStormDirection}");
    }
    
    // Public method to adjust magnetosphere strength
    public void SetDipoleMoment(float moment)
    {
        dipoleMoment = Mathf.Max(0.1f, moment);
        magnetosphereCompute.SetFloat("dipoleMoment", dipoleMoment);
    }
    
    // Public method to adjust magnetosphere size
    public void SetMagnetosphereRadius(float radius)
    {
        magnetosphereRadius = Mathf.Max(1f, radius);
        magnetosphereCompute.SetFloat("magnetosphereRadius", magnetosphereRadius);
        bounds = new Bounds(dipolePosition, Vector3.one * magnetosphereRadius * 10);
    }
    
    private void OnDrawGizmos()
    {
        if (!drawFieldLines) return;
        
        // Draw magnetosphere boundary
        Gizmos.color = fieldLineColor;
        
        // Draw boundary sphere
        DrawWireSphere(dipolePosition, magnetosphereRadius, 32);
        
        // Draw dipole field lines
        for (int i = 0; i < fieldLineCount; i++)
        {
            float angle = (i / (float)fieldLineCount) * 360f * Mathf.Deg2Rad;
            DrawFieldLine(angle);
        }
        
        // Draw storm direction if active
        if (Application.isPlaying && stormActive)
        {
            Gizmos.color = Color.red;
            Vector3 stormStart = dipolePosition + currentStormDirection * magnetosphereRadius * 2f;
            Gizmos.DrawRay(stormStart, -currentStormDirection * magnetosphereRadius);
            Gizmos.DrawWireSphere(stormStart, 0.5f);
        }
    }
    
    private void DrawFieldLine(float angle)
    {
        int segments = 50;
        Vector3 prevPoint = Vector3.zero;
        
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            
            // Parametric dipole field line
            float r = magnetosphereRadius * 0.3f * Mathf.Sin(t * Mathf.PI);
            r = Mathf.Max(r, 0.1f);
            
            float theta = Mathf.Lerp(0, Mathf.PI, t);
            
            Vector3 point = dipolePosition + new Vector3(
                r * Mathf.Sin(theta) * Mathf.Cos(angle),
                r * Mathf.Cos(theta) * 2f, // Stretch vertically
                r * Mathf.Sin(theta) * Mathf.Sin(angle)
            );
            
            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }
            
            prevPoint = point;
        }
    }
    
    private void DrawWireSphere(Vector3 center, float radius, int segments)
    {
        // Draw circles on three planes
        DrawCircle(center, radius, segments, Vector3.up);
        DrawCircle(center, radius, segments, Vector3.right);
        DrawCircle(center, radius, segments, Vector3.forward);
    }
    
    private void DrawCircle(Vector3 center, float radius, int segments, Vector3 normal)
    {
        Vector3 forward = Vector3.Slerp(Vector3.up, Vector3.forward, 0.5f);
        if (Vector3.Dot(normal, forward) > 0.9f)
        {
            forward = Vector3.right;
        }
        
        Vector3 right = Vector3.Cross(normal, forward).normalized;
        forward = Vector3.Cross(right, normal).normalized;
        
        Vector3 prevPoint = center + right * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
            Vector3 point = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
    
    private void OnDestroy()
    {
        if (particleBuffer != null)
        {
            particleBuffer.Release();
            particleBuffer = null;
        }
        
        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
        
        if (particleMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(particleMesh);
            }
            else
            {
                DestroyImmediate(particleMesh);
            }
        }
    }
    
    private void OnDisable()
    {
        // Clean up buffers when disabled
        if (particleBuffer != null)
        {
            particleBuffer.Release();
            particleBuffer = null;
        }
        
        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
    }
}