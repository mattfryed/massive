#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PostGameResultsValidation
{
    const string Key = "MASSIVE.PostGameResultsValidation";
    const string Output = "Library/PostGameResultsValidation";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new(), errors = new();
    static readonly Stack<IEnumerator> routines = new();
    static int nextFrame;
    static double deadline;
    static PostGameResultsValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update += Begin;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("PostGameResultsValidation/Project/"))
            throw new Exception("Run only in the isolated PostGameResultsValidation/Project copy.");
        SessionState.SetBool(Key + "Start", true);
    }
    static void Begin()
    {
        if (!SessionState.GetBool(Key + "Start", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + "Start", false);
        Directory.CreateDirectory(Output);
        try
        {
            PostGameResultsSetup.Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/PostGameStatsFixture.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { File.WriteAllText(Output + "/report.txt", e.ToString()); EditorApplication.Exit(1); }
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); routines.Clear(); routines.Push(Checks());
            deadline = EditorApplication.timeSinceStartup + 180; nextFrame = 0;
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + "Passed", false) ? 0 : 1);
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < nextFrame || routines.Count == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            while (routines.Count > 0)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                nextFrame = Time.frameCount + 1; return;
            }
            Finish(errors.Count == 0);
        }
        catch (Exception e) { checks.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool passed)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routines.Clear();
        File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\nERRORS\n" + string.Join("\n", errors));
        SessionState.SetBool(Key + "Passed", passed); Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
    static object Get(object target, string field) => target.GetType().GetField(field, Hidden).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
    static IEnumerator Seconds(float seconds)
    { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    static IEnumerator SettleText()
    {
        yield return null; yield return null;
        var transitions = Object.FindObjectsByType<TMPTextTransition>(FindObjectsSortMode.None);
        while (transitions.Any(t => t && t.IsPlaying)) yield return null;
        yield return null;
    }
    static PlayerControllerScript Player(int id)
    {
        var go = new GameObject("Statistics fixture P" + (id + 1)); go.SetActive(false);
        var rb = go.AddComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
        go.AddComponent<SphereCollider>().radius = .25f;
        var player = go.AddComponent<PlayerControllerScript>(); player.playerID = id; player.teamID = id < 2 ? 1 : 2;
        player.SetControlMode(PlayerControlMode.Disabled);
        Set(player, "playMatchSpawnOnSceneLoad", false);
        go.transform.position = new Vector3(id * 20, 0, 0); go.SetActive(true);
        return player;
    }
    static void Place(PlayerControllerScript player, Vector3 position)
    { player.GetComponent<Rigidbody>().position = position; player.transform.position = position; Physics.SyncTransforms(); }
    static PlayerMatchStatistics Stats(MatchScoreService service, int id) => service.GetStatisticsSnapshot().Single(s => s.playerID == id);
    static IEnumerator Checks()
    {
        GameFlowContext.EnsureExists(); GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
        var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
        var scores = new GameObject("Fixture scores").AddComponent<MatchScoreService>(); scores.Configure(profile);
        var players = Enumerable.Range(0, 4).Select(Player).ToArray();
        foreach (var player in players) scores.RegisterPlayer(player);
        scores.ResetForMatch(true); yield return Seconds(.1f);
        Check(scores.GetStatisticsSnapshot().Length == 4, "Zero-score players are present in the results snapshot");
        Place(players[0], new Vector3(3, 0, 4)); yield return Seconds(.08f);
        Check(Math.Abs(Stats(scores, 0).distanceTraveledMeters - 5) < .001, "3-4-5 world displacement records exactly five meters");
        Place(players[0], new Vector3(6, 0, 8)); yield return Seconds(.08f);
        Check(Math.Abs(Stats(scores, 0).distanceTraveledMeters - 10) < .001, "Distance accumulates path length");
        Call(players[0], "TeleportTo", new Vector3(100, 0, 100)); yield return Seconds(.08f);
        Check(Math.Abs(Stats(scores, 0).distanceTraveledMeters - 10) < .001, "Teleport displacement is excluded");
        players[0].SetMatchInputLocked(true); Place(players[0], new Vector3(120, 0, 100)); yield return Seconds(.08f);
        players[0].SetMatchInputLocked(false); yield return Seconds(.08f);
        Check(Math.Abs(Stats(scores, 0).distanceTraveledMeters - 10) < .001, "Match locks do not add distance on unlock");
        Time.timeScale = 0; yield return Seconds(.08f); Time.timeScale = 1;
        Check(Math.Abs(Stats(scores, 0).distanceTraveledMeters - 10) < .001, "Pause leaves distance unchanged");

        // Use the real player damage/death pipeline, including attribution and respawn.
        players[2].ApplyExternalMassDelta(-1f, players[0].gameObject, true, true);
        Check(Stats(scores, 2).deaths == 1 && Stats(scores, 0).playerKills == 1, "Attributed player death records one kill and one death");
        players[2].ApplyExternalMassDelta(-1f, players[0].gameObject, true, true);
        Check(Stats(scores, 2).deaths == 1 && Stats(scores, 0).playerKills == 1, "Repeated damage to a dead player cannot double count");
        players[1].ApplyExternalMassDelta(-1f, null, true, true);
        Check(Stats(scores, 1).deaths == 1 && Stats(scores, 0).playerKills == 1, "Environmental deaths count without inventing a killer");
        players[3].ApplyExternalMassDelta(-1f, players[2].gameObject, true, true);
        Check(Stats(scores, 3).deaths == 1 && Stats(scores, 2).playerKills == 0, "Friendly deaths do not award player kills");
        var drone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath));
        var enemy = drone.GetComponent<EnemyBase>();
        enemy.Init(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath), null);
        enemy.TakeDamage(10f, EnemyDamageSource.Sword, players[0]);
        Check(Stats(scores, 0).enemiesKilled == 1, "Real enemy defeat records the credited player");
        scores.RecordEnemyDefeat(players[0], enemy.SourceLifeToken);
        Check(Stats(scores, 0).enemiesKilled == 1, "Repeated enemy life token cannot double count");
        scores.GetPlayerChain(0).ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 4);
        scores.AdvanceTeamAmplifier(1);
        scores.TryAwardToPlayer(ScoreRewardKeys.MassNugget, players[0], "stats-pickup", Vector3.zero, out var award);
        Check(award.accepted && Stats(scores, 0).enemiesKilled == 1, "Pickup and multipliers cannot inflate kill counts");
        var snapshot = scores.GetStatisticsSnapshot();
        var managerObject = new GameObject("Result snapshot fixture"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<GameManagerScript>(); Set(manager, "scoreService", scores);
        var builtResult = (MatchResult)Call(manager, "BuildMatchResult", TeamSide.Light);
        Check(builtResult.playerStatistics.Length == 4 && builtResult.playerStatistics[0].enemiesKilled == 1 &&
            builtResult.playerContributions.Length > 0, "GameManager includes statistics and score contributions in MatchResult");
        Object.Destroy(managerObject);
        double distance = Stats(scores, 0).distanceTraveledMeters;
        scores.CloseScoring(); Place(players[0], Vector3.zero); yield return Seconds(.08f);
        scores.RecordEnemyDefeat(players[0], "after-end");
        Check(Stats(scores, 0).distanceTraveledMeters == distance && Stats(scores, 0).enemiesKilled == 1, "Stats freeze when scoring closes");
        scores.ResetForMatch(true);
        Check(scores.GetStatisticsSnapshot().All(s => s.deaths == 0 && s.playerKills == 0 && s.enemiesKilled == 0 && s.distanceTraveledMeters == 0), "Fresh match resets every statistic");
        Check(snapshot.Single(s => s.playerID == 0).enemiesKilled == 1, "Previous snapshots remain independent after reset");
        foreach (var player in players) Object.Destroy(player.gameObject);
        Object.Destroy(scores.gameObject); Object.Destroy(profile);
        yield return null;

        var result = new MatchResult
        {
            winner = TeamSide.Light, mode = GameMode.TwoVTwo, stageNumber = 3, stageTitle = "HIGGS",
            lightMilliElectronVolts = 2047987225019, darkMilliElectronVolts = 1832654098765,
            playerStatistics = new[] {
                new PlayerMatchStatistics { playerID=0,teamID=1,playerKills=3,deaths=2,enemiesKilled=36,distanceTraveledMeters=296.2 },
                new PlayerMatchStatistics { playerID=1,teamID=1,playerKills=4,deaths=2,enemiesKilled=45,distanceTraveledMeters=387 },
                new PlayerMatchStatistics { playerID=2,teamID=2,playerKills=2,deaths=4,enemiesKilled=29,distanceTraveledMeters=312 },
                new PlayerMatchStatistics { playerID=3,teamID=2,playerKills=2,deaths=3,enemiesKilled=32,distanceTraveledMeters=358 } },
            playerContributions = new[] {
                new PlayerScoreContribution {playerID=0,teamID=1,finalMilliElectronVolts=1020000000000},
                new PlayerScoreContribution {playerID=1,teamID=1,finalMilliElectronVolts=1027987225019},
                new PlayerScoreContribution {playerID=2,teamID=2,finalMilliElectronVolts=900000000000},
                new PlayerScoreContribution {playerID=3,teamID=2,finalMilliElectronVolts=932654098765} }
        };
        GameFlowContext.Instance.SetLastMatchResult(result);
        SceneFlow.GoToPostGame();
        yield return Seconds(1);
        var ui = Object.FindFirstObjectByType<PostGameScreenController>();
        Check(ui && SceneManager.GetActiveScene().name == SceneFlow.PostGameScene, "Real SceneFlow loads V2 from Build Settings");
        Set(ui, "autoReturnSeconds", 120f);
        yield return SettleText();
        var views = (PostGamePlayerStatsView[])Get(ui, "playerStats");
        Check(((TMP_Text)Get(ui,"scoreLightText")).text == "2,047,987,225,019" && ((TMP_Text)Get(ui,"scoreLightUnitText")).text == "meV", "Full exact team total and meV label");
        Check(views.All(v => v.root.gameObject.activeSelf && v.heading.text == "PLAYER " + (v.playerID+1) && v.individualScore.gameObject.activeSelf), "2v2 shows all four headings and contributions");
        Check(views[1].playerKills.text == "4" && views[1].deaths.text == "2" && views[1].enemiesKilled.text == "45" && views[1].distanceTraveled.text == "387m", "All per-player fields render the snapshot");
        Check(views.Where(v => v.teamID == 2).All(v => v.heading.horizontalAlignment == HorizontalAlignmentOptions.Left && v.deaths.horizontalAlignment == HorizontalAlignmentOptions.Left), "Team 2 uses left alignment");
        Check(views[2].root.position.x < views[3].root.position.x, "Team 2 reads Player 3 then Player 4");
        Check(((TMP_Text)Get(ui,"modeText")).color.r < .1f && ((TMP_Text)Get(ui,"countdownText")).color.r < .1f,
            "Mode and countdown use black text inside the Light winner disc");
        foreach (var v in views) { v.individualScore.ForceMeshUpdate(); Check(!v.individualScore.isTextOverflowing, "Full contribution fits Player " + (v.playerID+1)); }
        Capture("2v2-light.png");

        result.mode=GameMode.OneVOne; result.winner=TeamSide.Dark;
        result.hasBonusBreakdown=true; result.regulationLightMilliElectronVolts=10000; result.bonusLightMilliElectronVolts=250;
        result.regulationDarkMilliElectronVolts=12000; result.bonusDarkMilliElectronVolts=350;
        Call(ui,"ApplyResult",result); Call(ui,"ApplyWinnerTheme",result); yield return SettleText();
        Check(views[0].root.gameObject.activeSelf && views[2].root.gameObject.activeSelf && !views[1].root.gameObject.activeSelf && !views[3].root.gameObject.activeSelf, "1v1 uses P1/P3 and hides second players");
        Check(views[0].heading.text == "" && views[2].heading.text == "" && !views[0].individualScore.gameObject.activeSelf && !((GameObject)Get(ui,"individualScoreLabel")).activeSelf, "1v1 hides headings and individual score row");
        Check(views[0].root.anchoredPosition == views[0].soloPosition && views[2].root.anchoredPosition == views[2].soloPosition, "Solo stats align with team totals");
        Check(((TMP_Text)Get(ui,"scoreDarkBreakdownText")).gameObject.activeSelf, "NOVA bonus breakdown remains available");
        Capture("1v1-dark-bonus.png");
        result.mode=GameMode.TwoVTwo; result.winner=TeamSide.Tie; result.hasBonusBreakdown=false;
        result.playerStatistics=null; result.playerContributions=null; result.lightMilliElectronVolts=0; result.darkMilliElectronVolts=0;
        Call(ui,"ApplyResult",result); Call(ui,"ApplyWinnerTheme",result); yield return SettleText();
        Check(views.All(v => v.playerKills.text == "0" && v.individualScore.text == "0" && v.distanceTraveled.text == "0m"), "Missing stats safely show zero and mode can be reapplied");
        Check(((TMP_Text)Get(ui,"winnerText")).text == "TIE", "Tie results retain existing winner behavior");
        Capture("2v2-tie.png");
    }
    static void Capture(string file)
    {
        var camera = Camera.main; var rt = new RenderTexture(1920,1080,24); var old = camera.targetTexture;
        var active = RenderTexture.active; camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var image = new Texture2D(1920,1080,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        File.WriteAllBytes(Output+"/"+file,image.EncodeToPNG()); camera.targetTexture=old; RenderTexture.active=active;
        Object.Destroy(image); rt.Release(); Object.Destroy(rt);
    }
}
#endif
