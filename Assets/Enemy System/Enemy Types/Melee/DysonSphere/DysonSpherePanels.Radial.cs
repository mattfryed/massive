using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class DysonSpherePanels
    {
        private bool radialDriven;
        private float radialClock, radialBlend, radialVibration, radialFrequency;
        private float radialRadiusMultiplier = 1f;
        private Vector3 radialBaseScale;
        private float PoseTime => radialDriven ? radialClock : Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        public float ShellRadiusWorld { get; private set; }
        public float BodyScaleMultiplier { get; private set; } = 1f;

        public void BeginRadialControl(float clock)
        {
            if (!radialDriven) radialBaseScale = transform.localScale;
            radialClock = clock; radialDriven = true; SetSpear01(0f); SetStun01(0f);
        }
        public void EndRadialControl()
        {
            if (radialDriven) transform.localScale = radialBaseScale;
            radialDriven = false; radialBlend = 0f; BodyScaleMultiplier = 1f;
        }
        // The controller evaluates the exact rendered panel pose before querying its damage sphere.
        public void SetRadialPose(float clock, float bodyScale, float blend, float radiusMultiplier, float vibration, float frequency)
        {
            BeginRadialControl(clock);
            BodyScaleMultiplier = Mathf.Max(.05f, bodyScale);
            transform.localScale = radialBaseScale * BodyScaleMultiplier;
            radialBlend = Mathf.Clamp01(blend); radialRadiusMultiplier = Mathf.Max(1f, radiusMultiplier);
            radialVibration = Mathf.Max(0f, vibration); radialFrequency = Mathf.Max(0f, frequency);
            Update();
        }
        private static float LiftToRadius(Panel p, float targetRadius, float scale)
        {
            float Lift(Vector3 v)
            {
                Vector3 corner = p.baseCentroidLocal + p.baseRotLocal * (v * scale);
                float along = Vector3.Dot(corner, p.baseNormalLocal);
                float sideways = Mathf.Max(0f, corner.sqrMagnitude - along * along);
                return Mathf.Sqrt(Mathf.Max(0f, targetRadius * targetRadius - sideways)) - along;
            }
            return Mathf.Min(Lift(p.v0), Mathf.Min(Lift(p.v1), Lift(p.v2)));
        }
        private void MeasureRadialShell()
        {
            float max = 0f;
            foreach (var p in _panels)
            {
                if (!p.available || p.state != PanelState.Alive || !p.tf) continue;
                max = Mathf.Max(max, (p.tf.TransformPoint(p.v0) - transform.position).sqrMagnitude);
                max = Mathf.Max(max, (p.tf.TransformPoint(p.v1) - transform.position).sqrMagnitude);
                max = Mathf.Max(max, (p.tf.TransformPoint(p.v2) - transform.position).sqrMagnitude);
            }
            ShellRadiusWorld = Mathf.Sqrt(max);
        }
    }
}
