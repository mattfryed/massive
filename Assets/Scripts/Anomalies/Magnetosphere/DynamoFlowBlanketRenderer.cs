using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Massive.Dynamo
{
    /// <summary>GPU paths and arc-length streaks. No particle simulation or normal-frame readbacks.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DynamoFlowBlanketRenderer : MonoBehaviour
    {
        [Header("Sources")]
        public DynamoStormController controller;
        public MagnetosphereFieldLinesGPU2D field;
        public ComputeShader flowCompute;
        public Material flowMaterial;

        [Header("Blanket Detail")]
        [Tooltip("Solid, uniform-color diamonds taper to zero width at both ends. Storm fades shrink their width instead of their opacity.")]
        public bool opaqueTaperedStreaks;
        [Tooltip("Contribution of the sharp streak layer. Zero shows only bloom; one preserves the full solid streak. Does not change the bloom source.")]
        [Range(0f, 1f)] public float sharpCoreIntensity = 1f;
        [Range(2f, 24f)] public float pathsPerWorldUnit = 12f;
        [Range(64, 768)] public int maxPaths = 512;
        [Range(64, 512)] public int samplesPerPath = 192;
        [Range(16, 256)] public int maxStreaksPerPath = 128;
        [Min(.08f)] public float streakSpacing = .65f;
        [Min(.01f)] public float streakLength = .32f;
        [Min(.005f)] public float streakWidth = .035f;
        [Range(0f, 1f)] public float organicMotion = .25f;
        [Min(.01f)] public float boundaryClearance = .07f;
        [Range(.05f, 1f)] public float directionTransitionSeconds = .3f;
        [ColorUsage(true, true)] public Color color = new Color(.34f, .96f, .86f, 1f);
        [Range(0f, 4f)] public float brightness = 1.1f;

        private const int ProfileSamples = 256;
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Dynamo.FlowBlanket.Update");
        private sealed class Layout
        {
            public ComputeBuffer paths, streaks, count, args;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public DynamoStormState state;
            public int lanes;
            public bool valid;
            public void Release() { paths?.Release(); streaks?.Release(); count?.Release(); args?.Release(); }
        }
        private Layout current, outgoing;
        private ComputeBuffer profile;
        private readonly Vector2[] profileData = new Vector2[ProfileSamples];
        private readonly Vector4[] corners = new Vector4[4];
        private int allocatedPaths, allocatedNodes, allocatedSlots;
        private int buildPaths, buildStreaks, buildArguments;
        private float transitionStarted;
        private Vector4 profileRange;
        private bool reportedMissing;
        private Material drawMaterial, bloomMaterial, materialSource;
        private Camera drawCamera, attachedCamera;
        private CommandBuffer coreCommands;
        private bool previewRendering;
        public bool HasCoreCommands => coreCommands != null;
        private bool hasDraws;
        private float currentOpacity, outgoingOpacity;
        public int BufferAllocationCount { get; private set; }
        public bool HasBuffers => profile != null;
        public bool ExternalPresentation { get; set; }

        // Cached property IDs avoid string lookups in the presentation loop.
        private static readonly int Paths = Shader.PropertyToID("_Paths"), Envelope = Shader.PropertyToID("_Envelope"),
            Streaks = Shader.PropertyToID("_Streaks"), Count = Shader.PropertyToID("_Count"), Arguments = Shader.PropertyToID("_Arguments"),
            LaneCount = Shader.PropertyToID("_LaneCount"), NodeCount = Shader.PropertyToID("_NodeCount"), Slots = Shader.PropertyToID("_SlotsPerLane"),
            Flow = Shader.PropertyToID("_Flow"), Across = Shader.PropertyToID("_Across"), Center = Shader.PropertyToID("_Center"),
            Domain = Shader.PropertyToID("_Domain"), Corners = Shader.PropertyToID("_Corners"), ProfileRange = Shader.PropertyToID("_ProfileRange"),
            Travel = Shader.PropertyToID("_Travel"), MotionTime = Shader.PropertyToID("_MotionTime"), Spacing = Shader.PropertyToID("_Spacing"),
            Length = Shader.PropertyToID("_StreakLength"), Clearance = Shader.PropertyToID("_Clearance"), Jitter = Shader.PropertyToID("_Jitter"),
            ColorId = Shader.PropertyToID("_Color"), Opacity = Shader.PropertyToID("_Opacity"), Width = Shader.PropertyToID("_Width"),
            Front = Shader.PropertyToID("_Front"), Feather = Shader.PropertyToID("_Feather"), Wind = Shader.PropertyToID("_Wind"), FrontFlow = Shader.PropertyToID("_FrontFlow"),
            Brightness = Shader.PropertyToID("_Brightness"), DstBlend = Shader.PropertyToID("_DstBlend"), OpaqueStreaks = Shader.PropertyToID("_OpaqueStreaks"),
            CoreIntensity = Shader.PropertyToID("_CoreIntensity");

        private void OnEnable() => Camera.onPreRender += BeforeCameraRender;

        private void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject) || ExternalPresentation) return;
            using (UpdateMarker.Auto()) RenderFrame();
        }

        private void RenderFrame()
        {
            previewRendering = false;
            drawCamera = controller ? controller.StormCamera : null;
            BuildFrame(controller ? controller.CurrentState : default);
        }

        // Explicitly driven by an isolated editor preview; never advances gameplay.
        public void BuildVisualPreview(DynamoStormState state, Camera camera)
        {
            previewRendering = true;
            drawCamera = camera;
            if (current != null) current.valid = outgoing.valid = false;
            BuildFrame(state);
        }

        private void BuildFrame(DynamoStormState state)
        {
            hasDraws = false;
            if ((!previewRendering && !controller) || !field || !flowCompute || !flowMaterial || !SystemInfo.supportsComputeShaders)
            {
                if (!reportedMissing) Debug.LogError("Dynamo flow blanket requires its controller, field, compute shader and material on a compute-capable platform.", this);
                reportedMissing = true;
                return;
            }
            if ((!previewRendering && !controller.isActiveAndEnabled) || !state.Active || state.Opacity <= 0f)
            {
                if (current != null) current.valid = outgoing.valid = false;
                return;
            }
            EnsureBuffers();
            EnsureDrawMaterial();
            if (current.valid && current.state.Revision != state.Revision)
            {
                var swap = outgoing; outgoing = current; current = swap;
                current.valid = false;
                transitionStarted = state.Elapsed;
            }
            UploadEnvelope(state);
            BuildLayout(current, state);
            float blend = outgoing.valid ? Mathf.Clamp01((state.Elapsed - transitionStarted) / Mathf.Max(.05f, directionTransitionSeconds)) : 1f;
            currentOpacity = state.Opacity * blend;
            outgoingOpacity = state.Opacity * (1f - blend);
            if (outgoing.valid && blend < 1f)
            {
                GenerateStreaks(outgoing, state.Travel, state.Elapsed);
                Draw(outgoing, outgoingOpacity);
            }
            else outgoing.valid = false;
            GenerateStreaks(current, state.Travel, state.Elapsed);
            Draw(current, currentOpacity);
            hasDraws = true;
        }

        private void EnsureDrawMaterial()
        {
            if (!drawMaterial || materialSource != flowMaterial)
            {
                DestroyMaterial(drawMaterial); DestroyMaterial(bloomMaterial);
                drawMaterial = new Material(flowMaterial) { name = "Dynamo flow presentation (runtime)", hideFlags = HideFlags.HideAndDontSave };
                bloomMaterial = new Material(flowMaterial) { name = "Dynamo flow bloom source (runtime)", hideFlags = HideFlags.HideAndDontSave };
                materialSource = flowMaterial;
            }
            drawMaterial.SetInt(DstBlend, opaqueTaperedStreaks ? (int)BlendMode.Zero : (int)BlendMode.One);
            drawMaterial.SetFloat(OpaqueStreaks, opaqueTaperedStreaks ? 1f : 0f);
            drawMaterial.SetFloat(CoreIntensity, Mathf.Clamp01(sharpCoreIntensity));
            bloomMaterial.SetInt(DstBlend, opaqueTaperedStreaks ? (int)BlendMode.Zero : (int)BlendMode.One);
            bloomMaterial.SetFloat(OpaqueStreaks, opaqueTaperedStreaks ? 1f : 0f);
            bloomMaterial.SetFloat(CoreIntensity, 1f);
        }

        public bool CanRecordBloom => isActiveAndEnabled && hasDraws && drawMaterial;

        // Reuse this frame's exact geometry, color and clipping in an isolated bloom source.
        public void RecordBloomSource(CommandBuffer commands)
        {
            if (!CanRecordBloom) return;
            RecordStreaks(commands, bloomMaterial);
        }

        private void RecordStreaks(CommandBuffer commands, Material material)
        {
            if (outgoing.valid && outgoingOpacity > 0f)
                commands.DrawProceduralIndirect(Matrix4x4.identity, material, 0, MeshTopology.Triangles, outgoing.args, 0, outgoing.properties);
            if (current.valid && currentOpacity > 0f)
                commands.DrawProceduralIndirect(Matrix4x4.identity, material, 0, MeshTopology.Triangles, current.args, 0, current.properties);
        }

        private void BeforeCameraRender(Camera camera)
        {
            if (attachedCamera && attachedCamera != drawCamera) DetachCore();
            if (camera != drawCamera) return;
            if ((!Application.IsPlaying(gameObject) && !previewRendering) || !CanRecordBloom || sharpCoreIntensity <= 0f)
            {
                DetachCore();
                return;
            }
            EnsureDrawMaterial();
            if (coreCommands == null)
            {
                coreCommands = new CommandBuffer { name = "Dynamo: sharp flow core" };
                attachedCamera = camera;
                camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, coreCommands);
            }
            // Camera-driven submission also runs during Editor pause, just like bloom.
            coreCommands.Clear();
            coreCommands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            coreCommands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            RecordStreaks(coreCommands, drawMaterial);
        }

        private void DetachCore()
        {
            if (coreCommands != null)
            {
                if (attachedCamera) attachedCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, coreCommands);
                coreCommands.Release(); coreCommands = null;
            }
            attachedCamera = null;
        }

        private void EnsureBuffers()
        {
            int paths = Mathf.Clamp(maxPaths, 64, 768), nodes = Mathf.Clamp(samplesPerPath, 64, 512), slots = Mathf.Clamp(maxStreaksPerPath, 16, 256);
            if (profile != null && allocatedPaths == paths && allocatedNodes == nodes && allocatedSlots == slots) return;
            ReleaseBuffers();
            allocatedPaths = paths; allocatedNodes = nodes; allocatedSlots = slots;
            buildPaths = flowCompute.FindKernel("BuildPaths");
            buildStreaks = flowCompute.FindKernel("BuildStreaks");
            buildArguments = flowCompute.FindKernel("BuildArguments");
            profile = new ComputeBuffer(ProfileSamples, sizeof(float) * 2);
            current = NewLayout(); outgoing = NewLayout();
            BufferAllocationCount++;
        }

        private Layout NewLayout()
        {
            var layout = new Layout
            {
                paths = new ComputeBuffer(allocatedPaths * allocatedNodes, 16),
                streaks = new ComputeBuffer(allocatedPaths * allocatedSlots, 28, ComputeBufferType.Append),
                count = new ComputeBuffer(1, 4, ComputeBufferType.Raw),
                args = new ComputeBuffer(4, 4, ComputeBufferType.IndirectArguments)
            };
            layout.args.SetData(new uint[] { 0, 1, 0, 0 });
            return layout;
        }

        private void UploadEnvelope(DynamoStormState state)
        {
            Vector3 wind = field.EffectiveWindDirection;
            state.Footprint.Project(wind, out float min, out float max);
            float center = Vector3.Dot(field.dipolePosition, wind);
            profileRange = new Vector4(min - center - 2, max - center + 2, ProfileSamples, 0);
            for (int i = 0; i < ProfileSamples; i++)
            {
                field.GetGameplayEnvelopeScales(Mathf.Lerp(profileRange.x, profileRange.y, i / (float)(ProfileSamples - 1)), out float a, out float b);
                profileData[i] = new Vector2(a, b) * field.magnetosphereRadius;
            }
            profile.SetData(profileData);
        }

        private void BuildLayout(Layout layout, DynamoStormState state)
        {
            layout.state = state;
            Vector3 across = new Vector3(-state.Flow.z, 0, state.Flow.x);
            state.Footprint.Project(state.Flow, out float minS, out float maxS);
            state.Footprint.Project(across, out float minT, out float maxT);
            float cs = Vector3.Dot(field.dipolePosition, state.Flow), ct = Vector3.Dot(field.dipolePosition, across);
            layout.lanes = Mathf.Clamp(Mathf.CeilToInt((maxT - minT) * pathsPerWorldUnit), 16, allocatedPaths);
            flowCompute.SetInt(LaneCount, layout.lanes); flowCompute.SetInt(NodeCount, allocatedNodes);
            flowCompute.SetVector(Flow, state.Flow); flowCompute.SetVector(Across, across); flowCompute.SetVector(Center, field.dipolePosition);
            flowCompute.SetVector(Domain, new Vector4(minS - cs - .3f, maxS - cs + .3f, minT - ct, maxT - ct));
            flowCompute.SetVector(ProfileRange, profileRange);
            flowCompute.SetFloat(Clearance, boundaryClearance);
            flowCompute.SetFloat(Jitter, organicMotion);
            flowCompute.SetFloat(MotionTime, state.Elapsed);
            SetCorners(state.Footprint);
            flowCompute.SetBuffer(buildPaths, Envelope, profile); flowCompute.SetBuffer(buildPaths, Paths, layout.paths);
            flowCompute.Dispatch(buildPaths, (layout.lanes + 31) / 32, 1, 1);
            layout.valid = true;
        }

        private void SetCorners(DynamoStormFootprint footprint)
        {
            for (int i = 0; i < 4; i++) corners[i] = footprint.Corner(i);
            flowCompute.SetVectorArray(Corners, corners);
        }

        private void GenerateStreaks(Layout layout, float travel, float time)
        {
            layout.streaks.SetCounterValue(0);
            flowCompute.SetInt(LaneCount, layout.lanes); flowCompute.SetInt(NodeCount, allocatedNodes); flowCompute.SetInt(Slots, allocatedSlots);
            flowCompute.SetFloat(Travel, travel); flowCompute.SetFloat(MotionTime, time);
            flowCompute.SetFloat(Spacing, Mathf.Max(.08f, streakSpacing)); flowCompute.SetFloat(Length, streakLength);
            SetCorners(layout.state.Footprint);
            flowCompute.SetBuffer(buildStreaks, Paths, layout.paths); flowCompute.SetBuffer(buildStreaks, Streaks, layout.streaks);
            flowCompute.Dispatch(buildStreaks, (layout.lanes * allocatedSlots + 63) / 64, 1, 1);
            ComputeBuffer.CopyCount(layout.streaks, layout.count, 0);
            flowCompute.SetBuffer(buildArguments, Count, layout.count); flowCompute.SetBuffer(buildArguments, Arguments, layout.args);
            flowCompute.Dispatch(buildArguments, 1, 1, 1);
        }

        private void Draw(Layout layout, float opacity)
        {
            var block = layout.properties;
            block.SetBuffer(Streaks, layout.streaks); block.SetBuffer(Envelope, profile);
            block.SetVector(ProfileRange, profileRange); block.SetVector(Center, field.dipolePosition);
            block.SetVector(Wind, field.EffectiveWindDirection); block.SetVector(FrontFlow, layout.state.Flow);
            block.SetFloat(Front, layout.state.Front); block.SetFloat(Feather, layout.state.Feather);
            block.SetFloat(Opacity, opacity); block.SetFloat(Brightness, brightness); block.SetFloat(Width, streakWidth); block.SetColor(ColorId, color);
        }

        private void OnDisable() { Camera.onPreRender -= BeforeCameraRender; DetachCore(); ReleaseBuffers(); ReleaseMaterial(); previewRendering = false; }
        private void OnDestroy() { DetachCore(); ReleaseBuffers(); ReleaseMaterial(); }
        private static void DestroyMaterial(Material material)
        {
            if (!material) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
        private void ReleaseMaterial()
        {
            hasDraws = false;
            DestroyMaterial(drawMaterial); DestroyMaterial(bloomMaterial);
            drawMaterial = bloomMaterial = materialSource = null;
        }
        private void ReleaseBuffers()
        {
            current?.Release(); outgoing?.Release(); profile?.Release();
            current = outgoing = null; profile = null;
        }
    }
}
