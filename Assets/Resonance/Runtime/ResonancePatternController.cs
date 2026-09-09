using System.Collections.Generic;
using UnityEngine;

namespace Massive.Resonance
{
    public enum ResonanceLayoutFit { Stretch, RoundedRectangle }
    public enum ResonanceEnergyFlow { FromPatternCenter, AlongArc, Still }
    public enum ResonanceBlend { Alpha, Additive, Premultiplied, Screen, Multiply }
    public enum ResonancePlayerResponse { PatternDefault, HardWall, PassThrough, Sludge }
    public enum ResonanceCoreResponse { PatternDefault, MagneticRepulsion, PassThrough }
    public enum ResonanceMagneticPath { StopAndReflect, CurvedGlide }

    [System.Serializable]
    public sealed class ResonanceEnergyLayer
    {
        [Tooltip("Width relative to the arc's resting thickness. The diffuse haze is visual only.")]
        [Range(.01f, 6f)] public float width = 1f;
        [Range(0f, 1f)] public float opacity = .6f;
        [Min(0f)] public float brightness = 1f;
        [Range(.5f, 8f)] public float softness = 2f;
        [ColorUsage(true, true)] public Color color = new Color(.65f, .9f, 1f, 1f);
        public Vector4 Parameters => new Vector4(width, opacity, brightness, softness);
    }
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ResonancePatternController : MonoBehaviour
    {
        public ResonancePatternDefinition definition;
        public Material segmentMaterial;
        [Header("Renderer option — same pattern and interaction model")]
        public ResonanceRendering rendering = ResonanceRendering.OptionAContinuous;
        public Material particleMaterial;
        public ResonanceParticleSettings particles = new ResonanceParticleSettings();
        [Header("Layout (local XZ, keep transform scale at one)")]
        public Vector2 patternScale = Vector2.one;
        public ResonanceLayoutFit layoutFit;
        [Tooltip("Preserves local curve rounding while moving its anchors across a rectangular footprint. Zero reproduces stretched tangents.")]
        [Range(0f, 1f)] public float curvaturePreservation = 0.85f;
        public float rotationDegrees;
        [Min(0.1f)] public float colliderHeight = 3f;
        public float surfaceHeight = 0.04f;
        [Range(0.5f, 10f)] public float sampleDegrees = 2f;
        [Header("Optional arena placement")]
        public ArenaBoundsFromVectorGrid arenaBounds;
        [Tooltip("Keep the inner circle circular when fitting. Disable to stretch the whole pattern across a rectangular arena.")]
        public bool preserveAspectWhenFitting = true;
        [Range(0.1f, 1f)] public float arenaCoverage = 0.85f;
        [Header("Interaction")]
        [Tooltip("Hard walls use Unity's layer collision matrix. No global layer settings are modified.")]
        [Range(0, 31)] public int obstacleLayer;
        public PhysicsMaterial wallMaterial;
        public LayerMask fieldAffectedLayers = ~0;
        public bool fieldsAffectOnlyAmplifierCores = true;
        [Tooltip("Overrides player response on interactive arcs. Visual Only arcs always stay non-interactive.")]
        public ResonancePlayerResponse playerResponse;
        public ResonanceCoreResponse coreResponse;
        [Tooltip("Enemies pass through interactive arcs using the same sludge multiplier, drag and feather distance as players. Includes chase and lunge movement.")]
        public bool enemiesUseSludge = true;
        [Range(.05f, 1f)] public float sludgeMovementMultiplier = .3f;
        [Min(0f)] public float sludgeDrag = 8f;
        [Tooltip("Player/enemy sludge feather distance beyond the contact surface, in world units. Cores have a separate Magnetic Reach.")]
        [Min(.01f)] public float interactionReach = 1f;
        [Header("Magnetic Core — global path (independent of arc lens settings)")]
        public ResonanceMagneticPath magneticPath = ResonanceMagneticPath.CurvedGlide;
        [Tooltip("Core soft-field distance beyond the collision surface. Increase for a wider, more readable curved approach.")]
        [Min(.01f)] public float magneticReach = .8f;
        [Tooltip("Curved Glide retains motion along the surface while reversing its inward component. Stop And Reflect keeps the previous full stop.")]
        [Range(0f, 1.5f)] public float magneticTangentialCarry = 1f;
        [Tooltip("How quickly a gliding Core follows the changing normal of the curved wall. Zero uses its entry tangent.")]
        [Range(0f, 1f)] public float magneticSurfaceFollow = .65f;
        [Min(.02f)] public float magneticBrakeSeconds = .22f;
        [Min(.02f)] public float magneticReleaseSeconds = .3f;
        [Range(0f, 1.5f)] public float magneticSpeedRetention = .9f;
        public bool showFullPatternGizmos = true;
        [Header("Global taper overrides (definition values are preserved)")]
        public ResonanceGlobalProfile globalVisualProfile = new ResonanceGlobalProfile();
        public ResonanceGlobalProfile globalCollisionProfile = new ResonanceGlobalProfile { thickness = .16f, taperFraction = .2f };
        public bool overrideCollisionExtent;
        [Range(0f, 1f)] public float globalCollisionStart = .08f;
        [Range(0f, 1f)] public float globalCollisionEnd = .92f;
        [Header("Resting energy (visual motion only)")]
        [Range(0f, 1f)] public float organicMotion = 0.32f;
        [Min(0f)] public float energyFlowSpeed = 0.7f;
        public ResonanceEnergyFlow energyFlow = ResonanceEnergyFlow.FromPatternCenter;
        [Tooltip("Optional wave origin. Unassigned uses this pattern's center; assign the grid for its center.")]
        public Transform energyOrigin;
        [Min(.01f)] public float energyWavelength = 2f;
        public ResonanceBlend blending;
        [Header("Three resting visual layers")]
        public ResonanceEnergyLayer filament = new ResonanceEnergyLayer { width = .22f, opacity = .85f, brightness = 1.4f, softness = 2f };
        public ResonanceEnergyLayer ribbon = new ResonanceEnergyLayer { width = 1f, opacity = .48f, brightness = .85f, softness = 4f };
        public ResonanceEnergyLayer diffuse = new ResonanceEnergyLayer { width = 2.8f, opacity = .16f, brightness = .7f, softness = 1.5f };
        [Tooltip("Zero uses the three global layer colors directly. One also multiplies them by each arc's color.")]
        [Range(0f, 1f)] public float arcColorInfluence;
        [Header("Continuous plasma — distance-field rendering, no billboards")]
        [Range(16, 128)] public int plasmaCurveSamples = 96;
        [Range(0f, .8f)] public float plasmaDisplacement = .22f;
        [Min(.01f)] public float plasmaNoiseScale = 3f;
        [Min(0f)] public float plasmaNoiseSpeed = 1.3f;
        [Range(0f, 1f)] public float plasmaFineDetail = .35f;
        [Range(0f, 3f)] public float plasmaTipBoost = 1f;
        [Header("Localized contact effects — visual only")]
        public ResonanceCoreVisualSettings coreContactVisuals = new ResonanceCoreVisualSettings();
        public ResonancePlayerVisualSettings playerPassageVisuals = new ResonancePlayerVisualSettings();
        [Header("Grid attraction — exposed arcs only")]
        public bool attractGrid;
        public VectorGridGPU grid;
        [Min(0f)] public float gridAttractionStrength = 3f;
        [Min(.01f)] public float gridAttractionRadius = 1.1f;
        [Tooltip("Use this pattern's Outer Feather / Inner Softening instead of the VectorGridGPU global Force Falloff. " +
                 "Turn off to compare the previous attraction. Only affects Resonance grid forces, not actor collisions.")]
        [InspectorName("Override Grid Falloff")] public bool overrideGridFalloff = true;
        [Tooltip("Fraction of each arc sample's attraction radius used to fade the outer pull to zero. " +
                 "1 feathers across the whole radius; 0 is a hard cutoff. Only used when Override Grid Falloff is on.")]
        [InspectorName("Outer Feather"), Range(0f, 1f)] public float gridOuterFeather = .8f;
        [Tooltip("Fraction of the attraction radius where pull eases toward zero at each sample center. " +
                 "Higher values spread out the turn and reduce pinching; 0 disables inner softening. " +
                 "Only used when Override Grid Falloff is on.")]
        [InspectorName("Inner Softening"), Range(0f, 1f)] public float gridInnerSoftening = .5f;
        [Min(.1f)] public float gridSampleSpacing = .65f;
        [Range(8, 128)] public int gridSampleBudget = 64;
        [Range(0f, 1f)] public float gridEnergyPulse = .2f;
        [Header("Full pattern impact reveal — no collisions")]
        [Min(0.01f)] public float revealDuration = 0.75f;
        [Min(0.001f)] public float revealThickness = 0.07f;
        [Range(0f, 1f)] public float revealOpacity = 0.48f;
        public Color revealColor = new Color(0.65f, 0.85f, 1f, 1f);
        [Min(0f)] public float impactCooldown = 0.12f;
        [Tooltip("Testing only: keep the full visual pattern visible. Never creates colliders.")]
        public bool previewFullPattern;
        public bool showCollisionGizmos;

        private GameObject generatedRoot;
        private bool rebuildRequested = true;
        private int seenRevision = -1;
        private sealed class VisualBinding
        {
            public Renderer renderer;
            public MaterialPropertyBlock properties;
            public ResonanceContactVisuals contacts;
            public ResonanceParticleLayer particles;
        }
        private readonly List<VisualBinding> restingRenderers = new List<VisualBinding>();
        private readonly List<VisualBinding> revealRenderers = new List<VisualBinding>();
        private readonly List<ResonanceSegment> interactiveSegments = new List<ResonanceSegment>();
        private readonly List<Collider> generatedColliders = new List<Collider>();
        [System.NonSerialized] private ResonanceInteractionDriver interactionDriver;
        [System.NonSerialized] private bool interactionEnabled = true;
        [System.NonSerialized] private Vector4 manifestation = new Vector4(1f, 1f, 0f, 0f);
        [System.NonSerialized] private Vector4 manifestationArea;
        [System.NonSerialized] private Vector4 manifestationLocal, manifestationField;
        [System.NonSerialized] private float manifestationBoundsVibration;
        private static readonly int ManifestationId = Shader.PropertyToID("_Manifestation");
        private static readonly int ManifestationAreaId = Shader.PropertyToID("_ManifestationArea");
        private static readonly int ManifestationLocalId = Shader.PropertyToID("_ManifestationLocal");
        private static readonly int ManifestationFieldId = Shader.PropertyToID("_ManifestationField");
        private readonly List<GridSample> gridSamples = new List<GridSample>();
        private struct GridSample { public Vector3 point; public float weight, radius; }
        private Material generatedMaterial;
        private double revealStarted = double.NegativeInfinity;
        private double lastImpact = double.NegativeInfinity;
        private double localPreviewStarted;
        private int localPreviewKind, localPreviewArc;
        public double ContactClock => Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int GhostId = Shader.PropertyToID("_Ghost");
        private static readonly int MotionId = Shader.PropertyToID("_OrganicMotion");
        private static readonly int ClockId = Shader.PropertyToID("_ResonanceTime");
        private static readonly int SpeedId = Shader.PropertyToID("_FlowSpeed");
        public float CurrentReveal { get; private set; }
        public int CoreImpactCount { get; private set; }
        public GameObject GeneratedRoot => generatedRoot;
        public int GridSampleCount => gridSamples.Count;
        public int ParticleCount { get; private set; }
        public bool InteractionEnabled => interactionEnabled;
        public IReadOnlyList<ResonanceSegment> InteractiveSegments => interactiveSegments;
        public float SolidVisualWidth => Mathf.Max(filament.opacity > 0f && filament.brightness > 0f ? filament.width : 0f,
            ribbon.opacity > 0f && ribbon.brightness > 0f ? ribbon.width : 0f);
        private void OnEnable()
        {
            rebuildRequested = true;
            if (Application.isPlaying) Rebuild();
        }
        private void OnValidate() { rebuildRequested = true; }
        private void OnDisable() { revealStarted = lastImpact = double.NegativeInfinity; CurrentReveal = 0f; ClearGenerated(); }
        private void OnDestroy() { ClearGenerated(); }
        private void Update()
        {
            if (rebuildRequested || (definition != null && seenRevision != definition.revision)) Rebuild();
            UpdatePresentation();
        }
        public void RequestRebuild() { rebuildRequested = true; }

        /// <summary>Gate all physics and grid authority without rebuilding or destroying the visual populations.</summary>
        public void SetInteractionEnabled(bool value)
        {
            if (interactionEnabled == value) return;
            interactionEnabled = value;
            if (!value)
            {
                localPreviewKind = 0;
                revealStarted = lastImpact = double.NegativeInfinity; CurrentReveal = 0f;
                foreach (var segment in interactiveSegments) if (segment != null) segment.ContactVisuals?.Reset();
            }
            ApplyInteractionGate();
            UpdatePresentation();
        }
        private void ApplyInteractionGate()
        {
            // Disable the driver first: OnDisable releases sludge and restores temporary ignored pairs.
            if (interactionDriver != null && !interactionEnabled) interactionDriver.enabled = false;
            foreach (var collider in generatedColliders) if (collider != null) collider.enabled = interactionEnabled;
            foreach (var segment in interactiveSegments) if (segment != null) segment.enabled = interactionEnabled;
            if (interactionDriver != null && interactionEnabled) interactionDriver.enabled = true;
        }
        /// <summary>GPU-only visual pose: condensation, size, vibration amplitude and phase; local XZ area center and extents.</summary>
        public void SetManifestation(Vector4 pose, Vector4 area)
        { SetManifestation(pose, area, Vector4.zero, Vector4.zero); }
        public void SetManifestation(Vector4 pose, Vector4 area, Vector4 localSettings, Vector4 fieldSettings)
        {
            if (manifestation == pose && manifestationArea == area && manifestationLocal == localSettings && manifestationField == fieldSettings) return;
            bool areaChanged = manifestationArea != area || manifestationLocal != localSettings;
            bool boundsChanged = areaChanged || pose.z > manifestationBoundsVibration;
            manifestationBoundsVibration = areaChanged ? Mathf.Max(0f, pose.z) : Mathf.Max(manifestationBoundsVibration, pose.z);
            manifestation = pose; manifestationArea = area;
            manifestationLocal = localSettings; manifestationField = fieldSettings;
            if (boundsChanged)
            {
                foreach (var binding in restingRenderers) binding.particles?.SetDispersalBounds(area, manifestationBoundsVibration, localSettings);
                foreach (var binding in revealRenderers) binding.particles?.SetDispersalBounds(area, manifestationBoundsVibration, localSettings);
            }
            UpdatePresentation();
        }

        [ContextMenu("Fit to Arena Bounds")]
        public void FitToArena()
        {
            if (arenaBounds == null || !arenaBounds.IsValid || definition == null) return;
            arenaBounds.RefreshNow(false);
            Vector3 center = arenaBounds.Current.centerWS;
            transform.position = new Vector3(center.x, transform.position.y, center.z);
            Vector2 half = arenaBounds.GetHalfSizeLocalInset();
            Vector3 scale = arenaBounds.Grid.transform.lossyScale;
            float radius = 0.001f;
            foreach (ResonanceCurve curve in definition.curves)
                if (curve != null) for (int i = 0; i < 360; i++) radius = Mathf.Max(radius, curve.Evaluate(i).magnitude);
            patternScale = new Vector2(half.x * Mathf.Abs(scale.x), half.y * Mathf.Abs(scale.y)) * (arenaCoverage / radius);
            if (preserveAspectWhenFitting) patternScale = Vector2.one * Mathf.Min(patternScale.x, patternScale.y);
            rebuildRequested = true;
        }

        public Vector3 Evaluate(ResonanceCurve curve, float degrees)
        {
            if (layoutFit == ResonanceLayoutFit.Stretch || curve.kind == ResonanceCurveKind.ClosedPolyline)
                return Stretch(curve.Evaluate(degrees));
            // Octant anchors retain the authored footprint. Independent tangent lengths
            // prevent the local rounding from inheriting the full X/Z aspect ratio.
            float origin = curve.kind == ResonanceCurveKind.PolarLobes ? curve.rotationDegrees : 0f;
            float step = curve.kind == ResonanceCurveKind.PolarLobes ? 180f / Mathf.Clamp(curve.lobes, 2, 16) : 45f;
            float start = Mathf.Floor((degrees - origin) / step) * step + origin;
            float t = (degrees - start) / step;
            Vector3 a = Stretch(curve.Evaluate(start)), b = Stretch(curve.Evaluate(start + step));
            Vector3 ta = FittedTangent(curve, start) * step;
            Vector3 tb = FittedTangent(curve, start + step) * step;
            float t2 = t * t, t3 = t2 * t;
            return (2f*t3-3f*t2+1f)*a + (t3-2f*t2+t)*ta + (-2f*t3+3f*t2)*b + (t3-t2)*tb;
        }
        private Vector3 FittedTangent(ResonanceCurve curve, float degrees)
        {
            Vector3 d = (curve.Evaluate(degrees + 0.05f) - curve.Evaluate(degrees - 0.05f)) / 0.1f;
            Vector3 stretched = Stretch(d);
            float step = curve.kind == ResonanceCurveKind.PolarLobes ? 180f / Mathf.Clamp(curve.lobes, 2, 16) : 45f;
            Vector3 center = Stretch(curve.Evaluate(degrees));
            float chord = (Vector3.Distance(center, Stretch(curve.Evaluate(degrees - step))) + Vector3.Distance(center, Stretch(curve.Evaluate(degrees + step)))) * .5f;
            // Chord-based handles keep the concave scoops broad instead of crushing
            // their tangent lengths along the rectangle's short axis.
            Vector3 rounded = (Quaternion.AngleAxis(rotationDegrees, Vector3.up) * d).normalized * (chord * 1.1f / step);
            return Vector3.Lerp(stretched, rounded, curvaturePreservation);
        }
        private Vector3 Stretch(Vector3 p)
        {
            p = Quaternion.AngleAxis(rotationDegrees, Vector3.up) * p;
            return new Vector3(p.x * Mathf.Max(0.001f, patternScale.x), 0f, p.z * Mathf.Max(0.001f, patternScale.y));
        }

        [ContextMenu("Rebuild Resonance")]
        public void Rebuild()
        {
            rebuildRequested = false;
            if (!isActiveAndEnabled) return;
            ClearGenerated();
            seenRevision = definition != null ? definition.revision : -1;
            Material sourceMaterial=rendering==ResonanceRendering.OptionBParticles ? particleMaterial : segmentMaterial;
            if (definition == null || sourceMaterial == null) return;
            generatedRoot = NewChild("Generated Resonance (transient)", transform);
            generatedMaterial = new Material(sourceMaterial) { name = "Resonance presentation (transient)", hideFlags = HideFlags.DontSave };
            ConfigureBlending();
            if (definition.arcs != null && definition.curves != null)
                foreach (ResonanceArc arc in definition.arcs)
                {
                    if (arc == null || !arc.IsVisible || arc.curveIndex < 0 || arc.curveIndex >= definition.curves.Count) continue;
                    ResonanceCurve curve = definition.curves[arc.curveIndex];
                    if (curve == null) continue;
                    int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp(arc.sweepDegrees, 0f, 360f) / Mathf.Clamp(sampleDegrees, 0.5f, 10f)), 2, 720);
                    Vector3[] points = new Vector3[count + 1];
                    for (int i = 0; i <= count; i++) points[i] = Evaluate(curve, arc.startDegrees + Mathf.Clamp(arc.sweepDegrees, 0f, 360f) * i / count);
                    float length = 0f;
                    for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i-1], points[i]);
                    BuildArc(ResolveArc(arc, length), points);
                }
            if (definition.showCenter && definition.centerRadius > 0f && definition.centerColor.a > 0f) BuildCenter();
            BuildFullPattern();
            if (rendering==ResonanceRendering.OptionBParticles) BuildParticlePopulations();
            BuildGridSamples();
            if (Application.isPlaying)
            {
                interactionDriver = generatedRoot.AddComponent<ResonanceInteractionDriver>();
                interactionDriver.Initialize(this);
            }
            ApplyInteractionGate();
            UpdatePresentation();
        }

        public ResonanceArc ResolveArc(ResonanceArc source, float pathLength)
        {
            var arc = source.Copy();
            if (globalVisualProfile != null && globalVisualProfile.enabled)
            {
                arc.thickness = globalVisualProfile.thickness;
                arc.endTaperFraction = globalVisualProfile.taperFraction;
                arc.taperLength = globalVisualProfile.taperLength;
                arc.falloffPower = globalVisualProfile.falloffPower;
                arc.falloffShape = globalVisualProfile.falloffShape;
            }
            if (globalCollisionProfile != null && globalCollisionProfile.enabled)
            {
                // Preserve the old full-arc extent if the source was not independent.
                if (!arc.independentCollisionProfile) { arc.collisionStart = 0f; arc.collisionEnd = 1f; }
                arc.independentCollisionProfile = true;
                arc.colliderThickness = globalCollisionProfile.thickness;
                arc.collisionTaperFraction = globalCollisionProfile.taperFraction;
                arc.collisionTaperLength = globalCollisionProfile.taperLength;
                arc.collisionFalloffPower = globalCollisionProfile.falloffPower;
                arc.collisionFalloffShape = globalCollisionProfile.falloffShape;
            }
            if (overrideCollisionExtent)
            { arc.independentCollisionProfile = true; arc.collisionStart = globalCollisionStart; arc.collisionEnd = globalCollisionEnd; }
            if (arc.taperLength > 0f) arc.endTaperFraction = Mathf.Clamp(arc.taperLength / Mathf.Max(.001f, pathLength), 0f, .5f);
            if (arc.collisionTaperLength > 0f)
                arc.collisionTaperFraction = Mathf.Clamp(arc.collisionTaperLength / Mathf.Max(.001f, pathLength * (arc.collisionEnd-arc.collisionStart)), 0f, .5f);
            return arc;
        }

        private void BuildArc(ResonanceArc arc, Vector3[] points)
        {
            if (!arc.IsVisible || (SolidVisualWidth <= 0f && (diffuse.opacity <= 0f || diffuse.brightness <= 0f))) return;
            GameObject go = NewChild(arc.name, generatedRoot.transform);
            ResonanceSegment segment = go.AddComponent<ResonanceSegment>();
            segment.Initialize(arc, points, fieldAffectedLayers, fieldsAffectOnlyAmplifierCores, this);
            interactiveSegments.Add(segment);
            BuildRibbon(arc, points, go, segment, false);
            BuildContacts(arc, points, go, segment);
        }

        private void BuildRibbon(ResonanceArc arc, Vector3[] points, GameObject go, ResonanceSegment segment, bool ghost)
        {
            // One flat coverage quad; the shader evaluates distance to the entire
            // curve. No trapezoid UV seams, folded inner edges or overlapping slices.
            int count = Mathf.Clamp(Mathf.Max(points.Length, plasmaCurveSamples), 2, 128);
            var path = new Vector4[128];
            Vector3 min = points[0], max = points[0], previous = points[0];
            float length = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f), index = t * (points.Length - 1);
                int k = Mathf.Min(Mathf.FloorToInt(index), points.Length - 2);
                Vector3 p = Vector3.Lerp(points[k], points[k+1], index-k);
                if (i > 0) length += Vector3.Distance(previous,p);
                previous=p; min=Vector3.Min(min,p); max=Vector3.Max(max,p);
                path[i]=new Vector4(p.x,p.z,arc.Envelope(t),length);
            }
            var contacts = ghost ? null : new ResonanceContactVisuals(path,count,arc.sweepDegrees>=359.99f);
            if (!ghost) segment.ContactVisuals=contacts;
            if (rendering==ResonanceRendering.OptionBParticles)
            {
                Color particleColor=arc.color; particleColor.a*=arc.opacity;
                for (int layer=ghost ? 3 : 0; layer<=(ghost ? 3 : 2); layer++)
                {
                    var particleBlock=new MaterialPropertyBlock();
                    particleBlock.SetFloat("_CurveMode",1f); particleBlock.SetFloat("_HalfWidth",arc.thickness*.5f);
                    particleBlock.SetFloat("_ArcIntensity",arc.intensity);
                    var population=new ResonanceParticleLayer(go.transform,path,count,layer,particleColor,arc.thickness*.5f,
                        surfaceHeight,particles.seed+restingRenderers.Count*107+revealRenderers.Count*701);
                    (ghost ? revealRenderers : restingRenderers).Add(new VisualBinding { properties=particleBlock,contacts=contacts,particles=population });
                }
                return;
            }
            float extent = ghost ? 1f : Mathf.Max(filament.width,Mathf.Max(ribbon.width,diffuse.width));
            // Leave coverage for the displaced wake, without widening physics or grid sampling.
            float contactMargin = ghost ? 0f : 2.5f + Mathf.Clamp(playerPassageVisuals.wakeStretch,0f,3f);
            float margin = arc.thickness*.5f*(extent + plasmaDisplacement*(1f+plasmaTipBoost) + contactMargin + .2f);
            min -= new Vector3(margin,0,margin); max += new Vector3(margin,0,margin);
            var vertices = new[] { new Vector3(min.x,surfaceHeight,min.z), new Vector3(min.x,surfaceHeight,max.z),
                new Vector3(max.x,surfaceHeight,max.z), new Vector3(max.x,surfaceHeight,min.z) };
            Color color = arc.color; color.a *= arc.opacity;
            var mesh = new Mesh { name = arc.name + " plasma coverage", hideFlags = HideFlags.DontSave };
            mesh.vertices=vertices; mesh.colors=new[]{color,color,color,color}; mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateBounds();
            segment.Own(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=generatedMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            var block=new MaterialPropertyBlock();
            block.SetFloat("_CurveMode",1f); block.SetInt("_CurveCount",count);
            block.SetVectorArray("_CurvePoints",path); block.SetFloat("_HalfWidth",arc.thickness*.5f);
            block.SetFloat("_ArcIntensity",arc.intensity);
            (ghost ? revealRenderers : restingRenderers).Add(new VisualBinding { renderer=renderer, properties=block, contacts=contacts });
        }

        private void BuildParticlePopulations()
        {
            float desired=0f;
            foreach (var binding in restingRenderers) if (binding.particles!=null) desired+=binding.particles.DesiredCount(particles);
            foreach (var binding in revealRenderers) if (binding.particles!=null) desired+=binding.particles.DesiredCount(particles);
            float scale=Mathf.Min(1f,Mathf.Clamp(particles.particleBudget,256,60000)/Mathf.Max(1f,desired));
            foreach (var binding in restingRenderers) BuildParticlePopulation(binding,scale);
            foreach (var binding in revealRenderers) BuildParticlePopulation(binding,scale);
        }
        private void BuildParticlePopulation(VisualBinding binding,float scale)
        {
            if (binding.particles==null) return;
            int count=Mathf.FloorToInt(binding.particles.DesiredCount(particles)*scale);
            binding.particles.Build(count,generatedMaterial,particles);
            binding.particles.SetDispersalBounds(manifestationArea, manifestationBoundsVibration, manifestationLocal);
            binding.renderer=binding.particles.Renderer; ParticleCount+=count;
        }

        private void BuildContacts(ResonanceArc arc, Vector3[] visualPoints, GameObject go, ResonanceSegment segment)
        {
            if (arc.behavior == ResonanceBehavior.VisualOnly || arc.colliderThickness <= 0f) return;
            float from = arc.independentCollisionProfile ? Mathf.Clamp01(arc.collisionStart) : 0f;
            float to = arc.independentCollisionProfile ? Mathf.Clamp01(arc.collisionEnd) : 1f;
            if (to <= from) return;
            int n = Mathf.Max(3, Mathf.CeilToInt((visualPoints.Length - 1) * (to - from)) + 1);
            var points = new Vector3[n]; var normals = new Vector3[n]; var widths = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Lerp(from, to, i / (n - 1f));
                float index = t * (visualPoints.Length - 1);
                int k = Mathf.Min(Mathf.FloorToInt(index), visualPoints.Length - 2);
                points[i] = Vector3.Lerp(visualPoints[k], visualPoints[k + 1], index - k);
                Vector3 tangent = visualPoints[Mathf.Min(k + 1, visualPoints.Length - 1)] - visualPoints[Mathf.Max(k - 1, 0)];
                normals[i] = Vector3.Cross(Vector3.up, tangent).normalized;
                // Collision shape is independently tunable but cannot exceed the visible footprint.
                widths[i] = Mathf.Min(arc.colliderThickness * arc.CollisionEnvelope(t), arc.thickness * SolidVisualWidth * arc.Envelope(t)) * 0.5f;
            }
            GameObject contacts = NewChild("Hard collision / interaction", go.transform);
            for (int i = 0; i < n - 1; i++)
            {
                if ((points[i + 1] - points[i]).sqrMagnitude < 0.0000001f) continue;
                float wa = widths[i], wb = widths[i + 1];
                if (Mathf.Max(wa, wb) < 0.0001f) continue;
                Vector3 a = points[i], b = points[i + 1];
                Vector3[] footprint = { a - normals[i] * wa, a + normals[i] * wa,
                    b + normals[i + 1] * wb, b - normals[i + 1] * wb };
                Mesh collisionMesh = Prism(footprint, Mathf.Max(0.1f, colliderHeight));
                segment.Own(collisionMesh);
                GameObject cell = NewChild("Contact " + i, contacts.transform);
                MeshCollider collider = cell.AddComponent<MeshCollider>();
                generatedColliders.Add(collider);
                collider.sharedMesh = collisionMesh;
                collider.convex = true;
                collider.isTrigger = arc.behavior != ResonanceBehavior.HardWall && playerResponse != ResonancePlayerResponse.HardWall;
                collider.sharedMaterial = wallMaterial;
                if (!collider.isTrigger) segment.HardColliders.Add(collider);
                if (!collider.isTrigger && arc.behavior != ResonanceBehavior.HardWall)
                {
                    var field = cell.AddComponent<MeshCollider>(); field.sharedMesh = collisionMesh; field.convex = true; field.isTrigger = true;
                    generatedColliders.Add(field);
                }
                cell.AddComponent<ResonanceContactRelay>().owner = segment;
            }
        }

        private void BuildFullPattern()
        {
            if (definition.curves == null) return;
            GameObject full = NewChild("Full pattern — impact reveal (visual only)", generatedRoot.transform);
            foreach (ResonanceCurve curve in definition.curves)
            {
                if (curve == null) continue;
                int count = Mathf.Clamp(Mathf.CeilToInt(360f / Mathf.Clamp(sampleDegrees, .5f, 10f)), 36, 720);
                var points = new Vector3[count + 1];
                for (int i = 0; i <= count; i++) points[i] = Evaluate(curve, i * 360f / count);
                var arc = new ResonanceArc { name = curve.name + " ghost", sweepDegrees = 360f, thickness = revealThickness, color = revealColor, opacity = revealOpacity, intensity = 1f, behavior = ResonanceBehavior.VisualOnly };
                GameObject go = NewChild(arc.name, full.transform);
                var owner = go.AddComponent<ResonanceSegment>();
                BuildRibbon(arc, points, go, owner, true);
            }
        }

        public bool NotifyCoreImpact(Rigidbody body)
        {
            if (!isActiveAndEnabled || !interactionEnabled || body == null) return false;
            var core = body.GetComponent<Massive.Multiplier.AmplifierCoreGameplay>();
            if (core == null || core.IsPresentationOnly || core.IsCaptured) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastImpact < impactCooldown) return false;
            lastImpact = now;
            CoreImpactCount++;
            PreviewImpact();
            return true;
        }

        public void NotifyCoreContact(Rigidbody body, ResonanceSegment segment, Vector3 point, Vector3 incoming,
            bool turnaround, float progress = 1f)
        {
            if (!isActiveAndEnabled || !interactionEnabled || body==null || segment==null) return;
            var core=body.GetComponent<Massive.Multiplier.AmplifierCoreGameplay>();
            if (core==null || core.IsCaptured || core.IsPresentationOnly) return;
            var effects=segment.ContactVisuals;
            if (coreContactVisuals.enabled && effects!=null)
            {
                Vector3 p=segment.transform.InverseTransformPoint(point);
                Vector3 v=segment.transform.InverseTransformDirection(incoming); v.y=0f;
                var velocity=new Vector2(v.x,v.z);
                var contact=effects.Nearest(new Vector2(p.x,p.z),new Vector2(p.x,p.z));
                float strength=ResonanceContactVisuals.ImpactStrength(velocity,contact.tangent,coreContactVisuals.referenceSpeed);
                if (turnaround) effects.Impact(body.GetInstanceID(),contact,velocity,strength,ContactClock,impactCooldown);
                else effects.Approach(body.GetInstanceID(),contact,velocity,strength,progress,ContactClock);
            }
            // This existing higher-level reveal is independent of local effects and their toggles.
            if (turnaround) NotifyCoreImpact(body);
        }

        public void NotifyPlayerPassage(int actorKey, Vector3 from, Vector3 to, Vector3 velocity, float radius, float dt)
        {
            if (!isActiveAndEnabled || !interactionEnabled || !playerPassageVisuals.enabled || playerResponse==ResonancePlayerResponse.HardWall) return;
            foreach (var segment in interactiveSegments)
            {
                if (playerResponse==ResonancePlayerResponse.PatternDefault && segment.Settings.behavior==ResonanceBehavior.HardWall) continue;
                var effects=segment.ContactVisuals;
                if (effects==null) continue;
                Vector3 a=segment.transform.InverseTransformPoint(from), b=segment.transform.InverseTransformPoint(to);
                var contact=effects.Nearest(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
                float scale=Mathf.Max(.001f,Mathf.Min(Mathf.Abs(segment.transform.lossyScale.x),Mathf.Abs(segment.transform.lossyScale.z)));
                float edge=radius/scale + segment.Settings.thickness*SolidVisualWidth*contact.envelope*.5f;
                float feather=Mathf.Max(.05f,playerResponse==ResonancePlayerResponse.Sludge ? interactionReach/scale : .12f);
                float presence=Mathf.Clamp01(1f-(contact.distance-edge)/feather)*contact.envelope;
                if (presence<=.001f) continue;
                Vector3 v=segment.transform.InverseTransformDirection(velocity); v.y=0f;
                float strength=presence*Mathf.Lerp(.45f,1f,Mathf.Clamp01(v.magnitude/Mathf.Max(.1f,playerPassageVisuals.referenceSpeed)));
                effects.Passage(actorKey,contact,new Vector2(v.x,v.z),strength,ContactClock,dt,playerPassageVisuals);
            }
        }

        public void PreviewLocalContact(bool core, int arcIndex = 0)
        {
            if (!interactionEnabled || interactiveSegments.Count==0) return;
            localPreviewArc=Mathf.Clamp(arcIndex,0,interactiveSegments.Count-1);
            localPreviewStarted=ContactClock; localPreviewKind=core ? 1 : 2;
            UpdatePresentation();
        }

        private void UpdateLocalPreview()
        {
            if (localPreviewKind==0 || localPreviewArc>=interactiveSegments.Count) return;
            var segment=interactiveSegments[localPreviewArc];
            var effects=segment.ContactVisuals;
            if (effects==null) { localPreviewKind=0; return; }
            float t; Vector3 p=segment.SampleDistance(segment.PathLength*.5f,out t);
            var contact=effects.Nearest(new Vector2(p.x,p.z),new Vector2(p.x,p.z));
            Vector2 velocity=new Vector2(-contact.tangent.y,contact.tangent.x)*8f;
            float age=(float)(ContactClock-localPreviewStarted);
            if (localPreviewKind==1)
            {
                if (age<.3f) effects.Approach(int.MinValue,contact,velocity,1f,age/.3f,ContactClock);
                else
                {
                    localPreviewKind=0;
                    effects.Impact(int.MinValue,contact,velocity,1f,ContactClock,0f);
                    revealStarted=Time.realtimeSinceStartupAsDouble;
                }
            }
            else if (age<.7f) effects.Passage(int.MinValue,contact,velocity,1f,ContactClock,1f/60f,playerPassageVisuals);
            else localPreviewKind=0;
        }

        [ContextMenu("Preview Core Impact Reveal")]
        public void PreviewImpact()
        {
            if (!interactionEnabled) return;
            revealStarted = Time.realtimeSinceStartupAsDouble;
            UpdatePresentation();
        }
        private void UpdatePresentation()
        {
            UpdateLocalPreview();
            float age = (float)(Time.realtimeSinceStartupAsDouble - revealStarted);
            float t = Mathf.Clamp01(age / Mathf.Max(.01f, revealDuration));
            CurrentReveal = interactionEnabled ? (previewFullPattern ? 1f : Mathf.Pow(1f - t, 1.6f)) : 0f;
            foreach (var binding in restingRenderers) ApplyPresentation(binding,false);
            foreach (var binding in revealRenderers) ApplyPresentation(binding,true);
#if UNITY_EDITOR
            bool localActive=localPreviewKind!=0;
            foreach (var binding in restingRenderers)
                localActive |= binding.contacts!=null && (binding.contacts.ActiveCoreCount>0 || binding.contacts.ActivePlayerCount>0);
            if (!Application.isPlaying && (previewFullPattern || CurrentReveal > 0f || localActive || rendering==ResonanceRendering.OptionBParticles))
            { UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); UnityEditor.SceneView.RepaintAll(); }
#endif
        }

        private void ApplyPresentation(VisualBinding binding, bool ghost)
        {
            if (binding.renderer == null) return;
            var visualProperties=binding.properties;
            visualProperties.SetFloat(ClockId, (float)(Time.realtimeSinceStartupAsDouble % 10000));
            visualProperties.SetFloat("_ContactNoiseTime",(float)(ContactClock%10000));
            visualProperties.SetFloat(MotionId, organicMotion);
            visualProperties.SetFloat(SpeedId, energyFlowSpeed);
            visualProperties.SetFloat("_FlowMode", (float)energyFlow);
            visualProperties.SetFloat("_Wavelength", Mathf.Max(.01f, energyWavelength));
            visualProperties.SetVector("_PatternCenter", energyOrigin != null ? energyOrigin.position : transform.position);
            visualProperties.SetVector("_Filament", filament.Parameters);
            visualProperties.SetVector("_Ribbon", ribbon.Parameters);
            visualProperties.SetVector("_Diffuse", diffuse.Parameters);
            visualProperties.SetColor("_FilamentColor",filament.color);
            visualProperties.SetColor("_RibbonColor",ribbon.color);
            visualProperties.SetColor("_DiffuseColor",diffuse.color);
            visualProperties.SetFloat("_ArcColorInfluence",arcColorInfluence);
            visualProperties.SetVector("_Plasma",new Vector4(plasmaDisplacement,plasmaNoiseScale,plasmaNoiseSpeed,plasmaFineDetail));
            visualProperties.SetFloat("_TipBoost",plasmaTipBoost);
            visualProperties.SetFloat(GhostId, ghost ? 1f : 0f);
            visualProperties.SetFloat(RevealId, (ghost ? CurrentReveal : 1f)
                * (binding.particles == null ? manifestation.y : 1f));
            visualProperties.SetVector(ManifestationId, manifestation);
            visualProperties.SetVector(ManifestationAreaId, manifestationArea);
            visualProperties.SetVector(ManifestationLocalId, manifestationLocal);
            visualProperties.SetVector(ManifestationFieldId, manifestationField);
            if (binding.contacts!=null) binding.contacts.Apply(visualProperties,ContactClock,coreContactVisuals,playerPassageVisuals);
            else { visualProperties.SetInt("_ContactCoreCount",0); visualProperties.SetInt("_ContactPlayerCount",0); }
            if (binding.particles!=null) binding.particles.Apply(visualProperties,particles,ContactClock);
            binding.renderer.enabled=manifestation.y > .00001f && (!ghost || CurrentReveal>.001f);
            binding.renderer.SetPropertyBlock(visualProperties);
        }

        private static Mesh Prism(Vector3[] footprint, float height)
        {
            Vector3[] v = new Vector3[8];
            for (int i = 0; i < 4; i++) { v[i] = footprint[i] - Vector3.up * height * 0.5f; v[i + 4] = footprint[i] + Vector3.up * height * 0.5f; }
            int[] t = { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7 };
            Mesh mesh = new Mesh { name = "Resonance contact prism", hideFlags = HideFlags.DontSave };
            mesh.vertices = v; mesh.triangles = t; mesh.RecalculateBounds();
            return mesh;
        }

        private void ConfigureBlending()
        {
            var src = UnityEngine.Rendering.BlendMode.SrcAlpha;
            var dst = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
            if (blending == ResonanceBlend.Additive) dst = UnityEngine.Rendering.BlendMode.One;
            if (blending == ResonanceBlend.Premultiplied) src = UnityEngine.Rendering.BlendMode.One;
            if (blending == ResonanceBlend.Screen) { src = UnityEngine.Rendering.BlendMode.One; dst = UnityEngine.Rendering.BlendMode.OneMinusSrcColor; }
            if (blending == ResonanceBlend.Multiply) { src = UnityEngine.Rendering.BlendMode.DstColor; dst = UnityEngine.Rendering.BlendMode.Zero; }
            generatedMaterial.SetInt("_SrcBlend", (int)src);
            generatedMaterial.SetInt("_DstBlend", (int)dst);
            generatedMaterial.SetFloat("_BlendStyle", (float)blending);
        }

        private void BuildGridSamples()
        {
            float totalLength = 0f;
            foreach (var segment in interactiveSegments) if (segment.Settings.gridAttractionMultiplier > 0f) totalLength += segment.PathLength;
            int budget = Mathf.Clamp(gridSampleBudget, 8, 128);
            float spacing = Mathf.Max(.1f, Mathf.Max(gridSampleSpacing, totalLength / budget));
            // Samples cover visible arcs, not their full parent curves. Midpoint quadrature
            // and length weighting keep force approximately stable when sample count changes.
            foreach (var segment in interactiveSegments)
            {
                var arc = segment.Settings;
                if (arc.gridAttractionMultiplier <= 0f || arc.gridRadiusMultiplier <= 0f) continue;
                int count = Mathf.Max(1, Mathf.FloorToInt(segment.PathLength / spacing));
                count = Mathf.Min(count, budget - gridSamples.Count);
                for (int i = 0; i < count; i++)
                {
                    float t;
                    Vector3 p = segment.SampleDistance((i + .5f) * segment.PathLength / count, out t);
                    gridSamples.Add(new GridSample { point = p, radius = arc.gridRadiusMultiplier,
                        weight = arc.Envelope(t) * arc.gridAttractionMultiplier * segment.PathLength / count });
                }
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !interactionEnabled || !attractGrid || gridAttractionStrength <= 0f) return;
            var target = grid != null ? grid : VectorGridGPU.Instance;
            if (target == null || !target.isActiveAndEnabled) return;
            Vector3 s = target.transform.lossyScale;
            float gridScale = Mathf.Max(.001f, (Mathf.Abs(s.x) + Mathf.Abs(s.y)) * .5f);
            float patternScaleWS = Mathf.Max(.001f, (Mathf.Abs(transform.lossyScale.x) + Mathf.Abs(transform.lossyScale.z)) * .5f);
            Vector3 origin = energyOrigin != null ? energyOrigin.position : transform.position;
            foreach (var sample in gridSamples)
            {
                Vector3 ws = transform.TransformPoint(sample.point);
                Vector3 p = target.transform.InverseTransformPoint(ws); p.z = 0f;
                float radiusWS = gridAttractionRadius * sample.radius;
                float pulse = 1f + gridEnergyPulse * Mathf.Sin((Vector3.Distance(ws, origin) / Mathf.Max(.01f, energyWavelength)
                    - Time.realtimeSinceStartup * energyFlowSpeed) * Mathf.PI * 2f);
                // Positive radial strengths are inward in VectorGridGPU.
                float strength = gridAttractionStrength * sample.weight * patternScaleWS / Mathf.Max(.01f, radiusWS * 2f) * pulse;
                target.AddForce(overrideGridFalloff
                    ? VectorGridGPU.MakeSoftenedBalancedRadial(p, radiusWS / gridScale, strength, gridOuterFeather, gridInnerSoftening)
                    : VectorGridGPU.MakeBalancedRadial(p, radiusWS / gridScale, strength));
            }
        }

        private void BuildCenter()
        {
            GameObject go = NewChild("Center node (visual only)", generatedRoot.transform);
            ResonanceSegment owner = go.AddComponent<ResonanceSegment>();
            if (rendering==ResonanceRendering.OptionBParticles)
            {
                var block=new MaterialPropertyBlock(); block.SetFloat("_CurveMode",0f);
                restingRenderers.Add(new VisualBinding { properties=block,
                    particles=new ResonanceParticleLayer(go.transform,null,0,4,definition.centerColor,definition.centerRadius,
                        surfaceHeight,particles.seed+907,definition.centerRadius) });
                return;
            }
            Vector3[] v = new Vector3[33]; Color[] colors = new Color[33]; Vector2[] uv = new Vector2[33]; int[] triangles = new int[96];
            v[0] = Vector3.up * surfaceHeight;
            for (int i = 0; i < 33; i++) { colors[i] = definition.centerColor; uv[i] = Vector2.zero; }
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16f;
                v[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * definition.centerRadius + v[0];
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 32 + 1;
            }
            Mesh mesh = new Mesh { name = "Resonance center", hideFlags = HideFlags.DontSave };
            mesh.vertices = v; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds(); owner.Own(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = generatedMaterial;
            restingRenderers.Add(new VisualBinding { renderer=renderer, properties=new MaterialPropertyBlock() });
        }

        private GameObject NewChild(string childName, Transform parent)
        {
            GameObject go = new GameObject(childName) { hideFlags = HideFlags.DontSave, layer = Mathf.Clamp(obstacleLayer, 0, 31) };
            go.transform.SetParent(parent, false);
            return go;
        }
        private void ClearGenerated()
        {
            localPreviewKind=0;
            ParticleCount=0;
            restingRenderers.Clear(); revealRenderers.Clear(); gridSamples.Clear(); interactiveSegments.Clear(); generatedColliders.Clear();
            interactionDriver = null;
            if (generatedMaterial != null) { if (Application.isPlaying) Destroy(generatedMaterial); else DestroyImmediate(generatedMaterial); generatedMaterial = null; }
            if (generatedRoot == null) return;
            generatedRoot.SetActive(false);
            if (Application.isPlaying) Destroy(generatedRoot); else DestroyImmediate(generatedRoot);
            generatedRoot = null;
        }
        private void OnDrawGizmosSelected()
        {
            if (showCollisionGizmos && generatedRoot != null)
            {
                Gizmos.color = new Color(1f, .5f, .1f, .5f);
                foreach (var collider in generatedRoot.GetComponentsInChildren<MeshCollider>())
                    Gizmos.DrawWireMesh(collider.sharedMesh, collider.transform.position, collider.transform.rotation, collider.transform.lossyScale);
            }
            if (!showFullPatternGizmos || definition == null || definition.curves == null) return;
            Gizmos.color = new Color(0.4f, 0.7f, 0.9f, 0.25f);
            foreach (ResonanceCurve curve in definition.curves)
                if (curve != null) for (int i = 0; i < 180; i++)
                    Gizmos.DrawLine(transform.TransformPoint(Evaluate(curve, i * 2f)), transform.TransformPoint(Evaluate(curve, (i + 1) * 2f)));
        }
    }
}
