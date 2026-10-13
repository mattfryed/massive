using System.Collections.Generic;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Multiplier
{
    /// <summary>Aim dots that grow with charge, plus a contest-only team ring. No colliders.</summary>
    [DefaultExecutionOrder(100)] // After the Core consumes this frame's player aim in LateUpdate.
    public sealed class AmplifierPossessionPresentation : ImmediateModeShapeDrawer
    {
        private AmplifierCoreGameplay core;
        private Massive.Singularity.SingularityAmplifierAdapter surface;
        private readonly List<Vector3> path = new List<Vector3>(256);
        private readonly List<Vector3> aimDots = new List<Vector3>(128);
        private AmplifierCoreControlUI controlUI;
        public AmplifierCoreControlUI ControlUI => controlUI;
        private Vector3 drawOrigin;

        public bool ContestRingVisible => Application.isPlaying && isActiveAndEnabled && core &&
            core.ControlState == AmplifierControlState.Contested && core.Possession.showContestRing;
        public bool ContestBarVisible => ContestRingVisible; // Compatibility for existing diagnostic tools.
        public bool AimGuideVisible => Application.isPlaying && isActiveAndEnabled && core &&
            core.ControlState == AmplifierControlState.Aiming && core.Occupant &&
            core.Possession.showTrajectory;
        public bool ChargeGuideVisible => AimGuideVisible && !core.Occupant.IsAmplifierTransitioning && core.Possession.showDotCharge;
        public int AimDotCount => AimGuideVisible ? aimDots.Count : 0;
        public float LightBarFraction => ContestBarVisible ? Mathf.Clamp01(core.LightControl01) : 0f;

        public bool TryGetAimDot(int index, out Vector3 point, out float radius, out float charge)
        {
            point = Vector3.zero; radius = charge = 0f;
            if (index < 0 || index >= AimDotCount) return false;
            point = aimDots[index];
            var settings = core.Possession;
            radius = AmplifierCoreMotion.AimDotRadius(settings, index, aimDots.Count, core.Charge01, ChargeGuideVisible, out charge);
            return true;
        }

        private void RefreshAimDots()
        {
            aimDots.Clear();
            float untilDot = Radius + .12f;
            float spacing = Mathf.Max(.08f, core.Possession.dotSpacing);
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 start = path[i - 1], delta = path[i] - start;
                float length = delta.magnitude;
                if (length < .00001f) continue;
                while (untilDot <= length)
                {
                    aimDots.Add(start + delta * (untilDot / length));
                    untilDot += spacing;
                }
                untilDot -= length;
            }
        }

        public override void OnEnable()
        {
            core = GetComponent<AmplifierCoreGameplay>();
            surface = GetComponent<Massive.Singularity.SingularityAmplifierAdapter>();
            path.Clear(); aimDots.Clear();
            useCullingMasks = true;
            base.OnEnable();
        }

        public override void OnDisable()
        {
            // Release owned resources before script reload as well as normal disable.
            controlUI?.Dispose(); controlUI = null;
            path.Clear(); aimDots.Clear();
            base.OnDisable();
        }

        private void LateUpdate()
        {
            if (!core || !core.IsOccupied) { controlUI?.Hide(); return; }
            // This is a lightweight direction guide, not a throttled physics prediction.
            // Rebuild every frame so small stick changes remain continuous.
            if (AimGuideVisible) core.BuildTrajectory(path);
            else path.Clear();
            RefreshAimDots();
            var camera = Camera.main;
            if (!camera) return;
            if (controlUI == null) controlUI = new AmplifierCoreControlUI(transform);
            var state = core.ControlState == AmplifierControlState.Contested ? AmplifierPromptState.Contested :
                core.Occupant && core.Occupant.IsAmplifierTransitioning ? AmplifierPromptState.Entering :
                core.IsContestWinnerAiming ? AmplifierPromptState.Winner : AmplifierPromptState.Aiming;
            controlUI.Update(core.Possession, camera, Map(core.transform.position), Radius, state,
                core.Occupant ? core.Occupant.teamID : 1, core.ControlSecondsRemaining, core.LightControl01);
        }

        private void OnDestroy() { controlUI?.Dispose(); controlUI = null; }

        float Radius => core && core.GetComponent<Collider>() ? Mathf.Max(core.GetComponent<Collider>().bounds.extents.x, core.GetComponent<Collider>().bounds.extents.z) : .5f;
        Vector3 Map(Vector3 point) => surface && surface.IsRenderingOnSurface ? surface.MapChartPoint(point) : point;

        public override void DrawShapes(Camera cam)
        {
            if (!core || !core.IsOccupied || !Application.isPlaying) return;
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                drawOrigin = Map(transform.position);
                Draw.Matrix = Matrix4x4.Translate(drawOrigin);
                // Arena exterior masks write depth; the world-space guide must respect it.
                Draw.ZTest = CompareFunction.LessEqual;
                Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.LineGeometry = LineGeometry.Volumetric3D;
                Draw.LineEndCaps = LineEndCap.Round;
                for (int i = 0; i < AimDotCount; i++)
                    if (TryGetAimDot(i, out var point, out float dotSize, out _) && dotSize > .0001f)
                        Draw.Disc(Map(point) - drawOrigin, -cam.transform.forward, dotSize, Color.white);
            }
        }

    }

    public sealed partial class AmplifierCoreGameplay
    {
        private AmplifierPossessionPresentation possessionPresentation;
        private void UpdatePossessionPresentation()
        {
            if (!IsOccupied) { HidePossessionPresentation(); return; }
            if (!possessionPresentation) possessionPresentation = gameObject.AddComponent<AmplifierPossessionPresentation>();
            possessionPresentation.enabled = true;
        }
        private void HidePossessionPresentation()
        { if (possessionPresentation) possessionPresentation.enabled = false; }
    }
}
