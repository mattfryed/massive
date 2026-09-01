using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class SymmetryKnotVisual : MonoBehaviour
{
    [Header("Shapes References")]
    [SerializeField] private MonoBehaviour controlRingDashed;
    [SerializeField] private MonoBehaviour[] coreRings;

    [Header("Slice Prefab")]
    [SerializeField] private GameObject slicePrefab;
    [SerializeField] private Transform sourceSlicesParent;

    [Header("Orientation")]
    [SerializeField] private bool forceRingsToXZ = true;
    [SerializeField] private Vector3 ringXZRotationEuler = new Vector3(-90f, 0f, 0f);

    [Header("General")]
    [SerializeField] private float yLiftAboveGrid = 0.03f;
    [SerializeField] private float dashScrollSpeed = 0.8f;

    // ============================================================
    // Motion / Tweening
    // ============================================================

    [Header("Lock Tween")]
    [Tooltip("Seconds to ease the knot from forming -> locked (and back).")]
    [SerializeField] private float lockTweenSeconds = 0.35f;

    [Tooltip("Delay after fully unlocked before removing the end knot GameObject.")]
    [SerializeField] private float endKnotRemoveDelay = 0.20f;

    [Header("Bounce / Spring")]
    [Tooltip("Bounciness when aiming changes. Higher = more overshoot.")]
    [SerializeField] private float aimSpringStiffness = 18f;

    [Tooltip("Damping for aim spring. Lower = more oscillation.")]
    [SerializeField] private float aimSpringDamping = 8f;

    [Tooltip("Bounciness for lock amount (0..1). Higher = more overshoot.")]
    [SerializeField] private float lockSpringStiffness = 20f;

    [Tooltip("Damping for lock spring.")]
    [SerializeField] private float lockSpringDamping = 10f;

    // ============================================================
    // Geometry
    // ============================================================

    [Header("Source Cap")]
    [Range(6, 32)] [SerializeField] private int sourceSliceCount = 16;
    [SerializeField] private float sourceCapRise = 1.4f;
    [SerializeField] private float sourceCapLen = 2.2f;

    [Header("End Cap")]
    [Range(6, 32)] [SerializeField] private int endSliceCount = 14;
    [SerializeField] private float endEmitLen = 1.1f;
    [SerializeField] private float endCurvePull = 1.6f;

    [Header("Meeting / Ghost")]
    [Tooltip("Small separation between the source cap tip and end cap tip, to keep a 'ghosted middle'.")]
    [SerializeField] private float meetGap = 0.25f;

    // ============================================================
    // Wiggle / Breathing
    // ============================================================

    [Header("Wiggle")]
    [Tooltip("Side-to-side wiggle amplitude in world units.")]
    [SerializeField] private float wiggleAmplitude = 0.08f;

    [Tooltip("How many wiggles along the length (higher = more ripples).")]
    [SerializeField] private float wiggleFrequency = 1.4f;

    [Tooltip("How fast the wiggle travels.")]
    [SerializeField] private float wiggleSpeed = 1.2f;

    [Tooltip("0 = no wiggle at ends, 1 = full wiggle at ends. Usually keep near 0.")]
    [SerializeField, Range(0f, 1f)] private float wiggleEndInfluence = 0.15f;

    [Header("Breathing")]
    [Tooltip("Radius/Thickness breathing amplitude (fraction).")]
    [SerializeField] private float breatheAmplitude = 0.06f;

    [Tooltip("Breathing speed (Hz-ish).")]
    [SerializeField] private float breatheSpeed = 1.1f;

    // ============================================================
    // Slice Shrink / Ghost
    // ============================================================

    [Header("Slice Shrink / Ghost")]
    [SerializeField] private float sliceRadiusStart = 0.55f;
    [SerializeField] private float sliceRadiusEnd = 0.18f;
    [SerializeField] private float sliceThicknessStart = 0.08f;
    [SerializeField] private float sliceThicknessEnd = 0.02f;
    [SerializeField] private float sliceAlphaStart = 1.0f;
    [SerializeField] private float sliceAlphaEnd = 0.15f;

    [Header("Colors")]
    [SerializeField] private Color unclaimedColor = Color.white;
    [SerializeField] private Color claimedColor = Color.white;
    [SerializeField] private Color contestedColor = Color.white;

    [Header("End Knot Orientation Rule")]
    [SerializeField] private bool endEmitByWorldXSide = true;

    [Header("Despawn")]
    [SerializeField] private float fadeSpeed = 4f;

    [Header("Entry Despawn Collapse")]
[Tooltip("How much the collapse is staggered across the entry slices (0 = all at once).")]
[SerializeField] private float entryDespawnStagger = 0.35f;

[Tooltip("How long (in normalized fade progress) each slice takes to collapse once it starts.")]
[SerializeField] private float entryDespawnSliceWindow = 0.30f;

[Tooltip("Max rotation wiggle during collapse (degrees).")]
[SerializeField] private float entryDespawnWiggleDegrees = 18f;

[SerializeField] private float entryDespawnWiggleSpeed = 3.0f;

[Tooltip("If true, also collapses the dashed control ring + core rings.")]
[SerializeField] private bool collapseEntryRingsToo = true;

[SerializeField] private float entryRingWiggleDegrees = 8f;
[SerializeField] private float entryRingWiggleSpeed = 2.2f;

[Header("Spawn Fade")]
[SerializeField] private float spawnFadeSeconds = 0.25f;


// cached base rotation for control/core rings (XZ alignment)
private Quaternion _ringsBaseRot = Quaternion.identity;



    // ============================================================
    // Runtime state
    // ============================================================

    private struct Slice
    {
        public Transform tr;
        public MonoBehaviour ring;
    }

    private readonly List<Slice> sourceSlices = new();
    private readonly List<Slice> endSlices = new();

    private Transform endRoot;
    private Transform lastGoalMouth; // persists to allow retract-out tween

    private Vector3 sourcePos;
    private float captureRadius;

    private int ownerTeamID = -1;
    private Transform ownerGoalMouth;
    private bool contested;

    private float fade = 1f;
    private bool despawning;

    // Springy lock/aim
    private float lockValue;          // animated lock 0..1
    private float lockVel;

    private Vector3 aimDirXZ = Vector3.forward; // spring state (normalized XZ)
    private Vector3 aimVelXZ;

    private float endRemoveTimer;
    public float SpawnFadeSeconds => spawnFadeSeconds;

    private void ForceHideAllNow()
    {
        Color cc = unclaimedColor;
        cc.a = 0f;

        if (controlRingDashed != null)
            ShapesAdapter.TrySetColor(controlRingDashed, cc);

        if (coreRings != null)
        {
            for (int i = 0; i < coreRings.Length; i++)
                if (coreRings[i] != null)
                    ShapesAdapter.TrySetColor(coreRings[i], cc);
        }

        for (int i = 0; i < sourceSlices.Count; i++)
            if (sourceSlices[i].ring != null)
                ShapesAdapter.TrySetColor(sourceSlices[i].ring, cc);

        for (int i = 0; i < endSlices.Count; i++)
            if (endSlices[i].ring != null)
                ShapesAdapter.TrySetColor(endSlices[i].ring, cc);
    }

    public void Initialize(Vector3 sourceWorldPos, float captureRadiusWorld)
    {
        sourcePos = sourceWorldPos;
        captureRadius = captureRadiusWorld;

        transform.position = sourcePos;

        if (forceRingsToXZ)
            ForceRingsToXZPlane();

        EnsureSlices(sourceSlicesParent, sourceSliceCount, sourceSlices);

        if (controlRingDashed != null)
            ShapesAdapter.TrySetRadius(controlRingDashed, captureRadius);

        if (coreRings != null && coreRings.Length > 0)
        {
            for (int i = 0; i < coreRings.Length; i++)
            {
                float t = (coreRings.Length == 1) ? 1f : (i / (float)(coreRings.Length - 1));
                float r = Mathf.Lerp(captureRadius * 0.65f, captureRadius * 0.2f, t);
                ShapesAdapter.TrySetRadius(coreRings[i], r);
            }
        }

        fade = 0f;
        despawning = false;

        lockValue = 0f;
        lockVel = 0f;

        aimDirXZ = Vector3.forward;
        aimVelXZ = Vector3.zero;

        lastGoalMouth = null;
        endRemoveTimer = 0f;
        ForceHideAllNow();
    }

    public void BeginDespawn(float seconds)
    {
        despawning = true;
        fadeSpeed = (seconds <= 0.001f) ? 999f : (1f / seconds);
    }


    public void SetOwner(int teamID, Transform goalMouth, bool contested, float hold01)
    {
        ownerTeamID = teamID;
        ownerGoalMouth = goalMouth;
        this.contested = contested;

        // Persist last goal mouth so the end knot can retract instead of popping.
        if (goalMouth != null)
            lastGoalMouth = goalMouth;

        // Ensure end knot exists if we have a last mouth (even if unclaimed right now)
        if (lastGoalMouth != null && endRoot == null)
            EnsureEndKnot(lastGoalMouth);

        // If there is truly no known goal mouth (shouldn't happen), kill end knot.
        if (lastGoalMouth == null)
            DestroyEndKnot();
    }

    private void Update()
    {


// Update fade ONCE (spawn-in vs despawn-out)
if (despawning)
{
    fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime * fadeSpeed);
}
else
{
    float k = (spawnFadeSeconds <= 0.001f) ? 999f : (1f / spawnFadeSeconds);
    fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime * k);
}

