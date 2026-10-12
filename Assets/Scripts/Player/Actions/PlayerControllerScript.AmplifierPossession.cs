using Massive.Multiplier;
using Massive.Singularity;
using UnityEngine;

public partial class PlayerControllerScript
{
    private enum AmplifierTransitPhase { None, Entering, Held, Exiting }
    private AmplifierTransitPhase amplifierPhase;
    private AmplifierCoreGameplay amplifierOwner;
    private bool amplifierKinematic, amplifierDetectCollisions, amplifierAwaitRelease;
    private int amplifierLife;
    private Renderer[] amplifierRenderers;
    private bool[] amplifierRendererHidden;
    private Behaviour[] amplifierVisuals;
    private bool[] amplifierVisualEnabled;
    private float amplifierElapsed, amplifierDuration, amplifierScale = 1f, amplifierStretch = 1f;
    private float amplifierExponent, amplifierMinimumScale, amplifierMaximumStretch, amplifierStun;
    private Vector3 amplifierStart, amplifierEnd, amplifierDirection, amplifierRecoil;
    private static readonly int AmplifierCenterId = Shader.PropertyToID("_AmplifierTransitCenter");
    private static readonly int AmplifierShapeId = Shader.PropertyToID("_AmplifierTransitShape");
    private static readonly int AmplifierSourceId = Shader.PropertyToID("_AmplifierSourceToWorld");
    private static readonly int AmplifierVPId = Shader.PropertyToID("_AmplifierViewProjection");
    public bool IsInsideAmplifier => amplifierPhase != AmplifierTransitPhase.None;
    public bool IsAmplifierTransitioning => amplifierPhase == AmplifierTransitPhase.Entering || amplifierPhase == AmplifierTransitPhase.Exiting;
    public float AmplifierEntryProgress => amplifierPhase == AmplifierTransitPhase.Entering ? Mathf.Clamp01(amplifierElapsed / amplifierDuration) : amplifierPhase == AmplifierTransitPhase.Held ? 1f : 0f;
    public bool CanEnterAmplifier => CanStartSingularityTransit && !IsInsideAmplifier &&
        !isStunned && !IsExternallyStunned && controlMode != PlayerControlMode.Disabled && rb && !rb.isKinematic;
    public bool CanRemainInsideAmplifier => isActiveAndEnabled && !temporarilyEliminated &&
        !_matchSpawning && !_matchInputLocked && !_worldGameplaySuppressed && LifeSequence == amplifierLife &&
        controlMode != PlayerControlMode.Disabled;

    internal bool EnterAmplifier(AmplifierCoreGameplay core)
    {
        if (!core || !CanEnterAmplifier) return false;
        amplifierOwner = core;
        amplifierLife = LifeSequence;
        amplifierKinematic = rb.isKinematic;
        amplifierDetectCollisions = rb.detectCollisions;
        amplifierStart = transform.position;
        amplifierEnd = core.transform.position;
        amplifierDirection = amplifierEnd - amplifierStart; amplifierDirection.y = 0f;
        amplifierDirection = amplifierDirection.sqrMagnitude > .0001f ? amplifierDirection.normalized : Vector3.right;
        attackController?.CancelAttack();
        shieldAbility?.ForceStopShield();
        LatticeMotor?.Release();
        movement = Vector3.zero;
        moveHorizontal = moveVertical = 0f;
        shieldOn = didPlayerTapActionThisFrame = false;
        rb.linearVelocity = rb.angularVelocity = Vector3.zero;
        rb.detectCollisions = false;
        rb.isKinematic = true;
        var nuggets = GetComponentInChildren<PlayerNuggetsGPU>(true);
        amplifierVisuals = new Behaviour[] { visualsController, nuggets };
        amplifierVisualEnabled = new bool[amplifierVisuals.Length];
        for (int i = 0; i < amplifierVisuals.Length; i++)
            if (amplifierVisuals[i]) amplifierVisualEnabled[i] = amplifierVisuals[i].enabled;
        amplifierRenderers = GetComponentsInChildren<Renderer>(true);
        amplifierRendererHidden = new bool[amplifierRenderers.Length];
        for (int i = 0; i < amplifierRenderers.Length; i++)
        { amplifierRendererHidden[i] = amplifierRenderers[i].forceRenderingOff; amplifierRenderers[i].forceRenderingOff = true; }
        ReadAmplifierAnimation(core);
        amplifierPhase = AmplifierTransitPhase.Entering;
        amplifierDuration = Mathf.Max(.05f, core.Possession.entrySeconds);
        amplifierElapsed = 0f; amplifierScale = amplifierStretch = 1f;
        if (!core.Possession.animatePlayerTransit) CompleteAmplifierEntry();
        return true;
    }

