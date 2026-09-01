using System;
using UnityEngine;

// NOTE: Adjust namespace if needed for Rhythm Timeline 2
using Dypsloom.RhythmTimeline;
using Dypsloom.RhythmTimeline.Core.Playables;
using Dypsloom.RhythmTimeline.Core;
using Dypsloom.RhythmTimeline.Core.Notes;
using Dypsloom.RhythmTimeline.Core.Input;


[DisallowMultipleComponent]
public class PulsarUnisonNote : Note
{
    [Header("Prompt Type")]
    [SerializeField] private PulsarPromptType promptType = PulsarPromptType.Sword;

    [Header("Visuals")]
    [SerializeField] private Transform visualRoot; // optional, defaults to transform
    [SerializeField] private GameObject swordArcGO;
    [SerializeField] private GameObject shieldArcGO;
    [SerializeField] private GameObject bothRingGO;

    [Header("Angles (world)")]
    [SerializeField] private float swordAngleDeg = 180f;
    [SerializeField] private float shieldAngleDeg = 0f;

    public PulsarPromptType PromptType => promptType;

    // Exposed so the session can pick the closest-to-perfect note if overlaps occur.
    public float SignedTimeToPerfect { get; private set; }

    private PulsarJudgement[] _judgements = Array.Empty<PulsarJudgement>();
    private bool[] _attempted = Array.Empty<bool>();

    private float _lastSigned;
    private bool _beatTicked;

    public override void Initialize(RhythmClipData rhythmClipData)
    {
        base.Initialize(rhythmClipData);
        ApplyVariant();
        _beatTicked = false;
        _lastSigned = float.NegativeInfinity;
    }

    public override void Reset()
    {
        base.Reset();
        _beatTicked = false;
        _lastSigned = float.NegativeInfinity;
        // Don’t assume player count here (pooled notes); allocate on ActivateNote.
    }

    protected override void ActivateNote()
    {
        base.ActivateNote();

        if (!Application.isPlaying) return;

        var session = PulsarRhythmSession.Instance;
        if (session == null || !session.IsRunning) return;

        int players = session.PlayerCount;
        EnsureBuffers(players);

        for (int i = 0; i < players; i++)
        {
            _attempted[i] = false;
            _judgements[i] = PulsarJudgement.Miss;
        }

        _beatTicked = false;
        _lastSigned = float.NegativeInfinity;

        session.RegisterActive(this);
    }

    protected override void DeactivateNote()
    {
        base.DeactivateNote();

        if (!Application.isPlaying) return;

        var session = PulsarRhythmSession.Instance;
        if (session == null || !session.IsRunning) return;

        // Finalize: players who never attempted remain Miss.
        session.UnregisterActive(this);
        session.ResolveBeat(_judgements);
    }

    public override void OnTriggerInput(InputEventData inputEventData)
    {
        if (!Application.isPlaying) return;

        var session = PulsarRhythmSession.Instance;
        if (session == null || !session.IsRunning) return;

        var evt = inputEventData as PulsarInputEventData;
        if (evt == null) return;

        int p = evt.PlayerIndex;
        if (p < 0 || p >= _attempted.Length) return;
        if (_attempted[p]) return;

        // Judge timing relative to perfect (center of clip).
       double perfectTime = RhythmClipData.RealDuration * 0.5;
        double diff = TimeFromActivate - perfectTime;
        float abs = Mathf.Abs((float)diff);


        var cfg = session.Config;
        PulsarJudgement j;

        if (abs <= cfg.perfectWindowSec) j = PulsarJudgement.Perfect;
        else if (abs <= cfg.goodWindowSec) j = PulsarJudgement.Good;
        else j = PulsarJudgement.Miss;

        _attempted[p] = true;
        _judgements[p] = j;

        // Optional: you could do per-player VFX feedback here (spark, pulse, etc.)
    }

    protected override void HybridUpdate(double timeFromStart, double timeFromEnd)
    {
        // Perfect time convention: center of clip (same as TapNote). :contentReference[oaicite:16]{index=16}
        double perfectTime = RhythmClipData.RealDuration * 0.5;
        float signed = (float)(timeFromStart - perfectTime);
        SignedTimeToPerfect = signed; 

        // Beat tick detection (crossing perfect).
        if (Application.isPlaying && !_beatTicked)
        {
            if (_lastSigned < 0f && signed >= 0f)
            {
                _beatTicked = true;
                PulsarRhythmSession.Instance?.NotifyBeatTick();
            }
        }
        _lastSigned = signed;

        // Visual placement:
        var session = PulsarRhythmSession.Instance;
        Vector3 center = (session && session.PulsarCenter) ? session.PulsarCenter.position : RhythmClipData.TrackObject.EndPoint.position;

        float startR = (session && session.Config) ? session.Config.startRadius : 9f;
        float hitR = (session && session.Config) ? session.Config.hitRadius : 3f;

        // Approach speed so at clip start (signed = -perfectTime) radius == startRadius, at signed=0 radius==hitRadius.
        float approachSpeed = (startR - hitR) / Mathf.Max(0.001f, (float)perfectTime);
        float radius = hitR - signed * approachSpeed;

        if (!visualRoot) visualRoot = transform;

        switch (promptType)
        {
            case PulsarPromptType.Both:
            {
                visualRoot.position = center;

                // Scale ring so its radius appears to shrink toward hit radius.
                float s = Mathf.Max(0.01f, radius / hitR);
                visualRoot.localScale = Vector3.one * s;
                break;
            }
            case PulsarPromptType.Sword:
            {
                float angle = swordAngleDeg;
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                visualRoot.position = center + dir * radius;
                visualRoot.localScale = Vector3.one;
                break;
            }
            case PulsarPromptType.Shield:
            default:
            {
                float angle = shieldAngleDeg;
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                visualRoot.position = center + dir * radius;
                visualRoot.localScale = Vector3.one;
                break;
            }
        }
    }

    private void ApplyVariant()
    {
        if (swordArcGO) swordArcGO.SetActive(promptType == PulsarPromptType.Sword);
        if (shieldArcGO) shieldArcGO.SetActive(promptType == PulsarPromptType.Shield);
        if (bothRingGO) bothRingGO.SetActive(promptType == PulsarPromptType.Both);
    }

    private void EnsureBuffers(int players)
    {
        if (_judgements.Length == players && _attempted.Length == players) return;

        _judgements = new PulsarJudgement[players];
        _attempted = new bool[players];
    }
}