float despawn01 = despawning ? Mathf.Clamp01(1f - fade) : 0f;


// Optional: collapse the outer dashed ring + inner rings on despawn
if (collapseEntryRingsToo && despawning)
{
    float rMul = 1f - despawn01; // shrink radius to 0
    float wig = Mathf.Sin(Time.time * entryRingWiggleSpeed * 6.2831853f) * entryRingWiggleDegrees * despawn01;
    Quaternion wigRot = _ringsBaseRot * Quaternion.AngleAxis(wig, Vector3.up);

    if (controlRingDashed != null)
    {
        var t = ((Component)controlRingDashed).transform;
        t.localRotation = wigRot;
        ShapesAdapter.TrySetRadius(controlRingDashed, captureRadius * rMul);
        ShapesAdapter.TrySetThickness(controlRingDashed, Mathf.Lerp(sliceThicknessStart, 0f, despawn01));
    }

    if (coreRings != null)
    {
        for (int i = 0; i < coreRings.Length; i++)
        {
            if (coreRings[i] == null) continue;
            var tr = ((Component)coreRings[i]).transform;
            tr.localRotation = wigRot;

            // keep their relative spacing but shrink toward 0
            float t01 = (coreRings.Length == 1) ? 1f : i / (float)(coreRings.Length - 1);
            float r0 = Mathf.Lerp(captureRadius * 0.65f, captureRadius * 0.2f, t01);
            ShapesAdapter.TrySetRadius(coreRings[i], r0 * rMul);
            ShapesAdapter.TrySetThickness(coreRings[i], Mathf.Lerp(sliceThicknessStart * 0.85f, 0f, despawn01));
        }
    }
}


        // Dash scroll
        if (controlRingDashed != null)
            ShapesAdapter.TrySetDashOffset(controlRingDashed, Time.time * dashScrollSpeed);

        // Desired lock state
        bool wantsLock = (ownerTeamID != -1 && ownerGoalMouth != null && !contested);
        float lockTarget = wantsLock ? 1f : 0f;

        // Spring lockValue toward lockTarget (overshoot-capable)
        lockValue = SpringFloat(lockValue, ref lockVel, lockTarget, lockSpringStiffness, lockSpringDamping, Time.deltaTime);

        // Clamp for sanity (we still want overshoot a bit, but don't go crazy)
        lockValue = Mathf.Clamp(lockValue, -0.25f, 1.25f);

        // Aim direction: if we have any goal mouth (current or last), spring toward it
        Transform aimMouth = (ownerGoalMouth != null) ? ownerGoalMouth : lastGoalMouth;
        if (aimMouth != null)
        {
            Vector3 S = sourcePos; S.y = 0f;
            Vector3 G = aimMouth.position; G.y = 0f;

            Vector3 desired = (G - S);
            if (desired.sqrMagnitude < 0.0001f) desired = Vector3.forward;
            desired.y = 0f;
            desired.Normalize();

            aimDirXZ = SpringVector(aimDirXZ, ref aimVelXZ, desired, aimSpringStiffness, aimSpringDamping, Time.deltaTime);
            aimDirXZ.y = 0f;
            if (aimDirXZ.sqrMagnitude < 0.0001f) aimDirXZ = Vector3.forward;
            aimDirXZ.Normalize();
        }



        // Pick color
        Color c = unclaimedColor;
        if (contested) c = contestedColor;
        else if (ownerTeamID != -1) c = claimedColor;

        ApplyRingColor(controlRingDashed, c, fade);
        if (coreRings != null)
        {
            for (int i = 0; i < coreRings.Length; i++)
                ApplyRingColor(coreRings[i], c, fade);
        }

        UpdateSourceCap(c);
        UpdateEndCap(c);
    }

