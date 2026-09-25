using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Singularity
{
    /// <summary>
    /// Isolated traversal probe, not a full-game player. Logical position is an
    /// across coordinate and periodic surface distance; only presentation is 3D.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SingularityPlayerMotor : MonoBehaviour
    {
        [Header("Surface and Start")]
        [SerializeField] private SingularitySurface surface;
        [SerializeField] private float initialAcross = -5f;
        [SerializeField, Range(0f, 1f)] private float initialLoopFraction = 0.14f;
        [Tooltip("Edit-mode inspection only. Does not change the Play Mode start.")]
        [SerializeField] private bool usePreviewPosition;
        [SerializeField] private float previewAcross;
        [SerializeField, Range(0f, 1f)] private float previewLoopFraction;

        [Header("Surface Locomotion")]
        [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
        [SerializeField, Min(0.01f)] private float acceleration = 22f;
        [SerializeField, Min(0.01f)] private float braking = 28f;
        [SerializeField, Range(0f, 0.9f)] private float inputDeadzone = 0.12f;
        [Tooltip("Leave a visible margin at the two side edges. Top/bottom are continuous, not walls.")]
        [SerializeField, Min(0f)] private float sidePadding = 0.06f;

        [Header("Input — Prototype Only")]
        [SerializeField] private bool preferRewired = true;
        [SerializeField] private int rewiredPlayerId;
        [SerializeField] private string horizontalAction = "MoveH";
        [SerializeField] private string verticalAction = "MoveV";
        [Tooltip("Used when Rewired has no active directional input. Does not alter input assets.")]
        [SerializeField] private bool allowKeyboardFallback = true;
        [SerializeField] private bool arrowKeys = true;
        [SerializeField] private bool wasdKeys = true;

        [Header("Independent Grid Attraction")]
        [SerializeField, Min(0f)] private float attractionRadius = 2.145f;
        [SerializeField, Range(0f, 3f)] private float attractionPull = 0.8f;
        [Tooltip("Seconds to close 95% of the attraction-center gap; independent of movement inertia.")]
        [SerializeField, Min(0f)] private float attractionResponseSeconds = 0.06f;

        [Header("Surface-Conforming Player")]
        [SerializeField, Min(0.02f)] private float radius = 0.32f;
        [SerializeField, Range(0.01f, 0.3f)] private float ringThickness = 0.09f;
        [SerializeField, Range(0f, 1f)] private float fillOpacity = 0.12f;
        [SerializeField, Range(0f, 1f)] private float rearOpacity = 0.72f;
        [SerializeField, Range(0.5f, 1f)] private float rearVisualScale = 0.9f;
        [Tooltip("Actual world-space rim height along the surface normal. The rim base sits at Surface Lift; its face sits at Surface Lift + Rim Height. Zero restores a flat disc.")]
        [SerializeField, Min(0f)] private float rimHeight = 0.08f;
        [SerializeField, Min(0f)] private float surfaceLift = 0.035f;
        [SerializeField] private Shader playerShader;

        private Vector2 _position;
        private Vector2 _velocity;
        private Vector2 _attractionPosition;
        private Vector2 _facing = Vector2.up;
        private Vector2 _inputOverride;
        private bool _hasInputOverride;
        private bool _initialized;
        private bool _wasPlaying;
        private bool _authoringDirty = true;
        private int _surfaceRevision = -1;
        private float _surfaceBrightness = -1f;
        private Rewired.Player _rewiredPlayer;
        private bool _rewiredLookupFailed;
        private GameObject _visualObject;
        private Mesh _mesh;
        private Material _material;
        private MeshRenderer _meshRenderer;
        private Vector3[] _vertices;
        private Color[] _colors;
        private int[] _indices;
        private Vector2 _visualAcrossStep, _visualAlongStep;
        private Matrix4x4 _surfaceToVisual, _worldToVisual, _normalToWorld;

        private const int Segments = 64;
        private const int DiscCount = 4;
        private const int RingVertexCount = (Segments + 1) * 2;
        private const int DiscVertexCount = Segments + 2;
        private const float Log20 = 2.995732274f;

        public SingularitySurface Surface
        {
            get => surface;
            set { if (surface == value) return; surface = value; ResetToStart(); _authoringDirty = true; }
        }
        public Vector2 SurfacePosition => _initialized ? _position : AuthoredPosition(false);
        public Vector2 SmoothedAttractionPosition => _initialized ? _attractionPosition : SurfacePosition;
        public Vector2 SurfaceVelocity => _velocity;
        public float AttractionRadius => Mathf.Max(0f, attractionRadius);
        public float AttractionPull => Mathf.Max(0f, attractionPull);
        public float AttractionResponseSeconds => Mathf.Max(0f, attractionResponseSeconds);
        public float Radius => radius;
        public float RimHeight => Mathf.Max(0f, rimHeight);
        public float MoveSpeed => moveSpeed;

        private void OnEnable()
        {
            _wasPlaying = Application.IsPlaying(gameObject);
            _rewiredLookupFailed = false;
            _rewiredPlayer = null;
            ResetToStart();
            _authoringDirty = true;
        }

        private void OnValidate()
        {
            moveSpeed = SafePositive(moveSpeed, 5f);
            acceleration = SafePositive(acceleration, 22f);
            braking = SafePositive(braking, 28f);
            radius = SafePositive(radius, 0.32f);
            rimHeight = Mathf.Max(0f, Finite(rimHeight) ? rimHeight : 0.08f);
            attractionRadius = Mathf.Max(0f, Finite(attractionRadius) ? attractionRadius : 2.145f);
            attractionPull = Mathf.Clamp(Finite(attractionPull) ? attractionPull : 0.8f, 0f, 3f);
            attractionResponseSeconds = Mathf.Max(0f, Finite(attractionResponseSeconds) ? attractionResponseSeconds : 0.06f);
            _rewiredLookupFailed = false;
            _rewiredPlayer = null;
            _authoringDirty = true;
        }

        private void Update()
        {
            bool playing = Application.IsPlaying(gameObject);
            if (playing != _wasPlaying)
            {
                _wasPlaying = playing;
                ClearInputOverride();
                ResetToStart();
            }
            if (!surface || !gameObject.scene.IsValid())
            {
                if (_meshRenderer) _meshRenderer.enabled = false;
                return;
            }
            if (!_initialized) ResetToStart();
            bool changedSurface = _surfaceRevision != surface.Revision || !Mathf.Approximately(_surfaceBrightness, surface.BacksideBrightness);
            if (changedSurface)
            {
                _position = WrapPosition(_position);
                _position.x = Mathf.Clamp(_position.x, -AcrossLimit(_position), AcrossLimit(_position));
                _attractionPosition = WrapPosition(_attractionPosition);
            }
            if (!playing)
            {
                Vector2 preview = AuthoredPosition(usePreviewPosition);
                if (_authoringDirty || changedSurface || preview != _position)
                {
                    _position = preview;
                    _attractionPosition = preview;
                    _velocity = Vector2.zero;
                    _facing = Vector2.up;
                    RefreshVisual();
                }
            }
            else
            {
                Simulate(Time.deltaTime, _hasInputOverride ? _inputOverride : ReadInput());
                RefreshVisual();
            }
            if (!_meshRenderer || !_meshRenderer.enabled) RefreshVisual();
            _surfaceRevision = surface.Revision;
            _surfaceBrightness = surface.BacksideBrightness;
            _authoringDirty = false;
        }

        [ContextMenu("Reset Prototype Player To Start")]
        public void ResetToStart()
        {
            _position = AuthoredPosition(false);
            _attractionPosition = _position;
            _velocity = Vector2.zero;
            _facing = Vector2.up;
            _initialized = surface != null;
            _authoringDirty = true;
        }

        public void SetInputOverride(Vector2 input)
        {
            _inputOverride = SanitizeInput(input);
            _hasInputOverride = true;
        }

        public void ClearInputOverride() { _hasInputOverride = false; _inputOverride = Vector2.zero; }

        /// <summary>
        /// Advances logical state only. Input is surface-relative: +Y always
        /// advances around the loop, so its screen projection reverses on the rear.
        /// No input, rendering, physics, scene, or global-game-state side effects.
        /// </summary>
        public void Simulate(float deltaTime, Vector2 input)
        {
            if (!surface || !Finite(deltaTime) || deltaTime <= 0f || surface.LoopLength <= 0.001f) return;
            if (!_initialized) ResetToStart();
            input = SanitizeInput(input);
            float magnitude = input.magnitude;
            float deadzone = Mathf.Clamp(inputDeadzone, 0f, 0.9f);
            input = magnitude <= deadzone ? Vector2.zero : input / magnitude * Mathf.InverseLerp(deadzone, 1f, magnitude);
            Vector2 target = input * SafePositive(moveSpeed, 5f);
            float rate = input == Vector2.zero ? SafePositive(braking, 28f) : SafePositive(acceleration, 22f);

            // Small midpoint steps preserve physical surface speed around bends
            // and non-orthogonal/narrower parts of the map. Work remains bounded.
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(deltaTime, 30f) * 120f), 1, 3600);
            float stepTime = Mathf.Min(deltaTime, 30f) / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector2 oldVelocity = _velocity;
                _velocity = Vector2.MoveTowards(_velocity, target, rate * stepTime);
                Vector2 meanVelocity = (oldVelocity + _velocity) * 0.5f;
                Vector2 dq = TangentToParameters(_position, meanVelocity);
                Vector2 midpoint = WrapPosition(_position + dq * (stepTime * 0.5f));
                _position = WrapPosition(_position + TangentToParameters(midpoint, meanVelocity) * stepTime);
                float limit = AcrossLimit(_position);
                if (Mathf.Abs(_position.x) > limit)
                {
                    _position.x = Mathf.Clamp(_position.x, -limit, limit);
                    if (Mathf.Sign(_velocity.x) == Mathf.Sign(_position.x)) _velocity.x = 0f;
                }
                if (_velocity.sqrMagnitude > 0.0001f) _facing = _velocity.normalized;
                float alpha = attractionResponseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Log20 * stepTime / attractionResponseSeconds);
                _attractionPosition.x = Mathf.Lerp(_attractionPosition.x, _position.x, alpha);
                _attractionPosition.y = Mathf.Repeat(_attractionPosition.y + LoopDelta(_attractionPosition.y, _position.y) * alpha, surface.LoopLength);
            }
        }

        private Vector2 ReadInput()
        {
            if (preferRewired && Rewired.ReInput.isReady && !_rewiredLookupFailed)
            {
                try
                {
                    if (_rewiredPlayer == null) _rewiredPlayer = Rewired.ReInput.players.GetPlayer(rewiredPlayerId);
                    if (_rewiredPlayer != null)
                    {
                        Vector2 input = new Vector2(_rewiredPlayer.GetAxis(horizontalAction), _rewiredPlayer.GetAxis(verticalAction));
                        if (input.sqrMagnitude > inputDeadzone * inputDeadzone) return input;
                    }
                }
                catch (Exception e)
                {
                    _rewiredLookupFailed = true;
                    Debug.LogWarning("SINGULARITY prototype input unavailable; keyboard fallback remains available. " + e.Message, this);
                }
            }
            Vector2 keys = Vector2.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (allowKeyboardFallback)
            {
                if ((arrowKeys && Input.GetKey(KeyCode.LeftArrow)) || (wasdKeys && Input.GetKey(KeyCode.A))) keys.x -= 1f;
                if ((arrowKeys && Input.GetKey(KeyCode.RightArrow)) || (wasdKeys && Input.GetKey(KeyCode.D))) keys.x += 1f;
                if ((arrowKeys && Input.GetKey(KeyCode.DownArrow)) || (wasdKeys && Input.GetKey(KeyCode.S))) keys.y -= 1f;
                if ((arrowKeys && Input.GetKey(KeyCode.UpArrow)) || (wasdKeys && Input.GetKey(KeyCode.W))) keys.y += 1f;
            }
#endif
            return Vector2.ClampMagnitude(keys, 1f);
        }

        private Vector2 AuthoredPosition(bool preview)
        {
            if (!surface || surface.LoopLength <= 0f) return Vector2.zero;
            Vector2 p = new Vector2(preview ? previewAcross : initialAcross,
                Mathf.Repeat(preview ? previewLoopFraction : initialLoopFraction, 1f) * surface.LoopLength);
            if (!Finite(p.x)) p.x = 0f;
            if (!Finite(p.y)) p.y = 0f;
            p.x = Mathf.Clamp(p.x, -AcrossLimit(p), AcrossLimit(p));
            return p;
        }

        private float AcrossLimit(Vector2 p)
        {
            Vector3 across = Derivative(p, true);
            float unitsPerAcross = Mathf.Max(0.0001f, across.magnitude);
            return Mathf.Max(0f, surface.Width * 0.5f - (radius + Mathf.Max(0f, sidePadding)) / unitsPerAcross);
        }

        // Gram-Schmidt gives input orthonormal surface axes. Inverting their
        // Jacobian also removes the shear term, not just separate axis scaling.
        private Vector2 TangentToParameters(Vector2 p, Vector2 tangent)
        {
            surface.Frame(p.x, p.y, out Vector3 a, out Vector3 b);
            a = surface.transform.TransformVector(a);
            b = surface.transform.TransformVector(b);
            float aLength = Mathf.Max(0.0001f, a.magnitude);
            Vector3 aUnit = a / aLength;
            float shear = Vector3.Dot(b, aUnit);
            float bLength = Mathf.Max(0.0001f, (b - aUnit * shear).magnitude);
            float ds = tangent.y / bLength;
            return new Vector2((tangent.x - ds * shear) / aLength, ds);
        }

        private Vector3 Derivative(Vector2 p, bool across)
        {
            surface.Frame(p.x, p.y, out Vector3 a, out Vector3 b);
            return surface.transform.TransformVector(across ? a : b);
        }

        private Vector2 WrapPosition(Vector2 p) { p.y = Mathf.Repeat(p.y, surface.LoopLength); return p; }
        private float LoopDelta(float from, float to) => Mathf.Repeat(to - from + surface.LoopLength * 0.5f, surface.LoopLength) - surface.LoopLength * 0.5f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float SafePositive(float value, float fallback) => Finite(value) ? Mathf.Max(0.01f, value) : fallback;
        private static Vector2 SanitizeInput(Vector2 value) => Finite(value.x) && Finite(value.y) ? Vector2.ClampMagnitude(value, 1f) : Vector2.zero;

        /// <summary>Rebuilds only owned transient geometry; safe for explicit editor previews.</summary>
        public void RefreshVisual()
        {
            if (!surface || !isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            if (!EnsureVisual()) return;
            _visualAcrossStep = TangentToParameters(_position, Vector2.right);
            _visualAlongStep = TangentToParameters(_position, Vector2.up);
            _worldToVisual = transform.worldToLocalMatrix;
            _surfaceToVisual = _worldToVisual * surface.transform.localToWorldMatrix;
            _normalToWorld = surface.transform.worldToLocalMatrix.transpose;
            float rear = Mathf.Clamp01(surface.RearWeight(_position.y));
            float r = radius * Mathf.Lerp(1f, rearVisualScale, rear);
            float opacity = Mathf.Lerp(1f, rearOpacity, rear);
            // A dark center separates the player from both overlapping grid faces.
            WriteDisc(0, Vector2.zero, r, new Color(0f, 0f, 0f, 0.86f));
            WriteDisc(1, Vector2.zero, r * (1f - ringThickness), new Color(1f, 1f, 1f, fillOpacity * opacity));
            WriteDisc(2, _facing * (r * 0.52f), r * 0.16f, new Color(1f, 1f, 1f, opacity));
            WriteDisc(3, -_facing * (r * 0.42f), r * 0.07f, new Color(1f, 1f, 1f, opacity * 0.8f));
            int start = DiscCount * DiscVertexCount;
            int wall = start + RingVertexCount;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                Vector2 ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _vertices[start + i * 2] = MapVisual(ray * r);
                _vertices[start + i * 2 + 1] = MapVisual(ray * (r * (1f - ringThickness)));
                _colors[start + i * 2] = SurfaceColor(new Color(1f, 1f, 1f, opacity), ray * r);
                _colors[start + i * 2 + 1] = SurfaceColor(new Color(1f, 1f, 1f, opacity), ray * (r * (1f - ringThickness)));
                // Real side faces retain a small silhouette at an edge-on turn.
                // Every sample follows its own surface normal, including bends.
                _vertices[wall + i * 2] = MapVisual(ray * r, 0f);
                _vertices[wall + i * 2 + 1] = _vertices[start + i * 2];
                _colors[wall + i * 2] = _colors[wall + i * 2 + 1] = SurfaceColor(new Color(1f, 1f, 1f, opacity * 0.9f), ray * r);
            }
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();
            _meshRenderer.enabled = true;
        }

        private void WriteDisc(int disc, Vector2 offset, float discRadius, Color color)
        {
            int start = disc * DiscVertexCount;
            _vertices[start] = MapVisual(offset);
            _colors[start] = SurfaceColor(color, offset);
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                Vector2 point = offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * discRadius;
                _vertices[start + 1 + i] = MapVisual(point);
                _colors[start + 1 + i] = SurfaceColor(color, point);
            }
        }

        private Vector3 MapVisual(Vector2 offset) => MapVisual(offset, RimHeight);

        private Color SurfaceColor(Color color, Vector2 offset)
        {
            float distance = (_position + _visualAcrossStep * offset.x + _visualAlongStep * offset.y).y;
            float brightness = surface.EvaluateBrightness(distance);
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }

        private Vector3 MapVisual(Vector2 offset, float heightAboveBase)
        {
            Vector2 p = _position + _visualAcrossStep * offset.x + _visualAlongStep * offset.y;
            Vector3 normal = _normalToWorld.MultiplyVector(surface.Normal(p.x, p.y)).normalized;
            return _surfaceToVisual.MultiplyPoint3x4(surface.Evaluate(p.x, p.y)) + _worldToVisual.MultiplyVector(normal * (surfaceLift + heightAboveBase));
        }

        private bool EnsureVisual()
        {
            if (_meshRenderer && _mesh && _material)
            {
                if (playerShader && _material.shader != playerShader) _material.shader = playerShader;
                _visualObject.layer = gameObject.layer;
                return true;
            }
            Shader shader = playerShader ? playerShader : Shader.Find("MASSIVE/Singularity/Player Surface");
            if (!shader) return false;
            ReleaseVisual();
            _material = new Material(shader) { name = "SINGULARITY player (transient)", hideFlags = HideFlags.HideAndDontSave };
            _mesh = new Mesh { name = "SINGULARITY conformed player (transient)", hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
            _vertices = new Vector3[DiscCount * DiscVertexCount + RingVertexCount * 2];
            _colors = new Color[_vertices.Length];
            _indices = new int[DiscCount * Segments * 3 + Segments * 12];
            int n = 0;
            for (int disc = 0; disc < DiscCount; disc++)
            {
                int start = disc * DiscVertexCount;
                for (int i = 0; i < Segments; i++) { _indices[n++] = start; _indices[n++] = start + i + 1; _indices[n++] = start + i + 2; }
            }
            int ring = DiscCount * DiscVertexCount;
            for (int strip = 0; strip < 2; strip++)
            for (int i = 0; i < Segments; i++)
            {
                int a = ring + strip * RingVertexCount + i * 2;
                _indices[n++] = a; _indices[n++] = a + 1; _indices[n++] = a + 2;
                _indices[n++] = a + 2; _indices[n++] = a + 1; _indices[n++] = a + 3;
            }
            _mesh.vertices = _vertices;
            _mesh.triangles = _indices;
            _visualObject = new GameObject("Player Surface Visual (generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            _visualObject.transform.SetParent(transform, false);
            _visualObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _meshRenderer = _visualObject.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        private void OnDisable() { _velocity = Vector2.zero; _rewiredPlayer = null; ReleaseVisual(); }
        private void OnDestroy() => ReleaseVisual();
        private void ReleaseVisual()
        {
            if (_meshRenderer) _meshRenderer.enabled = false;
            DestroyOwned(_visualObject); DestroyOwned(_mesh); DestroyOwned(_material);
            _visualObject = null; _mesh = null; _material = null; _meshRenderer = null;
        }
        private void DestroyOwned(UnityEngine.Object owned)
        {
            if (!owned) return;
            if (Application.IsPlaying(gameObject)) Destroy(owned); else DestroyImmediate(owned);
        }
    }
}
