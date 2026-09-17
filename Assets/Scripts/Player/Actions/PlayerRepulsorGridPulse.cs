using System.Collections.Generic;
using UnityEngine;

namespace Massive.Player
{
    /// <summary>A brief displacement of the existing grid, emitted at repulsor activation.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("MASSIVE/Player/Repulsor Grid Pulse")]
    public sealed class PlayerRepulsorGridPulse : MonoBehaviour
    {
        [Header("Grid Pulse")]
        public bool pulseEnabled = true;
        [Tooltip("Maximum grid displacement at player size 1. Does not affect gameplay forces.")]
        [Range(0f, 0.5f)] public float intensity = 0.12f;
        [Tooltip("Short outward travel from the activation point, at player size 1.")]
        [Min(0.1f)] public float travelRadius = 2.5f;
        [Min(0.05f)] public float duration = 0.45f;
        [Tooltip("Width of the localized crest and trough, at player size 1.")]
        [Min(0.05f)] public float packetWidth = 0.55f;
        [Tooltip("Small tangential curl alongside the radial wave. Zero produces a radial pulse.")]
        [Range(0f, 1f)] public float curl = 0.2f;
        [SerializeField] private VectorGridGPU grid;

        private struct Pulse
        {
            public bool active;
            public Vector3 origin;
            public float started, seconds, radius, amplitude, width, curl, startRadius;
        }

        private const int MaxPulses = 16;
        private static readonly List<PlayerRepulsorGridPulse> Sources = new List<PlayerRepulsorGridPulse>(8);
        private static readonly Vector4[] Origins = new Vector4[MaxPulses];
        private static readonly Vector4[] Shapes = new Vector4[MaxPulses];
        private static readonly int CountId = Shader.PropertyToID("_RepulsorPulseCount");
        private static readonly int OriginsId = Shader.PropertyToID("_RepulsorPulseOrigins");
        private static readonly int ShapesId = Shader.PropertyToID("_RepulsorPulseShapes");
        private static readonly int MetricId = Shader.PropertyToID("_RepulsorGridMetric");

        private readonly Pulse[] pulses = new Pulse[2];
        private int nextPulse;
        private PlayerControllerScript owner;
        private PlayerRepulsorAOE repulsor;
        private bool preview;
        private float previewElapsed, previewWorldRadius = -1f;
        private Vector3? previewOrigin;

        public bool PreviewActive => preview;

        private void OnEnable()
        {
            Register();
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (repulsor != null) repulsor.PulseStarted -= OnRepulsorActivated;
            repulsor = null;
            Sources.Remove(this);
            ClearPulses();
        }

        private void Register()
        {
            if (!Sources.Contains(this)) Sources.Add(this);
        }

        private void ResolveReferences()
        {
            if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
            if (!repulsor)
            {
                var candidate = owner != null
                    ? owner.GetComponentInChildren<PlayerRepulsorAOE>(true)
                    : GetComponentInChildren<PlayerRepulsorAOE>(true);
                if (candidate != null)
                {
                    repulsor = candidate;
                    repulsor.PulseStarted += OnRepulsorActivated;
                }
            }
            if (!grid && VectorGridGPU.Instance != null && VectorGridGPU.Instance.gameObject.scene == gameObject.scene)
                grid = VectorGridGPU.Instance;
        }

        private void Update()
        {
            Register();
            ResolveReferences();
            if (Application.isPlaying)
            {
                // Editor scrubbing cannot become a persistent effect in a live match.
                preview = false;
                for (int i = 0; i < pulses.Length; i++)
                    if (pulses[i].active && Time.time - pulses[i].started >= pulses[i].seconds)
                        pulses[i].active = false;
            }
            else
            {
                for (int i = 0; i < pulses.Length; i++) pulses[i].active = false;
            }
        }

        private void OnRepulsorActivated(PlayerRepulsorAOE source)
        {
            TriggerPulse();
        }

        /// <summary>Creates one bounded pulse at the player's current position. Normal play calls this from PulseStarted.</summary>
        public void TriggerPulse()
        {
            if (!pulseEnabled || !isActiveAndEnabled || !Application.isPlaying) return;
            ResolveReferences();
            Pulse pulse = MakePulse();
            pulse.active = true;
            pulse.started = Time.time;
            pulses[nextPulse] = pulse;
            nextPulse = (nextPulse + 1) % pulses.Length;
        }

