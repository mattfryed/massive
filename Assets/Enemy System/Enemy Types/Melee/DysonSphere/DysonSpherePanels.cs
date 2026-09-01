using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes;

namespace Massive.Enemies
{
    /// <summary>
    /// Dyson Sphere shell renderer (panels).
    ///
    /// Visual goals:
    /// - Faces are real, separate triangle meshes (solid fill).
    /// - Each face gets a crisp volumetric outline (Shapes) to preserve the vector aesthetic.
    /// - Breathing expands/contracts by lifting + insetting the panels, opening gaps that reveal the core.
    /// - One random face "shatters" off per health lost (panel count reflects health).
    /// - During attacks, panels bias toward a spear formation along the sphere's local +Z.
    /// - Spawn-in: core emission ramps from 0 and panels "draw in" with easing.
    /// - Stun: panels jitter with intensity driven by the controller.
    ///
    /// Gameplay coupling:
    /// - Panel count syncs to <see cref="EnemyBase.HealthRemaining"/>.
    /// - This script does NOT handle damage; it only reacts visually.
    /// </summary>
    [ExecuteAlways]
    public sealed class DysonSpherePanels : ImmediateModeShapeDrawer
    {
        private enum PanelState
        {
            Alive,
            Shattering,
            Dead
        }

        public enum SpawnPanelMode
        {
            /// <summary>Panels reveal in a deterministic sequence (panel-by-panel / wave-like), using <see cref="spawnPanelOrder"/>.</summary>
            OrderedSequence = 0,
            /// <summary>Panels reveal with a random (stable) time offset up to <see cref="spawnPanelStaggerSeconds"/>.</summary>
            RandomStagger = 1,
            /// <summary>All panels reveal together (no staggering / ordering).</summary>
            AllTogether = 2
        }

        public enum SpawnPanelOrder
        {
            /// <summary>Reveals panels from the visually-dominant top hemisphere first (highest +Y normals), sweeping around in angle.</summary>
            TopDownSpiral = 0,
            /// <summary>Deterministic shuffle order (stable per-panel random).</summary>
            Random = 1
        }

        [Serializable]
        private sealed class Panel
        {
            public bool available;
            public PanelState state;

            // Stable randoms
            public float phase;   // 0..2π (used for breathe/jitter)
            public float rand01;  // 0..1 (used for spawn staggering)
            public float order01; // 0..1 (spawn sequence order)


            // Base geometry, in this component's local space.
            public Vector3 baseCentroidLocal;
            public Vector3 baseNormalLocal;
            public Vector3 baseUpLocal;
            public Quaternion baseRotLocal;

            // The panel GameObject
            public Transform tf;
            public Mesh mesh;
            public MeshRenderer mr;

            // Triangle verts in panel-local space (centroid at origin, triangle in XY plane, normal +Z)
            public Vector3 v0;
            public Vector3 v1;
            public Vector3 v2;

            // Cached vertex array to avoid allocations when animating draw-in.
            public Vector3[] meshVerts;
            public float lastMeshFill01 = -1f;

            // Runtime draw-in values (fed into DrawShapes)
            public float spawnEdge01 = 1f; // 0..1
            public float spawnFill01 = 1f; // 0..1

            // Shatter animation
            public float alpha01 = 1f;
            public float shatterStartTime;
            public Vector3 shatterStartPos;
            public Quaternion shatterStartRot;
            public Vector3 shatterStartScale;
            public Vector3 shatterDirLocal;
            public Vector3 shatterAxisLocal;
        }

        private struct CoreParticleDefaults
        {
            public ParticleSystem ps;
            public float rateOverTimeMul;
            public float rateOverDistanceMul;
        }

        [Header("Refs")]
        [SerializeField] private EnemyBase enemy;

        [Tooltip("Optional: core transform (e.g., inner particle system root) to pulse slightly during breathing + scale during spawn.")]
        [SerializeField] private Transform core;

        [Header("Generation")]
        [Tooltip("If enabled, the panel meshes will generate & update in edit mode.")]
        [SerializeField] private bool generateInEditMode = false;

        [Tooltip(
            "Total panel count for the shell. Supported counts map to true icosphere subdivision levels:\n" +
            "- 20   (subdiv 0; base icosahedron)\n" +
            "- 80   (subdiv 1; 1 refinement step)\n" +
            "- 320  (subdiv 2)\n" +
            "- 1280 (subdiv 3)\n\n" +
            "This value will snap to the nearest supported count in OnValidate().\n" +
            "Note: if you keep EnemyDefinition.healthMassEq at ~15–30, consider enabling \"Proportional Panel Health\" so the shell can start fully intact while still dying in a reasonable number of hits.")]
        public int maxPanels = 20;

        [Header("Health → Panels")]
        [Tooltip(
            "If enabled, intact panel count is driven by health percentage (HealthRemaining / starting health) instead of 1 panel per 1 health.\n\n" +
            "This is strongly recommended when using icosphere subdivisions (80+ panels), so the shell can start fully intact while still dying in a reasonable number of hits.")]
        public bool proportionalPanelHealth = true;

        [Header("Shell Shape")]
        [Min(0.01f)] public float radius = 0.65f;
        [Min(0f)] public float baseLift = 0.02f;
        [Range(0f, 0.35f)] public float inset = 0.085f;

        [Header("Breathing")]
        [Min(0f)] public float breatheSpeed = 1.25f;
        [Min(0f)] public float breatheLiftAmplitude = 0.03f;
        [Min(0f)] public float breatheInsetAmplitude = 0.03f;
        [Range(0f, 10f)] public float perPanelPhaseJitter = 2.2f;

        [Tooltip("If enabled, breathing only expands outward from the base shell (no inward motion). This prevents face overlap when baseLift = 0.")]
        public bool breatheOutOnly = true;

        [Header("Spawn In")]
        [Tooltip("If enabled, the shell will animate in when this object is enabled (good for Instantiate spawns & pooling).")]
        public bool playSpawnIn = true;

        [Min(0.05f)] public float spawnSeconds = 0.65f;

        [Tooltip("When the outlines begin drawing (0..1 of spawn).")]
        [Range(0f, 1f)] public float spawnOutlineStart01 = 0f;

        [Tooltip("When the fill begins drawing (0..1 of spawn). Outlines will usually draw first.")]
        [Range(0f, 1f)] public float spawnFillStart01 = 0.25f;

