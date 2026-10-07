using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Dynamo
{
    /// <summary>Intrinsic dipole plus a storm-driven residual from the recorded MHD field.</summary>
    [DisallowMultipleComponent]
    public sealed class DynamoScientificFieldRenderer : MonoBehaviour
    {
        [Header("Existing systems")]
        [SerializeField] private ScientificMagnetosphere source;
        [SerializeField] private MagnetosphereFieldLinesGPU2D gameplayField;
        [SerializeField] private ComputeShader tracer;
        [SerializeField] private Material lineMaterial;

        [Header("Recorded field tracing")]
        [SerializeField, Range(24, 192)] private int seedCount = 144;
        [SerializeField, Range(64, 320)] private int steps = 240;
        [SerializeField, Range(.05f, .5f)] private float stepRe = .22f;

        [Header("Remove the recorded wind stress")]
        [Tooltip("Signed equatorial coefficient in nT at 1 Re. This run's source-run.json specifies b_dipole = -3.11e-05 tesla.")]
        [SerializeField] private float internalDipoleCoefficientNt = -31100;
        [Tooltip("Gameplay pressure gain at which the recorded non-dipole residual is fully restored. This is not a pressure in nPa.")]
        [SerializeField, Range(.01f, 1)] private float recordedDisturbanceAtPressureGain = .35f;

        [Header("Arena presentation (art directed)")]
        [Tooltip("Maps model Earth radii into arena units; does not change the recorded field.")]
        [SerializeField, Min(.01f)] private float worldUnitsPerEarthRadius = .5f;
        [Tooltip("Retains real out-of-plane curvature at a reduced height for the top-down camera.")]
        [SerializeField, Range(0, 1)] private float depthScale = .3f;
        [SerializeField, Range(0, 1)] private float stormDeformation = .85f;
        [Tooltip("Time to turn the recorded disturbance toward a new downstream direction. The calm dipole stays fixed.")]
        [SerializeField, Min(.01f)] private float directionResponseSeconds = 1.25f;
        [SerializeField, Range(.5f, 4)] private float widthPixels = 1.8f;
        [SerializeField, Min(0)] private float brightness = 1.55f;
        [SerializeField, ColorUsage(false, true)] private Color innerColor = new Color(1, .38f, .64f);
        [SerializeField, ColorUsage(false, true)] private Color outerColor = new Color(.65f, .015f, .6f);
        [SerializeField, Range(0, 1)] private float backSideBrightness = .45f;

        private const int EnvelopeSamples = 257;
        private readonly Vector4[] envelopeScales = new Vector4[EnvelopeSamples];
        private ComputeBuffer segments, envelopeBuffer;
        private MaterialPropertyBlock properties;
        private ComputeShader traceInstance, originalTracer;
        private int kernel, capacity, previousSeeds, previousSteps;
        private Texture3D previousA, previousB;
        private float previousBlend = -1, previousStep;
        private Vector3 previousDipole, displayWind;
        private Vector3 previousCalmDipole, previousWind;
        private float previousExternalWeight = -1, previousCoefficient;
        private bool hasDirection;

        public ScientificMagnetosphere Source => source;
        public MagnetosphereFieldLinesGPU2D GameplayField => gameplayField;
        public int SegmentCapacity => capacity;
        public Vector3 DisplayWind => displayWind;
        public int TraceDispatchCount { get; private set; }
        public float ExternalDisturbance01 => gameplayField ? Mathf.Clamp01(gameplayField.pressureGain /
            Mathf.Max(.01f, recordedDisturbanceAtPressureGain)) : 0;
        public float ExtraCompression01 => gameplayField ? Mathf.InverseLerp(recordedDisturbanceAtPressureGain, 1,
            gameplayField.pressureGain) : 0;
        public Vector3 CalmDipoleAxis => gameplayField && gameplayField.dipoleAxis.sqrMagnitude > .0001f
            ? gameplayField.dipoleAxis.normalized : Vector3.forward;
        public float TraceDomainRadius { get; private set; }

        public bool CanRenderFor(MagnetosphereFieldLinesGPU2D field) => isActiveAndEnabled &&
            gameplayField == field && source && source.Ready && tracer && lineMaterial &&
            lineMaterial.shader && lineMaterial.shader.isSupported && SystemInfo.supportsComputeShaders;

        private void LateUpdate()
        {
            if (!gameplayField || !gameplayField.isActiveAndEnabled || !CanRenderFor(gameplayField))
            {
                ReleaseBuffers();
                return;
            }
            EnsureBuffers();

            Vector3 target = gameplayField.EffectiveWindDirection;
            target.y = 0;
            if (target.sqrMagnitude < .0001f) target = Vector3.right;
            target.Normalize();
            if (!hasDirection) { displayWind = target; hasDirection = true; }
            float angle = Mathf.Atan2(displayWind.z, displayWind.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(target.z, target.x) * Mathf.Rad2Deg;
            angle = Mathf.LerpAngle(angle, targetAngle, 1 - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, directionResponseSeconds)));
            displayWind = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad));
            TraceIfChanged();

            float extent = Mathf.Max(1, gameplayField.magnetosphereRadius * 4);
            for (int i = 0; i < EnvelopeSamples; i++)
            {
                float along = Mathf.Lerp(-extent, extent, i / (float)(EnvelopeSamples - 1));
                gameplayField.GetGameplayEnvelopeScales(along, out float a, out float b);
                envelopeScales[i] = new Vector4(a, b, 0, 0);
            }
            // Integrate positive axial scales instead of multiplying a point by its local scale.
            // This keeps the mapping monotonic, even at maximum compression: loops cannot fold
            // back through their footpoints solely because the presentation was compressed.
            int middle = EnvelopeSamples / 2;
            float spacing = 2 * extent / (EnvelopeSamples - 1);
            for (int i = middle + 1; i < EnvelopeSamples; i++)
                envelopeScales[i].z = envelopeScales[i - 1].z + (envelopeScales[i - 1].x + envelopeScales[i].x) * .5f * spacing;
            for (int i = middle - 1; i >= 0; i--)
                envelopeScales[i].z = envelopeScales[i + 1].z - (envelopeScales[i + 1].x + envelopeScales[i].x) * .5f * spacing;
            envelopeBuffer.SetData(envelopeScales);

            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetBuffer("_Segments", segments);
            properties.SetBuffer("_Envelope", envelopeBuffer);
            properties.SetInt("_EnvelopeCount", EnvelopeSamples);
            properties.SetFloat("_EnvelopeExtent", extent);
            properties.SetVector("_Center", gameplayField.dipolePosition);
            properties.SetVector("_DisplayWind", displayWind);
            properties.SetVector("_EnvelopeWind", target);
            properties.SetFloat("_WorldScale", worldUnitsPerEarthRadius);
            properties.SetFloat("_DepthScale", depthScale);
            // The recorded residual already includes wind stress. Additional deformation starts
            // only above its reference gameplay drive, avoiding compression counted twice.
            properties.SetFloat("_Deformation", stormDeformation * ExtraCompression01);
            properties.SetFloat("_InnerRadius", source.Episode.manifest.innerBoundaryRe);
            properties.SetFloat("_Radius", gameplayField.magnetosphereRadius);
            properties.SetFloat("_WidthPixels", widthPixels);
            properties.SetFloat("_Brightness", brightness * gameplayField.alpha);
            properties.SetColor("_NearColor", innerColor);
            properties.SetColor("_FarColor", outerColor);
            properties.SetFloat("_BackBrightness", backSideBrightness);
            var bounds = new Bounds(gameplayField.dipolePosition, Vector3.one * extent * 10);
            Graphics.DrawProcedural(lineMaterial, bounds, MeshTopology.Triangles, capacity * 6, 1,
                null, properties, ShadowCastingMode.Off, false, gameObject.layer);
        }

        private void EnsureBuffers()
        {
            int required = Mathf.Clamp(seedCount, 24, 192) * 2 * Mathf.Clamp(steps, 64, 320);
            if (segments == null || required != capacity)
            {
                ReleaseBuffers();
                capacity = required;
                segments = new ComputeBuffer(capacity, 32);
                envelopeBuffer = new ComputeBuffer(EnvelopeSamples, 16);
            }
            if (!traceInstance || originalTracer != tracer)
            {
                ReleaseTracer();
                traceInstance = Instantiate(tracer);
                traceInstance.hideFlags = HideFlags.HideAndDontSave;
                originalTracer = tracer;
                kernel = traceInstance.FindKernel("Trace");
                previousA = null;
            }
        }

        private void TraceIfChanged()
        {
            int seedsNow = Mathf.Clamp(seedCount, 24, 192), stepsNow = Mathf.Clamp(steps, 64, 320);
            float stepNow = Mathf.Clamp(stepRe, .05f, .5f);
            Vector3 dipole = source.DipoleAxisGsm;
            float externalWeight = ExternalDisturbance01;
            Vector3 calmDipole = CalmDipoleAxis;
            if (previousA == source.FirstB && previousB == source.SecondB &&
                previousBlend == source.FrameBlend && previousDipole == dipole &&
                previousSeeds == seedsNow && previousSteps == stepsNow && previousStep == stepNow &&
                previousExternalWeight == externalWeight && previousCalmDipole == calmDipole &&
                previousCoefficient == internalDipoleCoefficientNt &&
                (externalWeight == 0 || previousWind == displayWind)) return;

            var m = source.Episode.manifest;
            Vector3 high = m.originRe + Vector3.Scale(m.spacingRe, new Vector3(m.nx - 1, m.ny - 1, m.nz - 1));
            // A centered sphere inside the sample volume keeps the calm display domain symmetric
            // and valid for every wind orientation. This is a trace limit, not a magnetopause.
            float lowRadius = Mathf.Min(-m.originRe.x, Mathf.Min(-m.originRe.y, -m.originRe.z));
            float highRadius = Mathf.Min(high.x, Mathf.Min(high.y, high.z));
            TraceDomainRadius = Mathf.Min(lowRadius, highRadius) -
                .5f * Mathf.Max(m.spacingRe.x, Mathf.Max(m.spacingRe.y, m.spacingRe.z));
            traceInstance.SetTexture(kernel, "_FieldA", source.FirstB);
            traceInstance.SetTexture(kernel, "_FieldB", source.SecondB);
            traceInstance.SetBuffer(kernel, "_Segments", segments);
            traceInstance.SetVector("_Origin", m.originRe);
            traceInstance.SetVector("_Spacing", m.spacingRe);
            traceInstance.SetVector("_Dimensions", new Vector3(m.nx, m.ny, m.nz));
            traceInstance.SetVector("_SourceDipole", dipole);
            traceInstance.SetVector("_CalmDipole", calmDipole);
            traceInstance.SetVector("_Wind", displayWind);
            traceInstance.SetFloat("_DipoleCoefficient", internalDipoleCoefficientNt);
            traceInstance.SetFloat("_ExternalWeight", externalWeight);
            traceInstance.SetFloat("_OuterRadius", TraceDomainRadius);
            traceInstance.SetFloat("_Blend", source.FrameBlend);
            traceInstance.SetFloat("_InnerRadius", m.innerBoundaryRe);
            traceInstance.SetFloat("_Step", stepNow);
            traceInstance.SetInt("_SeedCount", seedsNow);
            traceInstance.SetInt("_Steps", stepsNow);
            traceInstance.Dispatch(kernel, Mathf.CeilToInt(seedsNow / 64f), 1, 1);
            previousA = source.FirstB; previousB = source.SecondB;
            previousBlend = source.FrameBlend; previousDipole = dipole;
            previousSeeds = seedsNow; previousSteps = stepsNow; previousStep = stepNow;
            previousExternalWeight = externalWeight; previousCalmDipole = calmDipole;
            previousWind = displayWind; previousCoefficient = internalDipoleCoefficientNt;
            TraceDispatchCount++;
        }

        public static Vector3 DipoleField(Vector3 positionRe, Vector3 axis, float coefficientNt)
        {
            float radius = positionRe.magnitude;
            if (radius < .0001f) return Vector3.zero;
            Vector3 radial = positionRe / radius;
            return coefficientNt * (3 * Vector3.Dot(axis, radial) * radial - axis) / (radius * radius * radius);
        }

        /// <summary>Independent CPU sampling for validation and diagnostics, in unscaled arena axes.</summary>
        public bool TrySampleReconstructedField(Vector3 positionRe, out Vector3 fieldNt)
        {
            fieldNt = DipoleField(positionRe, CalmDipoleAxis, internalDipoleCoefficientNt);
            if (!source || !source.Ready || positionRe.magnitude < source.Episode.manifest.innerBoundaryRe ||
                positionRe.magnitude > TraceDomainRadius) return false;
            float weight = ExternalDisturbance01;
            if (weight == 0) return true;
            Vector3 across = new Vector3(-displayWind.z, 0, displayWind.x);
            Vector3 gsm = new Vector3(-Vector3.Dot(positionRe, displayWind), positionRe.y, Vector3.Dot(positionRe, across));
            if (!source.TrySampleWorld(source.GsmToWorld.MultiplyPoint3x4(gsm), out var sample)) return false;
            Vector3 residual = sample.magneticFieldNt - DipoleField(gsm, source.DipoleAxisGsm, internalDipoleCoefficientNt);
            fieldNt += weight * (-displayWind * residual.x + Vector3.up * residual.y + across * residual.z);
            return true;
        }

        private void OnDisable()
        {
            ReleaseBuffers();
            ReleaseTracer();
            hasDirection = false;
        }

        private void ReleaseTracer()
        {
            if (traceInstance)
            {
                if (Application.isPlaying) Destroy(traceInstance);
                else DestroyImmediate(traceInstance);
            }
            traceInstance = null; originalTracer = null;
        }

        private void ReleaseBuffers()
        {
            segments?.Release(); segments = null;
            envelopeBuffer?.Release(); envelopeBuffer = null;
            capacity = 0; previousA = null; previousB = null;
        }
    }
}
