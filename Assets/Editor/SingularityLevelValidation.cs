#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Player;
using Massive.Singularity;
using Massive.PowerUps;
using UnityEditor.SceneManagement;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Disposable integration fixtures in the saved SINGULARITY scene, using its real encounters.</summary>
[InitializeOnLoad]
public static class SingularityLevelValidation
{
    const string Key = "MASSIVE.SingularityLevelValidation", Folder = SingularityLevelSetup.Folder;
    static readonly List<string> checks = new(), errors = new();
    static readonly Dictionary<EnemyDirector.SlotStatus, Vector3> warned = new();
    static EnemyDirector director;
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool background;
    static string Report => Folder + (SessionState.GetInt(Key + ".mode", 0) == 0 ? "/one-v-one.txt" : "/two-v-two.txt");

    static SingularityLevelValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool twoOnly = File.ReadAllText(Folder + "/test.request").Trim() == "2v2";
            File.Delete(Folder + "/test.request");
            if (twoOnly && SceneManager.GetActiveScene().path == SingularityLevelSetup.ScenePath)
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            RunModes(twoOnly ? 1 : 0);
        };
    }
    private static Object Ref(Object owner, string property) => new SerializedObject(owner).FindProperty(property).objectReferenceValue;
    private static Transform Owner(Object obj) => obj is GameObject go ? go.transform : (obj as Component)?.transform;

    public static string CheckScene(Scene scene)
    {
        int baseline = checks.Count;
        string[] names = { "00_Systems", "01_Presentation", "02_Arena", "GameplayObjects", "04_UI", "90_EditorOnly" };
        Check(scene.GetRootGameObjects().Select(g => g.name).SequenceEqual(names), "Six ordered universal roots");
        var all = SingularityLevelSetup.All<Transform>(scene);
        var context = SingularityLevelSetup.All<LevelSceneContext>(scene).Single();
        var score = SingularityLevelSetup.All<MatchScoreService>(scene).Single();
        var domains = SingularityLevelSetup.All<SingularityArenaDomains>(scene).Single();
        var surface = domains.surface;
        var grid = SingularityLevelSetup.All<SingularityGridRenderer>(scene).Single();
        var portal = SingularityLevelSetup.All<SingularityBlackHolePortal>(scene).Single();
        var bridge = SingularityLevelSetup.All<SingularityAmplifierEncounter>(scene).Single();
        Check(context.level && context.level.levelTitle == "SINGULARITY" && context.match && context.roster && context.inputManagerPrefab, "Identity, match, roster and direct-launch input bootstrap");
        Check(context.level.SceneName == System.IO.Path.GetFileNameWithoutExtension(SingularityLevelSetup.ScenePath) && context.level.levelNumber == 7, "Catalog launches the current S-8_SINGULARITY scene as STAGE_007");
        Check(context.stageTitleText.text == "SINGULARITY" && context.stageNumberText.text == "STAGE_007" && context.stage.stageId == "STAGE_007", "HUD and stage identity agree with the catalog");
        Check(Ref(context.match, "scoreService") == score && Ref(score, "profile") != null && Ref(context.match, "scoreEconomyProfile") == score.Profile, "Single explicit score authority and shared economy");
        Check(Ref(context.match, "regulationFinale") == null && SingularityLevelSetup.All<NovaStarController>(scene).Length == 0, "Ordinary timed ending without NOVA-specific content");
        Check(context.spawners && context.spawners.transform.parent.CompareTag("GameplayObjects"), "Phase-gated power-up spawning");
        foreach (var pair in new[] {
            new[]{"00_Systems/MatchRuntime","MatchRuntime"}, new[]{"GameplayObjects/Players","Players_Gameplay"},
            new[]{"02_Arena/Goals","TeamGoals_Planar"}, new[]{"GameplayObjects/Encounters/AmplifierResonance","AmplifierResonanceEncounter"},
            new[]{"04_UI/MatchHUD","MatchHUD"} })
        {
            var root = all.Single(t => AnimationUtility.CalculateTransformPath(t, null) == pair[0]);
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) == "Assets/Prefabs/Levels/" + pair[1] + ".prefab", "Shared module " + pair[1]);
        }
        Check(SingularityLevelSetup.All<GameManagerScript>(scene).Length == 1 && SingularityLevelSetup.All<PlayerRosterController>(scene).Length == 1, "No duplicate match owner or roster");
        Check(grid.surface == surface && grid.blackHole == portal && portal.surface == surface && portal.players.Length == 4, "Folded grid and black-hole player routing retained");
        Check(bridge.surface == surface && bridge.grid == grid && bridge.portal == portal && bridge.spawner.scoreService == score && bridge.spawner.waitForScoring, "Original folded Core/Resonance adapter remains wired to shared scoring");
        Check(bridge.spawner.spawnRegion.explicitPlane == bridge.frontPlacementPlane && bridge.spawner.spawnRegion.explicitPlaneSize == new Vector2(surface.Width, surface.FrontHeight), "Core patterns use the flat front face");
        foreach (var player in SingularityLevelSetup.All<PlayerControllerScript>(scene))
        {
            var adapter = player.GetComponent<SingularityPlayerAdapter>(); var aoe = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
            Check(adapter && adapter.Surface == surface && Ref(context.roster, "player" + (player.playerID + 1)) == player.gameObject, "Folded player and explicit roster slot " + player.name);
            Check(Ref(aoe,"owner") == player && Ref(aoe,"attackController") == player.GetComponent<PlayerAttackController>() && Ref(player.GetComponent<PlayerRepulsorGridPulse>(),"foldedGrid") == grid, "Shared Repulsor and folded ripple bindings " + player.name);
        }
        Check(SingularityLevelSetup.All<MatchTimerPresenter>(scene).Length == 1 && Ref(SingularityLevelSetup.All<MatchTimerPresenter>(scene).Single(), "gameManager") == context.match && SingularityLevelSetup.All<TeamAmplifierToastPresenter>(scene).Length == 1, "One timer and one Amplifier toast presenter");
        Check(SingularityLevelSetup.All<Rewired.InputManager>(scene).Length == 0, "Persistent input is bootstrapped rather than duplicated in the scene");
        Check(!SingularityLevelSetup.All<SingularityGameplaySession>(scene).Single().enabled, "Sandbox free-play session remains disabled");
        Check(!domains.frontDomain.enabled && !domains.projectedDomain.enabled && !domains.frontDomain.registerAsDefault && !domains.projectedDomain.registerAsDefault, "Placement-only domains create no second grid simulation or renderer");
        Check(domains.frontDomain.size == new Vector2(surface.Width, surface.FrontHeight) && domains.projectedDomain.size == new Vector2(surface.Width, surface.ProjectedHeight), "Spawn and mask domains separately follow front and full folded extents");
        var mask = domains.projectedDomain.GetComponent<ArenaOutsideMask>();
        Check(mask && mask.enabled && mask.MaskShader && mask.Arena.Grid == domains.projectedDomain, "Automatic exterior mask uses full folded silhouette bounds");
        var pickups = SingularityLevelSetup.All<PowerUpSpawner>(scene).Single();
        Check(pickups.vectorGrid == domains.frontDomain && bridge.blackHoleExclusion.GetComponent<PowerUpNoSpawnZone>(), "Global pickups use the front face and black-hole exclusion");
        Check(domains.enemies && domains.enemies.arena.Grid == domains.frontDomain && domains.enemies.exclusions.Contains(bridge.blackHoleExclusion), "Enemy placement uses local front-face bounds and stable exclusion");
        Check(new SerializedObject(context.match).FindProperty("disableGameplayOnEnd").boolValue && portal.transform.IsChildOf(context.spawners.transform.parent), "Gameplay and portal shut down with the match");
        Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
        int references = 0;
        foreach (var c in SingularityLevelSetup.All<Component>(scene).Where(c => c))
        {
            var p = new SerializedObject(c).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (!p.objectReferenceValue && p.objectReferenceInstanceIDValue != 0) throw new InvalidOperationException("Broken reference " + c.name + "." + p.propertyPath);
                var owner = Owner(p.objectReferenceValue);
                if (owner && owner.gameObject.scene.IsValid() && owner.gameObject.scene != scene) throw new InvalidOperationException("Cross-scene reference " + c.name + "." + p.propertyPath);
                references++;
            }
        }
        Check(true, "All " + references + " serialized reference slots are valid and scene-local");
        return (checks.Count - baseline) + " composition checks passed; " + references + " serialized reference slots inspected.";
    }

    [MenuItem("MASSIVE/Levels/Validate SINGULARITY Integration (1v1 and 2v2)")]
    public static void Run() => RunModes(0);
    static void RunModes(int firstMode)
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != SingularityLevelSetup.ScenePath)
            throw new InvalidOperationException("Open the saved SINGULARITY scene in Edit Mode.");
        Directory.CreateDirectory(Folder); checks.Clear(); errors.Clear();
        director = SingularityLevelSetup.All<EnemyDirector>(scene).Single();
        var layout = SingularityLevelSetup.All<EnemyArenaLayout>(scene).Single();
        checks.Add(CheckScene(scene));
        Check(director.isActiveAndEnabled && director.waitForScoring && director.previewCue == -1, "One active, match-gated Director plays the full timeline");
        Check(director.encounterTimeline && director.spawnProfile && director.spawnProfile.enabled, "Timeline and enabled spawn profile assigned");
        Check(director.arenaLayout == layout && layout.arena == director.arenaBounds && director.enemyRoot &&
            director.enemyRoot.IsChildOf(director.transform), "Arena and enemy container belong to SINGULARITY");
        Check(director.anomalyManager == null &&
            director.resonanceSpawner == SingularityLevelSetup.All<AmplifierResonanceSpawner>(scene).Single(), "Local Resonance binding without extra anomaly rules");
        Check(director.placementRegion == null && layout.allowResonanceAndAmplifierOverlap, "Shared timeline overlap policy retained");
        Check(layout.sockets.Count(s => s.enabled && s.useRange && EnemyArenaLayout.ValidWallRange(s)) == 4 &&
            layout.sockets.Count(s => s.enabled && s.id.StartsWith("Side")) == 4, "Four wall quadrants and four goal-safe side fallback mounts");
        var forms = Forms(director.encounterTimeline);
        Check(forms.All(f => f.slots.All(s => s.enemy && s.enemy.prefab && s.telegraph &&
            (string.IsNullOrEmpty(s.socket) ? layout.regions.Any(r => r.id == s.region) : layout.sockets.Any(m => m.enabled && m.id == s.socket)))),
            "Every formation, variant and fallback has its enemy, warning, region or wall mount");
        File.WriteAllText(Folder + "/authored.txt", string.Join("\n", checks));
        // Concurrent file edits must not trigger package reinitialization halfway
        // through a fixture. Release the refresh hold on completion or manual stop.
        AssetDatabase.DisallowAutoRefresh(); SessionState.SetBool(Key + ".refreshHeld", true);
        StartMode(firstMode);
    }
    static EnemyFormation[] Forms(EnemyEncounterTimeline timeline) => timeline.cues
        .SelectMany(c => new[] { c.formation, c.fallback }.Concat(c.variants)).Where(f => f).Distinct().ToArray();
    static void StartMode(int mode)
    {
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".mode", mode);
        SessionState.SetBool(Key + ".next", false);
        EnemyEncounterPreviewMode.Request(mode == 1); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); warned.Clear(); frame = -1;
            background = Application.runInBackground; Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 320;
            Application.logMessageReceived += Log; routine = Nested(Checks()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".next", false)) EditorApplication.delayCall += () => StartMode(1);
            else if (SessionState.GetBool(Key + ".refreshHeld", false))
            { SessionState.SetBool(Key + ".refreshHeld", false); AssetDatabase.AllowAutoRefresh(); }
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static IEnumerator Nested(IEnumerator source)
    {
        while (source.MoveNext())
            if (source.Current is IEnumerator nested)
            { var runner = Nested(nested); while (runner.MoveNext()) yield return runner.Current; }
            else yield return source.Current;
    }
    static IEnumerable<EnemyDirector.SlotStatus> Slots() => director.CueStates
        .SelectMany(c => c.slots.Concat(c.reinforcements?.slots ?? new List<EnemyDirector.SlotStatus>()));
    static EnemyBase[] Alive() => Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
        .Where(e => e.Director == director && !e.IsDead).ToArray();
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            frame = Time.frameCount;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Fixture timed out");
            if (!routine.MoveNext()) { Finish(null); return; }
            if (!director) return;
            foreach (var s in Slots().Where(s => s.announced))
            {
                if (warned.TryGetValue(s, out var p) && Vector3.Distance(p, s.position) > .0001f)
                    throw new Exception("An announced warning moved");
                warned[s] = s.position;
            }
        }
        catch (Exception e) { Finish(e); }
    }
    static string Cues() => director ? string.Join("\n", director.CueStates.Select(c => c.label + ": " + c.state + " " + c.Progress + " " + c.reason +
        " " + string.Join("; ", c.slots.Where(s => !string.IsNullOrEmpty(s.reason)).Select(s => s.reason)))) : "";
    static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label + "\n" + Cues());
        checks.Add("PASS " + label);
        // Authoring scans must not overwrite a completed Play Mode report.
        if (EditorApplication.isPlaying) File.WriteAllText(Report, "RUNNING\n" + string.Join("\n", checks));
    }
    static void Finish(Exception error)
    {
        Massive.Singularity.Editor.SingularityCorePortalPlayValidation.Cancel();
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Application.runInBackground = background; Time.timeScale = 1;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Report, (passed ? "PASSED" : "FAILED") + $" ({checks.Count} checks)\n" + string.Join("\n", checks) + "\n" + error +
            "\nConsole errors: " + errors.Count + "\n" + string.Join("\n", errors));
        SessionState.SetBool(Key + ".next", passed && SessionState.GetInt(Key + ".mode", 0) == 0);
        Debug.Log("SINGULARITY INTEGRATION " + (passed ? "PASSED" : "FAILED")); EditorApplication.isPlaying = false;
    }
    static IEnumerator Wait(float seconds)
    { float end = Time.unscaledTime + seconds; while (Time.unscaledTime < end) yield return null; }
    static IEnumerator Until(Func<bool> condition, float seconds, string label)
    {
        float end = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < end) yield return null;
        Check(condition(), label);
    }
    static IEnumerator FoldedChecks(PlayerControllerScript[] players, GameManagerScript gm, AmplifierResonanceSpawner pair)
    {
        var scene = SceneManager.GetActiveScene();
        var bridge = Object.FindFirstObjectByType<SingularityAmplifierEncounter>();
        var surface = bridge.surface; var grid = bridge.grid; var portal = bridge.portal;
        var player = players.First(p => p.teamID == 1); var adapter = player.GetComponent<SingularityPlayerAdapter>();
        var home = adapter.SurfacePosition; var originalScale = player.transform.localScale; int life = player.LifeSequence;
        var domains = Object.FindFirstObjectByType<SingularityArenaDomains>();
        var targetEnemy = Alive().First();
        adapter.SetSurfacePosition(new Vector2(-9, surface.FrontHeight * .5f));
        yield return null;
        Check(targetEnemy.CanTarget(player), "Front-face player is a valid shared enemy target");
        adapter.SetSurfacePosition(new Vector2(-9, (surface.RearStart + surface.BottomStart) * .5f)); yield return null;
        Check(!targetEnemy.CanTarget(player) && adapter.RearWeight > .99f, "Rear-face player is not pursued through the front-face enemy arena");
        var projected = adapter.RenderWorldPosition;
        Check(Mathf.Abs(projected.y - (surface.transform.position.y - surface.Depth)) < .1f && surface.EvaluateBrightness(adapter.SurfacePosition.y) < 1, "Rear mapping and dimming remain active");
        adapter.SetSurfacePosition(new Vector2(-9, surface.LoopLength + .2f)); yield return null;
        Check(Mathf.Abs(adapter.SurfacePosition.y - .2f) < .1f, "Player wraps through the periodic surface seam");
        adapter.SetSurfacePosition(home);
        var pulse = player.GetComponent<PlayerRepulsorGridPulse>(); grid.ClearRipples(); pulse.TriggerPulse(); yield return null; yield return null;
        Check(grid.ActiveRippleCount > 0, "Shared Repulsor feedback produces a ripple on the folded grid");
        var pickups = Object.FindFirstObjectByType<PowerUpSpawner>();
        PowerUpPickup pickup = PowerUpPickup.ActivePickups.FirstOrDefault(p => p && p.gameObject.scene == scene && p.CountsTowardSpawnLimit);
        for (int i = 0; i < 40 && !pickup; i++) { pickup = pickups.TrySpawnOne(); yield return null; }
        Check(pickup && domains.enemies.arena.ContainsWorldPoint(pickup.transform.position) && (bridge.blackHoleExclusion.ClosestPoint(pickup.transform.position) - pickup.transform.position).sqrMagnitude > .01f, "Global power-up spawns on the safe front face outside the black hole");
        int transfers = portal.TeleportCount;
        adapter.SetSurfacePosition(portal.FaceToSurface(Vector2.left * portal.EffectiveCaptureRadius * .8f, false));
        Physics.SyncTransforms();
        yield return Until(() => portal.IsInTransit(adapter), 3, "Player enters the original black-hole sequence");
        Check(player.IsSingularityTransitControlled && !targetEnemy.CanTarget(player), "Portal transit keeps its movement lease and enemy-target exclusion");
        yield return Until(() => portal.TeleportCount > transfers && !portal.IsInTransit(adapter), 5, "Player exits the black hole on the other face");
        Check(adapter.RearWeight > .99f && player.LifeSequence == life && !player.temporarilyEliminated && !player.IsSingularityTransitControlled && player.transform.localScale == originalScale, "Player transit preserves life, restores scale and releases control");
        adapter.SetSurfacePosition(home);
        ScreenCapture.CaptureScreenshot(Folder + "/folded-gameplay-" + SessionState.GetInt(Key + ".mode", 0) + ".png"); yield return null;
        // The existing Core suite also tests timeout and cancellation during both transit phases.
        director.enabled = false;
        typeof(GameManagerScript).GetField("_regulationRemainingSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(gm, 240f);
        Massive.Singularity.Editor.SingularityCorePortalPlayValidation.Begin();
        yield return Until(() => Massive.Singularity.Editor.SingularityCorePortalPlayValidation.Result.StartsWith("PASS") || Massive.Singularity.Editor.SingularityCorePortalPlayValidation.Result.StartsWith("FAIL"), 100, "Existing live Core portal regression suite finishes");
        var coreResult = Massive.Singularity.Editor.SingularityCorePortalPlayValidation.Result;
        File.WriteAllText(Folder + "/core-portal-" + SessionState.GetInt(Key + ".mode", 0) + ".txt", coreResult);
        Check(coreResult.StartsWith("PASS"), "Core folding, brightness, portal transit, goal capture and cancellation: " + coreResult.Split('\n')[0]);
        director.enabled = true;
    }

    static IEnumerator Checks()
    {
        director = Object.FindFirstObjectByType<EnemyDirector>();
        var gm = Object.FindFirstObjectByType<GameManagerScript>();
        // An unattended fixture has no cabinet input. Keep the real match running
        // without changing the saved scene's ordinary inactivity/Attract behavior.
        var matchSettings = new SerializedObject(gm);
        matchSettings.FindProperty("allPlayersInactiveHoldSeconds").floatValue = 600;
        matchSettings.ApplyModifiedPropertiesWithoutUndo();
        var scores = gm.ScoreService;
        var context = Object.FindFirstObjectByType<LevelSceneContext>();
        var pair = Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        var timeline = director.encounterTimeline;
        var paths = Forms(timeline).Select(AssetDatabase.GetAssetPath).Append(AssetDatabase.GetAssetPath(timeline))
            .Append(AssetDatabase.GetAssetPath(director.spawnProfile)).Append(SingularityLevelSetup.ReferenceScene).Append(SingularityLevelSetup.ScenePath);
        var hashes = paths.Distinct().ToDictionary(p => p, p => Hash128.Compute(File.ReadAllText(p)));
        bool two = SessionState.GetInt(Key + ".mode", 0) == 1;
        Check(GameFlowContext.Instance.IsTwoVTwo == two, "Requested match mode applied before scene Awake");
        while (gm.Phase != MatchRuntimePhase.Regulation)
        {
            if (director.GameplayAge > 0 || director.TotalSpawned > 0) throw new Exception("Enemies advanced before regulation");
            yield return null;
        }
        yield return null;
        Check(director.GameplayAge < .2f, "Countdown held enemy clock and arrivals");
        var players = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Where(p => !p.IsPseudoPlayer).ToArray();
        Check(players.Length == (two ? 4 : 2) && Rewired.ReInput.isReady, "Real roster and input initialized (players " + players.Length +
            ", input ready " + Rewired.ReInput.isReady + ", managers " + Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length + ")");
        Check(Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length == 1 && context.roster.IsRosterReady, "Scene-owned bootstrap initializes exactly one input manager and ready roster");
        Check(Object.FindObjectsByType<MatchScoreService>(FindObjectsSortMode.None).Length == 1 && gm.RegulationDurationSeconds == scores.Profile.RegulationDurationSeconds, "One score service uses the shared production clock");
        Check(context.stageTitleText.text == "SINGULARITY" && context.stageNumberText.text == "STAGE_007" && context.spawners.activeInHierarchy, "SINGULARITY identity and phase-gated spawners are active");
        var timer = Object.FindFirstObjectByType<MatchTimerPresenter>();
        var timerText = (TMPro.TMP_Text)Ref(timer, "timerText");
        Check(timerText && !string.IsNullOrWhiteSpace(timerText.text), "Timer renders match time");
        var limits = EnemyEncounterScaling.Limits.Capture(timeline, two);
        Check(director.TimelineIsTwoVTwo == two && director.TimelineLimits.total == limits.total && director.TimelineLimits.pressure == limits.pressure,
            "Population limits reflect match mode (total " + limits.total + ")");
        Check(Mathf.Abs(director.TimelineDurationSeconds - gm.EncounterRegulationDurationSeconds) < .01f, "Timeline fits the actual shared regulation duration");
        yield return Until(() => director.ActiveTelegraphCount > 0 && director.TotalSpawned == 0, 5, "Visible warnings precede first enemies");
        yield return Until(() => Alive().Length > 0, 6, "First authored wave arrives through the live Director");
        Check(Alive().All(e => e.transform.IsChildOf(director.enemyRoot) && e.GetComponent<EnemyScoreReward>() && !e.IsPaused),
            "Spawned enemies have local ownership, defeat scoring and active AI");
        var victim = Alive().First(); var player = players.First(p => p.teamID == 1);
        long before = scores.GetTeamScore(player.teamID);
        victim.TakeDamage(victim.HealthRemaining + 1, EnemyDamageSource.Sword, player);
        long after = scores.GetTeamScore(player.teamID);
        victim.TakeDamage(1000, EnemyDamageSource.Sword, player);
        Check(after > before && scores.GetTeamScore(player.teamID) == after, "Player-attributed defeat credits central score exactly once");
        yield return Until(() => director.GameplayAge >= 10 * gm.RegulationDurationSeconds / 120f, 20, "Opening wave and warnings complete");
        Check(two ? director.CueStates[0].reinforcements?.spawned > 0 : director.CueStates[0].reinforcements == null,
            "Opening wave uses appropriate 1v1/2v2 reinforcement policy");
        scores.SetScoringState(true, false); yield return null; yield return null;
        float age = director.GameplayAge; int count = director.TotalSpawned;
        yield return Wait(.4f);
        Check(Mathf.Abs(director.GameplayAge - age) < .0001f && director.TotalSpawned == count && Alive().All(e => e.IsPaused), "Paused match clock freezes enemies and arrivals");
        scores.SetScoringState(true, true); yield return null; yield return null;
        Check(director.GameplayAge > age && Alive().All(e => !e.IsPaused), "Enemies resume with the match clock");
        yield return Until(() => pair.ActiveCore && pair.ActivePattern && Alive().Length > 0, 35,
            "Enemies coexist with the SINGULARITY, Amplifier Core and Resonance pattern");
        ScreenCapture.CaptureScreenshot(Folder + (two ? "/two-v-two.png" : "/one-v-one.png")); yield return Wait(.15f);
        // Exercise the other families without changing the shared timeline or waiting
        // for a full match. Real Update, warning duration, placement and AI still run.
        for (int i = 1; i <= 5; i++)
        {
            warned.Clear(); director.previewCue = i; director.RestartTimeline(true); yield return null;
            yield return Until(() => director.TotalSpawned > 0, 10, "Runtime cue: " + timeline.cues[i].label);
            yield return Wait(.25f);
            Check(Alive().Any(e => e.Definition == director.CueStates[0].formation.slots[0].enemy), "Correct enemy family for " + timeline.cues[i].label);
        }
        Check(warned.Count > 0, "Announced warning positions remained fixed throughout fixtures");
        yield return FoldedChecks(players, gm, pair);

        director.previewCue = 0; director.RestartTimeline(true); yield return null;
        yield return Until(() => director.TotalSpawned > 0 && director.TimelinePendingCount > 0, 8, "Closing fixture has live enemies and pending arrivals");
        scores.CloseScoring(); yield return null; yield return null;
        age = director.GameplayAge; count = director.TotalSpawned; yield return Wait(.4f);
        Check(director.TimelinePendingCount == 0 && director.ActiveTelegraphCount == 0 && director.TotalSpawned == count &&
            Mathf.Abs(director.GameplayAge - age) < .0001f && Alive().All(e => e.IsPaused), "Closing scoring cancels warnings and freezes remaining enemies");
        scores.SetScoringState(true, true);
        typeof(GameManagerScript).GetField("_regulationRemainingSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(gm, .05f);
        yield return Until(() => gm.Phase == MatchRuntimePhase.Resolving, 3, "Regulation expiry enters resolving through the real match lifecycle");
        Check(!context.spawners.activeInHierarchy && !GameObject.Find("GameplayObjects") && !scores.IsScoringOpen, "Match end shuts down gameplay and closes scoring while HUD survives");
        var result = GameFlowContext.Instance.LastMatchResult;
        Check(GameFlowContext.Instance.HasLastMatchResult && result.stageTitle == "SINGULARITY" && result.stageNumber == 7 && result.mode == (two ? GameMode.TwoVTwo : GameMode.OneVOne)
            && result.lightMilliElectronVolts == scores.LightScoreMilliElectronVolts && result.darkMilliElectronVolts == scores.DarkScoreMilliElectronVolts,
            "Exact score totals and SINGULARITY identity reach the match result");
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.PostGameScene, 6, "Shared PostGame scene loads");
        Check(Object.FindFirstObjectByType<PostGameScreenController>() && GameFlowContext.Instance.HasLastMatchResult, "PostGame presenter receives the match result");
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.ChooseModeScene, 35, "PostGame returns to Attract through its normal timeout");
        Check(Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length == 1, "One persistent input manager survives navigation");
        Check(hashes.All(h => Hash128.Compute(File.ReadAllText(h.Key)) == h.Value), "Saved SINGULARITY, LATTICE and shared encounter assets unchanged by Play Mode fixtures");
        Check(errors.Count == 0, "No runtime errors, exceptions or assertions");
    }
}
#endif
