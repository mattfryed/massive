using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LevelCarouselController : MonoBehaviour
{
    public enum RotationAxis { X, Y, Z }

    [Header("Content")]
    [SerializeField] private List<LevelDefinition> levels = new();
    [SerializeField] private Transform carouselRoot;

    [Header("Layout")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;

    [Tooltip("Distance from center for each icon.")]
    [SerializeField] private float radius = 6f;

    [Tooltip("Offset along the rotation axis (ex: axis=Z => ring plane is at Z = axisOffset).")]
    [SerializeField] private float axisOffset = 0f;

    [Tooltip("Leave at 0 for dead-front. Use only for artistic layout tweaks.")]
    [SerializeField] private float angleOffsetDegrees = 0f;

    [Header("Front Alignment (selected icon goes here)")]
    [Tooltip("Optional explicit reference for what 'front' means. Uses direction center -> this transform, projected onto the ring plane.")]
    [SerializeField] private Transform frontReference;

    [Tooltip("Camera transform used to compute 'front' for top-down cameras (screen-down).")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Camera used for screen-space clockwise evaluation. If null, will try Camera.main.")]
    [SerializeField] private Camera screenCamera;

    [Header("Rotation")]
    [SerializeField] private float rotateSeconds = 0.35f;
    [SerializeField] private AnimationCurve rotateEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Icon Facing (optional)")]
    [Tooltip("If enabled, icons will be oriented consistently to match camera screen-down projected onto ring plane.")]
    [SerializeField] private bool iconsFaceCamera = false;

    [Header("Selection")]
    [SerializeField] private int startIndex = 0;

    [Header("Scene Flow (your current pattern)")]
    [SerializeField] private string instructionsSceneName = "S-0_INSTRUCTIONS";
    [SerializeField] private string dataManagerTag = "DataManager";

    public event Action<LevelDefinition, int> OnSelectionChanged;

    public int SelectedIndex => _selectedIndex;
    public LevelDefinition SelectedLevel =>
        (_selectedIndex >= 0 && _selectedIndex < levels.Count) ? levels[_selectedIndex] : null;

    private readonly List<GameObject> _spawned = new(); // index-aligned with levels (may contain null)
    private int _selectedIndex;
    private bool _isRotating;
    private bool _hasConfirmed;
    private float _stepAngle;

    // Allows you to rotate/aim the carouselRoot in the editor and still have selection math work.
    private Quaternion _baseRootRotation;

    private void Awake()
    {
        if (carouselRoot == null) carouselRoot = transform;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (screenCamera == null)
        {
            if (cameraTransform != null) screenCamera = cameraTransform.GetComponent<Camera>();
            if (screenCamera == null) screenCamera = Camera.main;
        }

        _baseRootRotation = carouselRoot.localRotation;
        _selectedIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, levels.Count - 1));

        Rebuild();

        // Ensure we start on a valid selectable level if any exist
        _selectedIndex = CoerceToValidIndex(_selectedIndex, preferDir: +1);

        ApplySelectionVisuals(instant: true);
        SnapToSelection();
        FireSelectionChanged();
    }

    private void LateUpdate()
    {
        if (!iconsFaceCamera || cameraTransform == null) return;

        Vector3 axisWorld = GetAxisWorld();
        Vector3 lookDirWorld = GetScreenDownDirWorldProjected(axisWorld);
        if (lookDirWorld.sqrMagnitude < 0.0001f) return;

        // Look "along" the planar screen-down direction; use axisWorld as up so it stays on the ring plane.
        Quaternion lookRot = Quaternion.LookRotation(lookDirWorld, axisWorld);

        for (int i = 0; i < _spawned.Count; i++)
        {
            var go = _spawned[i];
            if (go == null) continue;
            go.transform.rotation = lookRot;
        }
    }

    // ============================================================
    // Public API
    // ============================================================

    public void Rebuild()
    {
        ClearSpawned();

        int n = (levels != null) ? levels.Count : 0;
        _stepAngle = (n <= 0) ? 0f : 360f / n;
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, n - 1));

        if (n <= 0) return;

        // IMPORTANT: keep index alignment with levels by adding null placeholders.
        for (int i = 0; i < n; i++)
        {
            var def = levels[i];
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

    public void SelectNext() => SelectIndex(_selectedIndex + 1);
    public void SelectPrev() => SelectIndex(_selectedIndex - 1);

    /// <summary>
    /// Screen-space clockwise step (visual clockwise on screen), regardless of axis/orientation.
    /// Call this for RIGHT input.
    /// </summary>
    public void StepClockwise()
    {
        int d = GetIndexDeltaForScreenDirection(clockwise: true);
        SelectIndex(_selectedIndex + d);
    }

    /// <summary>
    /// Screen-space counterclockwise step (visual CCW on screen), regardless of axis/orientation.
    /// Call this for LEFT input.
    /// </summary>
    public void StepCounterClockwise()
    {
        int d = GetIndexDeltaForScreenDirection(clockwise: false);
        SelectIndex(_selectedIndex + d);
    }

    public void SelectIndex(int newIndex)
    {
        if (_hasConfirmed) return;
        if (_isRotating) return;
        if (levels == null || levels.Count == 0) return;

        int deltaHint = newIndex - _selectedIndex;
        int preferDir = (deltaHint == 0) ? +1 : (deltaHint > 0 ? +1 : -1);

        newIndex = Wrap(newIndex, levels.Count);
        newIndex = CoerceToValidIndex(newIndex, preferDir);

        if (newIndex == _selectedIndex) return;

        _selectedIndex = newIndex;

        ApplySelectionVisuals(instant: false);
        FireSelectionChanged();

        StopAllCoroutines();
        StartCoroutine(RotateToSelectionCoroutine());
    }

    public void ConfirmSelection()
    {
        if (_hasConfirmed) return;
        _hasConfirmed = true;

        var def = SelectedLevel;
        if (def == null) return;

        // Your existing handoff pattern: set DataManager.levelToLoad then go to instructions.
        GameObject dm = GameObject.FindWithTag(dataManagerTag);
        if (dm != null)
        {
            var data = dm.GetComponent<DataManagerScript>();
            if (data != null)
                data.levelToLoad = def.sceneName;
        }

        SceneManager.LoadScene(instructionsSceneName);
    }

    // ============================================================
    // Build / Layout
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

        Vector3 axis = GetAxisLocal();
        GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir); // both ⟂ axis

        for (int i = 0; i < n; i++)
        {
            if (_spawned[i] == null) continue;

            float ang = (angleOffsetDegrees + i * _stepAngle) * Mathf.Deg2Rad;

            Vector3 radial = (Mathf.Cos(ang) * baseDir + Mathf.Sin(ang) * orthoDir) * radius;
            Vector3 pos = radial + axis * axisOffset;

            _spawned[i].transform.localPosition = pos;
        }
    }

    // ============================================================
    // Visual selection hooks
    // ============================================================

    private void ApplySelectionVisuals(bool instant)
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] == null) continue;

            var icon = _spawned[i].GetComponentInChildren<LevelIcon>();
            if (icon != null)
                icon.ApplySelected(i == _selectedIndex, instant);
        }
    }

    private void FireSelectionChanged()
    {
        OnSelectionChanged?.Invoke(SelectedLevel, _selectedIndex);
    }

    // ============================================================
    // Rotation / Alignment
    // ============================================================

    private void SnapToSelection()
    {
        carouselRoot.localRotation = TargetRotationForIndex(_selectedIndex);
    }

    private IEnumerator RotateToSelectionCoroutine()
    {
        _isRotating = true;

        Quaternion a = carouselRoot.localRotation;
        Quaternion b = TargetRotationForIndex(_selectedIndex);

        float t = 0f;
        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += (rotateSeconds <= 0f) ? 1f : dt / rotateSeconds;

            float e = (rotateEase != null) ? rotateEase.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0f, 1f, t);
            carouselRoot.localRotation = Quaternion.Slerp(a, b, e);

            yield return null;
        }

        carouselRoot.localRotation = b;
        _isRotating = false;
    }

    private Quaternion TargetRotationForIndex(int index)
    {
        Vector3 axisLocal = GetAxisLocal();
        GetPlaneBasisLocal(out Vector3 baseDirLocal, out _);

        // Desired "front" direction in PARENT local space (projected onto ring plane).
        Vector3 desiredDirParent = GetDesiredFrontDirInParentSpace();
        if (desiredDirParent.sqrMagnitude < 0.0001f)
            desiredDirParent = GetDefaultFrontInParentSpace();

        // Convert desired direction into "base local space" (before dynamic rotation),
        // so SignedAngle math stays stable even if you rotated carouselRoot in the editor.
        Vector3 desiredDirLocal = Quaternion.Inverse(_baseRootRotation) * desiredDirParent;
        desiredDirLocal = Vector3.ProjectOnPlane(desiredDirLocal, axisLocal);
        if (desiredDirLocal.sqrMagnitude < 0.0001f)
            desiredDirLocal = baseDirLocal;
        desiredDirLocal.Normalize();

        float iconAngle = angleOffsetDegrees + index * _stepAngle;

        // Target angle (around axisLocal) to bring baseDirLocal to desiredDirLocal
        float targetAngle = Vector3.SignedAngle(baseDirLocal, desiredDirLocal, axisLocal);

        // Want: (rootAngle + iconAngle) == targetAngle  => rootAngle = targetAngle - iconAngle
        float rootAngle = targetAngle - iconAngle;

        return _baseRootRotation * Quaternion.AngleAxis(rootAngle, axisLocal);
    }

    private Vector3 GetDesiredFrontDirInParentSpace()
    {
        Vector3 axisWorld = GetAxisWorld();

        // Priority 1: explicit reference object
        if (frontReference != null)
        {
            Vector3 dWorld = frontReference.position - carouselRoot.position;
            dWorld = Vector3.ProjectOnPlane(dWorld, axisWorld);
            if (dWorld.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            return WorldDirToParentLocal(dWorld.normalized);
        }

        // Priority 2: camera screen-down projected onto ring plane (top-down friendly)
        if (cameraTransform != null)
        {
            Vector3 dWorld = GetScreenDownDirWorldProjected(axisWorld);
            if (dWorld.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            return WorldDirToParentLocal(dWorld.normalized);
        }

        return Vector3.zero;
    }

    private Vector3 GetScreenDownDirWorldProjected(Vector3 axisWorld)
    {
        if (cameraTransform == null) return Vector3.zero;

        // "Down the screen" is -camera.up (NOT camera.position-center, which collapses for top-down).
        Vector3 d = -cameraTransform.up;
        d = Vector3.ProjectOnPlane(d, axisWorld);

        if (d.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return d.normalized;
    }

    private Vector3 GetDefaultFrontInParentSpace()
    {
        // Fallback: use baseDir in parent space
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
    // Screen-space Clockwise mapping
    // ============================================================

    private int GetIndexDeltaForScreenDirection(bool clockwise)
    {
        // Fallback if we can't evaluate screen-space
        if (screenCamera == null || levels == null || levels.Count < 2) return clockwise ? +1 : -1;

        int n = levels.Count;
        int cur = Mathf.Clamp(_selectedIndex, 0, n - 1);

        // Find next selectable index forward (+1) and backward (-1)
        int plus = FindNextSelectable(Wrap(cur + 1, n), +1);
        int minus = FindNextSelectable(Wrap(cur - 1, n), -1);

        if (plus == cur && minus == cur) return clockwise ? +1 : -1; // nothing selectable

        // Need screen points for current and plus (preferred), otherwise use minus
        Transform tCur = GetIconTransform(cur);
        Transform tPlus = GetIconTransform(plus);

        if (tCur == null || tPlus == null)
        {
            // If plus isn't available, we can't infer orientation; just choose a stable fallback.
            return clockwise ? +1 : -1;
        }

        Vector3 c = screenCamera.WorldToScreenPoint(carouselRoot.position);
        Vector3 a = screenCamera.WorldToScreenPoint(tCur.position);
        Vector3 b = screenCamera.WorldToScreenPoint(tPlus.position);

        // If anything is behind the camera, bail to fallback
        if (a.z <= 0f || b.z <= 0f || c.z <= 0f) return clockwise ? +1 : -1;

        Vector2 va = new Vector2(a.x - c.x, a.y - c.y);
        Vector2 vb = new Vector2(b.x - c.x, b.y - c.y);

        if (va.sqrMagnitude < 0.0001f || vb.sqrMagnitude < 0.0001f) return clockwise ? +1 : -1;

        // Signed 2D cross product (z component). Negative => clockwise (screen coords y-up).
        float cross = va.x * vb.y - va.y * vb.x;
        bool plusIsClockwise = cross < 0f;

        // If (cur -> plus) is clockwise, then +1 (toward plus) is clockwise. Else -1 is clockwise.
        int clockwiseDelta = plusIsClockwise ? +1 : -1;
        return clockwise ? clockwiseDelta : -clockwiseDelta;
    }

    private Transform GetIconTransform(int index)
    {
        if (index < 0 || levels == null || index >= levels.Count) return null;
        if (index < 0 || index >= _spawned.Count) return null;

        GameObject go = _spawned[index];
        return (go != null) ? go.transform : null;
    }

    // ============================================================
    // Selectability / index safety
    // ============================================================

    private bool IsSelectable(int index)
    {
        if (levels == null) return false;
        if (index < 0 || index >= levels.Count) return false;

        var def = levels[index];
        if (def == null) return false;
        if (string.IsNullOrEmpty(def.sceneName)) return false;

        // Optional: require an iconPrefab to be selectable (usually true in your setup)
        if (def.iconPrefab == null) return false;

        return true;
    }

    private int CoerceToValidIndex(int index, int preferDir)
    {
        if (levels == null || levels.Count == 0) return 0;

        index = Wrap(index, levels.Count);
        if (IsSelectable(index)) return index;

        int a = FindNextSelectable(index, preferDir);
        if (IsSelectable(a)) return a;

        int b = FindNextSelectable(index, -preferDir);
        if (IsSelectable(b)) return b;

        // None selectable — stay put
        return _selectedIndex;
    }

    private int FindNextSelectable(int start, int dir)
    {
        if (levels == null || levels.Count == 0) return 0;

        int n = levels.Count;
        int idx = Wrap(start, n);

        for (int i = 0; i < n; i++)
        {
            if (IsSelectable(idx)) return idx;
            idx = Wrap(idx + dir, n);
        }

        return _selectedIndex;
    }

    // ============================================================
    // Axis / basis math
    // ============================================================

    private Vector3 GetAxisLocal()
    {
        switch (rotationAxis)
        {
            case RotationAxis.X: return Vector3.right;
            case RotationAxis.Y: return Vector3.up;
            default:             return Vector3.forward; // Z
        }
    }

    private Vector3 GetAxisWorld()
    {
        Vector3 axisLocal = GetAxisLocal();
        Vector3 axisParent = _baseRootRotation * axisLocal;

        Transform parent = carouselRoot.parent;
        if (parent == null) return axisParent.normalized;

        return parent.TransformDirection(axisParent).normalized;
    }

    private void GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir)
    {
        // baseDir is where angle=0 points. orthoDir is +90° direction in the ring plane.
        // Both are perpendicular to axis.
        switch (rotationAxis)
        {
            case RotationAxis.X:
                // Plane is YZ
                baseDir = Vector3.up;
                orthoDir = Vector3.forward;
                break;

            case RotationAxis.Y:
                // Plane is XZ
                baseDir = Vector3.forward;
                orthoDir = Vector3.right;
                break;

            default: // RotationAxis.Z
                // Plane is XY
                baseDir = Vector3.up;
                orthoDir = Vector3.right;
                break;
        }
    }

    private static int Wrap(int v, int n)
    {
        if (n <= 0) return 0;
        v %= n;
        if (v < 0) v += n;
        return v;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (carouselRoot == null) return;

        Vector3 axis = GetAxisLocal();
        GetPlaneBasisLocal(out Vector3 baseDir, out Vector3 orthoDir);

        Matrix4x4 m = carouselRoot.localToWorldMatrix;

        const int seg = 64;
        Vector3 prev = Vector3.zero;

        for (int i = 0; i <= seg; i++)
        {
            float t = (i / (float)seg) * Mathf.PI * 2f;
            Vector3 local = (Mathf.Cos(t) * baseDir + Mathf.Sin(t) * orthoDir) * radius + axis * axisOffset;
            Vector3 world = m.MultiplyPoint3x4(local);

            if (i > 0) Gizmos.DrawLine(prev, world);
            prev = world;
        }
    }
#endif
}