private void ForceRingsToXZPlane()
{
    _ringsBaseRot = Quaternion.Euler(ringXZRotationEuler);

    if (controlRingDashed != null)
        ((Component)controlRingDashed).transform.localRotation = _ringsBaseRot;

    if (coreRings != null)
    {
        for (int i = 0; i < coreRings.Length; i++)
        {
            if (coreRings[i] != null)
                ((Component)coreRings[i]).transform.localRotation = _ringsBaseRot;
        }
    }
}


    // ------------------------------------------------------------
    // Shared meeting point
    // ------------------------------------------------------------
    private void GetSharedMeeting(out Vector3 Sbase, out Vector3 Meet, out Vector3 dirXZ)
    {
        Sbase = sourcePos + Vector3.up * yLiftAboveGrid;

        dirXZ = aimDirXZ;
        if (dirXZ.sqrMagnitude < 0.0001f) dirXZ = Vector3.forward;
        dirXZ.y = 0f;
        dirXZ.Normalize();

        Vector3 topUnclaimed = Sbase + Vector3.up * sourceCapRise;
        Vector3 topClaimed = Sbase + Vector3.up * sourceCapRise + dirXZ * sourceCapLen;

        // lockValue may overshoot; clamp in [0..1] for geometry blend
        float t = Mathf.Clamp01(lockValue);
        Meet = Vector3.Lerp(topUnclaimed, topClaimed, t);
    }

    private void UpdateSourceCap(Color c)
    {
        if (sourceSlices.Count == 0) return;

        GetSharedMeeting(out Vector3 Sbase, out Vector3 Meet, out Vector3 dirXZ);

        // Curve from base -> meet (always exists, even unclaimed)
        Vector3 p0 = Sbase;
        Vector3 p1 = Sbase + Vector3.up * (sourceCapRise * 0.75f);
        Vector3 p2 = Sbase + Vector3.up * (sourceCapRise * 1.00f) + dirXZ * (sourceCapLen * 0.65f * Mathf.Clamp01(lockValue));
        Vector3 p3 = Meet;

        for (int i = 0; i < sourceSlices.Count; i++)
        {
            float t = (sourceSlices.Count == 1) ? 1f : i / (float)(sourceSlices.Count - 1);

            Vector3 pos = SampleWithWiggle(p0, p1, p2, p3, dirXZ, t, Time.time, 0.0f);
            Vector3 pos2 = SampleWithWiggle(p0, p1, p2, p3, dirXZ, Mathf.Min(1f, t + 0.02f), Time.time, 0.0f);
            Vector3 tan = (pos2 - pos);
            if (tan.sqrMagnitude < 0.000001f) tan = Vector3.up;

            float breath = 1f + breatheAmplitude * Mathf.Sin(Time.time * breatheSpeed * 6.2831853f + t * 2.3f);

            float despawn01 = despawning ? Mathf.Clamp01(1f - fade) : 0f;

            // Per-slice stagger: tip collapses first, base collapses last.
            // t=1 (tip) -> start=0, t=0 (base) -> start=entryDespawnStagger
            float start = (1f - t) * entryDespawnStagger;
            float u = (entryDespawnSliceWindow <= 0.0001f) ? 1f : Mathf.Clamp01((despawn01 - start) / entryDespawnSliceWindow);

            // collapseMul: 1 -> alive, 0 -> collapsed
            float collapseMul = despawning ? (1f - Smooth01(u)) : 1f;

            // rotation wiggle grows as it collapses
            float wigAmp = entryDespawnWiggleDegrees * despawn01;
            float wig = Mathf.Sin((Time.time * entryDespawnWiggleSpeed + t * 0.7f) * 6.2831853f) * wigAmp;

            ApplySlice(
                sourceSlices[i],
                pos,
                tan,
                t,
                c,
                extraAlphaMul: 1f * collapseMul,
                radiusMul: breath * collapseMul,
                thicknessMul: breath * collapseMul,
                extraRollDegrees: wig
            );

        }
    }

    private void UpdateEndCap(Color c)
    {
        if (endRoot == null || endSlices.Count == 0 || lastGoalMouth == null)
            return;

        GetSharedMeeting(out _, out Vector3 Meet, out Vector3 dirXZ);

        Vector3 meetFromEnd = Meet + dirXZ * (meetGap * 0.5f);

        Vector3 E = lastGoalMouth.position;

        // Emit direction rule on ZY plane (±X)
        Vector3 exitDir = Vector3.right;
        if (endEmitByWorldXSide)
            exitDir = (E.x < 0f) ? Vector3.right : Vector3.left;

        // Retraction:
        // As lockValue goes to 0, collapse q3 back toward the mouth so the end knot "shrinks back".
        float lock01 = Mathf.Clamp01(lockValue);
        Vector3 q3 = Vector3.Lerp(E, meetFromEnd, lock01);

        Vector3 q0 = E;
        Vector3 q1 = E + exitDir * endEmitLen;

        Vector3 toQ3 = (q3 - q1);
        if (toQ3.sqrMagnitude < 0.0001f) toQ3 = exitDir;
        toQ3.Normalize();

        Vector3 q2 = q1 + toQ3 * endCurvePull;

        // As we retract, also shrink radius/thickness toward 0
        float retractMul = lock01;

        for (int i = 0; i < endSlices.Count; i++)
        {
            float t = (endSlices.Count == 1) ? 1f : i / (float)(endSlices.Count - 1);

            Vector3 pos = SampleWithWiggle(q0, q1, q2, q3, dirXZ, t, Time.time, 1.7f);
            Vector3 pos2 = SampleWithWiggle(q0, q1, q2, q3, dirXZ, Mathf.Min(1f, t + 0.02f), Time.time, 1.7f);
            Vector3 tan = (pos2 - pos);
            if (tan.sqrMagnitude < 0.000001f) tan = exitDir;

            float breath = 1f + breatheAmplitude * Mathf.Sin(Time.time * breatheSpeed * 6.2831853f + 1.1f + t * 2.8f);

            // End cap alpha and geometry both shrink with retractMul.
            ApplySlice(endSlices[i], pos, tan, t, c,
                extraAlphaMul: retractMul,
                radiusMul: breath * Mathf.Lerp(0.15f, 1f, retractMul),
                thicknessMul: breath * retractMul,
                extraRollDegrees: 0f
            );

        }
    }

    // Wiggle is applied perpendicular to dirXZ, strongest near middle by default.
    private Vector3 SampleWithWiggle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 dirXZ, float t, float time, float phaseOffset)
    {
        Vector3 p = Bezier3(a, b, c, d, t);

        Vector3 perp = Vector3.Cross(Vector3.up, dirXZ);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
        perp.Normalize();

        // Envelope: mostly mid-curve, not ends
        float mid = 1f - Mathf.Abs(2f * t - 1f); // 0 at ends, 1 at middle
        float env = Mathf.Lerp(mid, 1f, wiggleEndInfluence);

        float w = Mathf.Sin((time * wiggleSpeed + phaseOffset) * 6.2831853f + t * wiggleFrequency * 6.2831853f);
        p += perp * (wiggleAmplitude * env * w);

        return p;
    }

    private void ApplySlice(
        Slice s,
        Vector3 pos,
        Vector3 tangent,
        float t01,
        Color baseColor,
        float extraAlphaMul,
        float radiusMul,
        float thicknessMul,
        float extraRollDegrees
    )
    {
        if (s.tr == null) return;

        s.tr.position = pos;

        Vector3 n = tangent.sqrMagnitude < 0.0001f ? Vector3.up : tangent.normalized;

        // Base orientation: ring normal = tangent
        Quaternion q = Quaternion.LookRotation(n, Vector3.up);

        // Add a roll wiggle around the tangent axis (makes “untying” feel alive)
        if (Mathf.Abs(extraRollDegrees) > 0.0001f)
            q = q * Quaternion.AngleAxis(extraRollDegrees, Vector3.forward); // roll in ring space

        s.tr.rotation = q;

        float r = Mathf.Lerp(sliceRadiusStart, sliceRadiusEnd, t01) * radiusMul;
        float th = Mathf.Lerp(sliceThicknessStart, sliceThicknessEnd, t01) * thicknessMul;

        float a = Mathf.Lerp(sliceAlphaStart, sliceAlphaEnd, t01) * fade * extraAlphaMul;

        ShapesAdapter.TrySetRadius(s.ring, r);
        ShapesAdapter.TrySetThickness(s.ring, th);

        Color cc = baseColor;
        cc.a *= a;
        ShapesAdapter.TrySetColor(s.ring, cc);
    }


    private void ApplyRingColor(MonoBehaviour ring, Color c, float alphaMul)
    {
        if (ring == null) return;
        Color cc = c;
        cc.a *= alphaMul;
        ShapesAdapter.TrySetColor(ring, cc);
    }

    private void EnsureEndKnot(Transform goalMouth)
    {
        if (endRoot == null)
        {
            var go = new GameObject("EndKnotRoot");
            endRoot = go.transform;
            endRoot.SetParent(transform, worldPositionStays: true);
            EnsureSlices(endRoot, endSliceCount, endSlices);
        }

        endRoot.position = goalMouth.position;
    }

    private void DestroyEndKnot()
    {
        if (endRoot != null)
        {
            Destroy(endRoot.gameObject);
            endRoot = null;
        }
        endSlices.Clear();
    }

    private void EnsureSlices(Transform parent, int count, List<Slice> list)
    {
        if (slicePrefab == null || parent == null) return;
        if (list.Count == count) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].tr != null)
                Destroy(list[i].tr.gameObject);
        }
        list.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(slicePrefab, parent);
            go.name = $"Slice_{i:00}";
            var ring = ShapesAdapter.FindFirstRingLikeComponent(go);

            // Immediately hide to prevent 1-frame pop-in before Update() applies fade.
            if (ring != null)
            {
                Color cc = unclaimedColor;
                cc.a = 0f;
                ShapesAdapter.TrySetColor(ring, cc);
            }

            list.Add(new Slice { tr = go.transform, ring = ring });

        }
    }

    // ------------------------------------------------------------
    // Bezier helpers
    // ------------------------------------------------------------
    private static Vector3 Bezier3(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return (u * u * u) * a +
               (3f * u * u * t) * b +
               (3f * u * t * t) * c +
               (t * t * t) * d;
    }

    private static float Smooth01(float x)
{
    // classic smoothstep(0,1,x)
    x = Mathf.Clamp01(x);
    return x * x * (3f - 2f * x);
}


    // ------------------------------------------------------------
    // Simple spring helpers (underdamped capable)
    // ------------------------------------------------------------
    private static float SpringFloat(float value, ref float vel, float target, float stiffness, float damping, float dt)
    {
        // semi-implicit Euler spring
        float force = (target - value) * stiffness;
        vel += force * dt;
        vel *= Mathf.Exp(-damping * dt);
        value += vel * dt;
        return value;
    }

    private static Vector3 SpringVector(Vector3 value, ref Vector3 vel, Vector3 target, float stiffness, float damping, float dt)
    {
        Vector3 force = (target - value) * stiffness;
        vel += force * dt;
        vel *= Mathf.Exp(-damping * dt);
        value += vel * dt;
        return value;
    }

    // ------------------------------------------------------------
    // Shapes reflection adapter (no hard dependency)
    // ------------------------------------------------------------
    private static class ShapesAdapter
    {
        private struct Access
        {
            public Action<object, float> setRadius;
            public Action<object, float> setThickness;
            public Action<object, Color> setColor;
            public Action<object, float> setDashOffset;
        }

        private static readonly Dictionary<Type, Access> cache = new();

        public static MonoBehaviour FindFirstRingLikeComponent(GameObject go)
        {
            var mbs = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] == null) continue;
                var t = mbs[i].GetType();
                if (HasMember(t, "Radius") || HasMember(t, "radius"))
                    return mbs[i];
            }
            return (mbs.Length > 0) ? mbs[0] : null;
        }

        public static void TrySetRadius(MonoBehaviour mb, float radius)
        {
            if (!mb) return;
            var a = GetAccess(mb.GetType());
            a.setRadius?.Invoke(mb, radius);
        }

        public static void TrySetThickness(MonoBehaviour mb, float thickness)
        {
            if (!mb) return;
            var a = GetAccess(mb.GetType());
            a.setThickness?.Invoke(mb, thickness);
        }

        public static void TrySetColor(MonoBehaviour mb, Color color)
        {
            if (!mb) return;
            var a = GetAccess(mb.GetType());
            a.setColor?.Invoke(mb, color);
        }

        public static void TrySetDashOffset(MonoBehaviour mb, float dashOffset)
        {
            if (!mb) return;
            var a = GetAccess(mb.GetType());
            a.setDashOffset?.Invoke(mb, dashOffset);
        }

        private static Access GetAccess(Type t)
        {
            if (cache.TryGetValue(t, out var a))
                return a;

            a = new Access
            {
                setRadius = BuildFloatSetter(t, "Radius", "radius"),
                setThickness = BuildFloatSetter(t, "Thickness", "thickness", "LineThickness", "lineThickness"),
                setColor = BuildColorSetter(t, "Color", "color"),
                setDashOffset = BuildFloatSetter(t, "DashOffset", "dashOffset")
            };

            cache[t] = a;
            return a;
        }

        private static bool HasMember(Type t, string name)
        {
            return t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null ||
                   t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        private static Action<object, float> BuildFloatSetter(Type t, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];

                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(float) && p.CanWrite)
                    return (obj, v) => p.SetValue(obj, v);

                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(float))
                    return (obj, v) => f.SetValue(obj, v);
            }
            return null;
        }

        private static Action<object, Color> BuildColorSetter(Type t, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];

                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(Color) && p.CanWrite)
                    return (obj, v) => p.SetValue(obj, v);

                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(Color))
                    return (obj, v) => f.SetValue(obj, v);
            }
            return null;
        }
    }
}
