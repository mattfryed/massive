using TMPro;
using UnityEngine;

namespace Massive.TextAnimation
{
    /// <summary>
    /// Single owner for one TMP target's runtime SDF material. Other systems set
    /// a persistent style; text animations add transient offsets. This prevents
    /// persistent tier styling and reusable animation presets from fighting over
    /// the same material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TMPMaterialStateController : MonoBehaviour
    {
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOffsetId = Shader.PropertyToID("_GlowOffset");
        private static readonly int GlowInnerId = Shader.PropertyToID("_GlowInner");
        private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");
        private const string GlowKeyword = "GLOW_ON";

        [SerializeField] private TMP_Text target;

        private Material _originalSharedMaterial;
        private Material _runtimeMaterial;
        private TMPMaterialPersistentStyle _originalStyle;
        private TMPMaterialPersistentStyle _persistentStyle;
        private TMPMaterialTransientState _transient;
        private bool _initialized;

        public TMP_Text Target => target;
        public bool IsInitialized => _initialized;
        public TMPMaterialPersistentStyle OriginalStyle => _originalStyle;
        public TMPMaterialPersistentStyle PersistentStyle => _persistentStyle;

        public Material RuntimeMaterial
        {
            get
            {
                EnsureInitialized();
                return _runtimeMaterial;
            }
        }

        private void Reset()
        {
            target = GetComponent<TMP_Text>();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (target != null && _originalSharedMaterial != null)
                target.fontSharedMaterial = _originalSharedMaterial;

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeMaterial);
                else
                    DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
            _initialized = false;
        }

        public void Configure(TMP_Text text)
        {
            if (text == null)
                return;

            if (_initialized && text != target)
            {
                Debug.LogWarning(
                    $"[{nameof(TMPMaterialStateController)}] '{name}' already owns " +
                    $"'{target?.name}'. Add one material-state controller per TMP target.",
                    this);
                return;
            }

            target = text;
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_initialized)
            {
                EnsureRuntimeMaterialAssigned();
                return;
            }

            if (target == null)
                target = GetComponent<TMP_Text>();

            if (target == null)
                return;

            _originalSharedMaterial = target.fontSharedMaterial != null
                ? target.fontSharedMaterial
                : target.fontMaterial;

            if (_originalSharedMaterial == null)
                return;

            _runtimeMaterial = new Material(_originalSharedMaterial)
            {
                name = _originalSharedMaterial.name + " (MASSIVE Text Runtime)"
            };

            target.fontMaterial = _runtimeMaterial;
            _initialized = true;

