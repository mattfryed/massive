using System.Collections.Generic;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    public sealed partial class DysonSpherePanels
    {
        // Four float4s match the shader's structured buffer exactly. Keep sampling
        // coordinates separate from the camera-facing surface offset.
        private struct EnergySegment
        {
            public Vector4 start, end, fieldStart, fieldEnd;
        }

        private sealed class EnergyCameraDraw
        {
            public Camera camera;
            public ComputeBuffer segments;
            public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public readonly CommandBuffer command = new CommandBuffer { name = "Dyson prismatic outlines" };
            public bool attached;

            public void Detach()
            {
                if (attached && camera) camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, command);
                attached = false;
                command.Clear();
            }

            public void Dispose()
            {
                Detach();
                segments?.Release();
                command.Release();
            }
        }

        private static readonly int GpuSegments = Shader.PropertyToID("_Segments");
        private static readonly int GpuField = Shader.PropertyToID("_EnergyField");
        private static readonly int GpuStroke = Shader.PropertyToID("_Stroke");
        private static readonly int GpuLocalToWorld = Shader.PropertyToID("_ShellToWorld");
        private static readonly int GpuWorldToLocal = Shader.PropertyToID("_WorldToShell");
        private readonly List<EnergySegment> _energySegments = new List<EnergySegment>();
        private readonly List<EnergyCameraDraw> _energyCameras = new List<EnergyCameraDraw>();
        private Material _energyGpuMaterial;
        private EnergyCameraDraw _energyCameraDraw;
        private bool _gpuEnergyActive;
#if UNITY_EDITOR
        // Nonserialized reference path for identical-pose visual/performance comparisons.
        internal bool forceCpuEnergyOutlinesForValidation;
#endif

        private void BeginGpuEnergyOutlines(Camera cam)
        {
            _gpuEnergyActive = false;
            _energyCameraDraw = null;
            _energySegments.Clear();
            for (int i = _energyCameras.Count - 1; i >= 0; i--)
            {
                var entry = _energyCameras[i];
                if (!entry.camera) { entry.Dispose(); _energyCameras.RemoveAt(i); }
                else if (entry.camera == cam) _energyCameraDraw = entry;
            }
            // The reference renderer also covers other pipelines and non-volumetric
            // inspector settings. The current game uses Built-in, round 3D strokes.
            if (!energyOutlines || !cam || GraphicsSettings.currentRenderPipeline != null ||
                lineGeometry != LineGeometry.Volumetric3D || Draw.LineEndCaps != LineEndCap.Round ||
                Draw.UseDashes || Draw.BlendMode != ShapesBlendMode.Transparent ||
                !SystemInfo.supportsInstancing || !SystemInfo.supportsComputeShaders || SystemInfo.graphicsShaderLevel < 45)
                return;
#if UNITY_EDITOR
            if (forceCpuEnergyOutlinesForValidation) return;
#endif
            if (!_energyGpuMaterial)
            {
                // Resources makes the shader available in builds without modifying
                // the shared Amplifier material or adding project-wide shader settings.
                var shader = Resources.Load<Shader>("DysonPrismaticOutline");
                if (!shader || !shader.isSupported) return;
                _energyGpuMaterial = new Material(shader)
                {
                    name = "Dyson prismatic outlines (runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                    enableInstancing = true
                };
                Camera.onPreCull += PrepareGpuEnergyCamera;
            }
            if (_energyCameraDraw == null)
            {
                _energyCameraDraw = new EnergyCameraDraw { camera = cam };
                _energyCameras.Add(_energyCameraDraw);
            }
            int capacity = Mathf.Max(32, _panels.Count * 3 * 32);
            if (_energySegments.Capacity < capacity) _energySegments.Capacity = capacity;
            if (_energyCameraDraw.segments == null || _energyCameraDraw.segments.count < capacity)
            {
                _energyCameraDraw.segments?.Release();
                _energyCameraDraw.segments = new ComputeBuffer(capacity, 64);
            }
            _gpuEnergyActive = true;
        }

        private void QueueGpuEnergyEdge(Vector3 start, Vector3 end, Vector3 fieldStart, Vector3 fieldEnd,
            float visible, float alpha, int samples)
        {
            Vector3 previous = start, previousField = fieldStart;
            for (int i = 1; i <= samples; i++)
            {
                float t = Mathf.Min((float)i / samples, visible);
                Vector3 next = Vector3.Lerp(start, end, t);
                Vector3 nextField = Vector3.Lerp(fieldStart, fieldEnd, t);
                _energySegments.Add(new EnergySegment
                {
                    start = new Vector4(previous.x, previous.y, previous.z, alpha),
                    end = next,
                    fieldStart = previousField,
                    fieldEnd = nextField
                });
                if (t >= visible) break;
                previous = next;
                previousField = nextField;
            }
        }

        private void SubmitGpuEnergyOutlines()
        {
            if (!_gpuEnergyActive || _energySegments.Count == 0) return;
            var draw = _energyCameraDraw;
            draw.segments.SetData(_energySegments, 0, 0, _energySegments.Count);
            var props = draw.properties;
            props.SetBuffer(GpuSegments, draw.segments);
            props.SetVector(GpuField, new Vector4(_energyScale, _energyTime,
                Mathf.Clamp01(outlineEnergySaturation), Mathf.Lerp(1f, 1.65f, _energyEmission01) * Mathf.Max(0f, outlineEnergyBrightness)));
            props.SetVector(GpuStroke, new Vector4(Draw.Thickness, (int)thicknessSpace, (int)Draw.ScaleMode, 0f));
            props.SetMatrix(GpuLocalToWorld, transform.localToWorldMatrix);
            props.SetMatrix(GpuWorldToLocal, transform.worldToLocalMatrix);
            // Reuse Shapes' capsule asset: identical cap tessellation, segment joins
            // and subpixel coverage, without creating a mesh per segment or camera.
            draw.command.DrawMeshInstancedProcedural(ShapesAssets.Instance.meshCapsule[(int)Draw.DetailLevel],
                0, _energyGpuMaterial, 0, _energySegments.Count, props);
            draw.camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, draw.command);
            draw.attached = true;
        }

        private void ClearGpuEnergyCamera(Camera cam)
        {
            for (int i = 0; i < _energyCameras.Count; i++)
                if (_energyCameras[i].camera == cam) _energyCameras[i].Detach();
        }

        private void PrepareGpuEnergyCamera(Camera cam)
        {
            // Built-in can consume BeforeImageEffects after onPostRender: removing
            // commands there makes the strokes disappear. Retire them at the next
            // pre-cull, before Shapes' pre-render callback, even if its layer filter
            // skips this shell. Explicit preview draws are retired by DrawShapes.
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            ClearGpuEnergyCamera(cam);
        }

        private void ReleaseGpuEnergyOutlines()
        {
            Camera.onPreCull -= PrepareGpuEnergyCamera;
            foreach (var draw in _energyCameras) draw.Dispose();
            _energyCameras.Clear();
            _energySegments.Clear();
            _energyCameraDraw = null;
            _gpuEnergyActive = false;
            if (_energyGpuMaterial)
            {
                if (Application.isPlaying) Destroy(_energyGpuMaterial);
                else DestroyImmediate(_energyGpuMaterial);
                _energyGpuMaterial = null;
            }
        }
    }
}
