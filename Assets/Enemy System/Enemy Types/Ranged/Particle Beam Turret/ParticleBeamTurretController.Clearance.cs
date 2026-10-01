using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class ParticleBeamTurretController
    {
        /// <summary>Expose a lane only when this Carrier is its first solid blocker.
        /// Checks eligible players before acquisition, since an occluded turret has no Target yet.</summary>
        public bool TryGetObstructedLane(EnemyBase blocker, out Vector3 origin, out Vector3 end)
        {
            origin = BeamOrigin; end = origin;
            if (!isActiveAndEnabled || !enemy || enemy.IsDead || enemy.IsPaused || !enemy.AttacksEnabled || !IsReady ||
                !blocker || !enemy.SharesSimulationWith(blocker)) return false;
            if (Phase == AttackPhase.Firing)
            {
                Trace(BeamDirection, beamRange, beamRadius, out var contact);
                if (!contact || contact.GetComponentInParent<EnemyBase>() != blocker) return false;
                end = origin + BeamDirection * beamRange; return true;
            }
            if (Target && InArc(Target)) return CarrierBlocksPlayer(Target, blocker, out end);
            float nearest = float.PositiveInfinity; bool found = false;
            foreach (var player in PlayerControllerScript.ActivePlayers)
            {
                if (!InArc(player)) continue;
                float distance = (player.transform.position - origin).sqrMagnitude;
                if (distance >= nearest || !CarrierBlocksPlayer(player, blocker, out var target)) continue;
                nearest = distance; end = target; found = true;
            }
            return found;
        }
        private bool CarrierBlocksPlayer(PlayerControllerScript player, EnemyBase blocker, out Vector3 end)
        {
            end = player.transform.position;
            Vector3 direction = end - BeamOrigin; direction.y = 0f;
            if (direction.sqrMagnitude < .0001f) return false;
            Trace(direction.normalized, direction.magnitude, .015f, out var contact);
            return contact && contact.GetComponentInParent<EnemyBase>() == blocker;
        }
    }
}
