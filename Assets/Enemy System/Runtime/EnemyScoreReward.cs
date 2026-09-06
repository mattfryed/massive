using Massive.Scoring;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Converts confirmed, attributed defeats into central score awards.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBase))]
    public sealed class EnemyScoreReward : MonoBehaviour
    {
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
                defeat.source != EnemyDamageSource.ShieldReflect) return;
            if (_enemy.OwnerTeamId == defeat.creditedPlayer.teamID) return;

            string key = _enemy.Definition != null ? _enemy.Definition.defeatRewardKey : null;
            if (string.IsNullOrWhiteSpace(key)) return;

            MatchScoreService service = MatchScoreService.Instance;
            if (service == null) return;
            service.TryAwardToPlayer(key, defeat.creditedPlayer, defeat.sourceLifeToken,
                defeat.worldPosition, out _);
        }
    }
}
