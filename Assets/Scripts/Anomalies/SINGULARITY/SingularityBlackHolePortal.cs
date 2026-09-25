using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Non-damaging two-face field and transit. Normal movement remains
    /// dynamic outside capture; the sequence leases movement without changing
    /// input mode, mass, or life/respawn state.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(450)]
    public sealed partial class SingularityBlackHolePortal : MonoBehaviour
    {
        public enum TransitPhase { None, Entering, Exiting }
        [Header("Explicit scene references")]
        public SingularitySurface surface;
        public SingularityPlayerAdapter[] players = new SingularityPlayerAdapter[0];

        [Header("Grid attraction — visual only")]
        [Tooltip("Steady grid distortion in Edit and Play Mode. Independent of player/Core forces and transfer ripples.")]
        public bool enableGridAttraction = true;
        [Tooltip("Visual influence radius in front-face units. The rear pattern is proportionally smaller; this does not change gameplay attraction or spawn exclusion.")]
        [Min(.1f)] public float gridAttractionRadius = 4.5f;
        [Tooltip("Inward grid compression. Zero disables it; this field alone is softly limited so lines cannot pass through its center.")]
        [Range(0f, 5f)] public float gridAttractionPull = 1.25f;
        [Tooltip("Fraction of the radius used for smooth falloff. 1 gives a broad bell-like attraction; smaller values concentrate the falloff near its edge.")]
        [Range(.05f, 1f)] public float gridAttractionFeather = 1f;
        [Tooltip("Apply the same normalized visual attraction at the rear mouth. Borders and folds remain anchored.")]
        public bool gridAttractionOnRear = true;

        [Header("Attraction field — both faces")]
        [Min(.1f)] public float attractionRadius = 3.6f;
        [Tooltip("Mass-independent inward acceleration. Existing player propulsion remains unchanged.")]
        [Min(0f)] public float attractionAcceleration = 18f;
        [Tooltip("Fraction of radius occupied by smooth outer falloff; 1 creates a broad bell-shaped field.")]
        [Range(.05f, 1f)] public float attractionFeather = 1f;
        [Tooltip("Signed tangential acceleration. Zero disables orbiting; negative reverses the swirl.")]
        public float swirlAcceleration = 7f;
        [Tooltip("Optional outward acceleration, strongest in the outer field. High values can overcome attraction.")]
        [Min(0f)] public float centrifugalAcceleration = 2f;

        [Header("Capture and re-entry")]
        [Tooltip("Sequence trigger in front-face surface units, outside the visible event horizon. Rear uses the same normalized units.")]
        [Min(.05f)] public float captureRadius = 1.8f;
        [Tooltip("Visible horizon radius. Minimum scale is also limited so the stretched player fits inside it.")]
        [Min(.01f)] public float eventHorizonRadius = .575f;
        [Tooltip("Exit travels this far beyond the attraction field before returning movement control.")]
        [Min(0f)] public float exitPadding = .6f;
        [Tooltip("After exit, scaled-time immunity from field and recapture. Leaving the field is also required.")]
        [Min(0f)] public float cooldownSeconds = .8f;
        [Tooltip("Retained for scene compatibility. Animated exits use the explicit outward release speed.")]
        public bool preserveScreenVelocity = true;

        [Header("Entry — accelerating pull and stretch")]
        [Min(.05f)] public float entrySeconds = .7f;
        [Tooltip("Tidal timing separation: the near end leads into the mouth and the far end accelerates after it. Higher values leave a more pronounced trailing tail, without delaying the start of movement.")]
        [Range(.1f, 8f)] public float entryExponent = 3.5f;
        [Range(.005f, .3f)] public float minimumVisualScale = .045f;
        [Range(1f, 8f)] public float maximumStretch = 3f;

        [Header("Exit — fast burst, easing to player control")]
        [Min(.05f)] public float exitSeconds = .6f;
        [Range(.1f, 8f)] public float exitExponent = 3.5f;
        [Tooltip("Outward speed at handoff. Integrated into the decelerating exit curve, capped at distance / duration to preserve deceleration.")]
        [Min(0f)] public float exitReleaseSpeed = 2.5f;

        public event Action<SingularityPlayerAdapter> TransitStarted;
        public event Action<SingularityPlayerAdapter> TransitCompleted;
        public event Action<SingularityPlayerAdapter> TransitCancelled;
        public event Action<SingularityPlayerAdapter, bool> FaceTransferred;
        public event Action<SingularityPlayerAdapter, float, bool> TransitProgress;

        private sealed class EntryState
        {
            public SingularityPlayerAdapter actor;
            public PlayerControllerScript player;
            public Rigidbody body;
            public TransitPhase phase;
            public bool latched;
            public float nextAllowedTime;
            public float elapsed;
            public bool rear;
            public Vector2 center;
            public Vector2 entryOffset;
            public Vector2 exitDirection;
            public float exitDistance;
            public float scaleAtHorizon;
            public float entryVisualRadius;
            public int life;
        }
        private readonly Dictionary<SingularityPlayerAdapter, EntryState> entries = new Dictionary<SingularityPlayerAdapter, EntryState>(4);
        private readonly List<EntryState> states = new List<EntryState>(4);
        public int TeleportCount { get; private set; }
        public SingularityPlayerAdapter LastTeleportedPlayer { get; private set; }
        public float EffectiveAttractionRadius => EffectiveRadius(CurrentCenter);
        public float EffectiveCaptureRadius => Mathf.Min(Safe(captureRadius, .05f, 100f), EffectiveAttractionRadius * .95f);

        public TransitPhase PhaseOf(SingularityPlayerAdapter actor)
        { return actor != null && entries.TryGetValue(actor, out EntryState state) ? state.phase : TransitPhase.None; }
        public bool IsInTransit(SingularityPlayerAdapter actor) => PhaseOf(actor) != TransitPhase.None;
        public float ElapsedOf(SingularityPlayerAdapter actor)
        { return actor != null && entries.TryGetValue(actor, out EntryState state) ? state.elapsed : 0f; }

        public void Configure(SingularitySurface targetSurface, SingularityPlayerAdapter[] actors)
        {
            CancelAll();
            surface = targetSurface;
            players = actors ?? new SingularityPlayerAdapter[0];
        }
        private void FixedUpdate() { AdvanceTransit(Time.fixedDeltaTime, Time.time); }
        private void OnDisable() { CancelAll(); }
        private void LateUpdate()
        {
            // Cancellation runs even while scaled time is paused. Motion waits.
            for (int i = 0; i < states.Count; i++)
                if (states[i].phase != TransitPhase.None && !CanContinue(states[i])) Cancel(states[i]);
            CheckCoreCancellations();
        }

        /// <summary>Compatibility entry point: starts capture without skipping
        /// the new sequence or moving scaled time forward.</summary>
        public void EvaluateCapture(float scaledTime) { AdvanceTransit(0f, scaledTime); }

        /// <summary>Explicit scaled step for deterministic validation. Zero delta
        /// may begin capture, but cannot progress it or apply attraction.</summary>
        public void AdvanceTransit(float deltaTime, float scaledTime)
        {
            if (!isActiveAndEnabled) { CancelAll(); return; }
            if (!surface || !Finite(deltaTime) || !Finite(scaledTime) || deltaTime < 0f) return;
            Vector2 center = CurrentCenter;
            if (!Finite(center.x) || !Finite(center.y)) return;
            float radius = EffectiveRadius(center);
            float trigger = Mathf.Min(Safe(captureRadius, .05f, 100f), radius * .95f);
            for (int i = 0; i < states.Count; i++)
            {
                EntryState state = states[i];
                if (state.phase != TransitPhase.None && (!Configured(state.actor) || !CanContinue(state))) Cancel(state);
            }
            for (int i = 0; i < (players != null ? players.Length : 0); i++)
            {
                SingularityPlayerAdapter actor = players[i];
                if (!actor || !actor.isActiveAndEnabled || actor.Surface != surface) continue;
                EntryState state = GetState(actor);
                if (state.phase != TransitPhase.None)
                {
                    if (deltaTime > 0f) AdvanceState(state, deltaTime, scaledTime);
                    continue;
                }
                if (!state.player || !state.player.CanStartSingularityTransit || !state.body || state.body.isKinematic) continue;
                bool onFace = TryGetFaceCoordinates(actor.SurfacePosition, out Vector2 point, out bool rear);
                float distance = onFace ? Vector2.Distance(point, center) : float.PositiveInfinity;
                // Time alone never rearms a player still inside either mouth.
                if (state.latched && distance > radius + .025f) state.latched = false;
                if (state.latched || scaledTime < state.nextAllowedTime || !onFace) continue;
                if (distance <= trigger)
                {
                    Begin(state, point, rear, center, radius);
                    continue;
                }
                if (deltaTime > 0f && distance < radius)
                {
                    Vector2 acceleration = FieldAcceleration(point - center, radius,
                        attractionAcceleration, swirlAcceleration, centrifugalAcceleration, attractionFeather);
                    Vector2 chart = FaceVectorToChart(acceleration, rear);
                    state.body.AddForce(surface.transform.TransformVector(new Vector3(chart.x, 0f, chart.y)), ForceMode.Acceleration);
                }
            }
            AdvanceCores(deltaTime, scaledTime, center, radius, trigger);
        }

        private EntryState GetState(SingularityPlayerAdapter actor)
        {
            if (entries.TryGetValue(actor, out EntryState state)) return state;
            state = new EntryState { actor = actor, player = actor.Player, body = actor.GetComponent<Rigidbody>() };
            entries.Add(actor, state);
            states.Add(state);
            if (state.player != null) state.player.DeathStarted += OnPlayerDeath;
            return state;
        }

        private void Begin(EntryState state, Vector2 point, bool rear, Vector2 center, float fieldRadius)
        {
            Vector2 previousVelocity = state.actor.SurfaceVelocity;
            if (!state.player.TryAcquireSingularityTransit(this)) return;
            state.phase = TransitPhase.Entering;
            state.elapsed = 0f;
            state.rear = rear;
            state.center = center;
            state.entryOffset = point - center;
            state.life = state.player.LifeSequence;
            Vector2 incoming = -state.entryOffset;
            if (incoming.sqrMagnitude < .000001f)
            {
                incoming = ChartVectorToFace(previousVelocity, rear);
                if (incoming.sqrMagnitude < .000001f) incoming = Vector2.right;
            }
            state.exitDirection = incoming.normalized;
            state.exitDistance = Mathf.Min(fieldRadius + Mathf.Max(.15f, Safe(exitPadding, 0f, 100f)),
                DistanceToFaceEdge(center, state.exitDirection) - .1f);
            float radius = state.player.visualsController != null
                ? state.player.visualsController.baseRadius * Massive.Player.PlayerScaleAdjuster.SizeOf(state.player) : .6f;
            state.entryVisualRadius = Safe(radius, .001f, 1000f);
            state.scaleAtHorizon = Mathf.Min(Safe(minimumVisualScale, .005f, .3f),
                Safe(eventHorizonRadius, .01f, 100f) * .8f / Mathf.Max(.01f, radius * Safe(maximumStretch, 1f, 8f)));
            state.actor.SetPortalVisual(FaceVectorToChart(state.exitDirection, rear).normalized, 1f, 1f);
            TransitStarted?.Invoke(state.actor);
            TransitProgress?.Invoke(state.actor, 0f, false);
        }

        private void AdvanceState(EntryState state, float dt, float clock)
        {
            if (!CanContinue(state)) { Cancel(state); return; }
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
                TransitProgress?.Invoke(state.actor, t, false);
                if (t < 1f) return;
                // Centered and fully shrunk inside the horizon before swapping.
                if (!state.actor.TeleportToOppositeFace(preserveScreenVelocity)) { Cancel(state); return; }
                state.rear = !state.rear;
                state.phase = TransitPhase.Exiting;
                state.elapsed = 0f;
                state.actor.SetPortalVisual(FaceVectorToChart(state.exitDirection, state.rear).normalized,
                    state.scaleAtHorizon, Safe(maximumStretch, 1f, 8f));
                TeleportCount++;
                LastTeleportedPlayer = state.actor;
                FaceTransferred?.Invoke(state.actor, state.rear);
                TransitProgress?.Invoke(state.actor, 0f, true);
            }
            else
            {
                float travel = ExitTravel(t, exitExponent, duration, state.exitDistance, exitReleaseSpeed);
                state.actor.SetSurfacePosition(FaceToSurface(state.center + state.exitDirection * state.exitDistance * travel, state.rear));
                float form = Smooth01(travel);
                state.actor.SetPortalVisual(direction, Mathf.Lerp(state.scaleAtHorizon, 1f, form),
                    Mathf.Lerp(Safe(maximumStretch, 1f, 8f), 1f, form));
                TransitProgress?.Invoke(state.actor, t, true);
                if (t < 1f) return;
                float terminalSpeed = Mathf.Min(Safe(exitReleaseSpeed, 0f, 100f), state.exitDistance / duration);
                Vector2 chartVelocity = FaceVectorToChart(state.exitDirection * terminalSpeed, state.rear);
                state.player.ReleaseSingularityTransit(this,
                    surface.transform.TransformVector(new Vector3(chartVelocity.x, 0f, chartVelocity.y)));
                state.actor.ClearPortalVisual();
                state.phase = TransitPhase.None;
                state.latched = true;
                state.nextAllowedTime = clock + Safe(cooldownSeconds, 0f, 60f);
                TransitCompleted?.Invoke(state.actor);
            }
        }

        private bool CanContinue(EntryState state)
        {
            return surface && state.actor && state.actor.isActiveAndEnabled && state.actor.Surface == surface &&
                state.player && state.player.IsSingularityTransitOwnedBy(this) && state.player.CanContinueSingularityTransit &&
                state.player.LifeSequence == state.life;
        }
        private bool Configured(SingularityPlayerAdapter actor)
        {
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++) if (players[i] == actor) return true;
            return false;
        }
        private void OnPlayerDeath(PlayerControllerScript player)
        {
            for (int i = 0; i < states.Count; i++)
                if (states[i].player == player && states[i].phase != TransitPhase.None) Cancel(states[i]);
        }
        private void Cancel(EntryState state)
        {
            if (state.phase == TransitPhase.None) return;
            if (state.player) state.player.ReleaseSingularityTransit(this, Vector3.zero);
            if (state.actor) state.actor.ClearPortalVisual();
            state.phase = TransitPhase.None;
            state.latched = true;
            state.nextAllowedTime = Time.time + Safe(cooldownSeconds, 0f, 60f);
            if (state.actor) TransitCancelled?.Invoke(state.actor);
        }
        public void CancelAll()
        {
            CancelCoreEntries();
            for (int i = 0; i < states.Count; i++)
            {
                Cancel(states[i]);
                if (states[i].player != null) states[i].player.DeathStarted -= OnPlayerDeath;
            }
            states.Clear();
            entries.Clear();
        }

        private Vector2 CurrentCenter
        {
            get
            {
                if (!surface) return Vector2.zero;
                Vector3 local = surface.transform.InverseTransformPoint(transform.position);
                return new Vector2(local.x, local.z);
            }
        }
        private float EffectiveRadius(Vector2 center)
        {
            if (!surface) return Safe(attractionRadius, .1f, 100f);
            // Keep an exit margin on the flat face even with a huge authored field.
            float room = Mathf.Min(surface.Width * .5f - Mathf.Abs(center.x), surface.FrontHeight * .5f - Mathf.Abs(center.y));
            return Mathf.Max(.1f, Mathf.Min(Safe(attractionRadius, .1f, 100f), room - Mathf.Max(.25f, Safe(exitPadding, 0f, 100f)) - .1f));
        }
        private float DistanceToFaceEdge(Vector2 center, Vector2 direction)
        {
            float x = Mathf.Abs(direction.x) < .0001f ? float.PositiveInfinity :
                (surface.Width * .5f - Mathf.Sign(direction.x) * center.x) / Mathf.Abs(direction.x);
            float z = Mathf.Abs(direction.y) < .0001f ? float.PositiveInfinity :
                (surface.FrontHeight * .5f - Mathf.Sign(direction.y) * center.y) / Mathf.Abs(direction.y);
            return Mathf.Max(.2f, Mathf.Min(x, z));
        }
        public Vector2 FaceToSurface(Vector2 facePoint, bool rear)
        {
            if (!surface) return Vector2.zero;
            return new Vector2(facePoint.x, rear
                ? surface.RearStart + (surface.FrontHeight * .5f - facePoint.y) * surface.RearScale
                : facePoint.y + surface.FrontHeight * .5f);
        }
        public Vector2 FaceVectorToChart(Vector2 direction, bool rear)
        { return new Vector2(direction.x, rear && surface ? -direction.y * surface.RearScale : direction.y); }
        private Vector2 ChartVectorToFace(Vector2 direction, bool rear)
        { return new Vector2(direction.x, rear && surface ? -direction.y / surface.RearScale : direction.y); }

        /// <summary>Maps either flat face to front-sized normalized XZ. Bends
        /// intentionally are not capture or attraction regions.</summary>
        public bool TryGetFaceCoordinates(Vector2 position, out Vector2 point, out bool rear)
        {
            point = Vector2.zero;
            rear = false;
            if (!surface || !Finite(position.x) || !Finite(position.y)) return false;
            float distance = surface.Wrap(position.y);
            float halfHeight = surface.FrontHeight * .5f;
            if (distance < surface.TopStart)
            {
                point = new Vector2(position.x, distance - halfHeight);
                return true;
            }
            if (distance >= surface.RearStart && distance < surface.BottomStart)
            {
                rear = true;
                point = new Vector2(position.x, halfHeight - (distance - surface.RearStart) / surface.RearScale);
                return true;
            }
            return false;
        }

        public static float ExponentialEaseIn(float normalizedTime, float exponent)
        {
            float t = Finite(normalizedTime) ? Mathf.Clamp01(normalizedTime) : 0f;
            float k = Safe(exponent, .1f, 8f);
            return (Mathf.Exp(k * t) - 1f) / (Mathf.Exp(k) - 1f);
        }
        /// <summary>Inverse-exponential distance with an integrated linear term.
        /// Its end derivative equals the release speed instead of jumping to a
        /// different Rigidbody speed when the player's controls resume.</summary>
        public static float ExitTravel(float normalizedTime, float exponent, float seconds, float distance, float releaseSpeed)
        {
            float t = Finite(normalizedTime) ? Mathf.Clamp01(normalizedTime) : 0f;
            float k = Safe(exponent, .1f, 8f);
            float expTail = k / (Mathf.Exp(k) - 1f);
            float desiredTail = Mathf.Clamp01(Safe(releaseSpeed, 0f, 100f) * Safe(seconds, .05f, 10f) / Safe(distance, .01f, 100f));
            float linearWeight = (desiredTail - expTail) / (1f - expTail);
            float inverseExponential = 1f - ExponentialEaseIn(1f - t, k);
            return Mathf.Clamp01((1f - linearWeight) * inverseExponential + linearWeight * t);
        }
        public static Vector2 FieldAcceleration(Vector2 offset, float radius, float attraction, float swirl, float centrifugal, float feather)
        {
            if (!Finite(offset.x) || !Finite(offset.y)) return Vector2.zero;
            float safeRadius = Safe(radius, .01f, 1000f);
            float distance = offset.magnitude;
            if (distance < .0001f || distance >= safeRadius) return Vector2.zero;
            Vector2 radial = offset / distance;
            float outer = Smooth01((safeRadius - distance) / (safeRadius * Safe(feather, .05f, 1f)));
            float r = distance / safeRadius;
            float outward = Safe(centrifugal, 0f, 1000f) * r * r - Safe(attraction, 0f, 1000f);
            float turn = Finite(swirl) ? Mathf.Clamp(swirl, -1000f, 1000f) : 0f;
            return (radial * outward + new Vector2(-radial.y, radial.x) * turn) * outer;
        }
        private static float Smooth01(float value)
        { float t = Mathf.Clamp01(value); return t * t * t * (t * (t * 6f - 15f) + 10f); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Safe(float value, float minimum, float maximum)
        { return Finite(value) ? Mathf.Clamp(value, minimum, maximum) : minimum; }

        private void OnDrawGizmosSelected()
        {
            if (!surface) return;
            DrawCircles(EffectiveAttractionRadius, new Color(.45f, .65f, 1f, .6f));
            DrawCircles(EffectiveCaptureRadius, new Color(1f, .7f, .3f, .85f));
            DrawCircles(Safe(eventHorizonRadius, .01f, 100f), new Color(.8f, .45f, 1f, .9f));
            DrawCircles(EffectiveAttractionRadius + Safe(exitPadding, 0f, 100f), new Color(.5f, 1f, .7f, .5f));
        }
        private void DrawCircles(float radius, Color color)
        {
            Vector2 center = CurrentCenter;
            Gizmos.color = color;
            for (int face = 0; face < 2; face++)
            {
                Vector3 previous = Vector3.zero;
                for (int i = 0; i <= 64; i++)
                {
                    float angle = i * (Mathf.PI * 2f / 64f);
                    Vector2 coordinates = FaceToSurface(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, face == 1);
                    Vector3 world = surface.WorldPosition(coordinates.x, coordinates.y);
                    if (i > 0) Gizmos.DrawLine(previous, world);
                    previous = world;
                }
            }
        }
    }
}
