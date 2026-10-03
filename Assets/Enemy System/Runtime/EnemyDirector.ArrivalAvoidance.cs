using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        [Header("Announced arrival avoidance")]
        public bool avoidAnnouncedArrivals = true;
        [Range(0f, 3f)] public float arrivalAvoidanceStrength = 1.5f;
        [Min(0f)] public float arrivalAvoidancePadding = .35f;
        [Min(0f)] public float arrivalAvoidanceLookAhead = .6f;

        /// <summary>Only ordinary movement consumes this bias; committed attacks never call it.</summary>
        public Vector3 ArrivalAvoidance(EnemyBase enemy, Vector3 heading)
        {
            if (!avoidAnnouncedArrivals || !encounterTimeline || timelinePaused || !isActiveAndEnabled ||
                !enemy || enemy.Director != this || enemy.IsDead || enemy.IsPaused || enemy.HoldPosition || !enemy.Definition) return Vector3.zero;
            Vector3 origin = enemy.transform.position, force = Vector3.zero;
            heading.y = 0; Vector3 ahead = origin + heading.normalized * arrivalAvoidanceLookAhead;
            float ownRadius = enemy.Definition.GetSpawnRadiusWorld(enemy.transform);
            foreach (var member in members)
            {
                if (!member.status.announced || !member.warning) continue;
                float radius = member.status.radius + ownRadius + arrivalAvoidancePadding;
                Vector3 away = origin - member.pose.clearance; away.y = 0;
                Vector3 predicted = ahead - member.pose.clearance; predicted.y = 0;
                float distance = Mathf.Min(away.magnitude, predicted.magnitude);
                if (distance >= radius) continue;
                if (away.sqrMagnitude < .001f)
                {
                    away = Vector3.Cross(Vector3.up, heading.normalized);
                    if (away.sqrMagnitude < .001f) away = Vector3.right;
                }
                float strength = 1 - Mathf.Clamp01(distance / Mathf.Max(.001f, radius));
                force += away.normalized * strength * strength;
            }
            return Vector3.ClampMagnitude(force, 1f) * arrivalAvoidanceStrength;
        }
    }
}
