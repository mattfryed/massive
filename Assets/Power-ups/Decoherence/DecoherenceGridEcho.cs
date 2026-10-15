using System;
using Massive.Player;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Massive.PowerUps
{
    /// <summary>Borrow the real grid mesh/position buffer; own only the paint mask and echo renderer.</summary>
    internal sealed class DecoherenceGridEcho : IDisposable
    {
        private const int Capacity = 512; // DecoherencePaint's bounded deposit count.
        private readonly PlayerControllerScript owner;
        private readonly DecoherencePowerUpDefinition settings;
        private readonly GridInteractor interactor;
        private readonly Vector4[] discs = new Vector4[Capacity];
        private readonly MaterialPropertyBlock properties = new();
        private VectorGridGPU grid;
        private MeshFilter gridMesh, echoMesh;
        private MeshRenderer gridRenderer, echoRenderer;
        private GameObject echoObject;
        private ComputeBuffer mask;

        private static readonly int DiscsId = Shader.PropertyToID("_DecoDiscs");
        private static readonly int CountId = Shader.PropertyToID("_DecoDiscCount");
        private static readonly int BoundsId = Shader.PropertyToID("_DecoBounds");
        private static readonly int EchoId = Shader.PropertyToID("_DecoEcho");
        private static readonly int InsetId = Shader.PropertyToID("_DecoInset");

        public DecoherenceGridEcho(PlayerControllerScript player, DecoherencePowerUpDefinition definition)
        { owner = player; settings = definition; interactor = player.GetComponentInChildren<GridInteractor>(true); }

        public void Update(DecoherencePaint paint)
        {
            var target = interactor && interactor.grid ? interactor.grid : VectorGridGPU.Instance;
            if (target && owner && target.gameObject.scene != owner.gameObject.scene) target = null;
            if (grid != target) { Dispose(); grid = target; }
            bool visible = owner && grid && grid.isActiveAndEnabled && settings && settings.gridEchoMaterial &&
                settings.gridEchoOpacity > 0f && paint != null && paint.Deposits.Count > 0;
            if (!visible) { if (echoRenderer) echoRenderer.enabled = false; return; }
            if (!gridMesh) gridMesh = grid.GetComponent<MeshFilter>();
            if (!gridRenderer) gridRenderer = grid.GetComponent<MeshRenderer>();
            if (!gridMesh || !gridMesh.sharedMesh || !gridRenderer || !gridRenderer.enabled)
            { if (echoRenderer) echoRenderer.enabled = false; return; }
            if (!echoObject)
            {
                echoObject = new GameObject("Decoherence grid echoes", typeof(MeshFilter), typeof(MeshRenderer));
                echoObject.hideFlags = HideFlags.DontSave;
                echoObject.layer = grid.gameObject.layer;
                echoObject.transform.SetParent(grid.transform, false);
                echoMesh = echoObject.GetComponent<MeshFilter>();
                echoRenderer = echoObject.GetComponent<MeshRenderer>();
                echoRenderer.shadowCastingMode = ShadowCastingMode.Off; echoRenderer.receiveShadows = false;
                echoRenderer.lightProbeUsage = LightProbeUsage.Off; echoRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mask = new ComputeBuffer(Capacity, sizeof(float) * 4, ComputeBufferType.Structured);
            }
            echoMesh.sharedMesh = gridMesh.sharedMesh; // Also follows live grid rebuilds.
            echoRenderer.sharedMaterial = settings.gridEchoMaterial;
            echoRenderer.sortingLayerID = gridRenderer.sortingLayerID;
            echoRenderer.sortingOrder = gridRenderer.sortingOrder + 1;
            int count = Mathf.Min(Capacity, paint.Deposits.Count);
            float minX = float.PositiveInfinity, minZ = minX, maxX = float.NegativeInfinity, maxZ = maxX;
            for (int i = 0; i < count; i++)
            {
                var d = paint.Deposits[i]; discs[i] = new Vector4(d.center.x, d.center.z, d.radius, 0f);
                minX = Mathf.Min(minX, d.center.x - d.radius); minZ = Mathf.Min(minZ, d.center.z - d.radius);
                maxX = Mathf.Max(maxX, d.center.x + d.radius); maxZ = Mathf.Max(maxZ, d.center.z + d.radius);
            }
            mask.SetData(discs, 0, 0, count);
            // Copies live curve, presentation, oval, attraction and pulse settings, without
            // reading GPU positions back to the CPU or touching the source grid's material.
            gridRenderer.GetPropertyBlock(properties);
            float size = PlayerScaleAdjuster.SizeOf(owner);
            properties.SetBuffer(DiscsId, mask); properties.SetInt(CountId, count);
            properties.SetVector(BoundsId, new Vector4(minX, minZ, maxX, maxZ));
            properties.SetVector(EchoId, new Vector4(paint.Clock * Mathf.Max(0f, settings.gridEchoSpeed),
                Mathf.Max(0f, settings.gridEchoDisplacement) * size, Mathf.Clamp01(settings.gridEchoOpacity), 1.35f / Mathf.Max(.01f, size)));
            properties.SetFloat(InsetId, Mathf.Max(.01f, settings.outlineNoiseAmount * 1.5f) * size);
            echoRenderer.SetPropertyBlock(properties); echoRenderer.enabled = true;
        }

        public void Dispose()
        {
            if (echoRenderer) echoRenderer.enabled = false;
            if (echoObject) Object.Destroy(echoObject);
            echoObject = null; echoMesh = null; echoRenderer = null;
            mask?.Release(); mask = null; gridMesh = null; gridRenderer = null;
        }
    }
}
