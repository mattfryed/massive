#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Scoring;
using Massive.Statistics;
using UnityEditor;
using UnityEngine;

public sealed class MatchHistoryWindow : EditorWindow
{
    private static readonly string[] Sources = { "Production", "Development", "Validation", "Choose folder" };
    [SerializeField] private int source;
    [SerializeField] private string customFolder;
    [SerializeField] private string levelFilter = "";
    [SerializeField] private int modeFilter;
    private List<MatchHistoryRecord> records = new List<MatchHistoryRecord>();
    private readonly List<string> issues = new List<string>();
    private Vector2 scroll;
    private string selectedId;
    private string Folder => source == 3 ? customFolder : Path.Combine(MatchHistoryRuntime.RootDirectory, Sources[source]);

    [MenuItem("MASSIVE/Statistics/Match History")]
    public static void Open() => GetWindow<MatchHistoryWindow>("Match History");

    private void OnEnable() { minSize = new Vector2(780, 500); Reload(); }

    private void Reload()
    {
        issues.Clear(); records.Clear();
        if (string.IsNullOrEmpty(Folder)) return;
        try
        {
            var store = new MatchHistoryStore(Folder);
            records = store.ReadAll(); issues.AddRange(store.Issues);
        }
        catch (Exception e) { issues.Add(e.Message); }
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MASSIVE · Match history", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        source = EditorGUILayout.Popup("Records", source, Sources);
        if (EditorGUI.EndChangeCheck()) Reload();
        if (GUILayout.Button("Refresh", GUILayout.Width(75))) Reload();
        if (GUILayout.Button("Open folder", GUILayout.Width(95)) && Directory.Exists(Folder)) EditorUtility.RevealInFinder(Folder);
        if (GUILayout.Button("Export CSVs", GUILayout.Width(95))) Export();
        EditorGUILayout.EndHorizontal();
        if (source == 3 && GUILayout.Button(string.IsNullOrEmpty(customFolder) ? "Choose a cabinet history folder…" : customFolder))
        {
            string chosen = EditorUtility.OpenFolderPanel("Match history folder", customFolder, "");
            if (!string.IsNullOrEmpty(chosen)) { customFolder = chosen; Reload(); }
        }
        EditorGUILayout.SelectableLabel(Folder ?? "Choose a folder", EditorStyles.miniLabel, GUILayout.Height(18));
        EditorGUILayout.BeginHorizontal();
        levelFilter = EditorGUILayout.TextField("Level contains", levelFilter);
        modeFilter = EditorGUILayout.Popup(modeFilter, new[] { "All modes", "1v1", "2v2" }, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        var filtered = Filtered();
        int completed = filtered.Count(r => r.status == MatchHistoryStatus.Completed);
        EditorGUILayout.LabelField($"{filtered.Count:N0} started    ·    {completed:N0} completed    ·    {filtered.Count(r => r.HighScoreEligible):N0} eligible for high scores");
        EditorGUILayout.HelpBox("Counts start when regulation begins. Unfinished matches retain partial stats. Production and development records are separate. Active time excludes pauses/transitions; bonus play counts even when the world is frozen. Refresh to see the latest saved checkpoint.", MessageType.Info);
        foreach (string issue in issues.Take(8)) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        if (issues.Count > 8) EditorGUILayout.LabelField($"{issues.Count - 8} more file warnings.");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (filtered.Count == 0) EditorGUILayout.LabelField("No recorded matches match these filters.");
        else
        {
            EditorGUILayout.LabelField("Playcounts by level and mode", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level / mode", GUILayout.Width(195));
            foreach (string heading in new[] { "Started", "Finished", "Abandoned", "Interrupted", "Running" })
                EditorGUILayout.LabelField(heading, GUILayout.Width(87));
            EditorGUILayout.EndHorizontal();
            foreach (var row in MatchHistoryReport.Summarize(filtered))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(row.title + " · " + MatchHistoryReport.Mode(row.mode), GUILayout.Width(195));
                foreach (int count in new[] { row.started, row.completed, row.abandoned, row.interrupted, row.inProgress })
                    EditorGUILayout.LabelField(count.ToString("N0"), GUILayout.Width(87));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Active {row.activeSeconds / 60d:N1} min · Elapsed {row.elapsedSeconds / 60d:N1} min · Light/Dark/Ties {row.lightWins}/{row.darkWins}/{row.ties}", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recent matches (latest 100; export includes all filtered records)", EditorStyles.boldLabel);
            foreach (var record in filtered.Take(100))
            {
                string label = $"{LocalDate(record.startedUtc)}   {record.result.stageTitle}   {MatchHistoryReport.Mode(record.result.mode)}   {record.status}   {record.ActiveSeconds:N1}s active";
                bool open = EditorGUILayout.Foldout(selectedId == record.matchId, label, true);
                if (open) { selectedId = record.matchId; DrawRecord(record); }
                else if (selectedId == record.matchId) selectedId = null;
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private List<MatchHistoryRecord> Filtered() => records.Where(r =>
        (string.IsNullOrEmpty(levelFilter) || (r.result.stageTitle ?? "").IndexOf(levelFilter, StringComparison.OrdinalIgnoreCase) >= 0) &&
        (modeFilter == 0 || (int)r.result.mode == modeFilter - 1)).ToList();

    private static string LocalDate(string utc) => DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date)
        ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : utc;

    private static void DrawRecord(MatchHistoryRecord r)
    {
        EditorGUI.indentLevel++;
        var s = r.result;
        EditorGUILayout.LabelField($"Light {s.LightScore:N0} meV    Dark {s.DarkScore:N0} meV    " + (r.status == MatchHistoryStatus.Completed ? s.winner.ToString() : "Partial result"));
        EditorGUILayout.LabelField($"Average Amplifier: Light ×{s.lightAmplifier.Average:N2} · Dark ×{s.darkAmplifier.Average:N2}");
        if (s.hasBonusBreakdown) EditorGUILayout.LabelField($"Regulation {s.regulationLightMilliElectronVolts:N0} / {s.regulationDarkMilliElectronVolts:N0} · Bonus +{s.bonusLightMilliElectronVolts:N0} / +{s.bonusDarkMilliElectronVolts:N0} meV");
        EditorGUILayout.LabelField($"Elapsed {r.elapsedSeconds:N1}s · Active regulation {r.activeRegulationSeconds:N1}s · Active bonus {r.activeBonusSeconds:N1}s · Configured {s.regulationDurationSeconds:N1}s");
        foreach (var p in s.playerStatistics ?? Array.Empty<PlayerMatchStatistics>())
        {
            var score = (s.playerContributions ?? Array.Empty<PlayerScoreContribution>()).FirstOrDefault(c => c.playerID == p.playerID && c.teamID == p.teamID);
            EditorGUILayout.LabelField($"P{p.playerID + 1} / Team {p.teamID}: {score.finalMilliElectronVolts:N0} meV · K {p.playerKills} / D {p.deaths} · Enemies {p.enemiesKilled} · {p.distanceTraveledMeters:N1}m · Avg ×{p.personalMultiplier.Average:N2}");
        }
        foreach (var reward in s.scoreTelemetry ?? Array.Empty<ScoreTelemetryRecord>())
            EditorGUILayout.LabelField($"{reward.rewardKey}: Light {reward.lightFinalMilliElectronVolts:N0} ({reward.lightAwardCount}) / Dark {reward.darkFinalMilliElectronVolts:N0} ({reward.darkAwardCount})", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"{r.environment} · {s.scoringRulesetId} v{s.scoringRulesetVersion} · Build {r.gameVersion}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("High scores: " + (r.HighScoreEligible ? "Eligible" : r.status != MatchHistoryStatus.Completed ? "Incomplete match" : r.eligibilityReason), EditorStyles.miniLabel);
        EditorGUILayout.LabelField(r.endReason ?? "In progress", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.SelectableLabel("Match " + r.matchId, EditorStyles.miniLabel, GUILayout.Height(18));
        EditorGUI.indentLevel--;
    }

    private void Export()
    {
        string parent = EditorUtility.OpenFolderPanel("Export match history CSV tables", "", "");
        if (string.IsNullOrEmpty(parent)) return;
        try
        {
            string output = Path.Combine(parent, "MASSIVE-Statistics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            MatchHistoryReport.Export(output, Filtered());
            EditorUtility.RevealInFinder(output);
        }
        catch (Exception e) { EditorUtility.DisplayDialog("Statistics export failed", e.Message, "OK"); }
    }
}
#endif
