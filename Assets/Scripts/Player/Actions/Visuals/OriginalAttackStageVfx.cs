using UnityEngine;

namespace Massive.Player
{
    /// <summary>Prefab-owned presentation. The attack controller remains the authority for timing and damage.</summary>
    [DisallowMultipleComponent]
    public sealed class OriginalAttackStageVfx : MonoBehaviour
    {
        public AttackStageType stage;
        [Tooltip("Start added effects at the damage window instead of the start of windup.")]
        public bool startAtActivation;
        [Min(0), Tooltip("Maximum time for particles to finish after emission stops.")]
        public float tailSeconds = 2f;
        [Tooltip("Follow player position while particles finish, retaining this stage's final direction. Disable to hold the final world position too.")]
        public bool followDuringTail = true;
        [Tooltip("Placement in attack space: X across, Y up, Z forward. Does not change gameplay reach.")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public Vector3 scale = Vector3.one;
        public bool IsRunning { get; private set; }
        public bool IsEmitting { get; private set; }
        public Vector3 LastDirection => direction;
        public AttackTrailGPU[] Emitters { get { Cache(); return emitters; } }
        public static int ContentRevision { get; private set; }
        public static void InvalidateContent() { ContentRevision++; }
        ParticleSystem[] systems;
        AttackTrailGPU[] emitters;
        bool started;
        float tail, duration = 1;
        Vector3 direction = Vector3.forward;
        Vector3 lastPosition;
        Quaternion lastRotation;

        void Cache()
        {
            if (systems != null) return;
            systems = GetComponentsInChildren<ParticleSystem>(true);
            emitters = GetComponentsInChildren<AttackTrailGPU>(true);
        }
        public void Begin()
        {
            Cache(); StopImmediately();
            IsRunning = true; tail = 0; started = false;
            RememberPose();
        }
        public void Sample(AttackStage attack, float normalizedTime, float activationStart, Vector3 aim, float dt)
        {
            Cache(); duration = attack.Duration; direction = aim;
            RememberPose();
            if (!started && (!startAtActivation || normalizedTime >= activationStart))
            {
                started = true; IsEmitting = true;
                foreach (var ps in systems)
                    if (ps && ps.gameObject.activeInHierarchy) ps.Play(false);
            }
            SampleEmitters(normalizedTime, IsEmitting, dt);
            SimulateEditorParticles(dt);
        }
        void RememberPose()
        {
            lastPosition = transform.position; lastRotation = transform.rotation;
        }
        public void EndEmission()
        {
            if (!IsRunning) return;
            IsEmitting = false;
            foreach (var ps in systems)
                if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        public void TickTail(float dt)
        {
            if (!IsRunning || IsEmitting) return;
            // Local velocity modules still use the emitter axes even with World simulation.
            // Freeze the finished stage's rotation, including inherited player rotation.
            transform.SetPositionAndRotation(followDuringTail ? transform.position : lastPosition, lastRotation);
            tail += dt;
            SampleEmitters(1, false, dt);
            SimulateEditorParticles(dt);
            if (tail >= tailSeconds) StopImmediately();
        }
        void SampleEmitters(float t, bool emitting, float dt)
        {
            foreach (var gpu in emitters)
                if (gpu && gpu.isActiveAndEnabled)
                    gpu.SampleStage(t, emitting, gpu.transform.forward, duration, dt);
        }
        void SimulateEditorParticles(float dt)
        {
            if (Application.IsPlaying(gameObject)) return;
            foreach (var ps in systems)
                if (ps && ps.gameObject.activeInHierarchy) ps.Simulate(Mathf.Max(0, dt), false, false, false);
        }
        public void StopImmediately()
        {
            Cache(); IsRunning = IsEmitting = started = false;
            foreach (var ps in systems)
                if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!Application.IsPlaying(gameObject))
                foreach (var gpu in emitters) if (gpu) gpu.StopEditorPreview();
        }
        void OnDisable() { StopImmediately(); }
    }
}