        /// <summary>Scrubs a single edit-mode pulse; optional radius override is in world units.</summary>
        public void PreviewPulse(float elapsedSeconds, float worldRadius = -1f, Vector3? worldOrigin = null)
        {
            if (Application.isPlaying) return;
            Register();
            ResolveReferences();
            preview = true;
            previewElapsed = Mathf.Max(0f, elapsedSeconds);
            previewWorldRadius = worldRadius;
            previewOrigin = worldOrigin;
            RequestEditorRefresh();
        }

        public void StopPreview()
        {
            preview = false;
            RequestEditorRefresh();
        }

        public void ClearPulses()
        {
            preview = false;
            for (int i = 0; i < pulses.Length; i++) pulses[i].active = false;
            RequestEditorRefresh();
        }

        private Pulse MakePulse()
        {
            float size = Mathf.Max(0.01f, PlayerScaleAdjuster.SizeOf(owner != null ? (Component)owner : this));
            float radius = Mathf.Max(0.1f, travelRadius) * size;
            float bodyRadius = owner != null ? PlayerScaleAdjuster.BodyRadiusOf(owner) : 0.5f * size;
            float startRadius = repulsor != null && repulsor.IsPulseActive ? repulsor.StartRadiusWorld : bodyRadius;
            return new Pulse
            {
                origin = repulsor != null && repulsor.IsPulseActive ? repulsor.OriginWorld
                    : owner != null ? owner.transform.position : transform.position,
                seconds = Mathf.Max(0.05f, duration),
                radius = radius,
                amplitude = Mathf.Clamp(intensity, 0f, 0.5f) * size,
                width = Mathf.Max(0.05f, packetWidth) * size,
                curl = Mathf.Clamp01(curl),
                startRadius = Mathf.Clamp(startRadius, 0f, radius * 0.6f)
            };
        }

        /// <summary>Called by both existing grid passes. No additional renderer or mesh is created.</summary>
        public static void WriteGridProperties(VectorGridGPU target, MaterialPropertyBlock block)
        {
            int count = 0;
            if (target != null)
            {
                for (int i = Sources.Count - 1; i >= 0; i--)
                {
                    PlayerRepulsorGridPulse source = Sources[i];
                    if (source == null) { Sources.RemoveAt(i); continue; }
                    if (!source.isActiveAndEnabled || !source.pulseEnabled) continue;
                    source.ResolveReferences();
                    if (source.grid != target || source.gameObject.scene != target.gameObject.scene) continue;
                    if (!Application.isPlaying && source.preview)
                    {
                        Pulse pulse = source.MakePulse();
                        if (source.previewOrigin.HasValue) pulse.origin = source.previewOrigin.Value;
                        if (source.previewWorldRadius > 0f)
                        {
                            pulse.radius = source.previewWorldRadius;
                            pulse.startRadius = Mathf.Min(pulse.startRadius, pulse.radius * 0.6f);
                        }
                        Append(target, pulse, source.previewElapsed, ref count);
                    }
                    else if (Application.isPlaying)
                    {
                        for (int p = 0; p < source.pulses.Length; p++)
                            if (source.pulses[p].active)
                                Append(target, source.pulses[p], Time.time - source.pulses[p].started, ref count);
                    }
                }
                Vector3 scale = target.transform.lossyScale;
                block.SetVector(MetricId, new Vector4(Mathf.Max(0.0001f, Mathf.Abs(scale.x)), Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 0f, 0f));
            }
            block.SetInt(CountId, count);
            if (count > 0)
            {
                block.SetVectorArray(OriginsId, Origins);
                block.SetVectorArray(ShapesId, Shapes);
            }
        }

        private static void Append(VectorGridGPU grid, Pulse pulse, float elapsed, ref int count)
        {
            if (count >= MaxPulses || elapsed < 0f || elapsed >= pulse.seconds || pulse.amplitude <= 0f) return;
            Vector3 origin = grid.transform.InverseTransformPoint(pulse.origin);
            Origins[count] = new Vector4(origin.x, origin.y, elapsed / pulse.seconds, pulse.radius);
            Shapes[count] = new Vector4(pulse.amplitude, pulse.width, pulse.curl, pulse.startRadius);
            count++;
        }

        private static void RequestEditorRefresh()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }
    }
}
