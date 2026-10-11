using System;
using System.Globalization;
using Massive.Scoring;
using TMPro;
using UnityEngine;

[Serializable]
public sealed class PostGamePlayerStatsView
{
    public int playerID;
    public int teamID;
    public RectTransform root;
    public TMP_Text heading;
    public TMP_Text individualScore;
    public TMP_Text playerKills;
    public TMP_Text deaths;
    public TMP_Text enemiesKilled;
    public TMP_Text distanceTraveled;
    public Vector2 twoPlayerPosition;
    public Vector2 soloPosition;

    public void Apply(MatchResult result)
    {
        bool doubles = result.mode == GameMode.TwoVTwo;
        bool visible = doubles || playerID == 0 || playerID == 2;
        if (root)
        {
            root.gameObject.SetActive(visible);
            root.anchoredPosition = doubles ? twoPlayerPosition : soloPosition;
        }
        if (!visible) return;
        if (heading) heading.text = doubles ? "PLAYER " + (playerID + 1) : string.Empty;
        if (individualScore) individualScore.gameObject.SetActive(doubles);

        var stats = default(PlayerMatchStatistics);
        if (result.playerStatistics != null)
            foreach (var entry in result.playerStatistics)
                if (entry.playerID == playerID && entry.teamID == teamID) { stats = entry; break; }
        long score = 0;
        if (result.playerContributions != null)
            foreach (var entry in result.playerContributions)
                if (entry.playerID == playerID && entry.teamID == teamID) { score = entry.finalMilliElectronVolts; break; }
        if (individualScore) individualScore.text = Math.Max(0L, score).ToString("N0", CultureInfo.InvariantCulture);
        if (playerKills) playerKills.text = stats.playerKills.ToString(CultureInfo.InvariantCulture);
        if (deaths) deaths.text = stats.deaths.ToString(CultureInfo.InvariantCulture);
        if (enemiesKilled) enemiesKilled.text = stats.enemiesKilled.ToString(CultureInfo.InvariantCulture);
        if (distanceTraveled) distanceTraveled.text = stats.distanceTraveledMeters.ToString("N0", CultureInfo.InvariantCulture) + "m";
    }
}
