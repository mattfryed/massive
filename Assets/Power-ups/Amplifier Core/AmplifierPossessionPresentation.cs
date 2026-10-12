using System.Collections.Generic;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Multiplier
{
    /// <summary>Aim dots that grow with charge, plus a contest-only team bar. No colliders.</summary>
    public sealed class AmplifierPossessionPresentation : ImmediateModeShapeDrawer
    {
        private AmplifierCoreGameplay core;
        private Massive.Singularity.SingularityAmplifierAdapter surface;
        private readonly List<Vector3> path = new List<Vector3>(256);
        private readonly List<Vector3> aimDots = new List<Vector3>(128);
        private float nextPrediction;
        private TMPro.TextMeshPro caption;
        private Transform captionRoot;
        private Vector3 drawOrigin;

        public bool ContestBarVisible => Application.isPlaying && isActiveAndEnabled && core &&
            core.ControlState == AmplifierControlState.Contested;
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
            float progress = aimDots.Count > 1 ? index / (float)(aimDots.Count - 1) : 0f;
            if (ChargeGuideVisible)
            {
                float spread = Mathf.Clamp(settings.dotChargeSpread, .02f, 1f);
                float growth = aimDots.Count > 1 ? (core.Charge01 - progress * (1f - spread)) / spread : core.Charge01;
                charge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(growth));
            }
            radius = Mathf.Max(.01f, settings.dotRadius) * Mathf.Lerp(1f, Mathf.Clamp01(settings.dotEndScale), progress) *
                Mathf.Lerp(1f, Mathf.Clamp(settings.chargedDotScale, 1f, 4f), charge);
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
            path.Clear(); aimDots.Clear(); nextPrediction = 0f;
            useCullingMasks = true;
            base.OnEnable();
        }

        public override void OnDisable()
        {
            if (captionRoot) captionRoot.gameObject.SetActive(false);
            path.Clear(); aimDots.Clear();
            base.OnDisable();
        }

        private void LateUpdate()
        {
            if (!core || !core.IsOccupied) { if (captionRoot) captionRoot.gameObject.SetActive(false); return; }
            if (Time.unscaledTime >= nextPrediction)
            {
                nextPrediction = Time.unscaledTime + .08f;
                if (core.ControlState == AmplifierControlState.Aiming && core.Possession.showTrajectory) core.BuildTrajectory(path);
                else path.Clear();
                RefreshAimDots();
            }
            if (!captionRoot)
            {
                captionRoot = new GameObject("Core control prompt", typeof(TMPro.TextMeshPro)).transform;
                captionRoot.SetParent(transform, false);
                captionRoot.gameObject.layer = gameObject.layer;
                caption = captionRoot.GetComponent<TMPro.TextMeshPro>();
                caption.fontSize = 2.2f;
                caption.alignment = TMPro.TextAlignmentOptions.Center;
                caption.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                caption.color = Color.white;
            }
            captionRoot.gameObject.SetActive(true);
            var camera = Camera.main;
            if (!camera) return;
            captionRoot.rotation = camera.transform.rotation;
            captionRoot.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f / transform.lossyScale.z);
            Vector3 center = Map(core.transform.position);
            float radius = Radius;
            captionRoot.position = center - camera.transform.up * (radius + core.Possession.meterOffset + (ContestBarVisible ? .38f : .08f));
            if (core.ControlState == AmplifierControlState.Contested)
                caption.SetText("ALTERNATE ATTACK / SHIELD  {0:1}s", core.ControlSecondsRemaining);
            else if (core.Occupant && core.Occupant.IsAmplifierTransitioning)
                caption.text = (core.Occupant.teamID == 1 ? "LIGHT" : "DARK") + "  ENTERING CORE";
            else
                caption.SetText((core.Occupant && core.Occupant.teamID == 1 ? "LIGHT" : "DARK") + "  HOLD / RELEASE ATTACK  {0:1}s", core.ControlSecondsRemaining);
        }

        float Radius => core && core.GetComponent<Collider>() ? Mathf.Max(core.GetComponent<Collider>().bounds.extents.x, core.GetComponent<Collider>().bounds.extents.z) : .5f;
        Vector3 Map(Vector3 point) => surface && surface.IsRenderingOnSurface ? surface.MapChartPoint(point) : point;

        public override void DrawShapes(Camera cam)
        {
            if (!core || !core.IsOccupied || !Application.isPlaying) return;
            var settings = core.Possession;
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                drawOrigin = Map(transform.position);
                Draw.Matrix = Matrix4x4.Translate(drawOrigin);
                Draw.ZTest = CompareFunction.Always;
                Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.LineGeometry = LineGeometry.Volumetric3D;
                Draw.LineEndCaps = LineEndCap.Round;
                for (int i = 0; i < AimDotCount; i++)
                    if (TryGetAimDot(i, out var point, out float dotSize, out _) && dotSize > .0001f)
                        Draw.Disc(Map(point) - drawOrigin, -cam.transform.forward, dotSize, Color.white);
                if (!ContestBarVisible) return;
                Vector3 middle = -cam.transform.up * (Radius + settings.meterOffset);
                Vector3 left = middle - cam.transform.right * settings.meterWidth * .5f;
                Vector3 right = middle + cam.transform.right * settings.meterWidth * .5f;
                float height = Mathf.Max(.05f, settings.meterHeight);
                Draw.Rectangle(middle, cam.transform.rotation, new Rect(-settings.meterWidth * .5f - .025f, -height * .5f - .025f, settings.meterWidth + .05f, height + .05f), Color.white);
                Draw.Rectangle(middle, cam.transform.rotation, new Rect(-settings.meterWidth * .5f, -height * .5f, settings.meterWidth, height), Color.black);
                float fill = LightBarFraction;
                if (fill > .001f) Draw.Rectangle(middle, cam.transform.rotation, new Rect(-settings.meterWidth * .5f, -height * .5f, settings.meterWidth * fill, height), Color.white);
                Vector3 mark = Vector3.Lerp(left, right, fill);
                // A monochrome notch marks the moving split; black team stays legible on a black field.
                Draw.Rectangle(mark, cam.transform.rotation, new Rect(-.0125f, -height * .75f, .025f, height * 1.5f), Color.white);
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