            _originalStyle = ReadStyle(_runtimeMaterial);
            _persistentStyle = _originalStyle;
            _transient = TMPMaterialTransientState.Identity;
            ApplyCombinedState();
        }

        /// <summary>
        /// Makes the material's current, unanimated values the persistent base.
        /// Call only after transient animation offsets have been cleared.
        /// </summary>
        public void CapturePersistentFromCurrentMaterial()
        {
            EnsureInitialized();
            if (_runtimeMaterial == null)
                return;

            _transient = TMPMaterialTransientState.Identity;
            _persistentStyle = ReadStyle(_runtimeMaterial);
            ApplyCombinedState();
        }

        public void SetPersistentStyle(TMPMaterialPersistentStyle style)
        {
            EnsureInitialized();
            if (_runtimeMaterial == null)
                return;

            _persistentStyle = style;
            ApplyCombinedState();
        }

        public void ResetPersistentToOriginal()
        {
            EnsureInitialized();
            if (_runtimeMaterial == null)
                return;

            _persistentStyle = _originalStyle;
            ApplyCombinedState();
        }

        internal void SetTransient(in TMPMaterialTransientState transient)
        {
            EnsureInitialized();
            if (_runtimeMaterial == null)
                return;

            _transient = transient;
            ApplyCombinedState();
        }

        public void ClearTransient()
        {
            EnsureInitialized();
            if (_runtimeMaterial == null)
                return;

            _transient = TMPMaterialTransientState.Identity;
            ApplyCombinedState();
        }

        private void EnsureRuntimeMaterialAssigned()
        {
            if (target == null || _runtimeMaterial == null)
                return;

            if (target.fontMaterial != _runtimeMaterial)
                target.fontMaterial = _runtimeMaterial;
        }

        private static TMPMaterialPersistentStyle ReadStyle(Material material)
        {
            return new TMPMaterialPersistentStyle
            {
                faceColor = GetColor(material, FaceColorId, Color.white),
                outlineColor = GetColor(material, OutlineColorId, Color.black),
                outlineWidth = GetFloat(material, OutlineWidthId, 0f),
                faceDilate = GetFloat(material, FaceDilateId, 0f),
                softness = GetFloat(material, SoftnessId, 0f),
                glowEnabled = material != null && material.IsKeywordEnabled(GlowKeyword),
                glowColor = GetColor(material, GlowColorId, Color.white),
                glowOffset = GetFloat(material, GlowOffsetId, 0f),
                glowInner = GetFloat(material, GlowInnerId, 0f),
                glowOuter = GetFloat(material, GlowOuterId, 0f),
                glowPower = GetFloat(material, GlowPowerId, 0.5f)
            };
        }

        private void ApplyCombinedState()
        {
            EnsureRuntimeMaterialAssigned();
            if (_runtimeMaterial == null)
                return;

            SetColor(_runtimeMaterial, FaceColorId, _persistentStyle.faceColor);

            Color outline = _persistentStyle.outlineColor;
            if (_transient.overrideOutlineColor)
            {
                outline = Color.LerpUnclamped(
                    outline,
                    _transient.outlineColor,
                    Mathf.Clamp01(_transient.outlineColorWeight));
            }
            SetColor(_runtimeMaterial, OutlineColorId, outline);

            SetFloat(_runtimeMaterial, OutlineWidthId,
                Mathf.Max(0f, _persistentStyle.outlineWidth + _transient.outlineWidthOffset));
            SetFloat(_runtimeMaterial, FaceDilateId,
                _persistentStyle.faceDilate + _transient.faceDilateOffset);
            SetFloat(_runtimeMaterial, SoftnessId,
                Mathf.Max(0f, _persistentStyle.softness + _transient.softnessOffset));

            Color glow = _persistentStyle.glowColor;
            if (_transient.overrideGlowColor)
            {
                glow = Color.LerpUnclamped(
                    glow,
                    _transient.glowColor,
                    Mathf.Clamp01(_transient.glowColorWeight));
            }
            SetColor(_runtimeMaterial, GlowColorId, glow);
            SetFloat(_runtimeMaterial, GlowOffsetId,
                _persistentStyle.glowOffset + _transient.glowOffsetOffset);
            SetFloat(_runtimeMaterial, GlowInnerId,
                Mathf.Max(0f, _persistentStyle.glowInner + _transient.glowInnerOffset));
            SetFloat(_runtimeMaterial, GlowOuterId,
                Mathf.Max(0f, _persistentStyle.glowOuter + _transient.glowOuterOffset));
            SetFloat(_runtimeMaterial, GlowPowerId,
                Mathf.Max(0f, _persistentStyle.glowPower + _transient.glowPowerOffset));

            bool glowEnabled = _persistentStyle.glowEnabled || _transient.forceGlow;
            if (glowEnabled)
                _runtimeMaterial.EnableKeyword(GlowKeyword);
            else
                _runtimeMaterial.DisableKeyword(GlowKeyword);

            if (target != null)
            {
                target.UpdateMeshPadding();
                target.SetMaterialDirty();
            }
        }

        private static float GetFloat(Material material, int id, float fallback)
        {
            return material != null && material.HasProperty(id)
                ? material.GetFloat(id)
                : fallback;
        }

        private static Color GetColor(Material material, int id, Color fallback)
        {
            return material != null && material.HasProperty(id)
                ? material.GetColor(id)
                : fallback;
        }

        private static void SetFloat(Material material, int id, float value)
        {
            if (material != null && material.HasProperty(id))
                material.SetFloat(id, value);
        }

        private static void SetColor(Material material, int id, Color value)
        {
            if (material != null && material.HasProperty(id))
                material.SetColor(id, value);
        }
    }
}
