using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>Shot rules shared by gameplay and the isolated editor physics preview.</summary>
    public static class AmplifierCoreMotion
    {
        public static float ChargeDrag(float speed, float range, float step) =>
            speed / (Mathf.Max(.1f, range) + speed * step);

        public static float ShotDrag(float speed, float range, float step, AmplifierPossessionSettings settings)
        {
            if (!settings.distanceDrivenShots) return ChargeDrag(speed, range, step);
            // Solve once at launch. PhysX still owns all movement and collisions;
            // there is no position correction, travel cutoff or per-step speed assignment.
            float target = Mathf.Max(.1f, range), low = 0f, high = 1f / Mathf.Max(.001f, step);
            float best = low, error = Mathf.Abs(PredictTravel(speed, low, step, settings, out _) - target);
            for (int i = 0; i < 28; i++)
            {
                float candidate = (low + high) * .5f;
                float distance = PredictTravel(speed, candidate, step, settings, out _);
                float difference = Mathf.Abs(distance - target);
                if (difference < error) { best = candidate; error = difference; }
                if (distance > target) low = candidate; else high = candidate;
            }
            return best;
        }

        /// <summary>Unobstructed travel including the low-speed settling interval and safety brake.
        /// PhysX applies v *= (1 - drag * dt) before integrating position. Geometric sums
        /// avoid simulating thousands of steps when inspecting very low drag values.</summary>
        public static float PredictTravel(float speed, float drag, float step, AmplifierPossessionSettings settings, out float stopTime)
        {
            double dt = Mathf.Max(.001f, step), velocity = Mathf.Max(0f, speed);
            double threshold = Mathf.Max(.01f, settings.settledSpeed);
            // Count with the same float accumulator as Settle, including non-integral timings.
            float age = 0f; int settlingTicks = 0;
            do { age += (float)dt; settlingTicks++; } while (age < Mathf.Max(0f, settings.settlingSeconds) && settlingTicks < 100000);
            double factor = System.Math.Max(0, 1 - Mathf.Max(0f, drag) * dt);
            double slowAt = StepsUntilSlow(velocity, threshold, factor);
            double steps = slowAt + settlingTicks - 1;
            double beforeBrake = System.Math.Max(0, System.Math.Ceiling(Mathf.Max(1f, settings.maximumFlightSeconds) / dt) - 1);
            double firstSteps = System.Math.Min(steps, beforeBrake);
            double distance = CoastDistance(velocity, factor, firstSteps, dt);
            if (steps > beforeBrake)
            {
                velocity *= System.Math.Pow(factor, firstSteps);
                double slowTicks = System.Math.Max(0, beforeBrake - slowAt);
                factor = System.Math.Max(0, 1 - Mathf.Max(drag, settings.finalBrakeDrag) * dt);
                double finalSteps = StepsUntilSlow(velocity, threshold, factor) + settlingTicks - slowTicks - 1;
                distance += CoastDistance(velocity, factor, finalSteps, dt);
                steps = beforeBrake + finalSteps;
            }
            stopTime = (float)((steps + 1) * dt);
            return (float)distance;
        }

        static double StepsUntilSlow(double speed, double threshold, double factor)
        {
            if (speed <= threshold) return 0;
            if (factor <= 0) return 1;
            return factor >= 1 ? double.PositiveInfinity : System.Math.Ceiling(System.Math.Log(threshold / speed) / System.Math.Log(factor));
        }

        static double CoastDistance(double speed, double factor, double steps, double dt)
        {
            if (steps <= 0 || speed <= 0) return 0;
            return factor >= 1 ? speed * dt * steps : speed * dt * factor * (1 - System.Math.Pow(factor, steps)) / (1 - factor);
        }

        public static float CoastingDrag(float chargeDrag, float bodyDrag, AmplifierPossessionSettings settings) =>
            settings.distanceDrivenShots ? Mathf.Max(0f, chargeDrag) : Mathf.Max(chargeDrag, bodyDrag);

        public static float Drag(float chargeDrag, float bodyDrag, float age, AmplifierPossessionSettings settings)
        {
            float coast = CoastingDrag(chargeDrag, bodyDrag, settings);
            return age >= Mathf.Max(1f, settings.maximumFlightSeconds) ? Mathf.Max(coast, settings.finalBrakeDrag) : coast;
        }

        public static bool Settle(float speed, bool guidedTurn, float step, ref float stillAge, AmplifierPossessionSettings settings)
        {
            bool slow = speed <= Mathf.Max(.01f, settings.settledSpeed) && !guidedTurn;
            stillAge = slow ? stillAge + step : 0f;
            return slow && stillAge >= Mathf.Max(0f, settings.settlingSeconds);
        }

        public static Vector3 LimitSpeed(Vector3 velocity, float maximum)
        {
            if (maximum <= 0f) return velocity;
            var planar = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z), maximum);
            return new Vector3(planar.x, velocity.y, planar.z);
        }

        public static Vector3 Bounce(Vector3 incoming, Vector3 normal, float restitution, float maximum)
        {
            normal.y = 0f; normal.Normalize();
            float toward = Vector3.Dot(incoming, normal);
            return toward < 0f ? LimitSpeed(incoming - (1f + restitution) * toward * normal, maximum) : incoming;
        }

        public static float AimDotRadius(AmplifierPossessionSettings settings, int index, int count, float charge01, bool showCharge, out float growth)
        {
            float progress = count > 1 ? index / (float)(count - 1) : 0f;
            float spread = Mathf.Clamp(settings.dotChargeSpread, .02f, 1f);
            float amount = count > 1 ? (charge01 - progress * (1f - spread)) / spread : charge01;
            growth = showCharge ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount)) : 0f;
            return Mathf.Max(.01f, settings.dotRadius) * Mathf.Lerp(1f, Mathf.Clamp01(settings.dotEndScale), progress) *
                Mathf.Lerp(1f, Mathf.Clamp(settings.chargedDotScale, 1f, 4f), growth);
        }
    }
}
