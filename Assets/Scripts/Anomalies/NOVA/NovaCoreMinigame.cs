using System.Collections.Generic;
using UnityEngine;
using Rewired;

/// <summary>
/// NOVA level anomaly: players inside the star tap to match incoming
/// "particles" before they hit the core.
///
/// This is logic-only:
/// - Visuals for the core and incoming particles should hook into the
///   Spawn/Resolve TODOs.
/// - Rewards for captured mass are handled by an AnomalyReward asset
///   using AnomalyResult.score (winning team's captured mass).
/// </summary>
public class NovaCoreMinigame : AnomalyMinigameBase
{
    [Header("Timing")]
    [Tooltip("How long this minigame runs, in seconds. If <= 0, Context.duration is used.")]
    [SerializeField] private float overrideDuration = 0f;

    [Tooltip("Seconds a particle takes to travel from spawn to the core.")]
    [SerializeField] private float particleLifetime = 0.75f;

    [Tooltip("Seconds between particle spawns once the minigame starts.")]
    [SerializeField] private float spawnInterval = 0.35f;

    [Header("Particles")]
    [Tooltip("Minimum taps required for a particle (inclusive).")]
    [SerializeField] private int tapsMin = 1;

    [Tooltip("Maximum taps required for a particle (inclusive).")]
    [SerializeField] private int tapsMax = 3;

    [Header("Scoring")]
    [Tooltip("Mass granted per tap. A 3-dot particle gives 3 * massPerTap.")]
    [SerializeField] private float massPerTap = 1f;

    [Tooltip("Total mass across all players required for this anomaly to count as 'success'.")]
    [SerializeField] private float minTotalMassForSuccess = 1f;

    // Internal per-player tracking
    private class ParticipantState
    {
        public PlayerControllerScript controller;
        public Player rewiredPlayer;

        public int tapsThisParticle;
        public int totalCapturedParticles;
        public float totalCapturedMass;
    }

    // Current particle state (single-stream version for now)
    private class ParticleState
    {
        public int tapsRequired;
        public float spawnTime;
        public bool resolved;
    }

    private readonly List<ParticipantState> _participants = new();
    private ParticleState _currentParticle;

    private float _timeRemaining;
    private float _nextSpawnTime;
    private bool _started;

    public override void Init(AnomalyContext context)
    {
        base.Init(context);

        // Choose duration: override wins, then Context.duration, then sensible default.
        _timeRemaining = (overrideDuration > 0f)
            ? overrideDuration
            : (Context.duration > 0f ? Context.duration : 5f);

        BuildParticipants();
    }

    public override void Begin()
    {
        base.Begin();
        _started = true;

        // Small delay before first particle so the player can orient.
        ScheduleNextParticle(0.25f);

        Debug.Log($"[NovaCoreMinigame] Begin. Participants={_participants.Count}, Duration={_timeRemaining:F2}s");
    }

    private void Update()
    {
        if (!_started || IsFinished)
            return;

        // Global timer
        _timeRemaining -= Time.deltaTime;
        if (_timeRemaining <= 0f)
        {
            EndMinigame();
            return;
        }

        // Spawn particle if needed
        if (_currentParticle == null && Time.time >= _nextSpawnTime)
        {
            SpawnParticle();
        }

        // Update current particle (inputs + timeout)
        if (_currentParticle != null)
        {
            UpdateParticleInput();

            if (!_currentParticle.resolved &&
                Time.time - _currentParticle.spawnTime >= particleLifetime)
            {
                // Particle hits the core without being captured
                ResolveParticle(null);
            }
        }

        // TODO: drive visual position/scale of the current particle here based on
        //       (Time.time - _currentParticle.spawnTime) / particleLifetime.
    }

    // ------------------------------------------------------
    // Setup
    // ------------------------------------------------------

    private void BuildParticipants()
    {
        _participants.Clear();

        if (Context.participants == null)
            return;

        foreach (var p in Context.participants)
        {
            if (p == null) continue;

            var state = new ParticipantState
            {
                controller = p,
                rewiredPlayer = ReInput.players.GetPlayer(p.playerID),
                tapsThisParticle = 0,
                totalCapturedParticles = 0,
                totalCapturedMass = 0f
            };

            _participants.Add(state);
        }
    }

