using Massive.Scoring;
using UnityEngine;

/// <summary>End-of-match presentation. The match owner decides the winner,
/// saves MatchResult and transitions; a star bounce never bypasses those rules.</summary>
public class SupernovaEndSequence : MonoBehaviour
{
    public NovaStarController star;
    public GameObject sequenceRoot;
    public float endDelaySeconds = 2.5f;
    public ParticleSystem[] playOnStart;
    public GameManagerScript match;
    public bool HasPlayed { get; private set; }

    private void Awake()
    {
        if (sequenceRoot && sequenceRoot != gameObject) sequenceRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (match) match.PhaseChanged += OnMatchPhase;
        if (star) star.OnFinalSupernova += HandleFinalSupernova;
    }

    private void OnDisable()
    {
        if (match) match.PhaseChanged -= OnMatchPhase;
        if (star) star.OnFinalSupernova -= HandleFinalSupernova;
    }

    private void OnMatchPhase(MatchRuntimePhase phase)
    {
        if (phase == MatchRuntimePhase.Resolving) PlaySequence();
    }

    private void HandleFinalSupernova()
    {
        // Standalone presentation remains usable; gameplay waits for regulation.
        if (!match) PlaySequence();
    }

    private void PlaySequence()
    {
        if (HasPlayed) return;
        HasPlayed = true;
        if (sequenceRoot) sequenceRoot.SetActive(true);
        if (playOnStart != null)
            foreach (var particles in playOnStart)
                if (particles) particles.Play(true);
    }
}
