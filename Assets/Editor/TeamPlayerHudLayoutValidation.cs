#if UNITY_EDITOR
using System;
using System.Collections;
using Massive.Player;
using Massive.Scoring;
using Shapes;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runs in the health HUD's isolated fixture with its real player roots.</summary>
public static class TeamPlayerHudLayoutValidation
{
    public static IEnumerator Checks(PlayerControllerScript[] players, Action<bool, string> check)
    {
        var service = new GameObject("Layout score service").AddComponent<MatchScoreService>();
        service.Configure(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
        foreach (var p in players) service.RegisterPlayer(p);

        var field = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHealthHudSetup.PlayingFieldPath);
        var teams = new Transform[2];
        var layouts = new TeamPlayerHudLayout[2];
        for (int i = 0; i < teams.Length; i++)
        {
            teams[i] = Object.Instantiate(field.transform.Find("UI/Text Objects/TEAM_0" + (i + 1)).gameObject).transform;
            layouts[i] = teams[i].GetComponent<TeamPlayerHudLayout>();
            check(layouts[i] != null, "Each team's prefab has an adaptive layout");
        }

        var rosterObject = new GameObject("Layout test roster");
        rosterObject.SetActive(false);
        var roster = rosterObject.AddComponent<PlayerRosterController>();
        var settings = new SerializedObject(roster);
        for (int i = 0; i < players.Length; i++) settings.FindProperty("player" + (i + 1)).objectReferenceValue = players[i].gameObject;
        settings.FindProperty("autoAssignByPlayerIdIfMissing").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        roster.enabled = false; // Exercise roster selection without starting the separate match-spawn sequence.
        rosterObject.SetActive(true);
        roster.ApplyRoster(true);
        yield return 0.15f;
        for (int i = 0; i < teams.Length; i++)
        {
            check(!layouts[i].IsSinglePlayerLayout, "Four-player roster uses two columns per team");
            CheckBounds(teams[i], false, check);
        }

        roster.ApplyRoster(false);
        yield return 0.15f;
        for (int i = 0; i < teams.Length; i++)
        {
            check(layouts[i].IsSinglePlayerLayout, "P1/P3 roster automatically selects full-width panels");
            CheckBounds(teams[i], true, check);
        }

        var health = teams[0].GetComponentsInChildren<PlayerHealthPresenter>()[0];
        var multiplier = teams[0].GetComponentsInChildren<PlayerScoreChainPresenter>()[0];
        players[0].massScore = 1f;
        service.GetPlayerChain(0).ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 1000f);
        yield return 0.9f;
        var hs = new SerializedObject(health);
        var ms = new SerializedObject(multiplier);
        var healthFill = (Rectangle)hs.FindProperty("healthFill").objectReferenceValue;
        var healthTrack = (Rectangle)hs.FindProperty("healthTrack").objectReferenceValue;
        var multiplierFill = (Rectangle)ms.FindProperty("progressRectangle").objectReferenceValue;
        var multiplierTrack = (Rectangle)ms.FindProperty("progressTrackRectangle").objectReferenceValue;
        check(Mathf.Approximately(healthFill.Width, healthTrack.Width) &&
              Mathf.Approximately(multiplierFill.Width, multiplierTrack.Width) && multiplier.MaximumEffectsActive,
            "Full health and maximum multiplier fill the expanded bars, including the maximum effect");

        players[0].massScore = 0.1f;
        yield return 0.7f;
        check(health.IsLowHealth && Mathf.Abs(healthFill.Width / healthTrack.Width - 0.1f) < 0.002f,
            "Low-health blink and fill fraction survive the wider layout");
        players[0].temporarilyEliminated = true;
        yield return 0.04f;
        check(layouts[0].IsSinglePlayerLayout && !healthFill.enabled,
            "Death empties the health bar without changing the roster layout");
        players[0].temporarilyEliminated = false;

        roster.ApplyRoster(true);
        yield return 0.15f;
        check(!layouts[0].IsSinglePlayerLayout && !layouts[1].IsSinglePlayerLayout &&
              Mathf.Approximately(multiplierFill.Width, multiplierTrack.Width) && multiplier.MaximumEffectsActive,
            "Returning to 2v2 resizes a live maximum multiplier without stale cached width");
        for (int i = 0; i < teams.Length; i++) CheckBounds(teams[i], false, check);

        for (int i = 0; i < 8; i++)
        {
            foreach (var layout in layouts) layout.SetLayoutMode(PlayerHudLayoutMode.OneVOne);
            foreach (var layout in layouts) layout.SetLayoutMode(PlayerHudLayoutMode.TwoVTwo);
        }
        for (int i = 0; i < teams.Length; i++) CheckBounds(teams[i], false, check);
        foreach (var layout in layouts) layout.SetLayoutMode(PlayerHudLayoutMode.Automatic);

        var playerGroup = new GameObject("Temporarily hidden gameplay rig");
        foreach (var p in players) p.transform.SetParent(playerGroup.transform, true);
        playerGroup.SetActive(false);
        yield return 0.04f;
        check(!layouts[0].IsSinglePlayerLayout && !layouts[1].IsSinglePlayerLayout,
            "Hiding an ancestor gameplay rig does not collapse the two-player layout");
        playerGroup.SetActive(true);

        players[0].gameObject.SetActive(false);
        yield return 0.04f;
        check(layouts[0].IsSinglePlayerLayout && teams[0].Find("Player 2 health").gameObject.activeSelf &&
              !teams[0].Find("Player 1 health").gameObject.activeSelf,
            "A sole second-slot participant receives the full team width");
        players[0].gameObject.SetActive(true);
    }

    private static void CheckBounds(Transform team, bool single, Action<bool, string> check)
    {
        var score = team.Find("SCORE NUMBER").GetComponent<TMP_Text>();
        float left = team.InverseTransformPoint(score.transform.TransformPoint(new Vector3(score.rectTransform.rect.xMin, 0f, 0f))).x;
        float right = team.InverseTransformPoint(score.transform.TransformPoint(new Vector3(score.rectTransform.rect.xMax, 0f, 0f))).x;
        int first = team.name.StartsWith("TEAM_01", StringComparison.Ordinal) ? 1 : 3;
        float half = (right - left - 0.25f) * 0.5f;
        for (int index = 0; index < 2; index++)
        {
            foreach (string kind in new[] { "health", "multiplier" })
            {
                Transform row = team.Find("Player " + (first + index) + " " + kind);
                if (single && index == 1) { check(!row.gameObject.activeSelf, "Unused 1v1 panel is hidden"); continue; }
                var label = row.Find("Player text").GetComponent<TMP_Text>();
                var frame = row.GetComponentInChildren<Rectangle>(true);
                float rowLeft = team.InverseTransformPoint(label.transform.TransformPoint(new Vector3(label.rectTransform.rect.xMin, 0f, 0f))).x;
                float rowRight = team.InverseTransformPoint(frame.transform.TransformPoint(new Vector3(frame.Width, 0f, 0f))).x;
                float expectedLeft = single || index == 0 ? left : right - half;
                float expectedRight = single || index == 1 ? right : left + half;
                check(row.gameObject.activeSelf && Mathf.Abs(rowLeft - expectedLeft) < 0.003f &&
                      Mathf.Abs(rowRight - expectedRight) < 0.003f && Mathf.Approximately(frame.Height, 0.25f),
                    "Player label and bar edge align to the score column while bar height stays fixed");
            }
        }
    }
}
#endif
