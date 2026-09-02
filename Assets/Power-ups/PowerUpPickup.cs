using UnityEngine;
using Massive.Scoring;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpDefinition definition;

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
            _despawning = false;
            _activated = false;

            if (neverDespawnFromTimeout)
            {
                _deathTime = float.PositiveInfinity;
            }
            else
            {
                float life = (definition != null) ? definition.worldLifetimeSeconds : 10f;

                // Optional: allow “<= 0 means never”
                if (life <= 0f) _deathTime = float.PositiveInfinity;
                else _deathTime = Time.time + life;
            }
        }

        private void Update()
        {
            if (_despawning) return;

            if (Time.time >= _deathTime)
            {
                // timed out -> play outro if possible
                if (_anim != null)
                {
                    _despawning = true;
                    _iconParticles?.PlayDespawn();
                    _anim.BeginDespawn(); // timeout uses existing outro
                    return;
                }

                Destroy(gameObject);
            }
        }

private void OnTriggerEnter(Collider other)
{
    if (_despawning || _activated) return;

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

        // Melee-only pickup: require a real PlayerMelee hitbox
        var melee = other.GetComponent<PlayerMelee>();
        if (!melee) melee = other.GetComponentInParent<PlayerMelee>();
        if (!melee) return;

        p = melee.GetComponentInParent<PlayerPowerUpController>();
        if (!p) return;
    }
    else
    {
        // Touch pickup (if you ever use it)
        p = other.GetComponentInParent<PlayerPowerUpController>();
        if (!p) return;
    }

    var player = p.GetComponent<PlayerControllerScript>();
    if (!player) return;

    // From this point forward, we commit to consuming this pickup exactly once.
    _activated = true;

    // Apply effect:
    // - Instant power-ups apply immediately and DO NOT occupy the equipped slot.
    // - Normal power-ups Equip() (overwrite behavior lives in the controller).
    if (definition is IInstantPowerUpEffect instant)
        instant.ApplyInstant(player, p, gameObject);
    else
        p.Equip(definition);

    // Score is committed immediately on the confirmed claim. The emitter stores
    // only a reward key; its numeric value comes from ScoreEconomyProfile.
    scoreRewardEmitter?.TryAward(player, transform.position);

    // Toast ALWAYS (you wanted mass nodes to still show it)
    if (PowerUpPickupToastSystem.Instance != null)
        PowerUpPickupToastSystem.Instance.Show(definition, transform.position);

    // Despawn visuals / shatter the cage
    if (_anim != null)
    {
        _despawning = true;
        _deathTime = float.PositiveInfinity;

        _iconParticles?.PlayDespawn();
        _anim.BeginAttackDespawn();
        return;
    }

    // If we don't have an animator, still destroy the WHOLE pickup prefab.
    // (Using transform.root avoids leaving the cage behind if this component is on a child.)
    Destroy(transform.root.gameObject);
}



    }
}
