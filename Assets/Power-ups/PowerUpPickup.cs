using UnityEngine;
using Massive.Scoring;
using Massive.Player;
using System.Collections.Generic;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickup : MonoBehaviour
    {
        private static readonly List<PowerUpPickup> activePickups = new List<PowerUpPickup>();
        public static IReadOnlyList<PowerUpPickup> ActivePickups => activePickups;
        public int ColliderHierarchyVersion { get; private set; }
        public PowerUpDefinition definition;
        public event System.Action<PlayerControllerScript> Claimed;
        private bool restrictClaimant;
        private PlayerControllerScript allowedClaimant;
        public bool CountsTowardSpawnLimit => !restrictClaimant;

        /// <summary>Isolate a staged pickup; null makes a natural-lifetime example unclaimable.</summary>
        public void RestrictClaimsTo(PlayerControllerScript player)
        {
            restrictClaimant = true;
            allowedClaimant = player;
        }

        /// <summary>Configure while inactive so lifetime, intro and physics all see final spawn data.</summary>
        public static PowerUpPickup Spawn(PowerUpDefinition def, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!def || !def.pickupPrefab) throw new System.ArgumentException("Power-up requires a pickup prefab.");
            var staging = new GameObject("Pickup initialization");
            staging.SetActive(false);
            GameObject go = null;
            try
            {
                go = Instantiate(def.pickupPrefab, position, rotation, staging.transform);
                go.SetActive(false);
                var pickup = go.GetComponent<PowerUpPickup>();
                if (!pickup) throw new System.InvalidOperationException("Pickup prefab requires PowerUpPickup on its root.");
                pickup.definition = def;
                if (go.TryGetComponent<Rigidbody>(out var body))
                {
                    body.position = position; body.rotation = rotation;
                    if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                }
                go.transform.SetParent(parent, true);
                go.SetActive(true);
                if (go.TryGetComponent<PowerUpIconTetheredBody>(out var tether)) tether.ResetAnchorHere();
                return pickup;
            }
            catch { if (go) Destroy(go); throw; }
            finally { Destroy(staging); }
        }

        [Header("Activation")]
        [Tooltip("If enabled, only colliders on these layers can activate the pickup (ex: PlayerAttackHitbox).")]
        [SerializeField] private bool requireAttackToActivate = true;

        [SerializeField] private LayerMask attackActivatorLayers;

        [Header("Scoring (Optional)")]
        [Tooltip("Add a ScoreRewardEmitter to this prefab to award the centrally configured POWER_UP_CLAIM reward.")]
        [SerializeField] private ScoreRewardEmitter scoreRewardEmitter;

        [Header("Tutorial / Debug")]
        [Tooltip("If enabled, the pickup will NOT despawn from worldLifetimeSeconds timing out (but it WILL still despawn when collected).")]
        [SerializeField] private bool neverDespawnFromTimeout = false;


        private float _deathTime;
        private bool _despawning;
        private bool _activated;
        private PowerUpIconParticleSizeEase _iconParticles;

        private PowerUpIconManifestAnimator _anim;

        private void Awake()
        {
            _anim = GetComponent<PowerUpIconManifestAnimator>();
            _iconParticles = GetComponentInChildren<PowerUpIconParticleSizeEase>(true);

            if (scoreRewardEmitter == null)
                scoreRewardEmitter = GetComponent<ScoreRewardEmitter>();
        }

        private void OnEnable()
        {
            if (!activePickups.Contains(this)) activePickups.Add(this);
            ColliderHierarchyVersion++;
            _despawning = false;
            _activated = false;

            if (neverDespawnFromTimeout)
            {
                _deathTime = float.PositiveInfinity;
            }
            else
            {
                float life = (definition != null) ? definition.WorldLifetimeSeconds : 10f;

                // Optional: allow “<= 0 means never”
                if (life <= 0f) _deathTime = float.PositiveInfinity;
                else _deathTime = Time.time + life;
            }
        }

        private void OnDisable() { activePickups.Remove(this); }
        private void OnTransformChildrenChanged() { ColliderHierarchyVersion++; }

        private void Update()
        {
            if (_despawning) return;

            if (Time.time >= _deathTime)
            {
                // timed out -> play outro if possible
                if (_anim != null)
                {
                    _despawning = true;
                    _anim.BeginDespawn();
                    return;
                }

                Destroy(gameObject);
            }
        }

private void OnTriggerEnter(Collider other) => TryActivate(other);
private void OnTriggerStay(Collider other) => TryActivate(other);

// Also used by the Repulsor's final overlap query when its last radius falls
// between physics ticks. Every contact shares the same one-time claim guard.
public void TryActivate(Collider other)
{
    if (_despawning || _activated || !other || !other.enabled || !other.gameObject.activeInHierarchy) return;

    if (definition == null)
    {
        Debug.LogWarning($"[{name}] PowerUpPickup triggered but definition is NULL.", this);
        return;
    }

    PlayerPowerUpController p = null;

    if (requireAttackToActivate)
    {
        // Optional layer gate (only if you set attackActivatorLayers)
        if (attackActivatorLayers.value != 0 &&
            (attackActivatorLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Thrust and Sweep share the sword; Repulsor has its own active pulse.
        var melee = other.GetComponentInParent<PlayerMelee>();
        var repulsor = other.GetComponentInParent<PlayerRepulsorAOE>();
        if (melee && melee.isActiveAndEnabled)
            p = melee.GetComponentInParent<PlayerPowerUpController>();
        else if (repulsor && repulsor.isActiveAndEnabled && repulsor.IsPulseActive)
            p = repulsor.GetComponentInParent<PlayerPowerUpController>();
        if (!p) return;
    }
    else
    {
        // Touch pickup (if you ever use it)
        p = other.GetComponentInParent<PlayerPowerUpController>();
        if (!p) return;
    }

    var player = p.GetComponent<PlayerControllerScript>();
    if (!player || !player.isActiveAndEnabled || player.temporarilyEliminated || player.IsMatchInputLocked) return;
    if (restrictClaimant && player != allowedClaimant) return;

    // From this point forward, we commit to consuming this pickup exactly once.
    _activated = true;

    // Begin the inner-icon outro in the confirmed-hit callback itself, before effects/toasts.
    if (_anim != null)
    {
        _despawning = true;
        _deathTime = float.PositiveInfinity;
        _anim.BeginAttackDespawn();
    }
    else _iconParticles?.PlayDespawn();

    // Apply effect:
    // - Instant power-ups apply immediately and DO NOT occupy the equipped slot.
    // - Normal power-ups Equip() (overwrite behavior lives in the controller).
    if (definition is IInstantPowerUpEffect instant)
        instant.ApplyInstant(player, p, gameObject);
    else
        p.Equip(definition);

    Claimed?.Invoke(player);

    // Score is committed immediately on the confirmed claim. The emitter stores
    // only a reward key; its numeric value comes from ScoreEconomyProfile.
    scoreRewardEmitter?.TryAward(player, transform.position);

    // Toast ALWAYS (you wanted mass nodes to still show it)
    var toastSystem = PowerUpPickupToastSystem.ForScene(gameObject.scene);
    if (player.ParticipatesInMatch && toastSystem != null)
        toastSystem.Show(definition, transform.position);

    if (_anim != null) return;

    // The component belongs to the pickup root, which may be inside a demo or spawn group.
    Destroy(gameObject);
}



    }
}