    private void ScheduleNextParticle(float delay)
    {
        _nextSpawnTime = Time.time + delay;
    }

    private void SpawnParticle()
    {
        int tapsRequired = Random.Range(tapsMin, tapsMax + 1);

        _currentParticle = new ParticleState
        {
            tapsRequired = tapsRequired,
            spawnTime = Time.time,
            resolved = false
        };

        // Reset per-player tap counts for this particle
        foreach (var ps in _participants)
        {
            ps.tapsThisParticle = 0;
        }

        // TODO: tell your view layer to spawn a new particle visual with
        //       "tapsRequired" dots and start its move toward the core.
        Debug.Log($"[NovaCoreMinigame] Spawn particle (tapsRequired={tapsRequired})");
    }

    // ------------------------------------------------------
    // Input / capture logic
    // ------------------------------------------------------

    private void UpdateParticleInput()
    {
        if (_currentParticle == null || _currentParticle.resolved)
            return;

        ParticipantState winnerThisFrame = null;

        foreach (var ps in _participants)
        {
            if (ps.rewiredPlayer == null) continue;

            // Attack button for this game is "Sword" (same as main game).
            if (ps.rewiredPlayer.GetButtonDown("Sword"))
            {
                ps.tapsThisParticle++;

                // First player to reach the required tap count captures the particle.
                if (ps.tapsThisParticle >= _currentParticle.tapsRequired)
                {
                    winnerThisFrame = ps;
                    break;
                }
            }
        }

        if (winnerThisFrame != null)
        {
            ResolveParticle(winnerThisFrame);
        }
    }

    private void ResolveParticle(ParticipantState winner)
    {
        if (_currentParticle == null || _currentParticle.resolved)
            return;

        _currentParticle.resolved = true;

        if (winner != null)
        {
            winner.totalCapturedParticles++;

            float mass = _currentParticle.tapsRequired * massPerTap;
            winner.totalCapturedMass += mass;

            // TODO: particle captured VFX (flash at core, highlight winning lane, etc.)
            Debug.Log($"[NovaCoreMinigame] Particle captured by {winner.controller.name}. Mass +{mass:F1}");
        }
        else
        {
            // TODO: missed particle VFX (e.g. red flash, louder core vibration).
            Debug.Log("[NovaCoreMinigame] Particle missed.");
        }

        // Clear current particle and schedule the next one.
        _currentParticle = null;
        ScheduleNextParticle(spawnInterval);
    }

    // ------------------------------------------------------
    // Finish & reporting
    // ------------------------------------------------------

    private void EndMinigame()
    {
        if (IsFinished)
            return;

        // Tally totals per team and find best performer.
        var teamToMass = new Dictionary<int, float>();
        ParticipantState bestParticipant = null;

        foreach (var ps in _participants)
        {
            if (ps.controller == null) continue;

            int team = ps.controller.teamID;

            if (!teamToMass.ContainsKey(team))
                teamToMass[team] = 0f;

            teamToMass[team] += ps.totalCapturedMass;

            if (bestParticipant == null ||
                ps.totalCapturedMass > bestParticipant.totalCapturedMass)
            {
                bestParticipant = ps;
            }
        }

        float totalMass = 0f;
        foreach (var kvp in teamToMass)
            totalMass += kvp.Value;

        bool success = totalMass >= minTotalMassForSuccess;

        int winningTeam = -1;
        float winningTeamMass = 0f;

        if (teamToMass.Count > 0)
        {
            foreach (var kvp in teamToMass)
            {
                if (kvp.Value > winningTeamMass)
                {
                    winningTeamMass = kvp.Value;
                    winningTeam = kvp.Key;
                }
            }
        }

        var result = new AnomalyResult
        {
            success = success,
            winningPlayer = bestParticipant != null ? bestParticipant.controller : null,
            winningTeamIndex = winningTeam,
            // Use score as "how much mass the winning team captured"
            score = winningTeamMass
        };

        Debug.Log($"[NovaCoreMinigame] End. success={success}, winningTeam={winningTeam}, mass={winningTeamMass:F1}");

        Complete(result);
    }
}