        [Tooltip("Exponent shaping for outline edge draw. 1=linear, >1 slower start, <1 faster start.")]
        [Min(0.01f)] public float spawnEdgeExponent = 1.6f;

        [Header("Spawn Panels")]
        [Tooltip("How panel reveal timing is distributed during spawn-in.")]
        public SpawnPanelMode spawnPanelMode = SpawnPanelMode.OrderedSequence;

        [Tooltip("Ordering pattern used when Spawn Panel Mode is OrderedSequence.")]
        public SpawnPanelOrder spawnPanelOrder = SpawnPanelOrder.TopDownSpiral;

        [Tooltip("When using OrderedSequence, this is the portion of the GLOBAL spawn (0..1) reserved for EACH panel's draw-in.\n\nSmaller = more panel-by-panel, larger = more overlap.")]
        [Range(0.01f, 1f)] public float spawnOrderWindow01 = 0.22f;

        [Tooltip("When Spawn Panel Mode is RandomStagger, panels start at random offsets up to this many seconds. 0 = no staggering.")]
        [Min(0f)] public float spawnPanelStaggerSeconds = 0.0f;

        [Tooltip("Whether to restart/clear core particle systems at spawn-in.")]
        public bool restartCoreParticlesOnSpawn = true;

        [Header("Spear Formation")]
        [Tooltip("Runtime value set by DysonSphereController. 0 = sphere, 1 = full spear.")]
        [Range(0f, 1f)]
        [SerializeField] private float spear01 = 0f;

        [Min(0f)] public float spearLength = 0.55f;
        [Range(-1f, 1f)] public float spearDotStart = 0.35f;
        [Min(0.1f)] public float spearPower = 2.4f;
        [Range(0f, 1f)] public float spearInsetMultiplier = 0.65f;
        [Range(0f, 1f)] public float spearPinch = 0.75f;
        [Range(0f, 1f)] public float spearRotate = 0.75f;

        [Header("Stun Jitter")]
        [Tooltip("Runtime value set by DysonSphereController. 0 = no stun jitter, 1 = max.")]
        [Range(0f, 1f)]
        [SerializeField] private float stun01 = 0f;

        [Min(0f)] public float stunJitterPos = 0.04f;
        [Min(0f)] public float stunJitterRotDegrees = 10f;
        [Min(0f)] public float stunJitterSpeed = 24f;

        [Header("Fill")]
        [Tooltip("Unlit material for the solid panel faces.")]
        public Material panelFillMaterial;

        public Color panelFillColor = Color.white;
        [Range(0f, 1f)] public float panelFillAlpha = 1f;

        [Header("Outline")]
        public LineGeometry lineGeometry = LineGeometry.Volumetric3D;
        public ThicknessSpace thicknessSpace = ThicknessSpace.Meters;
        [Min(0.0001f)] public float outlineThickness = 0.05f;

        [Tooltip("Pushes the outline slightly outward along each panel's normal so it reads on top of the fill (avoids z-fighting and makes per-panel borders much more readable).")]
        [Min(0f)] public float outlineSurfaceOffset = 0.003f;

        public Color outlineColor = Color.white;
        [Range(0f, 1f)] public float outlineAlpha = 1f;

        [Header("Shatter")]
        [Min(0.05f)] public float shatterSeconds = 0.22f;
        [Min(0f)] public float shatterDistance = 0.45f;
        [Min(0f)] public float shatterSpinDegrees = 320f;
        [Range(0f, 1f)] public float shatterRandomness = 0.35f;

        [Tooltip("If true, panels can be restored if enemy health increases (not typical).")]
        public bool allowPanelRestore = false;

        [Header("Core Pulse (Optional)")]
        [Min(0f)] public float coreBaseScale = 1f;
        [Min(0f)] public float corePulseAmplitude = 0.08f;
        [Min(0f)] public float corePulseSpeed = 1.8f;
        [Min(0f)] public float coreAttackBoost = 0.18f;

        // -------------------------
        // Runtime state
        // -------------------------

        private Transform _panelsRoot;
        private readonly List<Panel> _panels = new(20);
        private readonly List<int> _alive = new(20);
        private readonly List<int> _dead = new(20);

        private int _builtForMaxPanels = -1;
        private float _builtForRadius = -1f;
        private int _lastDesiredAlive = -1;
        private bool _didInitialSync = false;
        private float _healthMaxSnapshot = -1f;

        private System.Random _rng;
        private MaterialPropertyBlock _mpb;

        // Spawn
        private bool _spawnActive;
        private float _spawnStartTime;
        private float _spawn01 = 1f; // eased 0..1

        // Core emission cache (spawn-in)
        private readonly List<CoreParticleDefaults> _coreDefaults = new(4);
        private Vector3 _coreBaseLocalScale = Vector3.one;
        private bool _coreCached = false;

        public float Spear01 => spear01;
        public float Stun01 => stun01;

        public float Spawn01 => Mathf.Clamp01(_spawn01);
        public bool IsSpawning => Application.isPlaying && playSpawnIn && (_spawnActive || _spawn01 < 0.999f);

        public void SetSpear01(float value01) => spear01 = Mathf.Clamp01(value01);

        public void SetStun01(float value01) => stun01 = Mathf.Clamp01(value01);

        /// <summary>Restarts the spawn animation (useful for pooling).</summary>
        public void PlaySpawnIn()
        {
            if (!Application.isPlaying || !playSpawnIn)
            {
                _spawnActive = false;
                _spawn01 = 1f;
                ApplyCoreEmissionMul(1f);
                return;
            }

            _spawnActive = true;
            _spawnStartTime = Time.time;
            _spawn01 = 0f;

            CacheCoreIfNeeded();

            // Core: begin at emission=0 and scale=0.
            ApplyCoreEmissionMul(0f);
            if (core != null)
                core.localScale = Vector3.zero;

            if (restartCoreParticlesOnSpawn)
            {
                for (int i = 0; i < _coreDefaults.Count; i++)
                {
                    var ps = _coreDefaults[i].ps;
                    if (ps == null) continue;
                    ps.Clear(true);
                    ps.Play(true);
                }
            }

            // Panels: immediately reset draw-in state so we don't get a 1-frame flash of full outlines/fill.
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (!p.available) continue;
                if (p.state != PanelState.Alive) continue;

                p.spawnEdge01 = 0f;
                p.spawnFill01 = 0f;
                p.lastMeshFill01 = -1f;

                if (p.mesh != null && p.meshVerts != null && p.meshVerts.Length == 3)
                {
                    p.meshVerts[0] = Vector3.zero;
                    p.meshVerts[1] = Vector3.zero;
                    p.meshVerts[2] = Vector3.zero;
                    p.mesh.vertices = p.meshVerts;
                    p.mesh.RecalculateBounds();
                }

                // Ensure fill is fully transparent at time 0 (even if mesh is briefly visible).
                if (p.mr != null)
                {
                    _mpb ??= new MaterialPropertyBlock();
                    _mpb.Clear();
                    Color fc = panelFillColor;
                    fc.a = 0f;
                    var mat = p.mr.sharedMaterial;
                    if (mat != null && mat.HasProperty("_BaseColor"))
                        _mpb.SetColor("_BaseColor", fc);
                    else
                        _mpb.SetColor("_Color", fc);
                    p.mr.SetPropertyBlock(_mpb);
                }
            }

