// Assets/VectorGridNu/GridInteractor.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GridInteractor : MonoBehaviour
{
    public VectorGridGPU grid;
    public GridInteractionProfile profile;

    [Header("Overrides (optional)")]
    public bool overridesEnabled;
    public float overrideRadius = -1f;
    public float overrideStrength = -1f;
    [Range(0,0.9f)] public float overrideInnerFrac = -1f;

    // runtime state per-module (timers/activation)
    Rigidbody _rb;
    GridTuningMixer _mixer;
    readonly Dictionary<int, ModuleState> _state = new();
    class ModuleState { public float t = -1f; public bool active = true; }

    static Vector3 GetVelocity(Rigidbody rb)
    {
        #if UNITY_6000_0_OR_NEWER
        return rb ? rb.linearVelocity : Vector3.zero;
        #else
        return rb ? rb.velocity : Vector3.zero;
        #endif
    }

    void Reset()
    {
        if (!grid) grid = FindFirstObjectByType<VectorGridGPU>();
    }

    void OnEnable()
    {
        // cache RB once
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        // NEW: prefab-safe auto find
        if (!grid)
        {
        #if UNITY_2022_2_OR_NEWER
            grid = FindFirstObjectByType<VectorGridGPU>();
        #else
            grid = FindObjectOfType<VectorGridGPU>();
        #endif
        }

        // locate the mixer once (ok if null; mixer is optional)
    #if UNITY_2022_2_OR_NEWER
        _mixer = FindFirstObjectByType<GridTuningMixer>();
    #else
        _mixer = FindObjectOfType<GridTuningMixer>();
    #endif

        GridInteractionSystem.Register(this);
    }


    void OnDisable()
    {
        GridInteractionSystem.Unregister(this);
    }

    // Public controls
    public void Trigger(string tag = "")
    {
        if (!profile) return;
        for (int i = 0; i < profile.modules.Length; i++)
        {
            var m = profile.modules[i];
            if (!string.IsNullOrEmpty(tag) && m.tag != tag) continue;
            var s = GetState(i);
            // start one-shot or (re)start pulse/wave
            s.t = 0f; s.active = true;
        }
    }
    public void SetActive(string tag, bool active)
    {
        if (!profile) return;
        for (int i = 0; i < profile.modules.Length; i++)
        {
            var m = profile.modules[i];
            if (m.tag == tag) GetState(i).active = active;
        }
    }

    ModuleState GetState(int i)
    {
        if (!_state.TryGetValue(i, out var s)) _state[i] = s = new ModuleState();
        return s;
    }

    // Called by GridInteractionSystem once per frame
    public void EmitForces(List<VectorGridGPU.Force> outForces)
    {
        // NEW: lazy resolve
        if (!grid)
        {
        #if UNITY_2022_2_OR_NEWER
            grid = FindFirstObjectByType<VectorGridGPU>();
        #else
            grid = FindObjectOfType<VectorGridGPU>();
        #endif
            if (!grid) return;
        }

        if (!profile || profile.modules == null) return;

        Vector3 localPos = grid.transform.InverseTransformPoint(transform.position);
        Vector3 vel = GetVelocity(_rb);
        float speed = vel.magnitude;

        for (int i = 0; i < profile.modules.Length; i++)
        {
            var m = profile.modules[i];
            var s = GetState(i);
            if (!s.active && m.type != GridModuleType.ConstantRadial && m.type != GridModuleType.DirectionalWake && m.type != GridModuleType.Vortex && m.type != GridModuleType.Jiggle)
                continue;

            // base params
            float radius = m.radius * profile.radiusMultiplier;
            float strength = m.strength * profile.strengthMultiplier;
            float inner = m.innerFrac;

            if (overridesEnabled)
            {
                if (overrideRadius >= 0) radius = overrideRadius;
                if (overrideStrength >= 0) strength = overrideStrength;
                if (overrideInnerFrac >= 0) inner = overrideInnerFrac;
            }

            if (m.scaleBySpeed)
            {
                if (m.radiusOverSpeed != null) radius *= m.radiusOverSpeed.Evaluate(speed);
                if (m.strengthOverSpeed != null) strength *= m.strengthOverSpeed.Evaluate(speed);
            }

            bool useDir = m.directional || m.useVelocity || m.type == GridModuleType.DirectionalWake || m.type == GridModuleType.Vortex || m.type == GridModuleType.Jiggle;
            Vector3 dir = (m.fixedDirection.sqrMagnitude > 1e-6f ? m.fixedDirection.normalized : Vector3.right);
            if (m.useVelocity && speed > 1e-3f)
                dir = (m.pullAgainstVelocity ? -vel.normalized : vel.normalized);

            switch (m.type)
            {
                case GridModuleType.ConstantRadial:
                    outForces.Add(VectorGridGPU.MakeRadial(localPos, radius, strength, inner));
                    break;

                case GridModuleType.DirectionalWake:
                    outForces.Add(VectorGridGPU.MakeDirectional(localPos, radius, dir, strength, inner));
                    break;

                case GridModuleType.Vortex:
                    {
                        float ang = m.spinDegPerSec * Time.time * Mathf.Deg2Rad;
                        Vector3 spin = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                        outForces.Add(VectorGridGPU.MakeDirectional(localPos, radius, spin, strength, inner));
                        break;
                    }

                case GridModuleType.Jiggle:
                    {
                        float nx = Mathf.PerlinNoise(Time.time * m.noiseFrequency, 0.235f) * 2f - 1f;
                        float ny = Mathf.PerlinNoise(0.551f, Time.time * (m.noiseFrequency * 1.37f)) * 2f - 1f;
                        Vector3 jitter = new Vector3(nx, ny, 0).normalized;
                        float r = radius * (1f + m.noiseAmplitude * 0.2f);
                        float sMul = 1f + m.noiseAmplitude * 0.5f;
                        outForces.Add(VectorGridGPU.MakeDirectional(localPos, r, jitter, strength * sMul, inner));
                        break;
                    }

                case GridModuleType.BurstRadial:
                    {
                        if (s.t < 0f)
                        {
                            if (!m.autoStart) break;   // wait until Trigger(tag)
                            s.t = 0f;
                        }
                        float u = Mathf.Clamp01(s.t / Mathf.Max(0.0001f, m.duration));
                        float env = (m.envelope != null ? m.envelope.Evaluate(u) : 1f);
                        outForces.Add(VectorGridGPU.MakeRadial(localPos, radius, strength * env, inner));
                        s.t += Time.deltaTime;
                        if (s.t >= m.duration) s.t = -1f; // done
                        break;
                    }

                case GridModuleType.Pulse:
                    {
                        if (s.t < 0f) {
                            if (!m.autoStart) break;
                            s.t = 0f;
                        }

                        // 0..1 through the pulse
                        float u   = Mathf.Repeat(s.t / Mathf.Max(0.0001f, m.duration), 1f);

                        // amplitude envelope
                        float env = (m.envelope != null ? m.envelope.Evaluate(u) : 1f);

                        // radius growth (shockwave): multiply base radius
                        float rMul = (m.radiusOverTime != null && m.radiusOverTime.keys != null && m.radiusOverTime.keys.Length > 0)
                                    ? Mathf.Max(0.0001f, m.radiusOverTime.Evaluate(u))
                                    : 1f;
                        float rNow = radius * rMul;

                        // outward if repel, inward otherwise
                        float sign = m.repel ? -1f : 1f;

                        outForces.Add(VectorGridGPU.MakeRadial(localPos, rNow, sign * strength * env, inner));

                        s.t += Time.deltaTime;
                        if (!m.loopPulse && s.t >= m.duration) s.t = -1f;
                        break;
                    }


                case GridModuleType.TravelingWave:
                    {
                        if (s.t < 0f)
                        {
                            if (!m.autoStart) break;   // NEW
                            s.t = 0f;
                        }
                        float traveled = m.waveSpeed * s.t;
                        float rMid = radius + traveled;
                        float rMin = Mathf.Max(0.1f, rMid - m.waveThickness * 0.5f);
                        float rMax = rMid + m.waveThickness * 0.5f;
                        outForces.Add(VectorGridGPU.MakeRadial(localPos, rMin, strength * 0.5f, inner));
                        outForces.Add(VectorGridGPU.MakeRadial(localPos, rMax, -strength * 0.5f, inner));
                        s.t += Time.deltaTime;
                        if (!m.loopPulse && rMid > radius + 4f * m.waveSpeed) s.t = -1f;
                        break;
                    }
            }
        }

        if (_mixer != null)
        {
            // Walk again but only ConstantRadial modules with tuningEnabled
            for (int i = 0; i < profile.modules.Length; i++)
            {
                var m = profile.modules[i];
                if (m.type != GridModuleType.ConstantRadial) continue;
                var s = GetState(i);
                if (!s.active || !m.tuningEnabled) continue;

                _mixer.Add(new GridTuningMixer.Contribution
                {
                    blend = Mathf.Clamp01(m.tuningBlend),
                    springK = m.tuningSpringK,
                    damping = m.tuningDamping,
                    falloffMode = Mathf.Clamp(m.tuningFalloffMode, 0, 4),
                    falloffExp = m.tuningFalloffExp,
                    sharpness = m.tuningSharpness,
                    maxSpeed = m.tuningMaxSpeed
                });
            }
        }


    }
}
