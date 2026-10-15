using Massive.Scoring;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Converts confirmed, attributed defeats into central score awards.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBase))]
    public sealed class EnemyScoreReward : MonoBehaviour
    {
        [Tooltip("Optional pooled world-space label showing the final credited energy.")]
        public EnemyScoreToast scoreToastPrefab;
        private EnemyBase _enemy;

        private void OnEnable()
        {
            _enemy = GetComponent<EnemyBase>();
            _enemy.Defeated += OnDefeated;
        }

        private void OnDisable()
        {
            if (_enemy != null) _enemy.Defeated -= OnDefeated;
        }

        private void OnDefeated(EnemyDefeatContext defeat)
        {
            if (defeat.creditedPlayer == null || defeat.creditedPlayer.IsPseudoPlayer) return;
            if (defeat.source != EnemyDamageSource.Sword &&
                defeat.source != EnemyDamageSource.Projectile &&
                defeat.source != EnemyDamageSource.ShieldReflect &&
                defeat.source != EnemyDamageSource.Repulsor &&
                defeat.source != EnemyDamageSource.AmplifierShot &&
                defeat.source != EnemyDamageSource.Decoherence) return;
            if (_enemy.OwnerTeamId == defeat.creditedPlayer.teamID) return;

            MatchScoreService service = MatchScoreService.Instance;
            if (service == null) return;
            service.RecordEnemyDefeat(defeat.creditedPlayer, defeat.sourceLifeToken);
            string key = _enemy.Definition != null ? _enemy.Definition.defeatRewardKey : null;
            if (string.IsNullOrWhiteSpace(key)) return;
            if (service.TryAwardToPlayer(key, defeat.creditedPlayer, defeat.sourceLifeToken,
                defeat.worldPosition, out var award) && scoreToastPrefab != null)
                EnemyScoreToast.Show(scoreToastPrefab, award, gameObject.scene);
        }
    }
}
