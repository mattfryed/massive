using UnityEngine;

namespace Massive.Player
{
    /// <summary>
    /// Owns player hierarchy scale. World-space consumers use the helpers below;
    /// local geometry and local collider dimensions must not be multiplied again.
    /// </summary>
    [ExecuteAlways, DefaultExecutionOrder(-500), DisallowMultipleComponent]
    [AddComponentMenu("MASSIVE/Player/Player Scale Adjuster")]
    public sealed class PlayerScaleAdjuster : MonoBehaviour
    {
        [SerializeField] private bool useGlobalModifiers = true;
        [SerializeField, Range(0.1f, 3f)] private float size = 1f;
        [SerializeField] private bool scaleActionReach = true;
        [SerializeField] private bool scaleMovement;
        [SerializeField] private bool scaleProjectileRange;
        [SerializeField, HideInInspector] private Vector3 referenceLocalScale = Vector3.one;
        [SerializeField, HideInInspector] private bool initialized;
        private ParticleSystem[] attachedParticles;
        private PlayerControllerScript player;
        private bool resolvedPlayer;

        private PlayerGlobalModifiers.Slot GlobalSlot
        {
            get
            {
                if (!useGlobalModifiers) return null;
                if (!resolvedPlayer) { player = GetComponent<PlayerControllerScript>(); resolvedPlayer = true; }
                var profile = PlayerGlobalModifiers.For(player);
                return profile && profile.GlobalSizeEnabled ? profile.ForPlayer(player.playerID) : null;
            }
        }
        public bool UsesGlobalModifiers => GlobalSlot != null;
        public bool UseGlobalModifiers { get => useGlobalModifiers; set { useGlobalModifiers = value; ApplyScale(); } }
        public float LocalSize => size;

        public float Size { get => GlobalSlot?.size ?? size; set { size = Mathf.Clamp(value, 0.1f, 3f); ApplyScale(); } }
        public bool ScaleActionReach { get => GlobalSlot?.scaleActionReach ?? scaleActionReach; set => scaleActionReach = value; }
        public bool ScaleMovement { get => GlobalSlot?.scaleMovement ?? scaleMovement; set => scaleMovement = value; }
        public bool ScaleProjectileRange { get => GlobalSlot?.scaleProjectileRange ?? scaleProjectileRange; set => scaleProjectileRange = value; }
        public Vector3 ReferenceLocalScale => referenceLocalScale;
        public float WorldScale => LargestAxis(transform.lossyScale);
        public bool HasUniformPositiveScale => IsUniformPositive(referenceLocalScale)
            && (!transform.parent || IsUniformPositive(transform.parent.lossyScale));

        private void Reset() { initialized = false; Initialize(); ApplyScale(); }
        private void OnEnable() { Initialize(); resolvedPlayer = false; attachedParticles = null; ApplyScale(); }
        private void OnValidate() { size = Mathf.Clamp(size, 0.1f, 3f); }
        private void Update() { ApplyScale(); }
        private void OnTransformChildrenChanged() { attachedParticles = null; }

        private void Initialize()
        {
            if (initialized) return;
            referenceLocalScale = transform.localScale;
            initialized = true;
        }

        public void ApplyScale()
        {
            Initialize();
            Vector3 desired = referenceLocalScale * Size;
            if (transform.localScale != desired) transform.localScale = desired;
            // Authored local particles must inherit the player's root scale. Generated
            // melee adapters own their calibrated birth frames and are deliberately excluded.
            if (attachedParticles == null) attachedParticles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in attachedParticles)
            {
                if (!ps || IsTransient(ps.transform)) continue;
                var main = ps.main;
                if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        private bool IsTransient(Transform child)
        {
            for (var t = child; t && t != transform; t = t.parent)
                if ((t.gameObject.hideFlags & HideFlags.DontSave) != 0) return true;
            return false;
        }

        private static PlayerScaleAdjuster Find(Component source) => source ? source.GetComponentInParent<PlayerScaleAdjuster>(true) : null;
        public static float SizeOf(Component source) { var s = Find(source); return s ? s.WorldScale : 1f; }
        public static float ActionReachOf(Component source) { var s = Find(source); return s && s.ScaleActionReach ? s.WorldScale : 1f; }
        public static float ProjectileReachOf(Component source) { var s = Find(source); return s && s.ScaleProjectileRange ? s.WorldScale : 1f; }
        public static float MovementOf(Component source) { var s = Find(source); return s && s.ScaleMovement ? s.WorldScale : 1f; }

        public static float BodyRadiusOf(PlayerControllerScript player)
        {
            if (!player) return 0f;
            var sphere = player.GetComponent<SphereCollider>();
            // Collider.bounds is empty while disabled/dead. Read the authored shape instead.
            if (sphere) return sphere.radius * LargestAxis(sphere.transform.lossyScale);
            var visual = player.GetComponentInChildren<PlayerVisualController>(true);
            return visual ? visual.baseRadius * LargestAxis(visual.transform.lossyScale) : 0.5f * SizeOf(player);
        }

        private static float LargestAxis(Vector3 scale) => Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        private static bool IsUniformPositive(Vector3 scale) => scale.x > 0f && scale.y > 0f && scale.z > 0f
            && Mathf.Abs(scale.x - scale.y) < 0.0001f && Mathf.Abs(scale.x - scale.z) < 0.0001f;
    }
}
