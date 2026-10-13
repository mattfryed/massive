using Massive.PowerUps;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PlayerVisualController
{
    private DecoherencePowerUpDefinition equippedDecoherence;
    private TimeDilationPowerUpDefinition equippedTimeDilation;
    private PlayerControllerScript equippedVisualOwner;
    private MaterialPropertyBlock equippedProps;
    private float decoPhaseTime = float.NegativeInfinity;
    private float timeMotion, perimeterPhase;
    private Vector3 lastEquippedPosition, timeTravelDirection;
    private bool hasEquippedPosition;
    private int echoHead, echoSamples;
    private readonly TimeEchoPose[] timeHistory = new TimeEchoPose[128];
    private static readonly int EquippedArcsId = Shader.PropertyToID("_EquippedArcs");

    private struct TimeEchoPose
    {
        public Vector3 position, scale;
        public Quaternion rotation;
        public float time, radius, outline, teardrop, stretch, noise, wobble, frontGain;
        public float hit, hitAngle, hitTime, contact, contactAngle;
        public Vector4 direction;
    }

    private bool EquippedVisualsAllowed => isActiveAndEnabled && visuals && equippedVisualOwner &&
        equippedVisualOwner.isActiveAndEnabled && !equippedVisualOwner.IsSpawning &&
        !equippedVisualOwner.IsInsideAmplifier && !equippedVisualOwner.IsSingularityTransitControlled;
    public bool DecoherenceContoursVisible => EquippedVisualsAllowed && equippedDecoherence && equippedDecoherence.showEquippedContours;
    public bool TimeDilationEchoesVisible => EquippedVisualsAllowed && equippedTimeDilation && equippedTimeDilation.showMotionEchoes;
    public bool TimeDilationMarksVisible => EquippedVisualsAllowed && equippedTimeDilation && equippedTimeDilation.showPerimeterMarks;
    public float TimeDilationMotion01 => timeMotion;
    public float DecoherencePhaseScale => equippedDecoherence ? 1f + equippedDecoherence.phaseSeparationBoost *
        (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - decoPhaseTime) / Mathf.Max(.05f, equippedDecoherence.phasePulseSeconds)))) : 1f;

    public void SetDecoherenceEquipped(DecoherencePowerUpDefinition definition)
    {
        equippedDecoherence = definition;
        equippedVisualOwner = GetComponentInParent<PlayerControllerScript>();
        decoPhaseTime = float.NegativeInfinity;
    }

    public void SetTimeDilationEquipped(TimeDilationPowerUpDefinition definition)
    {
        equippedTimeDilation = definition;
        equippedVisualOwner = GetComponentInParent<PlayerControllerScript>();
        ClearEquippedVisualHistory();
    }

    public void PlayDecoherencePhasePulse() { if (equippedDecoherence) decoPhaseTime = Time.time; }

    private void ClearEquippedVisualHistory()
    {
        echoHead = echoSamples = 0;
        hasEquippedPosition = false;
        timeMotion = 0f;
        decoPhaseTime = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        if (!equippedTimeDilation || !EquippedVisualsAllowed || !blobMat)
        {
            echoHead = echoSamples = 0; hasEquippedPosition = false; timeMotion = 0f;
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 position = visuals.position;
        Vector3 travel = hasEquippedPosition ? position - lastEquippedPosition : Vector3.zero;
        travel.y = 0f;
        float size = Mathf.Max(.01f, Massive.Player.PlayerScaleAdjuster.SizeOf(this));
        // Teleports/portal wraps must not leave a line of old silhouettes across the arena.
        float expectedStep = _rb ? _rb.linearVelocity.magnitude * dt : 0f;
        if (travel.magnitude > Mathf.Max(size * 3f, expectedStep * 3f + size))
        { ClearEquippedVisualHistory(); travel = Vector3.zero; }
        lastEquippedPosition = position; hasEquippedPosition = true;
        float target = Mathf.Clamp01(travel.magnitude / dt / (Mathf.Max(.1f, equippedTimeDilation.echoFullSpeed) * size));
        timeMotion = Mathf.Lerp(timeMotion, target, 1f - Mathf.Exp(-dt * (target > timeMotion ? 14f : 8f)));
        if (travel.sqrMagnitude > .000001f) timeTravelDirection = travel.normalized;
        perimeterPhase = Mathf.Repeat(perimeterPhase + dt / Mathf.Max(.2f, equippedTimeDilation.perimeterCycleSeconds), 1f);
        // Limit history to 100 Hz. Visible echoes are interpolated by age, not frame count.
        if (echoSamples == 0 || Time.time - timeHistory[echoHead].time >= .01f)
        {
            echoHead = echoSamples == 0 ? 0 : (echoHead + 1) % timeHistory.Length;
            timeHistory[echoHead] = CaptureTimePose();
            echoSamples = Mathf.Min(echoSamples + 1, timeHistory.Length);
        }
    }

    private TimeEchoPose CaptureTimePose() => new TimeEchoPose
    {
        position = visuals.position + RepulsorVisualOffsetWS + ThrustChargeVisualOffsetWS,
        rotation = visuals.rotation, scale = visuals.lossyScale * RepulsorVisualScale,
        time = Time.time, radius = baseRadius, outline = outlineHalf,
        teardrop = _teardropStrength, stretch = 1f - _teardropStrength * maxVerticalSquash,
        noise = _noise, wobble = idleWobble, frontGain = teardropK1,
        direction = blobMat.GetVector("_DeformDir"), hit = _hit, hitAngle = _hitAngle,
        hitTime = _hitTime, contact = _contactStrength, contactAngle = _contactAngle
    };

    private bool TryTimeEcho(int index, out TimeEchoPose pose, out float opacity)
    {
        pose = default; opacity = 0f;
        if (!TimeDilationEchoesVisible || index < 0 || index >= Mathf.Clamp(equippedTimeDilation.echoCount, 1, 3) || echoSamples < 2) return false;
        float age = (index + 1) * Mathf.Clamp(equippedTimeDilation.echoSpacingSeconds, .025f, .15f);
        float target = Time.time - age;
        var newer = timeHistory[echoHead];
        for (int i = 1; i < echoSamples; i++)
        {
            var older = timeHistory[(echoHead - i + timeHistory.Length) % timeHistory.Length];
            if (older.time <= target)
            {
                float t = Mathf.InverseLerp(older.time, newer.time, target);
                pose = older;
                pose.position = Vector3.Lerp(older.position, newer.position, t);
                pose.rotation = Quaternion.Slerp(older.rotation, newer.rotation, t);
                pose.scale = Vector3.Lerp(older.scale, newer.scale, t);
                pose.teardrop = Mathf.Lerp(older.teardrop, newer.teardrop, t);
                pose.stretch = Mathf.Lerp(older.stretch, newer.stretch, t);
                float separation = Vector3.Distance(pose.position, visuals.position);
                float radius = Mathf.Max(.001f, baseRadius * visuals.lossyScale.x);
                opacity = equippedTimeDilation.echoOpacity * Mathf.Pow(.58f, index) * timeMotion *
                    Mathf.SmoothStep(0f, 1f, separation / (radius * .5f));
                return opacity > .005f;
            }
            newer = older;
        }
        return false;
    }

    public bool TryGetTimeDilationEcho(int index, out Vector3 position, out Quaternion rotation, out float opacity)
    {
        bool visible = TryTimeEcho(index, out var pose, out opacity);
        position = pose.position; rotation = pose.rotation;
        return visible;
    }

    private void PrepareEquippedDraw(Matrix4x4 model, Matrix4x4 vp, float opacity, float widthScale)
    {
        equippedProps ??= new MaterialPropertyBlock();
        equippedProps.Clear();
        equippedProps.SetMatrix("_MVP", vp * model);
        equippedProps.SetColor(_OutlineColor, new Color(1f, 1f, 1f, Mathf.Clamp01(opacity)));
        equippedProps.SetFloat("_OutlineHalf", outlineHalf * Mathf.Max(.2f, widthScale));
        ApplySingularityProjection(equippedProps, model, vp);
    }

    private void DrawEquippedPowerUpVisuals(CommandBuffer buffer, Matrix4x4 vp)
    {
        if (!EquippedVisualsAllowed) return;
        if (TimeDilationEchoesVisible)
        {
            for (int i = 2; i >= 0; i--)
            {
                if (!TryTimeEcho(i, out var pose, out float opacity)) continue;
                PrepareEquippedDraw(Matrix4x4.TRS(pose.position, pose.rotation, pose.scale), vp, opacity, equippedTimeDilation.echoWidthScale);
                equippedProps.SetFloat("_Radius", pose.radius);
                equippedProps.SetFloat("_OutlineHalf", pose.outline * equippedTimeDilation.echoWidthScale);
                equippedProps.SetFloat("_DeformLerp", pose.teardrop);
                equippedProps.SetFloat("_Stretch", pose.stretch);
                equippedProps.SetFloat("_NoisePhase", pose.noise);
                equippedProps.SetFloat("_IdleWobble", pose.wobble);
                equippedProps.SetFloat("_TeardropK1", pose.frontGain);
                equippedProps.SetVector("_DeformDir", pose.direction);
                equippedProps.SetFloat("_HitImpulse", pose.hit);
                equippedProps.SetFloat("_HitAngle", pose.hitAngle);
                equippedProps.SetFloat("_HitTime", pose.hitTime);
                equippedProps.SetFloat("_ContactStrength", pose.contact);
                equippedProps.SetFloat("_ContactAngle", pose.contactAngle);
                equippedProps.SetFloat(DamageImpulseId, 0f);
                buffer.DrawProcedural(Matrix4x4.identity, blobMat, 1, MeshTopology.Triangles, 128 * 3, 1, equippedProps);
            }
        }
        if (DecoherenceContoursVisible)
        {
            float split = Mathf.Clamp01(_decoSplit01);
            float breath = .5f - .5f * Mathf.Cos(Time.time * Mathf.PI * 2f / Mathf.Max(.2f, equippedDecoherence.contourCycleSeconds));
            float radius = baseRadius * Mathf.Abs(visuals.lossyScale.x);
            float separation = Mathf.Lerp(radius * equippedDecoherence.contourSeparation * breath, .5f * DecoSplitSeparationWorld, split);
            Vector3 offset = DecoSideWS * separation;
            Vector3 origin = visuals.position + _decoPreJitterWS + RepulsorVisualOffsetWS + ThrustChargeVisualOffsetWS;
            Vector3 scale = visuals.lossyScale * RepulsorVisualScale * Mathf.Lerp(equippedDecoherence.contourScale, 1f, split);
            float opacity = equippedDecoherence.contourOpacity * (1f - split);
            if (opacity > .005f) for (int side = -1; side <= 1; side += 2)
            {
                PrepareEquippedDraw(Matrix4x4.TRS(origin + offset * side, visuals.rotation, scale), vp, opacity, equippedDecoherence.contourWidthScale);
                buffer.DrawProcedural(Matrix4x4.identity, blobMat, 1, MeshTopology.Triangles, 128 * 3, 1, equippedProps);
            }
        }
        if (TimeDilationMarksVisible && blobMat.passCount > 4)
        {
            var definition = equippedTimeDilation;
            Vector3 localTravel = visuals.InverseTransformDirection(timeTravelDirection);
            float rear = Mathf.Atan2(-localTravel.z, -localTravel.x) * Mathf.Rad2Deg;
            float a = Mathf.LerpAngle(perimeterPhase * 360f, rear - 35f, timeMotion);
            float b = Mathf.LerpAngle(perimeterPhase * 360f + 180f, rear + 35f, timeMotion);
            Vector3 position = visuals.position + RepulsorVisualOffsetWS + ThrustChargeVisualOffsetWS;
            PrepareEquippedDraw(Matrix4x4.TRS(position, visuals.rotation, visuals.lossyScale * RepulsorVisualScale * definition.perimeterScale),
                vp, definition.perimeterOpacity * Mathf.Lerp(1f, .4f, timeMotion), 1.2f);
            equippedProps.SetVector(EquippedArcsId, new Vector4(a * Mathf.Deg2Rad, b * Mathf.Deg2Rad,
                Mathf.Clamp(definition.perimeterArcDegrees, 5f, 80f) * Mathf.Deg2Rad * Mathf.Lerp(.5f, 1f, timeMotion), 0f));
            buffer.DrawProcedural(Matrix4x4.identity, blobMat, 4, MeshTopology.Triangles, 128 * 3, 1, equippedProps);
        }
    }
}
