using System.Collections.Generic;

/// <summary>
/// Partial: end-of-minigame result building + wrap-up payload.
/// Keep this separate from the main sim file so rewards/UI wiring stays isolated.
/// </summary>
public partial class NovaCoreMinigame
{
    // Optional: keep the last computed wrap-up (useful for debugging / future UI).
    private NovaCoreWrapUpPayload _lastWrapUp;
    public NovaCoreWrapUpPayload LastWrapUp => _lastWrapUp;

    /// <summary>
    /// Computes winner/score, builds a NovaCoreWrapUpPayload, then calls Complete(result).
    /// NOTE: This method must exist in exactly ONE partial file.
    /// </summary>
    private void EndMinigame()
    {
        if (IsFinished) return;

        // Stop sim/input immediately; transition will handle visuals.
        SetGameplayEnabled(false);

        // Sum mass per team + find best individual player.
        var teamToMass = new Dictionary<int, float>();
        ParticipantState bestPlayer = null;

        for (int i = 0; i < _participants.Count; i++)
        {
            var ps = _participants[i];
            if (ps == null || ps.controller == null) continue;

            int team = ps.controller.teamID;
            if (!teamToMass.TryGetValue(team, out float teamMass))
                teamMass = 0f;
            teamMass += ps.totalCapturedMass;
            teamToMass[team] = teamMass;

            if (bestPlayer == null || ps.totalCapturedMass > bestPlayer.totalCapturedMass)
                bestPlayer = ps;
        }

        float totalMass = 0f;
        foreach (var kv in teamToMass)
            totalMass += kv.Value;

        bool success = totalMass >= minTotalMassForSuccess;

        int winningTeam = -1;
        float winningMass = 0f;
        foreach (var kv in teamToMass)
        {
            if (kv.Value > winningMass)
            {
                winningMass = kv.Value;
                winningTeam = kv.Key;
            }
        }

        // Build wrap-up payload for rewards + future post screens.
        float lightMass = teamToMass.TryGetValue(lightTeamIndex, out var lm) ? lm : 0f;
        float darkMass  = teamToMass.TryGetValue(darkTeamIndex,  out var dm) ? dm : 0f;

        var wrap = new NovaCoreWrapUpPayload
        {
            lightTeamIndex = lightTeamIndex,
            darkTeamIndex  = darkTeamIndex,
            lightParticles = _lightTeamParticlesCaptured,
            darkParticles  = _darkTeamParticlesCaptured,
            lightMass = lightMass,
            darkMass  = darkMass,
        };

        _lastWrapUp = wrap;

        var result = new AnomalyResult
        {
            success = success,
            winningPlayer = bestPlayer != null ? bestPlayer.controller : null,
            winningTeamIndex = winningTeam,
            score = winningMass,
            payload = wrap,
        };

        Complete(result);
    }
}
