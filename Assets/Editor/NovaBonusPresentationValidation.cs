#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Disposable Play Mode checks for accepted-award UI, layout, pooling and cleanup.</summary>
public static class NovaBonusPresentationValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int step, checks;
    static double deadline, capturedAt;
    static string screenshot;
    static GameManagerScript match;
    static NovaBonusPresentation hud;
    static bool[] hudStates;
    static bool sceneReady;
    public static string Status { get; private set; } = "Not run";
    static object Read(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
    static void Write(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        checks++;
    }

    public static string StartRun(string screenshotPath)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update -= Tick;
        step = checks = 0; match = null; hud = null;
        screenshot = screenshotPath;
        deadline = EditorApplication.timeSinceStartup + 90;
        GameFlowContext.Instance.SetMode(GameMode.OneVOne);
        sceneReady = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("S-3_NOVA");
        Status = "Running bonus presentation checks";
        EditorApplication.update += Tick;
        return Status;
    }

    static void Tick()
    {
        try
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Validation interrupted by editor/scene change.");
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(Status);
            if (!sceneReady) return;
            if (SceneManager.GetActiveScene().name != "S-3_NOVA")
                throw new InvalidOperationException("Validation interrupted by scene change.");
            if (!match) match = Object.FindFirstObjectByType<GameManagerScript>();
            if (!match) return;
            Write(match, "autoReturnOnAllPlayersInactive", false);
            if (step == 0 && match.Phase == MatchRuntimePhase.Regulation)
            {
                Write(match, "_regulationRemainingSeconds", 600f);
                hud = Object.FindFirstObjectByType<NovaBonusPresentation>();
                hudStates = hud.regulationHudRoots.Select(g => g.activeSelf).ToArray();
                var players = Object.FindFirstObjectByType<NovaStarController>().GetRosteredPlayers();
                var scores = match.ScoreService;
                scores.ResetForMatch(true);
                foreach (var p in players) scores.RegisterPlayer(p);
                for (int i = 0; i < 3; i++) scores.AdvanceTeamAmplifier(1);
                var adapter = Object.FindFirstObjectByType<NovaAnomalyAdapter>();
                Object.FindFirstObjectByType<AnomalyManager>().TriggerAnomaly(adapter.novaCoreAnomalyDefinition, players, 30f, players);
                step = 1;
            }
            var mini = Object.FindFirstObjectByType<NovaCoreMinigame>();
            if (step == 1 && mini && (bool)Read(mini, "_gameplayEnabled"))
            {
                Check(Time.timeScale == 0 && hud.HasSession, "bonus session and unscaled world pause");
                Check(hud.regulationHudRoots.All(g => !g.activeSelf), "normal HUD hidden");
                LayoutChecks();
                var light = Capture(mini, 1, 94001, 3, new Vector2(-6f, 1f));
                var dark = Capture(mini, 2, 94002, 2, new Vector2(6f, 1f));
                InvokeCapture(mini, light); InvokeCapture(mini, dark);
                Check(hud.lightCount.text == "01" && hud.darkCount.text == "01", "duplicate callbacks do not increment counters");
                Check(hud.lightEnergy.text == "75 meV" && hud.darkEnergy.text == "50 meV", "exact energy receipts shown");
                Check(match.ScoreService.LightScoreMilliElectronVolts == 75 && match.ScoreService.DarkScoreMilliElectronVolts == 50,
                    "flat awards ignore x8 Amplifier");
                Check(hud.captureFeedback.ActiveCount == 2, "one effect for each accepted capture");
                var scoring = mini.GetComponent<NovaMinigameScoring>();
                scoring.SetAcceptingInteractions(false);
                Capture(mini, 1, 94003, 3, Vector2.zero);
                scoring.SetAcceptingInteractions(true);
                Capture(mini, 2, 94004, 0, Vector2.zero);
                Check(hud.captureFeedback.ActiveCount == 2 && hud.lightCount.text == "01" && hud.darkCount.text == "01",
                    "closed/invalid awards produce no counters or effects");
                var effects = (IEnumerable)Read(hud.captureFeedback, "pool");
                foreach (var effect in effects)
                    if ((bool)Read(effect, "active"))
                        Check((Transform)Read(effect, "goal") == ((int)Read(effect, "team") == 1 ? hud.lightGoal : hud.darkGoal),
                            "stream targets credited team's actual goal");
                capturedAt = EditorApplication.timeSinceStartup;
                step = 2;
            }
            if (step == 2 && EditorApplication.timeSinceStartup - capturedAt > .22)
            {
                if (!string.IsNullOrEmpty(screenshot)) ScreenCapture.CaptureScreenshot(screenshot);
                step = 3;
            }
            if (step == 3 && EditorApplication.timeSinceStartup - capturedAt > 1.4)
            {
                Check(hud.captureFeedback.ActiveCount == 0, "effects expire while world time is paused");
                int children = hud.captureFeedback.effectsRoot.childCount;
                for (int i = 0; i < 30; i++) hud.ShowCapture(i % 2 + 1, 25, Vector3.zero);
                Check(hud.captureFeedback.ActiveCount == 12, "effect pool bounded at 12 simultaneous captures");
                Check(hud.captureFeedback.effectsRoot.childCount == children, "burst reuses pool without allocating more objects");
                Object.FindFirstObjectByType<AnomalyManager>().CancelCurrentAnomaly();
                step = 4;
            }
            if (step == 4 && !mini)
            {
                Check(match.Phase == MatchRuntimePhase.Regulation && Time.timeScale == 1, "cancel restores regulation/world time");
                Check(!hud.HasSession && !hud.footer.gameObject.activeSelf, "cancel hides bonus footer");
                Check(hud.captureFeedback.ActiveCount == 0, "cancel clears pooled effects");
                Check(hud.regulationHudRoots.Select((g, i) => g.activeSelf == hudStates[i]).All(v => v), "cancel restores previous HUD visibility");
                Status = "PASS: " + checks + " bonus presentation assertions (16:9 Play Mode).";
                EditorApplication.update -= Tick;
            }
        }
        catch (Exception e)
        {
            Status = "FAIL: " + e.Message;
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Debug.LogError(Status);
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "S-3_NOVA") return;
        match = null;
        sceneReady = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    static object Capture(NovaCoreMinigame mini, int team, int id, int quantity, Vector2 position)
    {
        var participants = (IList)Read(mini, "_participants");
        var state = participants.Cast<object>().First(p => ((PlayerControllerScript)Read(p, "controller")).teamID == team);
        var particle = Activator.CreateInstance(typeof(NovaCoreMinigame).GetNestedType("CoreParticle", BindingFlags.NonPublic));
        Write(particle, "lastHitBy", state); Write(particle, "originalSubCount", quantity);
        Write(particle, "scoreId", id); Write(particle, "position", position);
        InvokeCapture(mini, particle);
        return particle;
    }

    static void InvokeCapture(NovaCoreMinigame mini, object particle) =>
        typeof(NovaCoreMinigame).GetMethod("AwardParticleMass", Flags).Invoke(mini, new[] { particle });

    static void LayoutChecks()
    {
        Canvas.ForceUpdateCanvases();
        var grid = hud.arenaBounds.Grid;
        float bottom = Screen.height;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                bottom = Mathf.Min(bottom, hud.worldCamera.WorldToScreenPoint(grid.transform.TransformPoint(
                    new Vector3(grid.size.x * .5f * x, grid.size.y * .5f * y, 0))).y);
        var corners = new Vector3[4]; hud.footer.GetWorldCorners(corners);
        Check(corners.All(p => p.y < bottom && p.x >= 0 && p.x <= Screen.width && p.y >= 0), "footer bounds below field and inside screen");
        foreach (var text in hud.footer.GetComponentsInChildren<TMP_Text>(true))
        {
            text.ForceMeshUpdate(true);
            Check(!text.isTextOverflowing, text.name + " fits its text bounds");
        }
    }
}
#endif