    private void ReadAmplifierAnimation(AmplifierCoreGameplay core)
    {
        amplifierExponent = core.Possession.transitExponent;
        amplifierMinimumScale = core.Possession.transitMinimumScale;
        amplifierMaximumStretch = core.Possession.transitMaximumStretch;
    }

    internal void CancelAmplifierTransit(AmplifierCoreGameplay core)
    {
        if (ReferenceEquals(amplifierOwner, core) && IsInsideAmplifier) CompleteAmplifierExit();
    }

    private void CompleteAmplifierEntry()
    {
        SetAmplifierPosition(amplifierEnd);
        amplifierPhase = AmplifierTransitPhase.Held;
        amplifierScale = amplifierStretch = 1f;
        SetAmplifierBodyVisible(false);
    }

    internal void ExitAmplifier(AmplifierCoreGameplay core, Vector3 position, Vector3 recoil, float stun, bool animate = true)
    {
        if (!ReferenceEquals(amplifierOwner, core) || !IsInsideAmplifier) return;
        amplifierEnd = position; amplifierRecoil = recoil; amplifierStun = stun;
        if (!animate || !core || !core.isActiveAndEnabled || !core.Possession.animatePlayerTransit || !CanRemainInsideAmplifier)
        { CompleteAmplifierExit(); return; }
        // Snapshot the departure point: the released Core immediately moves away.
        amplifierStart = transform.position;
        amplifierDirection = position - amplifierStart; amplifierDirection.y = 0f;
        amplifierDirection = amplifierDirection.sqrMagnitude > .0001f ? amplifierDirection.normalized : Vector3.right;
        ReadAmplifierAnimation(core);
        amplifierPhase = AmplifierTransitPhase.Exiting;
        amplifierElapsed = 0f; amplifierDuration = Mathf.Max(.05f, core.Possession.exitSeconds);
        amplifierScale = Mathf.Clamp(amplifierMinimumScale, .005f, .3f);
        amplifierStretch = Mathf.Clamp(amplifierMaximumStretch, 1f, 8f);
        SetAmplifierBodyVisible(true);
    }

    private void TickAmplifierTransit()
    {
        if (!IsInsideAmplifier) return;
        if (!CanRemainInsideAmplifier || !amplifierOwner || !amplifierOwner.isActiveAndEnabled)
        {
            if (amplifierOwner) amplifierOwner.ReleaseOccupant(this);
            if (IsInsideAmplifier) CompleteAmplifierExit();
            return;
        }
        if (amplifierPhase == AmplifierTransitPhase.Held) return;
        amplifierElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(amplifierElapsed / amplifierDuration);
        if (amplifierPhase == AmplifierTransitPhase.Entering)
        {
            amplifierEnd = amplifierOwner.transform.position;
            float radius = visualsController ? visualsController.baseRadius * Massive.Player.PlayerScaleAdjuster.SizeOf(this) : .5f;
            Vector3 pose = SingularityBlackHolePortal.EvaluateEntryPose(t, amplifierExponent,
                Vector3.Distance(amplifierStart, amplifierEnd), radius, amplifierMinimumScale, amplifierMaximumStretch);
            SetAmplifierPosition(amplifierEnd - amplifierDirection * pose.x);
            amplifierScale = pose.y; amplifierStretch = pose.z;
            if (t >= 1f) CompleteAmplifierEntry();
        }
        else
        {
            float travel = SingularityBlackHolePortal.ExitTravel(t, amplifierExponent, amplifierDuration,
                Vector3.Distance(amplifierStart, amplifierEnd), amplifierRecoil.magnitude);
            SetAmplifierPosition(Vector3.Lerp(amplifierStart, amplifierEnd, travel));
            float form = travel * travel * travel * (travel * (travel * 6f - 15f) + 10f);
            amplifierScale = Mathf.Lerp(amplifierMinimumScale, 1f, form);
            amplifierStretch = Mathf.Lerp(amplifierMaximumStretch, 1f, form);
            if (t >= 1f) CompleteAmplifierExit();
        }
    }

