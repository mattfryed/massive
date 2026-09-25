using Massive.Multiplier;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>
    /// Scene-only opt-in for the ordinary Amplifier Core. Physics stays in the
    /// players' unrolled XZ chart; only its collider-free Visual Root is folded.
    /// Capture, attack impulses, warning, and spawn/despawn remain Core-owned.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(410)]
    [RequireComponent(typeof(AmplifierCoreGameplay), typeof(Rigidbody))]
    public sealed partial class SingularityAmplifierAdapter : MonoBehaviour, ISingularityAttractor
    {
        [SerializeField] private SingularitySurface surface;
        [SerializeField] private SingularityGridRenderer grid;
        [Tooltip("Collider-free presentation child. Its scale remains owned by the Core lifecycle animation.")]
        [SerializeField] private Transform visualRoot;
        [Header("Periodic chart")]
        [SerializeField, Min(0f)] private float sidePadding = .05f;
        [Header("Independent grid attraction")]
        [SerializeField, Min(0f)] private float attractionRadius = 1.7f;
        [SerializeField, Range(0f, 3f)] private float attractionPull = .55f;
        [SerializeField, Min(0f)] private float attractionResponseSeconds = .06f;

        private AmplifierCoreGameplay core;
        private Rigidbody body;
        private Collider collisionShape;
        private SphereCollider sphere;
        private Transform cachedVisualRoot;
        private Vector3 originalVisualPosition;
        private Quaternion originalVisualRotation;
        private float planeHeight;
        private Vector2 attractionPosition;
        private bool initialized;
        private SingularityBlackHolePortal boundPortal;

        public SingularitySurface Surface => surface;
        public AmplifierCoreGameplay Core => core;
        public Rigidbody Body => body;
        public float CollisionRadius => WorldCollisionRadius();
        public Transform VisualRoot => visualRoot;
        public Vector2 SurfacePosition
        {
            get
            {
                if (surface == null) return Vector2.zero;
                Vector3 p = surface.transform.InverseTransformPoint(transform.position);
                return new Vector2(p.x, surface.Wrap(p.z + surface.FrontHeight * .5f));
            }
        }
        public Vector2 SurfaceVelocity
        {
            get
            {
                if (surface == null || body == null) return Vector2.zero;
                Vector3 v = surface.transform.InverseTransformVector(body.linearVelocity);
                return new Vector2(v.x, v.z);
            }
        }
        public float RearWeight => surface != null ? surface.RearWeight(SurfacePosition.y) : 0f;
        public Vector3 RenderWorldPosition => MapChartPoint(transform.position);
        public Vector2 SmoothedAttractionPosition => initialized ? attractionPosition : SurfacePosition;
        public float AttractionRadius => Mathf.Max(0f, attractionRadius);
        public float AttractionPull => Mathf.Max(0f, attractionPull);
        public bool IsAttractionActive => isActiveAndEnabled && surface != null && core != null
            && core.isActiveAndEnabled && !core.IsCaptured && !core.IsPresentationOnly;
        public bool IsRenderingOnSurface => isActiveAndEnabled && surface != null && visualRoot != null
            && visualRoot != transform;

        private void Awake() { Cache(); }
        private void OnEnable()
        {
            Cache(); Initialize();
            if (grid != null) grid.RegisterAttractor(this);
            if (boundPortal != null) boundPortal.RegisterCore(this);
        }
        private void OnDisable()
        {
            if (boundPortal != null) boundPortal.UnregisterCore(this);
            ClearPortalVisual();
            if (grid != null) grid.UnregisterAttractor(this);
            RestorePresentation(); initialized = false;
        }

        public void Configure(SingularitySurface newSurface, SingularityGridRenderer newGrid = null,
            Transform presentationRoot = null)
        {
            if (boundPortal != null) boundPortal.UnregisterCore(this);
            ClearPortalVisual();
            if (grid != null) grid.UnregisterAttractor(this);
            RestorePresentation();
            surface = newSurface;
            grid = newGrid != null ? newGrid : newSurface != null
                ? newSurface.GetComponentInChildren<SingularityGridRenderer>(true) : null;
            if (presentationRoot != null) visualRoot = presentationRoot;
            Cache(); Initialize();
            if (isActiveAndEnabled && grid != null) grid.RegisterAttractor(this);
            if (isActiveAndEnabled && boundPortal != null) boundPortal.RegisterCore(this);
            RefreshPresentation();
        }

        public void ConfigurePortal(SingularityBlackHolePortal portal)
        {
            if (boundPortal != null && boundPortal != portal) boundPortal.UnregisterCore(this);
            boundPortal = portal;
            if (isActiveAndEnabled && boundPortal != null) boundPortal.RegisterCore(this);
        }

        private void Cache()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (core == null) core = GetComponent<AmplifierCoreGameplay>();
            if (collisionShape == null) collisionShape = GetComponent<Collider>();
            if (sphere == null) sphere = collisionShape as SphereCollider;
            if (visualRoot == null) visualRoot = transform.Find("Visual Root");
            if (visualRoot == transform || (visualRoot != null &&
                (visualRoot.GetComponentInChildren<Collider>(true) != null ||
                 visualRoot.GetComponentInChildren<Rigidbody>(true) != null)))
            {
                Debug.LogError("SINGULARITY Amplifier requires a separate collider-free Visual Root; physics must stay on the chart.", this);
                visualRoot = null;
            }
            if (visualRoot == null || cachedVisualRoot == visualRoot) return;
            cachedVisualRoot = visualRoot;
            originalVisualPosition = visualRoot.localPosition;
            originalVisualRotation = visualRoot.localRotation;
        }
        private void Initialize()
        {
            if (surface == null) return;
            planeHeight = surface.transform.InverseTransformPoint(transform.position).y;
            attractionPosition = SurfacePosition;
            initialized = true;
        }

        private void FixedUpdate() { ConstrainMotion(); }

        /// <summary>Apply only the periodic seam and side walls after physics.
        /// Uses the existing wall restitution, not a second collision tuning set.</summary>
        public void ConstrainMotion()
        {
            if (!isActiveAndEnabled || surface == null || body == null || body.isKinematic
                || core == null || core.IsCaptured || core.IsPresentationOnly) return;
            if (!initialized) Initialize();
            Vector3 local = surface.transform.InverseTransformPoint(body.position);
            float radius = WorldCollisionRadius() / Mathf.Max(.0001f, Mathf.Abs(surface.transform.lossyScale.x));
            float limit = Mathf.Max(.1f, surface.Width * .5f - radius - Mathf.Max(0f, sidePadding));
            float clampedX = Mathf.Clamp(local.x, -limit, limit);
            float wrappedZ = surface.Wrap(local.z + surface.FrontHeight * .5f) - surface.FrontHeight * .5f;
            bool hitSide = !Mathf.Approximately(clampedX, local.x);
            if (!hitSide && Mathf.Abs(wrappedZ - local.z) <= .001f) return;
            local.x = clampedX; local.z = wrappedZ;
            body.position = surface.transform.TransformPoint(local);
            transform.position = body.position;
            if (!hitSide) return;
            Vector3 velocity = surface.transform.InverseTransformVector(body.linearVelocity);
            if (velocity.x * clampedX > 0f) velocity.x = -velocity.x * Mathf.Clamp01(core.Effective_wallRestitution);
            body.linearVelocity = surface.transform.TransformVector(velocity);
        }

        private float WorldCollisionRadius()
        {
            if (sphere != null)
            {
                Vector3 scale = sphere.transform.lossyScale;
                return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }
            return collisionShape != null ? Mathf.Max(collisionShape.bounds.extents.x, collisionShape.bounds.extents.z) : .5f;
        }

        private void LateUpdate()
        {
            if (surface == null) return;
            if (!initialized) Initialize();
            Vector2 position = SurfacePosition;
            float k = attractionResponseSeconds <= 0f ? 1f
                : 1f - Mathf.Exp(-2.995732274f * Time.deltaTime / attractionResponseSeconds);
            attractionPosition.x = Mathf.Lerp(attractionPosition.x, position.x, k);
            attractionPosition.y = surface.Wrap(attractionPosition.y + surface.LoopDelta(attractionPosition.y, position.y) * k);
            RefreshPresentation();
        }

        /// <summary>Fold presentation independently of lifecycle scale. A temporary
        /// parent supplies portal stretch while the Core's own visual root and
        /// child mesh breathing retain their authored scale ownership.</summary>
        public void RefreshPresentation()
        {
            if (!IsRenderingOnSurface) return;
            Vector3 chartPoint = transform.TransformPoint(originalVisualPosition);
            Vector3 local = surface.transform.InverseTransformPoint(chartPoint);
            float s = surface.Wrap(local.z + surface.FrontHeight * .5f);
            Vector3 across, along;
            surface.Frame(local.x, s, out across, out along);
            Vector3 normal = surface.Normal(local.x, s);
            Quaternion frame = Quaternion.LookRotation(along, normal);
            Quaternion rotation = surface.transform.rotation * frame * Quaternion.Inverse(surface.transform.rotation)
                * transform.rotation * originalVisualRotation;
            Vector3 position = MapChartPoint(chartPoint);
            if (portalVisualActive && portalDeformation != null)
                ApplyPortalPose(position, frame, rotation);
            else visualRoot.SetPositionAndRotation(position, rotation);
        }

        public Vector3 MapChartPoint(Vector3 chartWorld)
        {
            if (surface == null) return chartWorld;
            Vector3 local = surface.transform.InverseTransformPoint(chartWorld);
            float s = surface.Wrap(local.z + surface.FrontHeight * .5f);
            return surface.transform.TransformPoint(surface.Evaluate(local.x, s) + surface.Normal(local.x, s) * local.y);
        }

        public void RestorePresentation()
        {
            ClearPortalVisual();
            if (cachedVisualRoot == null) return;
            cachedVisualRoot.localPosition = originalVisualPosition;
            cachedVisualRoot.localRotation = originalVisualRotation;
        }

        public void SetSurfacePosition(Vector2 position, bool clearVelocity = true)
        {
            if (surface == null || !Finite(position.x) || !Finite(position.y)) return;
            Cache(); if (!initialized) Initialize();
            Vector3 local = new Vector3(Mathf.Clamp(position.x, -surface.Width * .5f, surface.Width * .5f),
                planeHeight, surface.Wrap(position.y) - surface.FrontHeight * .5f);
            Vector3 world = surface.transform.TransformPoint(local);
            if (body != null)
            {
                body.position = world;
                if (clearVelocity && !body.isKinematic)
                { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            transform.position = world;
            attractionPosition = SurfacePosition;
            RefreshPresentation();
        }

        public bool TeleportToOppositeFace(bool preserveScreenVelocity = true)
        {
            if (surface == null || core == null || core.IsCaptured) return false;
            Vector2 position = SurfacePosition;
            float target;
            if (position.y <= surface.TopStart)
                target = surface.RearStart + (surface.FrontHeight - position.y) * surface.RearScale;
            else if (position.y >= surface.RearStart && position.y <= surface.BottomStart)
                target = surface.FrontHeight - (position.y - surface.RearStart) / surface.RearScale;
            else return false;
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            SetSurfacePosition(new Vector2(position.x, target), false);
            if (body != null && !body.isKinematic && preserveScreenVelocity)
            {
                Vector3 local = surface.transform.InverseTransformVector(velocity);
                local.z = -local.z;
                body.linearVelocity = surface.transform.TransformVector(local);
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
