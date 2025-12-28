using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelCarouselController : MonoBehaviour
{
    public enum RotationAxis { X, Y, Z }

    [Header("Content")]
    [SerializeField] private LevelCatalog catalog; // assign a LevelCatalog asset here
    [SerializeField] private Transform carouselRoot;

    [Header("Layout")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;
    [SerializeField] private float radius = 6f;
    [SerializeField] private float axisOffset = 0f;
    [SerializeField] private float angleOffsetDegrees = 0f;

    [Header("Front Alignment")]
    [SerializeField] private Transform frontReference;
    [SerializeField] private Transform cameraTransform;

    [Header("Rotation")]
    [SerializeField] private float rotateSeconds = 0.35f;
    [SerializeField] private AnimationCurve rotateEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Flip this to reverse the visual spin direction WITHOUT changing stage order.")]
    [SerializeField] private int rotationDirectionSign = +1; // +/- 1

    [Header("Selection")]
    [SerializeField] private int startIndex = 0;

    [Header("Optional legacy CW/CCW stepping (not used by StepNext/Prev)")]
    [SerializeField] private int clockwiseIndexDirection = -1;

    public event Action<LevelDefinition, int> OnSelectionChanged;

    public int SelectedIndex => _selectedIndex;

    public LevelDefinition SelectedLevel
    {
        get
        {
            if (catalog == null) return null;
            if (_selectedIndex < 0 || _selectedIndex >= catalog.Count) return null;
            return catalog.Levels[_selectedIndex];
        }
    }

    private readonly List<GameObject> _spawned = new(); // index-aligned with catalog.Levels
    private int _selectedIndex;
    private bool _hasConfirmed;
    private bool _isRotating;
    private float _stepAngle;

    private Quaternion _baseRootRotation;
    private float _currentRootAngleDeg;
    private Coroutine _rotateRoutine;

    private void Awake()
    {
        if (carouselRoot == null) carouselRoot = transform;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        rotationDirectionSign = rotationDirectionSign >= 0 ? +1 : -1;

        _baseRootRotation = carouselRoot.localRotation;

        int count = catalog != null ? catalog.Count : 0;
        _selectedIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, count - 1));

        Rebuild();

        _selectedIndex = CoerceToValidIndex(_selectedIndex, preferDir: +1);

        SnapToSelection();
        ApplySelectionVisuals(instant: true);
        FireSelectionChanged();
    }

    // ============================================================
    // Public API
    // ============================================================

    public void StepNextStage() => StepByIndex(+1);
    public void StepPrevStage() => StepByIndex(-1);

    // Optional older semantics
    public void StepClockwise() => StepByIndex(clockwiseIndexDirection);
    public void StepCounterClockwise() => StepByIndex(-clockwiseIndexDirection);

    public void ConfirmSelection()
    {
        if (_hasConfirmed) return;
        _hasConfirmed = true;

        var def = SelectedLevel;
        if (def == null) return;

        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SelectLevel(def);

        SceneFlow.GoToInstructions();
    }

    public void Rebuild()
    {
        ClearSpawned();

        int n = (catalog != null) ? catalog.Count : 0;
        _stepAngle = (n <= 0) ? 0f : 360f / n;

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, n - 1));
        if (n <= 0) return;

        for (int i = 0; i < n; i++)
        {
            var def = catalog.Levels[i];
            if (def == null || def.iconPrefab == null)
            {
                _spawned.Add(null);
                continue;
            }

            GameObject icon = Instantiate(def.iconPrefab, carouselRoot);
            icon.name = $"LevelIcon_{i:00}_{def.levelTitle}";
            _spawned.Add(icon);
        }

        LayoutIcons();
    }

    // ============================================================
    // Layout
    // ============================================================

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null) Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }

    private void LayoutIcons()
    {
        int n = _spawned.Count;
        if (n <= 0) return;

        Vector3 axis = GetAxisLocalUnsigned();
        GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir);

        for (int i = 0; i < n; i++)
        {
            if (_spawned[i] == null) continue;

            float ang = (angleOffsetDegrees + i * _stepAngle) * Mathf.Deg2Rad;
            Vector3 radial = (Mathf.Cos(ang) * baseDir + Mathf.Sin(ang) * orthoDir) * radius;
            _spawned[i].transform.localPosition = radial + axis * axisOffset;
        }
    }

    // ============================================================
    // Stepping
    // ============================================================

    private void StepByIndex(int dir)
    {
        if (_hasConfirmed) return;
        if (_isRotating) return;
        if (catalog == null || catalog.Count == 0) return;

        int n = catalog.Count;
        int cur = _selectedIndex;

        int next = FindNextSelectable(Wrap(cur + dir, n), dir);
        if (next == cur) return;

        int steps = CountSteps(cur, next, dir);
        if (steps <= 0) steps = 1;

        ApplySelectionIndex(next);

        float targetAngle = _currentRootAngleDeg + (-dir) * _stepAngle * steps;
        StartAngleRotation(targetAngle);
    }

    private void ApplySelectionIndex(int newIndex)
    {
        _selectedIndex = newIndex;
        ApplySelectionVisuals(instant: false);
        FireSelectionChanged();
    }

    private void StartAngleRotation(float targetAngleDeg)
    {
        if (_rotateRoutine != null) StopCoroutine(_rotateRoutine);
        _isRotating = true;
        _rotateRoutine = StartCoroutine(RotateAngleRoutine(targetAngleDeg));
    }

    private IEnumerator RotateAngleRoutine(float targetAngleDeg)
    {
        float a = _currentRootAngleDeg;
        float b = targetAngleDeg;

        if (rotateSeconds <= 0f)
        {
            _currentRootAngleDeg = b;
            ApplyRootAngleNow(_currentRootAngleDeg);
            _isRotating = false;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / rotateSeconds;

            float e = (rotateEase != null) ? rotateEase.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0f, 1f, t);

            float ang = Mathf.Lerp(a, b, e);
            ApplyRootAngleNow(ang);
            yield return null;
        }

        _currentRootAngleDeg = b;
        ApplyRootAngleNow(_currentRootAngleDeg);

        _isRotating = false;
    }

    private void ApplyRootAngleNow(float angleDeg)
    {
        Vector3 axisSigned = GetRotationAxisLocalSigned();
        carouselRoot.localRotation = _baseRootRotation * Quaternion.AngleAxis(angleDeg, axisSigned);
    }

    // ============================================================
    // Snap
    // ============================================================

    private void SnapToSelection()
    {
        _currentRootAngleDeg = ComputeRootAngleForIndex(_selectedIndex);
        ApplyRootAngleNow(_currentRootAngleDeg);
    }

    private float ComputeRootAngleForIndex(int index)
    {
        Vector3 axisSigned = GetRotationAxisLocalSigned();
        GetPlaneBasisLocal(out Vector3 baseDirLocal, out _);

        Vector3 desiredDirParent = GetDesiredFrontDirInParentSpace();
        if (desiredDirParent.sqrMagnitude < 0.0001f)
            desiredDirParent = GetDefaultFrontInParentSpace();

        Vector3 desiredDirLocal = Quaternion.Inverse(_baseRootRotation) * desiredDirParent;
        desiredDirLocal = Vector3.ProjectOnPlane(desiredDirLocal, axisSigned);
        if (desiredDirLocal.sqrMagnitude < 0.0001f)
            desiredDirLocal = baseDirLocal;

        desiredDirLocal.Normalize();

        float iconAngle = angleOffsetDegrees + index * _stepAngle;
        float targetAngle = Vector3.SignedAngle(baseDirLocal, desiredDirLocal, axisSigned);

        return targetAngle - iconAngle;
    }

    private Vector3 GetDesiredFrontDirInParentSpace()
    {
        Vector3 axisWorld = GetAxisWorldUnsigned();

        if (frontReference != null)
        {
            Vector3 dWorld = frontReference.position - carouselRoot.position;
            dWorld = Vector3.ProjectOnPlane(dWorld, axisWorld);
            if (dWorld.sqrMagnitude < 0.0001f) return Vector3.zero;
            return WorldDirToParentLocal(dWorld.normalized);
        }

        if (cameraTransform != null)
        {
            Vector3 dWorld = -cameraTransform.up; // down the screen
            dWorld = Vector3.ProjectOnPlane(dWorld, axisWorld);
            if (dWorld.sqrMagnitude < 0.0001f) return Vector3.zero;
            return WorldDirToParentLocal(dWorld.normalized);
        }

        return Vector3.zero;
    }

    private Vector3 GetDefaultFrontInParentSpace()
    {
        GetPlaneBasisLocal(out Vector3 baseDirLocal, out _);
        Vector3 baseDirParent = _baseRootRotation * baseDirLocal;
        return baseDirParent.normalized;
    }

    private Vector3 WorldDirToParentLocal(Vector3 worldDir)
    {
        Transform parent = carouselRoot.parent;
        return (parent == null) ? worldDir : parent.InverseTransformDirection(worldDir);
    }

    // ============================================================
    // Selection visuals
    // ============================================================

    private void ApplySelectionVisuals(bool instant)
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] == null) continue;
            var icon = _spawned[i].GetComponentInChildren<LevelIcon>();
            if (icon != null) icon.ApplySelected(i == _selectedIndex, instant);
        }
    }

    private void FireSelectionChanged()
    {
        OnSelectionChanged?.Invoke(SelectedLevel, _selectedIndex);
    }

    // ============================================================
    // Selectability
    // ============================================================

    private bool IsSelectable(int index)
    {
        if (catalog == null) return false;
        if (index < 0 || index >= catalog.Count) return false;

        var def = catalog.Levels[index];
        if (def == null) return false;
        if (string.IsNullOrEmpty(def.SceneName)) return false;
        if (def.iconPrefab == null) return false;

        return true;
    }

    private int CoerceToValidIndex(int index, int preferDir)
    {
        if (catalog == null || catalog.Count == 0) return 0;

        index = Wrap(index, catalog.Count);
        if (IsSelectable(index)) return index;

        int a = FindNextSelectable(index, preferDir);
        if (IsSelectable(a)) return a;

        int b = FindNextSelectable(index, -preferDir);
        if (IsSelectable(b)) return b;

        return _selectedIndex;
    }

    private int FindNextSelectable(int start, int dir)
    {
        if (catalog == null || catalog.Count == 0) return 0;

        int n = catalog.Count;
        int idx = Wrap(start, n);

        for (int i = 0; i < n; i++)
        {
            if (IsSelectable(idx)) return idx;
            idx = Wrap(idx + dir, n);
        }

        return _selectedIndex;
    }

    private int CountSteps(int from, int to, int dir)
    {
        if (catalog == null || catalog.Count == 0) return 0;
        if (from == to) return 0;

        int n = catalog.Count;
        int steps = 0;
        int idx = from;

        while (idx != to && steps <= n)
        {
            idx = Wrap(idx + dir, n);
            steps++;
        }

        return steps;
    }

    private static int Wrap(int v, int n)
    {
        if (n <= 0) return 0;
        v %= n;
        if (v < 0) v += n;
        return v;
    }

    // ============================================================
    // Axis / basis
    // ============================================================

    private Vector3 GetAxisLocalUnsigned()
    {
        return rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward // Z
        };
    }

    private Vector3 GetRotationAxisLocalSigned()
    {
        return GetAxisLocalUnsigned() * rotationDirectionSign;
    }

    private Vector3 GetAxisWorldUnsigned()
    {
        Vector3 axisLocal = GetAxisLocalUnsigned();
        Vector3 axisParent = _baseRootRotation * axisLocal;

        Transform parent = carouselRoot.parent;
        if (parent == null) return axisParent.normalized;

        return parent.TransformDirection(axisParent).normalized;
    }

    private void GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir)
    {
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
}
