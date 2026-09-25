using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>
    /// Opt-in presentation of a real MASSIVE player on the folded surface. The
    /// existing controller, attack system and Rigidbody keep a planar periodic
    /// chart: front XZ is unchanged, rear physics is a separate strip farther +Z.
    /// No gameplay transform is moved to its overlapping screen-space projection.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    [RequireComponent(typeof(PlayerControllerScript), typeof(Rigidbody))]
    public sealed partial class SingularityPlayerAdapter : MonoBehaviour, ISingularityAttractor
    {
        [SerializeField] private SingularitySurface surface;
        [SerializeField] private SingularityGridRenderer grid;
        [Header("Periodic chart")]
        [SerializeField, Min(0f)] private float sidePadding = .05f;
        [Tooltip("Moves render geometry toward the camera without moving physics. Front/rear still have their actual depth separation.")]
        [SerializeField, Min(0f)] private float renderLift = .05f;
        [Header("Independent grid attraction")]
        [SerializeField, Min(0f)] private float attractionRadius = 2.145f;
        [SerializeField, Range(0f, 3f)] private float attractionPull = .8f;
        [SerializeField, Min(0f)] private float attractionResponseSeconds = .06f;

        private Rigidbody body;
        private PlayerControllerScript player;
        private PlayerVisualController visuals;
        private float planeHeight;
        private Vector2 attractionPosition;
        private bool initialized;

        private static readonly int EnabledId = Shader.PropertyToID("_SingularityEnabled");
        private static readonly int BackBrightnessId = Shader.PropertyToID("_SingularityBackBrightness");
        private static readonly int LookupId = Shader.PropertyToID("_SingularityLookup");
        private static readonly int LookupSizeId = Shader.PropertyToID("_SingularityLookup_TexelSize");
        private static readonly int ShapeId = Shader.PropertyToID("_SingularityShape");
        private static readonly int SourceId = Shader.PropertyToID("_SingularitySourceToWorld");
        private static readonly int WorldToLocalId = Shader.PropertyToID("_SingularityWorldToLocal");
        private static readonly int LocalToWorldId = Shader.PropertyToID("_SingularityLocalToWorld");
        private static readonly int ViewProjectionId = Shader.PropertyToID("_SingularityViewProjection");

        public SingularitySurface Surface => surface;
        public PlayerControllerScript Player => player != null ? player : GetComponent<PlayerControllerScript>();
        public Vector2 SurfacePosition
        {
            get
            {
                if (surface == null) return Vector2.zero;
                Vector3 local = surface.transform.InverseTransformPoint(transform.position);
                return new Vector2(local.x, surface.Wrap(local.z + surface.FrontHeight * .5f));
            }
        }
        public Vector2 SurfaceVelocity
        {
            get
            {
                if (body == null || surface == null) return Vector2.zero;
                Vector3 local = surface.transform.InverseTransformVector(body.linearVelocity);
                return new Vector2(local.x, local.z);
            }
        }
        public Vector2 SmoothedAttractionPosition => initialized ? attractionPosition : SurfacePosition;
        public float AttractionRadius => Mathf.Max(0f, attractionRadius);
        public float AttractionPull => Mathf.Max(0f, attractionPull);
        public bool IsAttractionActive => isActiveAndEnabled && surface != null && Player != null && !Player.temporarilyEliminated;
        public float RearWeight => surface != null ? surface.RearWeight(SurfacePosition.y) : 0f;
        public Vector3 RenderWorldPosition => MapChartPoint(transform.position);
        public bool IsRenderingOnSurface => isActiveAndEnabled && surface != null && grid != null && grid.SurfaceLookup != null;

        private void Awake() { Cache(); }
        private void OnEnable()
        {
            Cache(); Initialize();
            if (grid != null) grid.RegisterAttractor(this);
        }
        private void OnDisable()
        {
            if (grid != null) grid.UnregisterAttractor(this);
            ClearPortalVisual();
            initialized = false;
        }
        private void Cache()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (player == null) player = GetComponent<PlayerControllerScript>();
            if (visuals == null) visuals = player != null && player.visualsController != null
                ? player.visualsController : GetComponentInChildren<PlayerVisualController>(true);
        }
        public void Configure(SingularitySurface newSurface, SingularityGridRenderer newGrid = null)
        {
            if (grid != null) grid.UnregisterAttractor(this);
            surface = newSurface;
            grid = newGrid != null ? newGrid : newSurface != null ? newSurface.GetComponentInChildren<SingularityGridRenderer>(true) : null;
            Cache(); Initialize();
            if (isActiveAndEnabled && grid != null) grid.RegisterAttractor(this);
        }
        private void Initialize()
        {
            if (surface == null) return;
            planeHeight = surface.transform.InverseTransformPoint(transform.position).y;
            attractionPosition = SurfacePosition;
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (surface == null || body == null) return;
            if (!initialized) Initialize();
            // Apply after the preceding physics step. Only the periodic seam and
            // two side limits are constrained; all input/forces still belong to
            // the authored PlayerControllerScript and attack components.
            Vector3 local = surface.transform.InverseTransformPoint(body.position);
            float margin = sidePadding + (visuals != null ? visuals.baseRadius * Massive.Player.PlayerScaleAdjuster.SizeOf(this) : .4f);
            float limit = Mathf.Max(.1f, surface.Width * .5f - margin);
            float clampedX = Mathf.Clamp(local.x, -limit, limit);
            float wrappedZ = surface.Wrap(local.z + surface.FrontHeight * .5f) - surface.FrontHeight * .5f;
            if (!Mathf.Approximately(clampedX, local.x) || Mathf.Abs(wrappedZ - local.z) > .001f)
            {
                bool hitSide = !Mathf.Approximately(clampedX, local.x);
                local.x = clampedX; local.z = wrappedZ;
                body.position = surface.transform.TransformPoint(local);
                transform.position = body.position;
                if (hitSide && !body.isKinematic)
                {
                    Vector3 v = surface.transform.InverseTransformVector(body.linearVelocity);
                    if (Mathf.Sign(v.x) == Mathf.Sign(clampedX)) v.x = 0f;
                    body.linearVelocity = surface.transform.TransformVector(v);
                }
            }
        }
        private void LateUpdate()
        {
            if (surface == null) return;
            if (!initialized) Initialize();
            Vector2 position = SurfacePosition;
            float k = attractionResponseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-2.995732274f * Time.deltaTime / attractionResponseSeconds);
            attractionPosition.x = Mathf.Lerp(attractionPosition.x, position.x, k);
            attractionPosition.y = surface.Wrap(attractionPosition.y + surface.LoopDelta(attractionPosition.y, position.y) * k);
        }

        public void SetSurfacePosition(Vector2 position, bool clearVelocity = true)
        {
            if (surface == null || !Finite(position.x) || !Finite(position.y)) return;
            Cache(); if (!initialized) Initialize();
            Vector3 local = new Vector3(Mathf.Clamp(position.x, -surface.Width * .5f, surface.Width * .5f), planeHeight,
                surface.Wrap(position.y) - surface.FrontHeight * .5f);
            Vector3 world = surface.transform.TransformPoint(local);
            if (body != null)
            {
                body.position = world;
                if (clearVelocity && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            transform.position = world;
            attractionPosition = SurfacePosition;
            // The normal Update will do this too; updating immediately avoids one
            // render frame at the old body location after a portal transfer.
            if (visuals != null && visuals.visuals != null) visuals.visuals.position = world;
        }

        public bool TeleportToOppositeFace(bool preserveScreenVelocity = true)
        {
            if (surface == null || Player == null || Player.temporarilyEliminated) return false;
            Vector2 p = SurfacePosition;
            float target;
            if (p.y <= surface.TopStart)
                target = surface.RearStart + (surface.FrontHeight - p.y) * surface.RearScale;
            else if (p.y >= surface.RearStart && p.y <= surface.BottomStart)
                target = surface.FrontHeight - (p.y - surface.RearStart) / surface.RearScale;
            else return false; // Portals are on faces, not ambiguous turn sections.
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            SetSurfacePosition(new Vector2(p.x, target), false);
            if (body != null && !body.isKinematic && preserveScreenVelocity)
            {
                Vector3 localVelocity = surface.transform.InverseTransformVector(velocity);
                localVelocity.z = -localVelocity.z;
                body.linearVelocity = surface.transform.TransformVector(localVelocity);
            }
            return true;
        }

        public Vector3 MapChartPoint(Vector3 chartWorld, float additionalLift = 0f)
        {
            if (surface == null) return chartWorld;
            Vector3 local = surface.transform.InverseTransformPoint(chartWorld);
            local = DeformPortalPoint(local);
            float s = surface.Wrap(local.z + surface.FrontHeight * .5f);
            Vector3 point = surface.Evaluate(local.x, s);
            point += surface.Normal(local.x, s) * (local.y - planeHeight);
            point.y += renderLift + additionalLift;
            return surface.transform.TransformPoint(point);
        }

        /// <summary>Matches the per-vertex GPU cue for CPU-drawn effects without
        /// changing their alpha, depth, authored colors, or physical position.</summary>
        public float EvaluateChartBrightness(Vector3 chartWorld)
        {
            if (!IsRenderingOnSurface) return 1f;
            Vector3 local = DeformPortalPoint(surface.transform.InverseTransformPoint(chartWorld));
            return surface.EvaluateBrightness(local.z + surface.FrontHeight * .5f);
        }

        /// <summary>Per-draw opt-in, never edits shared material assets. GPU
        /// vertices (including individual nugget corners) sample the same surface
        /// lookup as the grid instead of rigidly tilting a flat body approximation.</summary>
        public void ApplyRenderProperties(MaterialPropertyBlock properties, Matrix4x4 sourceToWorld, Matrix4x4 viewProjection, float additionalLift = 0f)
        {
            bool active = IsRenderingOnSurface;
            properties.SetFloat(EnabledId, active ? 1f : 0f);
            properties.SetFloat(BackBrightnessId, active ? surface.BacksideBrightness : 1f);
            if (!active) return;
            properties.SetTexture(LookupId, grid.SurfaceLookup);
            properties.SetVector(LookupSizeId, new Vector4(1f / grid.SurfaceLookup.width, 1f,
                grid.SurfaceLookup.width, 1f));
            properties.SetVector(ShapeId, new Vector4(surface.FrontHeight, surface.LoopLength, planeHeight, renderLift + additionalLift));
            properties.SetMatrix(SourceId, sourceToWorld);
            properties.SetMatrix(WorldToLocalId, surface.transform.worldToLocalMatrix);
            properties.SetMatrix(LocalToWorldId, surface.transform.localToWorldMatrix);
            properties.SetMatrix(ViewProjectionId, viewProjection);
            ApplyPortalVisualProperties(properties);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