            // Panels: their mesh vertices will be driven by spawnFill01 each Update.
        }

        private void Reset()
        {
            enemy = GetComponentInParent<EnemyBase>();
        }

        private void Awake()
        {
            if (!enemy) enemy = GetComponentInParent<EnemyBase>();
            if (_rng == null) _rng = new System.Random(GetInstanceID());
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (!Application.isPlaying && !generateInEditMode)
                return;

            RebuildIfNeeded(force: true);
            CacheCoreIfNeeded();
        }
        private void OnEnable()
        {
            if (_rng == null) _rng = new System.Random(GetInstanceID());
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (!Application.isPlaying && !generateInEditMode)
            {
                // If panels were generated previously (ex: user toggled edit-mode generation on/off)
                // clean them up so the prefab/scene doesn't get polluted.
                DestroyCreatedMeshesImmediate();
                DestroyGeneratedPanelsImmediate();
                return;
            }

            RebuildIfNeeded(force: true);

            CacheCoreIfNeeded();

            // Spawn-in animation (runtime only)
            if (Application.isPlaying && playSpawnIn)
                PlaySpawnIn();
            else
            {
                _spawnActive = false;
                _spawn01 = 1f;
                ApplyCoreEmissionMul(1f);
            }

            // NOTE: Shapes' ImmediateModeShapeDrawer registers itself for rendering in its own OnEnable.
            // Since this class defines its own OnEnable, we MUST call the base method or DrawShapes()
            // will never be invoked (resulting in missing outlines).
            //
            // We call base.OnEnable() LAST so spawn state is initialized before the first draw, preventing
            // a 1-frame outline flash.
            base.OnEnable();
        }


        private void OnDisable()
        {
            // Ensure Shapes deregisters correctly.
            base.OnDisable();

            // In edit mode, keep things tidy when the component is disabled.
            if (!Application.isPlaying)
            {
                // Only clean up generated children if we were generating in edit mode.
                if (generateInEditMode)
                {
                    DestroyCreatedMeshesImmediate();
                    DestroyGeneratedPanelsImmediate();
                }
            }
        }

        private void OnValidate()
        {
            // Snap to supported topology counts.
            maxPanels = SnapPanelCount(maxPanels);

            _builtForMaxPanels = -1;
            _builtForRadius = -1f;
            _lastDesiredAlive = -1;
            _didInitialSync = false;
            _healthMaxSnapshot = -1f;
        }

        private void Update()
        {
            if (!Application.isPlaying && !generateInEditMode) return;
            if (enemy != null && enemy.IsPaused) return;

            RebuildIfNeeded(force: false);

            // Tick spawn progress (runtime only)
            TickSpawn();

            // Pulse the core (runtime only by default).
            if (core != null)
            {
                float now = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);

                float t = now * Mathf.Max(0f, corePulseSpeed);
                float sin = Mathf.Sin(t);
                float pulse = 1f + (sin * corePulseAmplitude);
                pulse += spear01 * coreAttackBoost;

                // Spawn multiplier scales core from 0→1.
                float spawnMul = Mathf.Clamp01(_spawn01);
                float s = Mathf.Max(0.0001f, coreBaseScale * pulse * spawnMul);

                // Preserve authored local scale as a base.
                core.localScale = _coreBaseLocalScale * s;
            }

