#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Massive.Singularity.Editor
{
    /// <summary>Pure entry-pose regression checks. No active scene, physics body,
    /// prefab, material or authored portal setting is modified.</summary>
    public static class SingularityTidalEntryValidation
    {
        private const float Distance = 1.8f;
        private const float Radius = .6f;
        private const float HorizonScale = .045f;
        private const float MaximumStretch = 3f;
        private const float Exponent = 4.58f;
        private const float Tolerance = .0002f;

        [MenuItem("MASSIVE/SINGULARITY/Validate Tidal Entry Animation")]
        public static string Run()
        {
            int checks = 0;
            Vector3 start = Pose(0f);
            Vector3 end = Pose(1f);
            Check(Near(start.x, Distance) && Near(start.y, 1f) && Near(start.z, 1f),
                "Capture begins at the unchanged position, size and shape", ref checks);
            Check(Near(end.x, 0f) && Near(end.y, HorizonScale) && Near(end.z, MaximumStretch),
                "Entry finishes at the exact existing exit-start pose", ref checks);
            Check((Pose(-1f) - start).sqrMagnitude < Tolerance * Tolerance &&
                (Pose(2f) - end).sqrMagnitude < Tolerance * Tolerance,
                "Time outside the animation clamps to endpoint poses", ref checks);

            Vector3 early = Pose(.1f);
            Vector3 quarter = Pose(.25f);
            Vector3 middle = Pose(.5f);
            float initialNear = Distance - Radius;
            float initialFar = Distance + Radius;
            float nearQuarter = NearEnd(quarter, Radius);
            float farQuarter = FarEnd(quarter, Radius);
            Check(early.x < Distance - .08f,
                "The first tenth immediately moves inward instead of stretching in place", ref checks);
            Check(quarter.x < Distance * .85f,
                "The first quarter travels visibly inward, not the previous two percent", ref checks);
            Check(middle.x < Distance * .7f,
                "Mid-entry has covered more than thirty percent, not the previous nine percent", ref checks);
            Check(initialNear - nearQuarter > 2f * (initialFar - farQuarter) && farQuarter < initialFar,
                "The leading end is pulled inward much faster while the trailing end also advances", ref checks);
            Check(quarter.y > .8f && quarter.y * quarter.z > 1.25f,
                "Early pull lengthens the silhouette before the final collapse", ref checks);
            Check(NearEnd(early, Radius) < initialNear && FarEnd(early, Radius) < initialFar,
                "Neither end first jerks away from the hole", ref checks);
            Check(early.y / Mathf.Sqrt(early.z) < 1f &&
                quarter.y / Mathf.Sqrt(quarter.z) < early.y / Mathf.Sqrt(early.z),
                "Increasing axial pull also narrows the silhouette", ref checks);

            bool centerOrdered = true, nearOrdered = true, farOrdered = true;
            bool bounded = true, shapeContinuous = true;
            Vector3 previous = start;
            float previousNear = initialNear, previousFar = initialFar;
            for (int i = 1; i <= 256; i++)
            {
                Vector3 pose = Pose(i / 256f);
                float near = NearEnd(pose, Radius), far = FarEnd(pose, Radius);
                centerOrdered &= pose.x <= previous.x + Tolerance;
                nearOrdered &= near <= previousNear + Tolerance;
                farOrdered &= far <= previousFar + Tolerance;
                bounded &= IsFinite(pose) && pose.x >= -Tolerance && pose.x <= Distance + Tolerance &&
                    pose.y >= HorizonScale - Tolerance && pose.y <= 1f + Tolerance &&
                    pose.z >= 1f - Tolerance && pose.z <= MaximumStretch + Tolerance && near <= far;
                shapeContinuous &= Mathf.Abs(pose.y - previous.y) < .025f && Mathf.Abs(pose.z - previous.z) < .08f;
                previous = pose;
                previousNear = near;
                previousFar = far;
            }
            Check(centerOrdered, "Center approaches the mouth continuously without reversing", ref checks);
            Check(nearOrdered && farOrdered, "Both silhouette ends remain inward-ordered over 256 steps", ref checks);
            Check(bounded, "All sampled poses respect center, scale and maximum-stretch bounds", ref checks);
            Check(shapeContinuous, "Scale and stretch change continuously across the collapse handoff", ref checks);
            Check(Near(NearEnd(end, Radius), -Radius * HorizonScale * MaximumStretch) &&
                Near(FarEnd(end, Radius), Radius * HorizonScale * MaximumStretch),
                "Both ends fit symmetrically around the center at face transfer", ref checks);
            Check(Vector3.Distance(Pose(.99999f), end) < .001f,
                "The last entry sample converges to the existing exit pose without a visual jump", ref checks);

            CheckSpecialCases(ref checks);
            string report = "SINGULARITY tidal entry: PASS " + checks +
                " pure checks (immediate pull, differential endpoint motion, delayed collapse, continuous handoff and finite edge cases).";
            Debug.Log(report);
            return report;
        }

        private static void CheckSpecialCases(ref int checks)
        {
            // Already-deep participants have no room for a leading extension:
            // shrinking must not first kick their center outward.
            foreach (float distance in new[] { 0f, .1f, Radius })
            {
                float previous = distance;
                bool ordered = true;
                for (int i = 0; i <= 64; i++)
                {
                    Vector3 pose = SingularityBlackHolePortal.EvaluateEntryPose(i / 64f, Exponent,
                        distance, Radius, HorizonScale, MaximumStretch);
                    ordered &= IsFinite(pose) && pose.x >= -Tolerance && pose.x <= previous + Tolerance;
                    previous = pose.x;
                }
                Check(ordered, "Already-deep capture remains inward at distance " + distance, ref checks);
            }

            bool noStretch = true;
            for (int i = 0; i <= 64; i++)
            {
                Vector3 pose = SingularityBlackHolePortal.EvaluateEntryPose(i / 64f, Exponent,
                    Distance, Radius, HorizonScale, 1f);
                noStretch &= IsFinite(pose) && Near(pose.z, 1f);
            }
            Check(noStretch, "A maximum stretch of one really disables elongation", ref checks);

            foreach (float exponent in new[] { .1f, 3.5f, 8f })
            foreach (float maximum in new[] { 1f, 1.1f, 3f, 8f })
            {
                bool bounded = true;
                float previousCenter = Distance;
                for (int i = 0; i <= 64; i++)
                {
                    Vector3 pose = SingularityBlackHolePortal.EvaluateEntryPose(i / 64f, exponent,
                        Distance, Radius, HorizonScale, maximum);
                    bounded &= IsFinite(pose) && pose.x >= -Tolerance && pose.x <= previousCenter + Tolerance &&
                        pose.y > 0f && pose.y <= 1f + Tolerance && pose.z >= 1f - Tolerance && pose.z <= maximum + Tolerance;
                    previousCenter = pose.x;
                }
                Check(bounded, "Supported exponent/stretch remain bounded: " + exponent + "/" + maximum, ref checks);
            }

            // Safety-only assertions for malformed or unusually proportioned
            // inputs: do not require the normal silhouette path when capped.
            float[][] extremeCases =
            {
                new[] { .5f, Exponent, Distance, 0f, HorizonScale, MaximumStretch },
                new[] { .5f, Exponent, 1000f, .001f, HorizonScale, MaximumStretch },
                new[] { .5f, Exponent, .001f, 1000f, HorizonScale, MaximumStretch },
                new[] { .5f, 8f, Distance, Radius, .005f, 8f },
                new[] { .5f, .1f, Distance, Radius, .3f, 1f },
                new[] { float.NaN, Exponent, Distance, Radius, HorizonScale, MaximumStretch },
                new[] { .5f, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN },
                new[] { .5f, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity },
                new[] { .5f, -10f, -10f, -10f, -10f, -10f }
            };
            for (int i = 0; i < extremeCases.Length; i++)
            {
                float[] c = extremeCases[i];
                Vector3 pose = SingularityBlackHolePortal.EvaluateEntryPose(c[0], c[1], c[2], c[3], c[4], c[5]);
                Check(IsFinite(pose) && pose.x >= -Tolerance && pose.y > 0f && pose.z >= 1f - Tolerance,
                    "Degenerate input produces a finite, non-inverted pose " + i, ref checks);
            }
        }

        private static Vector3 Pose(float t)
        { return SingularityBlackHolePortal.EvaluateEntryPose(t, Exponent, Distance, Radius, HorizonScale, MaximumStretch); }
        private static float NearEnd(Vector3 pose, float radius) { return pose.x - radius * pose.y * pose.z; }
        private static float FarEnd(Vector3 pose, float radius) { return pose.x + radius * pose.y * pose.z; }
        private static bool Near(float a, float b) { return Mathf.Abs(a - b) <= Tolerance; }
        private static bool IsFinite(Vector3 value) { return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z); }
        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void Check(bool condition, string message, ref int checks)
        { if (!condition) throw new InvalidOperationException("SINGULARITY tidal entry: " + message); checks++; }
    }
}
#endif
