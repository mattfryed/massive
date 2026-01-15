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
    /// - One random face "shatters" off per health lost (1 health ~= 1 panel).
    /// - During attacks, panels bias toward a spear formation along the sphere's local +Z.
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

        [Serializable]
        private sealed class Panel
        {
            public bool available;
            public PanelState state;

            public float phase;

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

            // Shatter animation
            public float alpha01 = 1f;
            public float shatterStartTime;
            public Vector3 shatterStartPos;
            public Quaternion shatterStartRot;
            public Vector3 shatterStartScale;
            public Vector3 shatterDirLocal;
            public Vector3 shatterAxisLocal;
        }

        [Header("Refs")]
        [SerializeField] private EnemyBase enemy;
        [Tooltip("Optional: core transform (e.g., inner particle system root) to pulse slightly during breathing.")]
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

        public float Spear01 => spear01;

        public void SetSpear01(float value01)
        {
            spear01 = Mathf.Clamp01(value01);
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
        }

        private void OnEnable()
        {
            // NOTE: Shapes' ImmediateModeShapeDrawer registers itself for rendering in its own OnEnable.
            // Since this class defines its own OnEnable, we MUST call the base method or DrawShapes()
            // will never be invoked (resulting in missing outlines).
            base.OnEnable();

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

            // Pulse the core (runtime only by default).
            if (core != null)
            {
                float t = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * Mathf.Max(0f, corePulseSpeed);
                float sin = Mathf.Sin(t);
                float pulse = 1f + (sin * corePulseAmplitude);
                pulse += spear01 * coreAttackBoost;
                float s = Mathf.Max(0.001f, coreBaseScale * pulse);
                core.localScale = new Vector3(s, s, s);
            }

            SyncPanelsToHealth();
            TickPanelsPoseAndMaterials();
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
                mesh.vertices = new[] { la, lb, lc };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;

                float phase = Hash01(f, GetInstanceID()) * Mathf.PI * 2f;

                _panels.Add(new Panel
                {
                    available = true,
                    state = PanelState.Alive,
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
                    alpha01 = 1f
                });
            }

            // Populate alive list
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i].available && _panels[i].state == PanelState.Alive)
                    _alive.Add(i);
                else if (_panels[i].available)
                    _dead.Add(i);
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
                // This allows high-panel-count shells (icosphere subdivisions) to start fully intact
                // while still dying in a reasonable number of hits.
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
            _panels[idx] = p;
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
            _panels[idx] = p;
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

            _panels[idx] = p;
        }

        // -------------------------
        // Pose + material updates
        // -------------------------

        private void TickPanelsPoseAndMaterials()
        {
            if (_panels.Count == 0) return;

            float t = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);

            float spd = Mathf.Max(0f, breatheSpeed);
            float s01 = Mathf.Clamp01(spear01);

            float baseFillA = Mathf.Clamp01(panelFillAlpha);

            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (!p.available) continue;
                if (p.tf == null) continue;

                if (p.state == PanelState.Alive)
                {
                    float breatheSin = Mathf.Sin(t * spd + (p.phase * perPanelPhaseJitter));
                    // If breatheOutOnly is enabled, we remap to 0..1 so panels never move "inward".
                    // This avoids coplanar overlap at the contracted point (especially when baseLift = 0).
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

                    p.tf.localPosition = pos;
                    p.tf.localRotation = rot;
                    p.tf.localScale = new Vector3(scale, scale, scale);

                    p.alpha01 = 1f;
                }
                else if (p.state == PanelState.Shattering)
                {
                    float start = p.shatterStartTime;
                    float now = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
                    float dur = Mathf.Max(0.05f, shatterSeconds);
                    float u = Mathf.Clamp01((now - start) / dur);
                    float ease = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic

                    p.alpha01 = 1f - u;
                    p.tf.localPosition = p.shatterStartPos + p.shatterDirLocal * (shatterDistance * ease);
                    p.tf.localRotation = p.shatterStartRot * Quaternion.AngleAxis(shatterSpinDegrees * ease, p.shatterAxisLocal);
                    p.tf.localScale = p.shatterStartScale * Mathf.Lerp(1f, 0.15f, u);

                    if (u >= 0.999f)
                    {
                        p.state = PanelState.Dead;
                        p.alpha01 = 0f;
                        if (p.mr != null) p.mr.enabled = false;
                        p.tf.gameObject.SetActive(false);
                    }
                }

                // Apply fill color via MaterialPropertyBlock
                if (p.mr != null)
                {
                    float a = baseFillA * Mathf.Clamp01(p.alpha01);

                    // Some shaders use _BaseColor (URP), some use _Color.
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

                _panels[i] = p;
            }
        }

        // -------------------------
        // Outlines
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
                    if (a <= 0.001f) continue;

                    oc.a = a;
                    Draw.Color = oc;

                    // Build a matrix from panel local -> shell local
                    Matrix4x4 m = Matrix4x4.TRS(p.tf.localPosition, p.tf.localRotation, p.tf.localScale);

                    Vector3 a0 = m.MultiplyPoint3x4(p.v0);
                    Vector3 b0 = m.MultiplyPoint3x4(p.v1);
                    Vector3 c0 = m.MultiplyPoint3x4(p.v2);

                    // Lift outlines slightly off the face plane so they don't get swallowed by the fill mesh.
                    // This also helps the "each panel has its own border" readability you're after.
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

                    Draw.Line(a0, b0);
                    Draw.Line(b0, c0);
                    Draw.Line(c0, a0);
                }
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
            // Supported "sane" topologies:
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