            SyncPanelsToHealth();
            TickPanelsPoseAndMaterials();
        }

        // -------------------------
        // Spawn
        // -------------------------

        private void CacheCoreIfNeeded()
        {
            if (_coreCached) return;

            _coreDefaults.Clear();

            if (core != null)
            {
                _coreBaseLocalScale = core.localScale;

                var systems = core.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
                for (int i = 0; i < systems.Length; i++)
                {
                    var ps = systems[i];
                    if (ps == null) continue;

                    var em = ps.emission;
                    _coreDefaults.Add(new CoreParticleDefaults
                    {
                        ps = ps,
                        rateOverTimeMul = em.rateOverTimeMultiplier,
                        rateOverDistanceMul = em.rateOverDistanceMultiplier
                    });
                }
            }
            else
            {
                _coreBaseLocalScale = Vector3.one;
            }

            _coreCached = true;
        }

        private void TickSpawn()
        {
            if (!Application.isPlaying || !_spawnActive)
                return;

            float dur = Mathf.Max(0.05f, spawnSeconds);
            float raw = Mathf.Clamp01((Time.time - _spawnStartTime) / dur);

            // SmoothStep easing (feel free to swap to another curve later)
            float eased = raw * raw * (3f - 2f * raw);
            _spawn01 = eased;

            // Drive core emission multiplier
            ApplyCoreEmissionMul(eased);

            if (raw >= 0.999f)
            {
                _spawnActive = false;
                _spawn01 = 1f;
                ApplyCoreEmissionMul(1f);
            }
        }

        private void ApplyCoreEmissionMul(float mul01)
        {
            mul01 = Mathf.Clamp01(mul01);
            if (_coreDefaults.Count == 0) return;

            for (int i = 0; i < _coreDefaults.Count; i++)
            {
                var d = _coreDefaults[i];
                if (d.ps == null) continue;

                var em = d.ps.emission;
                em.rateOverTimeMultiplier = d.rateOverTimeMul * mul01;
                em.rateOverDistanceMultiplier = d.rateOverDistanceMul * mul01;
            }
        }

        
        private float GetPanelSpawn01(Panel p, float now)
        {
            if (!Application.isPlaying || !playSpawnIn) return 1f;
            if (!_spawnActive && _spawn01 >= 0.999f) return 1f;

            float dur = Mathf.Max(0.05f, spawnSeconds);
            float globalRaw = Mathf.Clamp01((now - _spawnStartTime) / dur);

            static float Smooth(float t)
            {
                t = Mathf.Clamp01(t);
                return t * t * (3f - 2f * t);
            }

            switch (spawnPanelMode)
            {
                case SpawnPanelMode.AllTogether:
                    return Smooth(globalRaw);

                case SpawnPanelMode.RandomStagger:
                {
                    if (spawnPanelStaggerSeconds <= 0.0001f)
                        return Smooth(globalRaw);

                    float start = _spawnStartTime + (p.rand01 * spawnPanelStaggerSeconds);
                    float raw = Mathf.Clamp01((now - start) / dur);
                    return Smooth(raw);
                }

                case SpawnPanelMode.OrderedSequence:
                default:
                {
                    // Reveal panels in sequence across the GLOBAL spawn time.
                    // Each panel gets a window of the global progress to animate in.
                    float win = Mathf.Clamp(spawnOrderWindow01, 0.01f, 1f);
                    float start01 = p.order01 * (1f - win);
                    float localRaw = Mathf.Clamp01((globalRaw - start01) / win);
                    return Smooth(localRaw);
                }
            }
        }


        // -------------------------
        // Build
        // -------------------------

        private void EnsurePanelsRoot()
        {
            if (_panelsRoot != null) return;

            Transform existing = transform.Find("_DysonPanelsRoot");
            if (existing != null)
            {
                _panelsRoot = existing;
                _panelsRoot.localPosition = Vector3.zero;
                _panelsRoot.localRotation = Quaternion.identity;
                _panelsRoot.localScale = Vector3.one;
                return;
            }

            var go = new GameObject("_DysonPanelsRoot");
            go.transform.SetParent(transform, worldPositionStays: false);
            _panelsRoot = go.transform;
            _panelsRoot.localPosition = Vector3.zero;
            _panelsRoot.localRotation = Quaternion.identity;
            _panelsRoot.localScale = Vector3.one;
        }

        private void DestroyGeneratedPanelsImmediate()
        {
            if (_panelsRoot == null)
            {
                var t = transform.Find("_DysonPanelsRoot");
                if (t != null) _panelsRoot = t;
            }
            if (_panelsRoot == null) return;

            for (int i = _panelsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _panelsRoot.GetChild(i);
                if (child != null)
                    DestroyImmediate(child.gameObject);
            }
        }

        private void DestroyGeneratedPanelsRuntime()
        {
            if (_panelsRoot == null) return;
            for (int i = _panelsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _panelsRoot.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        private void DestroyCreatedMeshesImmediate()
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                var m = _panels[i].mesh;
                if (m != null)
                    DestroyImmediate(m);
            }
        }

        private void DestroyCreatedMeshesRuntime()
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                var m = _panels[i].mesh;
                if (m != null)
                    Destroy(m);
            }
        }

        private void RebuildIfNeeded(bool force)
        {
            int mp = SnapPanelCount(maxPanels);
            float r = Mathf.Max(0.01f, radius);

            if (!force && mp == _builtForMaxPanels && Mathf.Abs(r - _builtForRadius) < 0.0001f && _panels.Count > 0)
                return;

            _builtForMaxPanels = mp;
            _builtForRadius = r;
            _lastDesiredAlive = -1;
            _didInitialSync = false;
            _healthMaxSnapshot = -1f;

            EnsurePanelsRoot();

            if (Application.isPlaying)
            {
                DestroyCreatedMeshesRuntime();
                DestroyGeneratedPanelsRuntime();
            }
            else
            {
                DestroyCreatedMeshesImmediate();
                DestroyGeneratedPanelsImmediate();
            }

            _panels.Clear();
            _alive.Clear();
            _dead.Clear();

            // Reduce per-rebuild allocations when using higher subdivision counts.
            _panels.Capacity = Mathf.Max(_panels.Capacity, mp);
            _alive.Capacity = Mathf.Max(_alive.Capacity, mp);
            _dead.Capacity = Mathf.Max(_dead.Capacity, mp);

            // Fallback material if none assigned.
            if (panelFillMaterial == null)
            {
                Shader sh = Shader.Find("Unlit/Color");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh != null)
                {
                    panelFillMaterial = new Material(sh)
                    {
                        name = "DysonPanelFill_Runtime"
                    };
                }
            }

            BuildPanels(mp, r);
        }

        private void BuildPanels(int mp, float r)
        {
            int subdivisions = SubdivFromPanelCount(mp);

            // Build an icosphere (true subdivision + projection to the sphere) so we get a more faceted,
            // higher-resolution silhouette than a plain icosahedron.
            var verts = new List<Vector3>(12 + (mp * 2));
            var triIndices = new List<int>(mp * 3);
            GenerateIcosphere(subdivisions, r, verts, triIndices);

            int faceCount = triIndices.Count / 3;
            if (faceCount > mp) faceCount = mp;

            for (int f = 0; f < faceCount; f++)
            {
                Vector3 a = verts[triIndices[f * 3 + 0]];
                Vector3 b = verts[triIndices[f * 3 + 1]];
                Vector3 c = verts[triIndices[f * 3 + 2]];

                Vector3 centroid = (a + b + c) * (1f / 3f);

                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 0.00001f) n = centroid;
                n.Normalize();
                if (Vector3.Dot(n, centroid) < 0f) n = -n;

                // Tangent for stable twist
                Vector3 up = (a - centroid);
                if (up.sqrMagnitude < 0.00001f) up = (b - centroid);
                up.Normalize();
                // Ensure not parallel
                if (Mathf.Abs(Vector3.Dot(up, n)) > 0.95f) up = Vector3.Cross(n, Vector3.right).normalized;

                Quaternion rot = Quaternion.LookRotation(n, up);
                Quaternion inv = Quaternion.Inverse(rot);

                // Panel-local verts (centered at centroid)
                Vector3 la = inv * (a - centroid);
                Vector3 lb = inv * (b - centroid);
                Vector3 lc = inv * (c - centroid);

                // Force planar z=0 (numerical stability)
                la.z = 0f; lb.z = 0f; lc.z = 0f;

                var go = new GameObject($"Panel_{f:000}");
                go.transform.SetParent(_panelsRoot, worldPositionStays: false);
                go.transform.localPosition = centroid;
                go.transform.localRotation = rot;
                go.transform.localScale = Vector3.one;

                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = panelFillMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;

                Mesh mesh = new Mesh
                {
                    name = $"DysonPanelMesh_{f:000}"
                };
                mesh.MarkDynamic();

                // Start fully built; spawn-in will animate vertices down to 0 if enabled.
                Vector3[] mv = { la, lb, lc };
                mesh.vertices = mv;
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;

                float rand01 = Hash01(f, GetInstanceID());
                float phase = rand01 * Mathf.PI * 2f;

                _panels.Add(new Panel
                {
                    available = true,
                    state = PanelState.Alive,

                    rand01 = rand01,
                    phase = phase,

                    baseCentroidLocal = centroid,
                    baseNormalLocal = n,
                    baseUpLocal = up,
                    baseRotLocal = rot,

                    tf = go.transform,
                    mesh = mesh,
                    mr = mr,

                    v0 = la,
                    v1 = lb,
                    v2 = lc,

                    meshVerts = mv,

                    alpha01 = 1f,
                    spawnEdge01 = 1f,
                    spawnFill01 = 1f
                });
            }

            ComputeSpawnOrder();

            // Populate alive list
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i].available && _panels[i].state == PanelState.Alive)
                    _alive.Add(i);
                else if (_panels[i].available)
                    _dead.Add(i);
            }
        }


        private void ComputeSpawnOrder()
        {
            int n = _panels.Count;
            if (n <= 0) return;

            // Build an index list we can sort.
            var indices = new List<int>(n);
            for (int i = 0; i < n; i++)
                indices.Add(i);

            // Sort based on the chosen order mode.
            switch (spawnPanelOrder)
            {
                case SpawnPanelOrder.Random:
                    indices.Sort((ia, ib) => _panels[ia].rand01.CompareTo(_panels[ib].rand01));
                    break;

                case SpawnPanelOrder.TopDownSpiral:
                default:
                    indices.Sort((ia, ib) =>
                    {
                        Panel a = _panels[ia];
                        Panel b = _panels[ib];

                        // Sort by +Y normal (top hemisphere first)
                        int yCmp = b.baseNormalLocal.y.CompareTo(a.baseNormalLocal.y); // desc
                        if (yCmp != 0) return yCmp;

                        // Then sweep around Y by angle (spiral-ish ordering)
                        float angA = Mathf.Atan2(a.baseNormalLocal.z, a.baseNormalLocal.x);
                        float angB = Mathf.Atan2(b.baseNormalLocal.z, b.baseNormalLocal.x);
                        if (angA < 0f) angA += Mathf.PI * 2f;
                        if (angB < 0f) angB += Mathf.PI * 2f;
                        return angA.CompareTo(angB);
                    });
                    break;
            }

            float denom = Mathf.Max(1, n - 1);
            for (int rank = 0; rank < n; rank++)
            {
                int idx = indices[rank];
                Panel p = _panels[idx];
                p.order01 = rank / denom;
            }
        }

        // -------------------------
        // Health sync + shatter
        // -------------------------

        private void SyncPanelsToHealth()
        {
            int availableCount = 0;
            for (int i = 0; i < _panels.Count; i++)
                if (_panels[i].available) availableCount++;
            if (availableCount <= 0) return;

            int desiredAlive;
            if (enemy != null)
            {
                // Capture a stable "max health" snapshot so we can map health → panel count cleanly.
                if (_healthMaxSnapshot <= 0f)
                {
                    float defMax = (enemy.Definition != null) ? enemy.Definition.healthMassEq : enemy.HealthRemaining;
                    if (defMax <= 0f) defMax = enemy.HealthRemaining;
                    _healthMaxSnapshot = Mathf.Max(0.0001f, defMax);
                }

                if (proportionalPanelHealth)
                {
                    float maxH = Mathf.Max(0.0001f, _healthMaxSnapshot);
                    float health01 = Mathf.Clamp01(enemy.HealthRemaining / maxH);
                    desiredAlive = Mathf.Clamp(Mathf.RoundToInt(availableCount * health01), 0, availableCount);
                }
                else
                {
                    desiredAlive = Mathf.Clamp(Mathf.RoundToInt(enemy.HealthRemaining), 0, availableCount);
                }
            }
            else
            {
                desiredAlive = availableCount;
            }

            if (desiredAlive == _lastDesiredAlive && _didInitialSync) return;
            _lastDesiredAlive = desiredAlive;

            // Refresh alive/dead indices (Alive = panels not currently shattering)
            _alive.Clear();
            _dead.Clear();
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (!p.available) continue;
                if (p.state == PanelState.Alive) _alive.Add(i);
                else if (p.state == PanelState.Dead) _dead.Add(i);
            }

            int currentAlive = _alive.Count;

            // First sync: snap to desired count without dramatic shatter.
            if (!_didInitialSync)
            {
                if (currentAlive > desiredAlive)
                {
                    int removeCount = currentAlive - desiredAlive;
                    for (int k = 0; k < removeCount; k++)
                    {
                        if (_alive.Count == 0) break;
                        int pick = _rng.Next(0, _alive.Count);
                        int idx = _alive[pick];
                        _alive.RemoveAt(pick);
                        HardDisablePanel(idx);
                    }
                }
                else if (allowPanelRestore && currentAlive < desiredAlive)
                {
                    int addCount = desiredAlive - currentAlive;
                    for (int k = 0; k < addCount; k++)
                    {
                        if (_dead.Count == 0) break;
                        int pick = _rng.Next(0, _dead.Count);
                        int idx = _dead[pick];
                        _dead.RemoveAt(pick);
                        RestorePanel(idx);
                    }
                }

                _didInitialSync = true;
                return;
            }

            // Runtime: shatter panels off to reach desired count.
            if (currentAlive > desiredAlive)
            {
                int removeCount = currentAlive - desiredAlive;
                for (int k = 0; k < removeCount; k++)
                {
                    if (_alive.Count == 0) break;
                    int pick = _rng.Next(0, _alive.Count);
                    int idx = _alive[pick];
                    _alive.RemoveAt(pick);
                    BeginShatter(idx);
                }
            }
            else if (allowPanelRestore && currentAlive < desiredAlive)
            {
                int addCount = desiredAlive - currentAlive;
                for (int k = 0; k < addCount; k++)
                {
                    if (_dead.Count == 0) break;
                    int pick = _rng.Next(0, _dead.Count);
                    int idx = _dead[pick];
                    _dead.RemoveAt(pick);
                    RestorePanel(idx);
                }
            }
        }

        private void HardDisablePanel(int idx)
        {
            if (idx < 0 || idx >= _panels.Count) return;
            var p = _panels[idx];
            if (!p.available) return;

            p.state = PanelState.Dead;
            p.alpha01 = 0f;
            if (p.mr != null) p.mr.enabled = false;
            if (p.tf != null) p.tf.gameObject.SetActive(false);
        }

        private void RestorePanel(int idx)
        {
            if (idx < 0 || idx >= _panels.Count) return;
            var p = _panels[idx];
            if (!p.available) return;

            p.state = PanelState.Alive;
            p.alpha01 = 1f;
            if (p.tf != null) p.tf.gameObject.SetActive(true);
            if (p.mr != null) p.mr.enabled = true;

            // Ensure mesh is fully formed when restored.
            if (p.mesh != null && p.meshVerts != null && p.meshVerts.Length == 3)
            {
                p.meshVerts[0] = p.v0;
                p.meshVerts[1] = p.v1;
                p.meshVerts[2] = p.v2;
                p.mesh.vertices = p.meshVerts;
            }
        }

        private void BeginShatter(int idx)
        {
            if (idx < 0 || idx >= _panels.Count) return;

            var p = _panels[idx];
            if (!p.available) return;
            if (p.state != PanelState.Alive) return;

            p.state = PanelState.Shattering;
            p.alpha01 = 1f;
            p.shatterStartTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            p.shatterStartPos = p.tf.localPosition;
            p.shatterStartRot = p.tf.localRotation;
            p.shatterStartScale = p.tf.localScale;

            // Bias outward in XZ so it reads in top-down.
            Vector3 radial = new Vector3(p.baseCentroidLocal.x, 0f, p.baseCentroidLocal.z);
            if (radial.sqrMagnitude < 0.0001f) radial = Vector3.right;
            radial.Normalize();

            Vector3 rnd = HashDir(idx + 101, GetInstanceID());
            Vector3 dir = Vector3.Lerp(radial, rnd, Mathf.Clamp01(shatterRandomness));
            if (dir.sqrMagnitude < 0.0001f) dir = radial;
            dir.Normalize();

            p.shatterDirLocal = dir;
            p.shatterAxisLocal = HashDir(idx + 1337, GetInstanceID());
            if (p.shatterAxisLocal.sqrMagnitude < 0.0001f) p.shatterAxisLocal = Vector3.up;
            p.shatterAxisLocal.Normalize();
        }

        // -------------------------
        // Pose + material updates
        // -------------------------

        private void TickPanelsPoseAndMaterials()
        {
            if (_panels.Count == 0) return;

            float now = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);

            float spd = Mathf.Max(0f, breatheSpeed);
            float s01 = Mathf.Clamp01(spear01);

            float baseFillA = Mathf.Clamp01(panelFillAlpha);

            // Stun
            float st01 = Mathf.Clamp01(stun01);
            float stPosAmp = Mathf.Max(0f, stunJitterPos) * st01;
            float stRotAmp = Mathf.Max(0f, stunJitterRotDegrees) * st01;
            float stSpd = Mathf.Max(0f, stunJitterSpeed);

            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (!p.available) continue;
                if (p.tf == null) continue;

                // Spawn draw-in values
                float panelSpawn01 = GetPanelSpawn01(p, now);

                // Outline drawing begins at spawnOutlineStart01
                float edgeRaw = Mathf.InverseLerp(spawnOutlineStart01, 1f, panelSpawn01);
                edgeRaw = Mathf.Clamp01(edgeRaw);
                p.spawnEdge01 = Mathf.Clamp01(Mathf.Pow(edgeRaw, Mathf.Max(0.01f, spawnEdgeExponent)));

                // Fill drawing begins at spawnFillStart01
                float fillRaw = Mathf.InverseLerp(spawnFillStart01, 1f, panelSpawn01);
                fillRaw = Mathf.Clamp01(fillRaw);
                // smoothstep the fill itself
                p.spawnFill01 = fillRaw * fillRaw * (3f - 2f * fillRaw);

                if (p.state == PanelState.Alive)
                {
                    float breatheSin = Mathf.Sin(now * spd + (p.phase * perPanelPhaseJitter));
                    // If breatheOutOnly is enabled, we remap to 0..1 so panels never move "inward".
                    float breathe = breatheOutOnly ? (0.5f + 0.5f * breatheSin) : breatheSin;

                    float lift = baseLift + (breatheLiftAmplitude * breathe);
                    float ins = inset + (breatheInsetAmplitude * breathe);
                    ins = Mathf.Clamp(ins, 0f, 0.95f);

                    // Spear weighting (based on face normal vs local forward)
                    float dot = Vector3.Dot(p.baseNormalLocal, Vector3.forward);
                    float front = Mathf.Clamp01((dot - spearDotStart) / Mathf.Max(0.0001f, 1f - spearDotStart));
                    front = Mathf.Pow(front, Mathf.Max(0.1f, spearPower));

                    float spearShift = spearLength * front * s01;

                    // Tighten the shell slightly during spear (reads as a weapon)
                    if (s01 > 0.001f)
                        ins *= Mathf.Lerp(1f, spearInsetMultiplier, s01);

                    float scale = Mathf.Clamp(1f - ins, 0.05f, 1.5f);

                    // Base position: face centroid + lift
                    Vector3 pos = p.baseCentroidLocal + (p.baseNormalLocal * lift);

                    // Pinch toward spear axis for front faces
                    if (s01 > 0.001f && spearPinch > 0.001f)
                    {
                        float along = Vector3.Dot(pos, Vector3.forward);
                        Vector3 lateral = pos - Vector3.forward * along;
                        float pinchMul = Mathf.Lerp(1f, Mathf.Clamp01(1f - spearPinch), s01 * front);
                        pos = Vector3.forward * along + lateral * pinchMul;
                    }

                    // Extend forward
                    pos += Vector3.forward * spearShift;

                    // Rotation bias toward spear direction for front faces
                    Quaternion rot = p.baseRotLocal;
                    if (s01 > 0.001f && spearRotate > 0.001f)
                    {
                        Vector3 up = p.baseUpLocal;
                        if (up.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(up.normalized, Vector3.forward)) > 0.95f)
                            up = Vector3.up;
                        Quaternion spearRot = Quaternion.LookRotation(Vector3.forward, up);
                        rot = Quaternion.Slerp(p.baseRotLocal, spearRot, (s01 * front) * spearRotate);
                    }

                    // Stun jitter (degrading intensity is driven by controller via stun01)
                    if (st01 > 0.001f && (stPosAmp > 0.000001f || stRotAmp > 0.000001f))
                    {
                        float j0 = Mathf.Sin(now * stSpd + (p.phase * 11.17f));
                        float j1 = Mathf.Cos(now * (stSpd * 1.37f) + (p.phase * 7.31f));

                        if (stPosAmp > 0.000001f)
                        {
                            // Jitter in the panel plane (panel local XY), then rotate into shell space.
                            Vector3 jitterLocal = new Vector3(j0, j1, 0f) * stPosAmp;
                            pos += (rot * jitterLocal);
                        }

                        if (stRotAmp > 0.000001f)
                        {
                            float ang = j0 * stRotAmp;
                            Vector3 nAxis = (rot * Vector3.forward);
                            if (nAxis.sqrMagnitude < 0.000001f) nAxis = p.baseNormalLocal;
                            rot = Quaternion.AngleAxis(ang, nAxis) * rot;
                        }
                    }

                    p.tf.localPosition = pos;
                    p.tf.localRotation = rot;
                    p.tf.localScale = new Vector3(scale, scale, scale);

                    p.alpha01 = 1f;
                }
                else if (p.state == PanelState.Shattering)
                {
                    float start = p.shatterStartTime;
                    float dur = Mathf.Max(0.05f, shatterSeconds);
                    float u = Mathf.Clamp01((now - start) / dur);
                    float ease = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic

                    p.alpha01 = 1f - u;
                    p.tf.localPosition = p.shatterStartPos + p.shatterDirLocal * (shatterDistance * ease);
                    p.tf.localRotation = p.shatterStartRot * Quaternion.AngleAxis(shatterSpinDegrees * ease, p.shatterAxisLocal);
                    p.tf.localScale = p.shatterStartScale * Mathf.Lerp(1f, 0.15f, u);

                    // Shattering panels should remain fully formed (not spawn-drawn).
                    p.spawnFill01 = 1f;
                    p.spawnEdge01 = 1f;

                    if (u >= 0.999f)
                    {
                        p.state = PanelState.Dead;
                        p.alpha01 = 0f;
                        if (p.mr != null) p.mr.enabled = false;
                        p.tf.gameObject.SetActive(false);
                    }
                }

                // Animate panel mesh "draw in" (robust even with opaque materials)
                if (p.mesh != null && p.meshVerts != null && p.meshVerts.Length == 3 && p.state == PanelState.Alive)
                {
                    float fill01 = Mathf.Clamp01(p.spawnFill01);
                    // Only touch the mesh while it is still drawing in.
                    if (fill01 < 0.999f || p.lastMeshFill01 < 0f)
                    {
                        // Cheap thresholding avoids extra mesh writes after spawn finishes.
                        if (Mathf.Abs(fill01 - p.lastMeshFill01) > 0.0005f)
                        {
                            p.lastMeshFill01 = fill01;

                            p.meshVerts[0] = Vector3.Lerp(Vector3.zero, p.v0, fill01);
                            p.meshVerts[1] = Vector3.Lerp(Vector3.zero, p.v1, fill01);
                            p.meshVerts[2] = Vector3.Lerp(Vector3.zero, p.v2, fill01);
                            p.mesh.vertices = p.meshVerts;
                        }
                    }
                    else if (p.lastMeshFill01 < 0.999f)
                    {
                        // Snap back to exact base verts once at the end.
                        p.lastMeshFill01 = 1f;
                        p.meshVerts[0] = p.v0;
                        p.meshVerts[1] = p.v1;
                        p.meshVerts[2] = p.v2;
                        p.mesh.vertices = p.meshVerts;
                    }
                }

                // Apply fill color via MaterialPropertyBlock
                if (p.mr != null)
                {
                    float a = baseFillA * Mathf.Clamp01(p.alpha01);

                    // During spawn-in, we also fade the fill (helps readability even if material is transparent).
                    a *= Mathf.Clamp01(p.spawnFill01);

                    _mpb.Clear();
                    Color fc = panelFillColor;
                    fc.a *= a;

                    var mat = p.mr.sharedMaterial;
                    if (mat != null && mat.HasProperty("_BaseColor"))
                        _mpb.SetColor("_BaseColor", fc);
                    else
                        _mpb.SetColor("_Color", fc);

                    p.mr.SetPropertyBlock(_mpb);
                }
            }
        }

        // -------------------------
        // Outlines (Shapes)
        // -------------------------

        public override void DrawShapes(Camera cam)
        {
            if (!Application.isPlaying && !generateInEditMode) return;
            if (_panels.Count == 0) return;
            if (enemy != null && enemy.IsPaused) return;

            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix;
                Draw.LineGeometry = lineGeometry;
                Draw.ThicknessSpace = thicknessSpace;
                Draw.Thickness = Mathf.Max(0.0001f, outlineThickness);

                Color oc = outlineColor;

                for (int i = 0; i < _panels.Count; i++)
                {
                    var p = _panels[i];
                    if (!p.available) continue;
                    if (p.state == PanelState.Dead) continue;
                    if (p.tf == null) continue;

                    float a = Mathf.Clamp01(outlineAlpha) * Mathf.Clamp01(p.alpha01);
                    // During spawn, fade outlines in too (in addition to edge draw).
                    a *= Mathf.Clamp01(p.spawnEdge01);

                    if (a <= 0.001f) continue;

                    oc.a = a;
                    Draw.Color = oc;

                    // Build a matrix from panel local -> shell local
                    Matrix4x4 m = Matrix4x4.TRS(p.tf.localPosition, p.tf.localRotation, p.tf.localScale);

                    // Use the same draw-in factor as the fill so outlines don't "jump" larger than the face.
                    float fill01 = Mathf.Clamp01(p.spawnFill01);

                    Vector3 av = Vector3.Lerp(Vector3.zero, p.v0, fill01);
                    Vector3 bv = Vector3.Lerp(Vector3.zero, p.v1, fill01);
                    Vector3 cv = Vector3.Lerp(Vector3.zero, p.v2, fill01);

                    Vector3 a0 = m.MultiplyPoint3x4(av);
                    Vector3 b0 = m.MultiplyPoint3x4(bv);
                    Vector3 c0 = m.MultiplyPoint3x4(cv);

                    // Lift outlines slightly off the face plane so they don't get swallowed by the fill mesh.
                    float offMag = Mathf.Max(0f, outlineSurfaceOffset);
                    if (offMag > 0.000001f)
                    {
                        Vector3 nLocal = (p.tf.localRotation * Vector3.forward);
                        if (nLocal.sqrMagnitude < 0.000001f) nLocal = p.baseNormalLocal;
                        nLocal.Normalize();
                        Vector3 off = nLocal * offMag;
                        a0 += off;
                        b0 += off;
                        c0 += off;
                    }

                    // Draw-in along the perimeter with easing:
                    // - edge01 = 0..1
                    // - we draw edges sequentially (1/3 each) for a "tracing" feel.
                    float e01 = Mathf.Clamp01(p.spawnEdge01);
                    float t3 = e01 * 3f;

                    DrawEdge(a0, b0, t3 - 0f);
                    DrawEdge(b0, c0, t3 - 1f);
                    DrawEdge(c0, a0, t3 - 2f);
                }
            }
        }

        private static void DrawEdge(Vector3 p0, Vector3 p1, float u)
        {
            if (u <= 0.001f) return;
            if (u >= 0.999f)
            {
                Draw.Line(p0, p1);
            }
            else
            {
                Draw.Line(p0, Vector3.Lerp(p0, p1, Mathf.Clamp01(u)));
            }
        }

        // -------------------------
        // Icosphere generation
        // -------------------------

        private static int SubdivFromPanelCount(int panelCount)
        {
            // Base icosahedron has 20 faces. Each subdivision step splits every triangle into 4.
            int subdiv = 0;
            int faces = 20;
            while (faces < panelCount && subdiv < 8)
            {
                faces *= 4;
                subdiv++;
            }
            return subdiv;
        }

        /// <summary>
        /// Builds a true icosphere mesh (subdivide + project vertices onto the sphere).
        /// Output is vertices on a sphere of the given radius + a triangle index list.
        /// </summary>
        private static void GenerateIcosphere(int subdivisions, float radius, List<Vector3> outVerts, List<int> outTris)
        {
            outVerts.Clear();
            outTris.Clear();

            float t = (1f + Mathf.Sqrt(5f)) * 0.5f; // golden ratio

            Vector3[] baseVerts =
            {
                new Vector3(-1,  t,  0),
                new Vector3( 1,  t,  0),
                new Vector3(-1, -t,  0),
                new Vector3( 1, -t,  0),

                new Vector3( 0, -1,  t),
                new Vector3( 0,  1,  t),
                new Vector3( 0, -1, -t),
                new Vector3( 0,  1, -t),

                new Vector3( t,  0, -1),
                new Vector3( t,  0,  1),
                new Vector3(-t,  0, -1),
                new Vector3(-t,  0,  1),
            };

            for (int i = 0; i < baseVerts.Length; i++)
                outVerts.Add(baseVerts[i].normalized * radius);

            int[] baseTris =
            {
                0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
                1,5,9,   5,11,4,  11,10,2, 10,7,6,  7,1,8,
                3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
                4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1
            };

            var tris = new List<int>(baseTris);

            for (int s = 0; s < Mathf.Max(0, subdivisions); s++)
            {
                var midpointCache = new Dictionary<long, int>(tris.Count);
                var newTris = new List<int>(tris.Count * 4);

                for (int i = 0; i < tris.Count; i += 3)
                {
                    int v0 = tris[i + 0];
                    int v1 = tris[i + 1];
                    int v2 = tris[i + 2];

                    int a = GetMidpointIndex(v0, v1, outVerts, midpointCache, radius);
                    int b = GetMidpointIndex(v1, v2, outVerts, midpointCache, radius);
                    int c = GetMidpointIndex(v2, v0, outVerts, midpointCache, radius);

                    // 4 new triangles
                    newTris.Add(v0); newTris.Add(a); newTris.Add(c);
                    newTris.Add(v1); newTris.Add(b); newTris.Add(a);
                    newTris.Add(v2); newTris.Add(c); newTris.Add(b);
                    newTris.Add(a);  newTris.Add(b); newTris.Add(c);
                }

                tris = newTris;
            }

            outTris.AddRange(tris);
        }

        private static int GetMidpointIndex(int i0, int i1, List<Vector3> verts, Dictionary<long, int> cache, float radius)
        {
            int a = Mathf.Min(i0, i1);
            int b = Mathf.Max(i0, i1);
            long key = ((long)a << 32) | (uint)b;

            if (cache.TryGetValue(key, out int idx))
                return idx;

            Vector3 mid = (verts[i0] + verts[i1]) * 0.5f;
            mid = mid.normalized * radius;
            verts.Add(mid);
            idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }

        // -------------------------
        // Hash utilities
        // -------------------------

        private static int SnapPanelCount(int requested)
        {
            // Supported topologies:
            // 20   = icosahedron (subdiv 0)
            // 80   = icosphere (subdiv 1)
            // 320  = icosphere (subdiv 2)
            // 1280 = icosphere (subdiv 3)
            int[] options = { 20, 80, 320, 1280 };
            int best = options[0];
            int bestDist = Mathf.Abs(requested - best);

            for (int i = 1; i < options.Length; i++)
            {
                int d = Mathf.Abs(requested - options[i]);
                if (d < bestDist)
                {
                    best = options[i];
                    bestDist = d;
                }
            }

            return best;
        }

        private static float Hash01(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393) ^ (uint)seed;
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        private static float HashSigned01(int i, int seed)
        {
            float u = Hash01(i, seed);
            return u * 2f - 1f;
        }

        private static Vector3 HashDir(int i, int seed)
        {
            Vector3 v = new Vector3(
                HashSigned01(i * 3 + 0, seed),
                HashSigned01(i * 3 + 1, seed),
                HashSigned01(i * 3 + 2, seed)
            );
            if (v.sqrMagnitude < 0.0001f) v = Vector3.right;
            return v.normalized;
        }
    }
}
