using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelCarouselController : MonoBehaviour
{
    public enum RotationAxis { X, Y, Z }

    [Header("Content")]
    [SerializeField] private LevelCatalog catalog;      // assign a LevelCatalog asset here
    [SerializeField] private Transform carouselRoot;    // optional; defaults to this.transform

    [Header("Layout")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;
    [SerializeField] private float radius = 6f;
    [SerializeField] private float iconHeight = 0f;

    [Tooltip("Flip this to reverse the visual spin direction without changing stage order.")]
    [SerializeField] private int rotationDirectionSign = 1; // +1 or -1

    [Header("Rotation Feel")]
    [SerializeField] private float rotateDurationSeconds = 0.18f;
    [SerializeField] private AnimationCurve rotateEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Relative Icon Sizes (Optional)")]
    [Tooltip("Resize icons relative to the selection, including across the carousel wrap. Sizes multiply each icon's authored scale and can be adjusted during Play Mode.")]
    [SerializeField] private bool useRelativeIconSizes;
    [Tooltip("Size multiplier for the selected icon. 1 keeps its authored size.")]
    [Min(0.01f), SerializeField] private float selectedIconScale = 1f;
    [Min(0.01f), SerializeField] private float oneStepLeftScale = 0.75f;
    [Tooltip("Size for icons two or more positions to the left.")]
    [Min(0.01f), SerializeField] private float twoStepsLeftScale = 0.5f;
    [Min(0.01f), SerializeField] private float oneStepRightScale = 1.5f;
    [Tooltip("Size for icons two or more positions to the right.")]
    [Min(0.01f), SerializeField] private float twoStepsRightScale = 2f;

    [Header("Selection")]
    [SerializeField] private int startIndex = 0;

    [Header("Confirm Lockout (prevents accidental level confirm on scene load)")]
    [Tooltip("Blocks ConfirmSelection() for a short time after the scene loads. Movement still works.")]
    [SerializeField] private float confirmLockoutSecondsOnSceneLoad = 0.6f;

    [Tooltip("Use unscaled time for the confirm lockout (recommended for menus).")]
    [SerializeField] private bool confirmLockoutUsesUnscaledTime = true;

    // Runtime state
    public event Action<LevelDefinition, int> OnSelectionChanged;

    public int SelectedIndex => _selectedIndex;

    public LevelDefinition SelectedLevel =>
        (_levels != null && _selectedIndex >= 0 && _selectedIndex < _levels.Count)
            ? _levels[_selectedIndex]
            : null;

    private readonly List<LevelDefinition> _levels = new();
    private readonly List<IconPresentation> _spawnedIcons = new();

    private int _selectedIndex = 0;
    private float _stepAngleDeg = 0f;

    // Prevent “wrong-way shortcut” by accumulating our own angle
    private float _currentRootAngleDeg = 0f;
    private float _targetRootAngleDeg;
    private Coroutine _rotateRoutine;
    private bool _relativeIconSizesApplied;
    private bool _iconScaleSettingsDirty;

    private sealed class IconPresentation
    {
        public Transform root;
        public float scale = 1f;
        public float scaleFrom = 1f;
        public float scaleTarget = 1f;
        public readonly List<(Transform transform, Vector3 scale)> localParticles = new();

        public void ApplyScale(float value)
        {
            if (Mathf.Approximately(scale, value)) return;
            scale = value;
            if (root != null) root.localScale = Vector3.one * value;
            // Local-mode particles ignore parent scale for both positions and size.
            // Apply only the relative multiplier to their authored local scale.
            foreach (var particle in localParticles)
                if (particle.transform != null)
                    particle.transform.localScale = particle.scale * value;
        }
    }

    private bool _hasConfirmed = false;

    // confirm gate
    private float _confirmAllowedAt = 0f;

    private void Awake()
    {
        if (carouselRoot == null)
            carouselRoot = transform;

        rotationDirectionSign = rotationDirectionSign >= 0 ? 1 : -1;
    }

    private void Start()
    {
        // Arm confirm lockout (movement still allowed)
        float delay = Mathf.Max(0f, confirmLockoutSecondsOnSceneLoad);
        _confirmAllowedAt = NowConfirmGate() + delay;

        RebuildFromCatalog();

        if (_levels.Count == 0)
            return;

        _selectedIndex = Wrap(startIndex, _levels.Count);
        SnapToSelection();

        // Fire initial selection so UI updates immediately on scene start
        OnSelectionChanged?.Invoke(SelectedLevel, _selectedIndex);
    }

    private void Update()
    {
        if (_levels.Count > 0 && (_iconScaleSettingsDirty || _relativeIconSizesApplied != useRelativeIconSizes))
            StartRotateTo(_targetRootAngleDeg);
    }

    private void OnValidate() => _iconScaleSettingsDirty = true;

    private void OnEnable()
    {
        if (_levels.Count > 0 && carouselRoot != null) SnapToSelection();
    }

    private void OnDisable()
    {
        if (_rotateRoutine != null) StopCoroutine(_rotateRoutine);
        _rotateRoutine = null;
        // The separate carousel root may already be gone during scene unload.
        // Resume at the selected slot in OnEnable instead of moving it here.
    }

    // ============================================================
    // Public API (called by input scripts)
    // ============================================================

    public void StepNextStage() => StepByIndex(+1);
    public void StepPrevStage() => StepByIndex(-1);

    public void ConfirmSelection()
    {
        // Don't allow confirm during the lockout window
        if (NowConfirmGate() < _confirmAllowedAt)
            return;

        if (_hasConfirmed) return;

        // Only mark confirmed AFTER passing the gate
        _hasConfirmed = true;

        // UI Confirm SFX (meaningful action point)
        if (AudioSystem.I != null)
            AudioSystem.I.Play2D(AudioEventId.UI_Confirm);

        var def = SelectedLevel;
        if (def == null) return;

        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SelectLevel(def);

        SceneFlow.GoToInstructions();
    }

    // ============================================================
    // Core selection + rotation logic
    // ============================================================

    private void StepByIndex(int dir)
    {
        if (_levels.Count == 0) return;

        int cur = _selectedIndex;
        int next = Wrap(cur + dir, _levels.Count);

        if (next == cur) return;

        // UI Navigate SFX (meaningful action point)
        if (AudioSystem.I != null)
            AudioSystem.I.Play2D(AudioEventId.UI_Navigate);

        _selectedIndex = next;

        // Rotate root by exactly one step (no shortest-path shortcuts)
        float delta = -dir * _stepAngleDeg * rotationDirectionSign;
        // Accumulate from the destination so rapid inputs cannot leave the
        // selected icon between slots when they interrupt an animation.
        float targetAngle = _targetRootAngleDeg + delta;

        StartRotateTo(targetAngle);

        OnSelectionChanged?.Invoke(SelectedLevel, _selectedIndex);
    }

    private void StartRotateTo(float targetAngleDeg)
    {
        if (_rotateRoutine != null)
            StopCoroutine(_rotateRoutine);

        _targetRootAngleDeg = targetAngleDeg;
        _relativeIconSizesApplied = useRelativeIconSizes;
        _iconScaleSettingsDirty = false;
        for (int i = 0; i < _spawnedIcons.Count; i++)
        {
            var icon = _spawnedIcons[i];
            if (icon == null) continue;
            icon.scaleFrom = icon.scale;
            icon.scaleTarget = ScaleForIcon(i);
        }
        _rotateRoutine = StartCoroutine(RotateToRoutine(targetAngleDeg));
    }

    private IEnumerator RotateToRoutine(float targetAngleDeg)
    {
        float start = _currentRootAngleDeg;
        float t = 0f;

        while (t < 1f)
        {
            t += (rotateDurationSeconds <= 0f) ? 1f : (Time.deltaTime / rotateDurationSeconds);
            float eased = rotateEase != null ? rotateEase.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            float angle = Mathf.Lerp(start, targetAngleDeg, eased);
            ApplyRootAngleNow(angle);
            ApplyIconScaleProgress(eased);

            yield return null;
        }

        ApplyRootAngleNow(targetAngleDeg);
        ApplyIconScaleProgress(1f);
        _rotateRoutine = null;
    }

    private void SnapToSelection()
    {
        _currentRootAngleDeg = ComputeRootAngleForIndex(_selectedIndex);
        _targetRootAngleDeg = _currentRootAngleDeg;
        ApplyRootAngleNow(_currentRootAngleDeg);
        _relativeIconSizesApplied = useRelativeIconSizes;
        _iconScaleSettingsDirty = false;
        for (int i = 0; i < _spawnedIcons.Count; i++)
            _spawnedIcons[i]?.ApplyScale(ScaleForIcon(i));
    }

    private float ScaleForIcon(int index)
    {
        if (!useRelativeIconSizes || _levels.Count == 0) return 1f;
        int offset = Wrap(index - _selectedIndex, _levels.Count);
        if (offset > _levels.Count / 2) offset -= _levels.Count;
        float scale = offset switch
        {
            0 => selectedIconScale,
            -1 => oneStepLeftScale,
            1 => oneStepRightScale,
            < -1 => twoStepsLeftScale,
            _ => twoStepsRightScale
        };
        return Mathf.Max(0.01f, scale);
    }

    private void ApplyIconScaleProgress(float progress)
    {
        foreach (var icon in _spawnedIcons)
            if (icon != null)
                icon.ApplyScale(Mathf.Lerp(icon.scaleFrom, icon.scaleTarget, progress));
    }

    private float ComputeRootAngleForIndex(int index)
    {
        // rootAngle = -index * stepAngle * rotationDirectionSign
        return -index * _stepAngleDeg * rotationDirectionSign;
    }

    private void ApplyRootAngleNow(float angleDeg)
    {
        _currentRootAngleDeg = angleDeg;

        Vector3 axisLocal = GetRotationAxisLocalSigned();
        carouselRoot.localRotation = Quaternion.AngleAxis(_currentRootAngleDeg, axisLocal);
    }

    // ============================================================
    // Build / layout
    // ============================================================

    private void RebuildFromCatalog()
    {
        ClearSpawned();

        _levels.Clear();
        if (catalog == null || catalog.Levels == null) return;

        foreach (var def in catalog.Levels)
        {
            if (def != null)
                _levels.Add(def);
        }

        int n = _levels.Count;
        if (n == 0) return;

        _stepAngleDeg = 360f / n;

        // Spawn icons
        for (int i = 0; i < n; i++)
        {
            var def = _levels[i];
            if (def == null || def.iconPrefab == null)
            {
                // Keep catalog positions aligned even if an icon is unassigned.
                _spawnedIcons.Add(null);
                continue;
            }

            // A separate parent leaves prefab-owned scale animations intact.
            var slot = new GameObject($"LevelIcon_{i:00}_{def.name}").transform;
            slot.SetParent(carouselRoot, false);
            var icon = Instantiate(def.iconPrefab, slot);
            icon.transform.localPosition = Vector3.zero;
            icon.transform.localRotation = Quaternion.identity;
            var presentation = new IconPresentation { root = slot };
            foreach (var particles in icon.GetComponentsInChildren<ParticleSystem>(true))
                if (particles.main.scalingMode == ParticleSystemScalingMode.Local)
                    presentation.localParticles.Add((particles.transform, particles.transform.localScale));
            _spawnedIcons.Add(presentation);
        }

        LayoutIcons();
    }

    private void LayoutIcons()
    {
        int n = _levels.Count;
        if (n == 0) return;

        // Defines the ring plane
        GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir);

        for (int i = 0; i < _spawnedIcons.Count; i++)
        {
            Transform icon = _spawnedIcons[i]?.root;
            if (icon == null) continue;

            float angle = i * _stepAngleDeg * Mathf.Deg2Rad;

            Vector3 localPos =
                (baseDir * Mathf.Cos(angle) + orthoDir * Mathf.Sin(angle)) * radius;

            localPos += Vector3.forward * iconHeight; // local forward height (works with your current setup)
            icon.localPosition = localPos;

            // Optional: face inward toward center
            Vector3 toCenter = -new Vector3(localPos.x, localPos.y, localPos.z);
            if (toCenter.sqrMagnitude > 0.0001f)
                icon.localRotation = Quaternion.LookRotation(Vector3.forward, toCenter.normalized);
        }
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawnedIcons.Count; i++)
        {
            if (_spawnedIcons[i]?.root != null)
                Destroy(_spawnedIcons[i].root.gameObject);
        }
        _spawnedIcons.Clear();
    }

    // ============================================================
    // Helpers: axis + basis
    // ============================================================

    private Vector3 GetRotationAxisLocalSigned()
    {
        Vector3 axis = rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward, // Z
        };

        return axis;
    }

    private void GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir)
    {
        // For Z-axis rotation (top-down), we use up/right for ring placement (XY plane).
        switch (rotationAxis)
        {
            case RotationAxis.X:
                baseDir = Vector3.up;
                orthoDir = Vector3.forward;
                break;

            case RotationAxis.Y:
                baseDir = Vector3.forward;
                orthoDir = Vector3.right;
                break;

            default: // Z
                baseDir = Vector3.up;
                orthoDir = Vector3.right;
                break;
        }
    }

    private static int Wrap(int i, int n)
    {
        if (n <= 0) return 0;
        i %= n;
        if (i < 0) i += n;
        return i;
    }

    private float NowConfirmGate()
    {
        return confirmLockoutUsesUnscaledTime ? Time.unscaledTime : Time.time;
    }
}
