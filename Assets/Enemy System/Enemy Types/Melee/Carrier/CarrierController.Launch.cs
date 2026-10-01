using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class CarrierController
    {
        private static readonly float[] DepartureAngles = { 0f, -45f, 45f, -90f, 90f, -135f, 135f, -170f, 170f };

        private Vector3 BoundaryInward(Vector3 position, float padding)
        {
            if (!bounds || !bounds.IsValid) return Vector3.zero;
            // Insetting by the influence distance supplies a smooth force from both walls at corners.
            Vector3 inward = bounds.ClampWorldPointInside(position, padding + boundaryInfluenceDistance) - position;
            inward.y = 0f;
            return Vector3.ClampMagnitude(inward / Mathf.Max(.1f, boundaryInfluenceDistance), 1f);
        }

        public bool TryPlanLaunch(Transform dock, float padding, out DroneLaunchTrajectory best)
        {
            Vector3 outward = dock.forward; outward.y = 0f; outward.Normalize();
            best = new DroneLaunchTrajectory { direction = outward, speed = Mathf.Max(0f, launchSpeed),
                straightSeconds = Mathf.Max(.05f, launchClearanceSeconds) };
            if (!bounds || !bounds.IsValid) return true;
            if (!bounds.ContainsWorldPoint(dock.position, padding)) return false;
            Vector3 inward = BoundaryInward(dock.position, padding);
            if (inward.sqrMagnitude < .0001f && SafeDeparture(dock.position, padding, best, out _)) return true;
            float bestScore = float.NegativeInfinity;
            foreach (float angle in DepartureAngles)
            {
                var candidate = new DroneLaunchTrajectory { direction = outward, speed = Mathf.Max(0f, launchSpeed),
                    straightSeconds = Mathf.Max(.05f, launchStraightSeconds),
                    bendSeconds = Mathf.Max(.1f, launchBendSeconds), turnDegrees = angle };
                if (!SafeDeparture(dock.position, padding, candidate, out var end)) continue;
                float score = -BoundaryInward(end, padding).sqrMagnitude * 4f
                    + Vector3.Dot(candidate.Velocity(candidate.Duration).normalized, inward) * 2f
                    - Mathf.Abs(angle) / 360f;
                if (score <= bestScore) continue;
                bestScore = score; best = candidate;
            }
            // The ordered bay waits while the assembly rotates; never teleport a launch point or clamp a bad arc.
            return !float.IsNegativeInfinity(bestScore);
        }

        private bool SafeDeparture(Vector3 start, float padding, DroneLaunchTrajectory path, out Vector3 end)
        {
            end = start;
            Vector3 radial = end - transform.position; radial.y = 0f;
            float previousRadius = radial.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(path.Duration / .01f));
            float step = path.Duration / steps;
            for (int i = 0; i < steps; i++)
            {
                end += path.Velocity((i + .5f) * step) * step;
                if (!bounds.ContainsWorldPoint(end, padding + .02f)) return false;
                radial = end - transform.position; radial.y = 0f;
                // Never hook back through the rotating mothership after leaving its bay.
                if (radial.magnitude + .0001f < previousRadius) return false;
                previousRadius = radial.magnitude;
            }
            return true;
        }
    }
}
