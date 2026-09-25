using System.Collections.Generic;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Singularity
{
    /// <summary>Scene-local portal presentation. Physics and transit ownership stay
    /// in the portal; pulses are bounded, and timed haptics cannot remain stuck on.</summary>
    [DisallowMultipleComponent]
    public sealed class SingularityBlackHoleFeedback : ImmediateModeShapeDrawer
    {
        public SingularityBlackHolePortal portal;
        public SingularityGridRenderer grid;
        [Header("Transfer ripple")]
        public bool showRipple = true;
        [Min(.05f)] public float rippleDuration = .55f;
        [Min(.1f)] public float rippleRadius = 4f;
        [Range(.005f, .2f)] public float rippleThickness = .045f;
        public Color rippleColor = new Color(1f, .9f, .65f, 1f);
        [Range(0f, .4f)] public float gridRippleAmplitude = .11f;
        [Range(.05f, 2f)] public float gridRippleWidth = .36f;
        [Header("Assigned controller haptics")]
        public bool enableHaptics = true;
        [Range(0f, 1f)] public float entryRumble = .28f;
        [Range(0f, 1f)] public float transferRumble = .6f;
        [Range(0f, 1f)] public float exitRumble = .2f;

        private struct Burst { public float began; public bool rear; public Vector2 center; }
        private struct HapticState { public float nextSend, burstUntil; }
        private readonly List<Burst> bursts = new List<Burst>(8);
        private readonly Dictionary<SingularityPlayerAdapter, HapticState> haptics =
            new Dictionary<SingularityPlayerAdapter, HapticState>(4);
        private SingularityBlackHolePortal boundPortal;
        public int TransferBursts { get; private set; }
        public int HapticCommandsSent { get; private set; }
        public int ActiveBurstCount => bursts.Count;

        public override void OnEnable()
        {
            base.OnEnable();
            if (!portal) portal = GetComponent<SingularityBlackHolePortal>();
            Bind();
        }
        public override void OnDisable()
        {
            Unbind(); bursts.Clear(); haptics.Clear();
            // Do not StopVibration globally: it could cancel an unrelated effect.
            // Every command has a short positive timeout and expires on its own.
            base.OnDisable();
        }
        public void Configure(SingularityBlackHolePortal source, SingularityGridRenderer targetGrid)
        { Unbind(); bursts.Clear(); haptics.Clear(); portal = source; grid = targetGrid; if (isActiveAndEnabled) Bind(); }
        private void Bind()
        {
            if (boundPortal == portal) return;
            bursts.Clear(); haptics.Clear();
            Unbind(); boundPortal = portal;
            if (!boundPortal) return;
            boundPortal.TransitStarted += OnStarted;
            boundPortal.TransitProgress += OnProgress;
            boundPortal.FaceTransferred += OnTransferred;
            boundPortal.CoreFaceTransferred += OnCoreTransferred;
            boundPortal.TransitCompleted += OnEnded;
            boundPortal.TransitCancelled += OnCancelled;
        }
        private void Unbind()
        {
            if (boundPortal)
            {
                boundPortal.TransitStarted -= OnStarted;
                boundPortal.TransitProgress -= OnProgress;
                boundPortal.FaceTransferred -= OnTransferred;
                boundPortal.CoreFaceTransferred -= OnCoreTransferred;
                boundPortal.TransitCompleted -= OnEnded;
                boundPortal.TransitCancelled -= OnCancelled;
            }
            boundPortal = null;
        }
        private void Update()
        {
            if (boundPortal != portal) Bind();
            for (int i = bursts.Count - 1; i >= 0; i--)
                if (Time.time - bursts[i].began >= Mathf.Max(.05f, rippleDuration)) bursts.RemoveAt(i);
        }
        private void OnStarted(SingularityPlayerAdapter actor) { SendRumble(actor, entryRumble * .5f, .06f, true); }
        private void OnProgress(SingularityPlayerAdapter actor, float progress, bool exiting)
        {
            float strength = exiting ? exitRumble * (1f - Mathf.Clamp01(progress))
                : entryRumble * Mathf.Lerp(.4f, 1f, Mathf.Clamp01(progress));
            SendRumble(actor, strength, strength * .45f, false);
        }
        private void OnTransferred(SingularityPlayerAdapter actor, bool destinationRear)
        {
            if (!portal || !portal.surface) return;
            EmitTransferBursts();
            HapticState state;
            haptics.TryGetValue(actor, out state);
            state.burstUntil = Time.unscaledTime + .13f; state.nextSend = 0f;
            haptics[actor] = state;
            SendRumble(actor, transferRumble, transferRumble * .7f, true);
        }
        private void OnCoreTransferred(SingularityAmplifierAdapter actor, bool destinationRear)
        {
            if (portal && portal.surface) EmitTransferBursts();
            // The neutral Core has no assigned controller; no rumble is sent.
        }
        private void EmitTransferBursts()
        {
            TransferBursts++;
            Vector3 center = portal.surface.transform.InverseTransformPoint(portal.transform.position);
            AddBurst(new Vector2(center.x, center.z), false);
            AddBurst(new Vector2(center.x, center.z), true);
        }
        private void OnEnded(SingularityPlayerAdapter actor)
        { SendRumble(actor, exitRumble, exitRumble * .4f, true); haptics.Remove(actor); }
        private void OnCancelled(SingularityPlayerAdapter actor) { haptics.Remove(actor); }
        private void AddBurst(Vector2 center, bool rear)
        {
            if (bursts.Count >= 8) bursts.RemoveAt(0);
            bursts.Add(new Burst { center = center, rear = rear, began = Time.time });
            if (!grid || gridRippleAmplitude <= 0f) return;
            var surface = portal.surface;
            float s = rear ? surface.RearStart + (surface.FrontHeight * .5f - center.y) * surface.RearScale
                : center.y + surface.FrontHeight * .5f;
            grid.EmitRipple(new Vector2(center.x, s), rippleDuration,
                rippleRadius * (rear ? surface.RearScale : 1f), gridRippleAmplitude, gridRippleWidth,
                .55f * (rear ? surface.RearScale : 1f));
        }
        private void SendRumble(SingularityPlayerAdapter actor, float low, float high, bool immediate)
        {
            if (!enableHaptics || !Application.isPlaying || Time.timeScale <= 0f ||
                !actor || !actor.Player || actor.Player.IsPseudoPlayer || !Rewired.ReInput.isReady) return;
            HapticState state;
            haptics.TryGetValue(actor, out state);
            if (!immediate && Time.unscaledTime < state.nextSend) return;
            if (Time.unscaledTime < state.burstUntil)
            { low = Mathf.Max(low, transferRumble); high = Mathf.Max(high, transferRumble * .7f); }
            state.nextSend = Time.unscaledTime + .075f; haptics[actor] = state;
            var inputPlayer = Rewired.ReInput.players.GetPlayer(actor.Player.playerID);
            if (inputPlayer == null) return;
            foreach (var joystick in inputPlayer.controllers.Joysticks)
            {
                if (!joystick.isConnected || !joystick.supportsVibration) continue;
                if (joystick.vibrationMotorCount > 0) { joystick.SetVibration(0, Mathf.Clamp01(low), .12f, false); HapticCommandsSent++; }
                if (joystick.vibrationMotorCount > 1) { joystick.SetVibration(1, Mathf.Clamp01(high), .12f, false); HapticCommandsSent++; }
            }
        }
        public override void DrawShapes(Camera cam)
        {
            if (!showRipple || bursts.Count == 0 || !portal || !portal.surface) return;
            var surface = portal.surface;
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                Draw.ZTest = CompareFunction.LessEqual;
                Draw.ThicknessSpace = ThicknessSpace.Meters;
                for (int i = 0; i < bursts.Count; i++)
                {
                    Burst b = bursts[i];
                    float u = Mathf.Clamp01((Time.time - b.began) / Mathf.Max(.05f, rippleDuration));
                    float scale = b.rear ? surface.RearScale : 1f;
                    Vector3 local = new Vector3(b.center.x * scale, (b.rear ? -surface.Depth : 0f) + .025f, b.center.y * scale);
                    Draw.Matrix = surface.transform.localToWorldMatrix * Matrix4x4.TRS(local, Quaternion.Euler(-90f, 0f, 0f), Vector3.one * scale);
                    float radius = Mathf.Lerp(.55f, rippleRadius, 1f - (1f - u) * (1f - u));
                    float alpha = (1f - u) * (1f - u) * rippleColor.a;
                    float brightness = b.rear ? surface.BacksideBrightness : 1f;
                    Color color = new Color(rippleColor.r * brightness, rippleColor.g * brightness, rippleColor.b * brightness, alpha);
                    Draw.Color = new Color(color.r, color.g, color.b, alpha * .18f);
                    Draw.Ring(Vector3.zero, radius, thickness: rippleThickness * 4f);
                    Draw.Color = color;
                    Draw.Ring(Vector3.zero, radius, thickness: rippleThickness);
                    Draw.Color = new Color(color.r, color.g, color.b, alpha * .5f);
                    Draw.Ring(Vector3.zero, Mathf.Max(.05f, radius - .16f * u), thickness: rippleThickness * .5f);
                }
            }
        }
    }
}