    private void SetAmplifierPosition(Vector3 point)
    { if (rb) rb.position = point; transform.position = point; }

    private void SetAmplifierBodyVisible(bool visible)
    {
        if (amplifierVisuals == null) return;
        for (int i = 0; i < amplifierVisuals.Length; i++)
            if (amplifierVisuals[i]) amplifierVisuals[i].enabled = visible && amplifierVisualEnabled[i];
    }

    private void CompleteAmplifierExit()
    {
        amplifierOwner = null;
        amplifierPhase = AmplifierTransitPhase.None;
        amplifierScale = amplifierStretch = 1f;
        amplifierAwaitRelease = true;
        bool sameLife = LifeSequence == amplifierLife && !temporarilyEliminated && !_matchSpawning;
        if (rb)
        {
            rb.detectCollisions = amplifierDetectCollisions;
            if (sameLife)
            {
                SetAmplifierPosition(amplifierEnd);
                rb.isKinematic = amplifierKinematic;
                if (!rb.isKinematic) rb.linearVelocity = _matchInputLocked || _worldGameplaySuppressed ? Vector3.zero : amplifierRecoil;
            }
        }
        if (amplifierRenderers != null)
            for (int i = 0; i < amplifierRenderers.Length; i++)
                if (amplifierRenderers[i]) amplifierRenderers[i].forceRenderingOff = amplifierRendererHidden[i];
        if (sameLife) SetAmplifierBodyVisible(true);
        amplifierRenderers = null; amplifierVisuals = null;
        if (sameLife && amplifierStun > 0f) ExternalStun(amplifierStun);
        amplifierStun = 0f;
    }

    public void ApplyAmplifierTransitProperties(MaterialPropertyBlock properties, Matrix4x4 sourceToWorld, Matrix4x4 viewProjection)
    {
        Vector3 center = transform.position;
        properties.SetVector(AmplifierCenterId, new Vector4(center.x, center.y, center.z, IsAmplifierTransitioning ? 1f : 0f));
        properties.SetVector(AmplifierShapeId, new Vector4(amplifierDirection.x, amplifierDirection.z, amplifierScale, amplifierStretch));
        properties.SetMatrix(AmplifierSourceId, sourceToWorld);
        properties.SetMatrix(AmplifierVPId, viewProjection);
    }

    private bool RouteAmplifierInput(ref PlayerInputFrame input)
    {
        if (IsInsideAmplifier)
        {
            if (amplifierOwner && amplifierPhase != AmplifierTransitPhase.Exiting) amplifierOwner.ReceivePossessionInput(this, input);
            movement = Vector3.zero; moveHorizontal = moveVertical = 0f;
            shieldOn = didPlayerTapActionThisFrame = false;
            lastActivityTime = Time.time; isActive = true;
            return true;
        }
        if (amplifierAwaitRelease)
        {
            if (!input.attackHeld && !input.shieldHeld) amplifierAwaitRelease = false;
            input.attackDown = input.attackHeld = input.attackUp = input.shieldDown = input.shieldHeld = false;
        }
        return false;
    }

    private void ReleaseAmplifierOnDisable()
    {
        if (amplifierOwner) amplifierOwner.ReleaseOccupant(this);
        if (IsInsideAmplifier) CompleteAmplifierExit();
    }
}
