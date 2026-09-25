using System;
using System.Collections.Generic;
using Massive.Multiplier;
using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityBlackHolePortal
    {
        private sealed class CoreEntry
        {
            public SingularityAmplifierAdapter actor;
            public AmplifierCoreGameplay core;
            public Rigidbody body;
            public TransitPhase phase;
            public bool latched, rear;
            public float nextAllowedTime, elapsed, exitDistance, scaleAtHorizon, entryVisualRadius;
            public Vector2 center, entryOffset, exitDirection;
            public Action<UnityEngine.Object> revoked;
        }
        private readonly List<SingularityAmplifierAdapter> coreActors = new List<SingularityAmplifierAdapter>(2);
        private readonly Dictionary<SingularityAmplifierAdapter, CoreEntry> coreEntries = new Dictionary<SingularityAmplifierAdapter, CoreEntry>(2);
        private readonly List<CoreEntry> coreStates = new List<CoreEntry>(2);
        public int CoreTeleportCount { get; private set; }
        public SingularityAmplifierAdapter LastTeleportedCore { get; private set; }
        public event Action<SingularityAmplifierAdapter> CoreTransitStarted;
        public event Action<SingularityAmplifierAdapter> CoreTransitCompleted;
        public event Action<SingularityAmplifierAdapter> CoreTransitCancelled;
        public event Action<SingularityAmplifierAdapter, bool> CoreFaceTransferred;

        public TransitPhase PhaseOf(SingularityAmplifierAdapter actor)
        { return actor != null && coreEntries.TryGetValue(actor, out CoreEntry state) ? state.phase : TransitPhase.None; }
        public bool IsInTransit(SingularityAmplifierAdapter actor) => PhaseOf(actor) != TransitPhase.None;
        public float ElapsedOf(SingularityAmplifierAdapter actor)
        { return actor != null && coreEntries.TryGetValue(actor, out CoreEntry state) ? state.elapsed : 0f; }

        public void RegisterCore(SingularityAmplifierAdapter actor)
        {
            if (actor != null && !coreActors.Contains(actor)) coreActors.Add(actor);
        }
        public void UnregisterCore(SingularityAmplifierAdapter actor)
        {
            if (ReferenceEquals(actor, null)) return;
            coreActors.Remove(actor);
            if (!coreEntries.TryGetValue(actor, out CoreEntry state)) return;
            RemoveCoreEntry(state);
        }
        private void RemoveCoreEntry(CoreEntry state)
        {
            CancelCore(state);
            if (state.core != null) state.core.ExternalTransitRevoked -= state.revoked;
            coreEntries.Remove(state.actor);
            coreStates.Remove(state);
        }
        private CoreEntry CoreState(SingularityAmplifierAdapter actor)
        {
            if (coreEntries.TryGetValue(actor, out CoreEntry state)) return state;
            state = new CoreEntry { actor = actor, core = actor.Core, body = actor.Body };
            state.revoked = owner => { if (owner == this) CancelCore(state); };
            if (state.core != null) state.core.ExternalTransitRevoked += state.revoked;
            coreEntries.Add(actor, state);
            coreStates.Add(state);
            return state;
        }
        private bool CoreReady(CoreEntry state)
        {
            return surface && state.actor && state.actor.isActiveAndEnabled && state.actor.Surface == surface &&
                state.actor.gameObject.scene == gameObject.scene && state.actor.IsRenderingOnSurface && state.body &&
                state.core && state.core.isActiveAndEnabled && !state.core.IsCaptured && !state.core.IsPresentationOnly;
        }
        private bool CoreCanContinue(CoreEntry state)
        { return CoreReady(state) && state.core.IsTransitOwnedBy(this); }

        private void CheckCoreCancellations()
        {
            for (int i = coreStates.Count - 1; i >= 0; i--)
                if (coreStates[i].phase != TransitPhase.None && !CoreCanContinue(coreStates[i])) CancelCore(coreStates[i]);
        }
        private void AdvanceCores(float dt, float clock, Vector2 center, float radius, float trigger)
        {
            CheckCoreCancellations();
            for (int i = coreActors.Count - 1; i >= 0; i--)
            {
                var actor = coreActors[i];
                if (!actor) { coreActors.RemoveAt(i); continue; }
                if (!actor.isActiveAndEnabled || actor.Surface != surface || actor.gameObject.scene != gameObject.scene) continue;
                CoreEntry state = CoreState(actor);
                if (state.phase != TransitPhase.None)
                {
                    if (dt > 0f) AdvanceCoreState(state, dt, clock);
                    continue;
                }
                if (!CoreReady(state) || state.body.isKinematic || state.core.IsInExternalTransit) continue;
                bool onFace = TryGetFaceCoordinates(actor.SurfacePosition, out Vector2 point, out bool rear);
                float distance = onFace ? Vector2.Distance(point, center) : float.PositiveInfinity;
                if (state.latched && distance > radius + .025f) state.latched = false;
                if (state.latched || clock < state.nextAllowedTime || !onFace) continue;
                if (distance <= trigger) { BeginCore(state, point, rear, center, radius); continue; }
                if (dt > 0f && distance < radius)
                {
                    Vector2 acceleration = FieldAcceleration(point - center, radius,
                        attractionAcceleration, swirlAcceleration, centrifugalAcceleration, attractionFeather);
                    Vector2 chart = FaceVectorToChart(acceleration, rear);
                    state.body.AddForce(surface.transform.TransformVector(new Vector3(chart.x, 0f, chart.y)), ForceMode.Acceleration);
                }
            }
        }
        private void BeginCore(CoreEntry state, Vector2 point, bool rear, Vector2 center, float radius)
        {
            Vector2 previousVelocity = state.actor.SurfaceVelocity;
            if (!state.core.TryAcquireTransit(this)) return;
            state.phase = TransitPhase.Entering;
            state.elapsed = 0f;
            state.rear = rear;
            state.center = center;
            state.entryOffset = point - center;
            Vector2 incoming = -state.entryOffset;
            if (incoming.sqrMagnitude < .000001f)
            {
                incoming = ChartVectorToFace(previousVelocity, rear);
                if (incoming.sqrMagnitude < .000001f) incoming = Vector2.right;
            }
            state.exitDirection = incoming.normalized;
            state.exitDistance = Mathf.Min(radius + Mathf.Max(.15f, Safe(exitPadding, 0f, 100f)),
                DistanceToFaceEdge(center, state.exitDirection) - .1f);
            float actorRadius = state.actor.CollisionRadius /
                Mathf.Max(.001f, Mathf.Abs(surface.transform.lossyScale.x));
            state.entryVisualRadius = Safe(actorRadius, .001f, 1000f);
            state.scaleAtHorizon = Mathf.Min(Safe(minimumVisualScale, .005f, .3f),
                Safe(eventHorizonRadius, .01f, 100f) * .8f / Mathf.Max(.01f, actorRadius * Safe(maximumStretch, 1f, 8f)));
            state.actor.SetPortalVisual(FaceVectorToChart(state.exitDirection, rear).normalized, 1f, 1f);
            CoreTransitStarted?.Invoke(state.actor);
        }
        private void AdvanceCoreState(CoreEntry state, float dt, float clock)
        {
            if (!CoreCanContinue(state)) { CancelCore(state); return; }
            float duration = state.phase == TransitPhase.Entering ? Safe(entrySeconds, .05f, 10f) : Safe(exitSeconds, .05f, 10f);
            state.elapsed = Mathf.Min(duration, state.elapsed + dt);
            float t = Mathf.Clamp01(state.elapsed / duration);
            Vector2 direction = FaceVectorToChart(state.exitDirection, state.rear).normalized;
            if (state.phase == TransitPhase.Entering)
            {
                Vector3 pose = EvaluateEntryPose(t, entryExponent, state.entryOffset.magnitude,
                    state.entryVisualRadius, state.scaleAtHorizon, maximumStretch);
                state.actor.SetSurfacePosition(FaceToSurface(state.center - state.exitDirection * pose.x, state.rear));
                state.actor.SetPortalVisual(direction, pose.y, pose.z);
                if (t < 1f) return;
                if (!state.actor.TeleportToOppositeFace(preserveScreenVelocity)) { CancelCore(state); return; }
                state.rear = !state.rear;
                state.phase = TransitPhase.Exiting;
                state.elapsed = 0f;
                state.actor.SetPortalVisual(FaceVectorToChart(state.exitDirection, state.rear).normalized,
                    state.scaleAtHorizon, Safe(maximumStretch, 1f, 8f));
                CoreTeleportCount++;
                LastTeleportedCore = state.actor;
                CoreFaceTransferred?.Invoke(state.actor, state.rear);
            }
            else
            {
                float travel = ExitTravel(t, exitExponent, duration, state.exitDistance, exitReleaseSpeed);
                state.actor.SetSurfacePosition(FaceToSurface(state.center + state.exitDirection * state.exitDistance * travel, state.rear));
                float form = Smooth01(travel);
                state.actor.SetPortalVisual(direction, Mathf.Lerp(state.scaleAtHorizon, 1f, form),
                    Mathf.Lerp(Safe(maximumStretch, 1f, 8f), 1f, form));
                if (t < 1f) return;
                float speed = Mathf.Min(Safe(exitReleaseSpeed, 0f, 100f), state.exitDistance / duration);
                Vector2 velocity = FaceVectorToChart(state.exitDirection * speed, state.rear);
                state.core.ReleaseTransit(this, surface.transform.TransformVector(new Vector3(velocity.x, 0f, velocity.y)));
                state.actor.ClearPortalVisual();
                state.phase = TransitPhase.None;
                state.latched = true;
                state.nextAllowedTime = clock + Safe(cooldownSeconds, 0f, 60f);
                CoreTransitCompleted?.Invoke(state.actor);
            }
        }
        private void CancelCore(CoreEntry state)
        {
            if (state.phase == TransitPhase.None) return;
            if (state.core) state.core.ReleaseTransit(this, Vector3.zero);
            if (state.actor) state.actor.ClearPortalVisual();
            state.phase = TransitPhase.None;
            state.latched = true;
            state.nextAllowedTime = Time.time + Safe(cooldownSeconds, 0f, 60f);
            if (state.actor) CoreTransitCancelled?.Invoke(state.actor);
        }
        private void CancelCoreEntries()
        {
            for (int i = coreStates.Count - 1; i >= 0; i--) RemoveCoreEntry(coreStates[i]);
            // Registration remains while the portal is disabled. Re-enabling it
            // can accept the still-live Core, just like the configured players.
        }
    }
}
