using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Minimal environmental energy pickup for the first scoring slice.
    /// It can be claimed by a melee hitbox or by body contact and awards score
    /// instantly through the central profile.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(ScoreRewardEmitter))]
    public sealed class EnergyScorePickup : MonoBehaviour
    {
        [SerializeField] private ScoreRewardEmitter rewardEmitter;
        [SerializeField] private bool requireAttackToClaim = true;
        [SerializeField] private LayerMask activatorLayers;
        [SerializeField] private GameObject collectedVfxPrefab;
        [Tooltip("Optional explicit prefab root to destroy. If empty, this GameObject is destroyed.")]
        [SerializeField] private GameObject objectToDestroy;

        private bool _claimed;

        private void Reset()
        {
            Collider collider = GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            rewardEmitter = GetComponent<ScoreRewardEmitter>();
            if (rewardEmitter != null)
                rewardEmitter.SetRewardKey(ScoreRewardKeys.EnergyPickupSmall);
        }

        private void Awake()
        {
            if (rewardEmitter == null)
                rewardEmitter = GetComponent<ScoreRewardEmitter>();
        }

        private void OnEnable()
        {
            _claimed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_claimed || other == null || rewardEmitter == null)
                return;

            if (activatorLayers.value != 0 &&
                (activatorLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            PlayerControllerScript player = ResolvePlayer(other);
            if (player == null || player.IsPseudoPlayer || player.temporarilyEliminated)
                return;

            if (!rewardEmitter.TryAward(player, transform.position))
                return;

            _claimed = true;

            if (collectedVfxPrefab != null)
                Instantiate(collectedVfxPrefab, transform.position, transform.rotation);

            Destroy(objectToDestroy != null ? objectToDestroy : gameObject);
        }

        private PlayerControllerScript ResolvePlayer(Collider other)
        {
            if (requireAttackToClaim)
            {
                PlayerMelee melee = other.GetComponent<PlayerMelee>();
                if (melee == null)
                    melee = other.GetComponentInParent<PlayerMelee>();

                return melee != null ? melee.Owner : null;
            }

            return other.GetComponentInParent<PlayerControllerScript>();
        }
    }
}
