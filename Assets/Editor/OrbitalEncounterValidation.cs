#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Orbital;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class OrbitalEncounterValidation
{
    private const string Folder = OrbitalEncounterSetup.Folder, Key = "OrbitalEncounterValidation.Run";
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static float resume;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    private static readonly MethodInfo TickTimeline = typeof(EnemyDirector).GetMethod("TickTimeline", BindingFlags.NonPublic | BindingFlags.Instance);
    static OrbitalEncounterValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/ORBITAL/Validate Encounter Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != OrbitalLevelSetup.ScenePath)
            throw new InvalidOperationException("Open ORBITAL in Edit Mode.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            frame = 0; resume = 0; deadline = EditorApplication.timeSinceStartup + 240;
            results.Clear(); errors.Clear(); owned.Clear();
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Update;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Application.runInBackground = SessionState.GetBool(Key + "Background", true); SessionState.SetBool(Key, false); }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Update()
    {
        if (!EditorApplication.isPlaying || routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            if (Time.frameCount <= frame || Time.time < resume) return;
            if (routine.MoveNext()) { frame = Time.frameCount; resume = Time.time + (routine.Current is float delay ? delay : 0); }
            else Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Update; Application.logMessageReceived -= Log; routine = null;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
        Debug.Log("[ORBITAL encounters] " + (passed ? "Validation PASSED" : "Validation FAILED: " + error));
        foreach (var item in owned) if (item) Object.Destroy(item);
        Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        results.Add("PASS " + label); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Step(EnemyDirector director, float seconds)
    { for (float t = 0; t < seconds; t += .05f) TickTimeline.Invoke(director, new object[] { .05f }); }

    private static IEnumerator Checks()
    {
        var scene = SceneManager.GetActiveScene();
        var director = OrbitalEncounterSetup.InScene<EnemyDirector>(scene);
        var cloud = OrbitalEncounterSetup.InScene<OrbitalProbabilityCloud>(scene);
        var pair = OrbitalEncounterSetup.InScene<AmplifierResonanceSpawner>(scene);
        var score = OrbitalEncounterSetup.InScene<MatchScoreService>(scene);
        var source = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        string sourceJson = EditorJsonUtility.ToJson(source);
        Check(director && director.isActiveAndEnabled && director.encounterTimeline == source, "ORBITAL uses the shared Enemy Lab timeline on its active Director");
        Check(Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None).Count(d => d.gameObject.scene == scene && d.isActiveAndEnabled) == 1,
            "Exactly one enemy Director owns ORBITAL spawning");
        Check(!Object.FindFirstObjectByType<EnemyLab>() && !Object.FindFirstObjectByType<EnemyEncounterLab>() && !director.SimulationRoot,
            "Playable level uses normal match players and enemy scope, without Lab automation");
        Check(director.waitForScoring && director.previewCue == -1 && !director.placementRegion,
            "Full timeline follows the match clock with independent enemy placement");
        Check(director.arenaLayout.gameplayHeight == 0 && director.arenaLayout.allowResonanceAndAmplifierOverlap &&
            director.arenaLayout.spawnOverlapRoots.Contains(cloud.transform), "ORBITAL permits environmental overlaps on the Y=0 gameplay plane");
        var composer = EnemyEncounterComposer.ShowForDirector(director);
        Check(composer.Director == director, "Composer binds directly to the playable scene Director");
        if (!score.IsScoringOpen) { yield return .2f; Check(director.GameplayAge == 0 && director.TotalSpawned == 0, "No enemy clock or spawns before match scoring opens"); }
        while (!score.IsScoringOpen || !score.IsChainClockRunning) yield return null;
        var players = PlayerControllerScript.ActivePlayers.Where(p => p && p.gameObject.scene == scene && !p.IsPseudoPlayer).ToArray();
        Check(players.Length >= 2 && players.All(p => p.gameObject.activeInHierarchy), "Normal roster remains active for player control");
        // Keep the autonomous regression alive; this only applies to this Play Mode run.
        foreach (var player in players) player.HitAccepted += hit => { if (player) player.ApplyExternalMassDelta(hit.massLost01); };
        Check(cloud.IsHazardActive && pair.isActiveAndEnabled, "Cloud and paired spawner run alongside the match");
        director.timelinePaused = true;
        float pairDeadline = Time.time + 18;
        while ((!pair.ActivePattern || !pair.ActiveCore) && Time.time < pairDeadline) yield return null;
        Check(pair.ActivePattern && pair.ActiveCore, "Resonance and Amplifier finish spawning while the enemy timeline is paused");
        var pattern = pair.ActivePattern; var core = pair.ActiveCore;
        var fixtureRoot = Own(new GameObject("ORBITAL overlap validation"));
        var fixture = fixtureRoot.AddComponent<EnemyDirector>(); fixture.enabled = false;
        fixture.spawnProfile = director.spawnProfile; fixture.arenaBounds = director.arenaBounds; fixture.arenaLayout = director.arenaLayout;
        fixture.enemyRoot = fixtureRoot.transform; fixture.spawnBlockMask = director.spawnBlockMask;
        fixture.borderBufferWorld = director.borderBufferWorld; fixture.spawnCheckRadiusWorld = director.spawnCheckRadiusWorld;
        fixture.minDistanceFromPlayers = director.minDistanceFromPlayers;
        var template = source.cues[0].formation.slots[0];
        bool Clear(Vector3 point, out string reason)
        {
            var type = typeof(EnemyDirector).GetNestedType("FormationMember", BindingFlags.NonPublic);
            var member = Activator.CreateInstance(type);
            type.GetField("slot").SetValue(member, template);
            type.GetField("pose").SetValue(member, new EnemyArenaLayout.Pose { position = point, clearance = point, rotation = Quaternion.identity });
            object[] args = { member, null, null };
            bool clear = (bool)typeof(EnemyDirector).GetMethod("MemberClear", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fixture, args);
            reason = args[2] as string; return clear;
        }
        Vector3 resonancePoint = default; bool found = false;
        foreach (var shape in pattern.GetComponentsInChildren<Collider>())
        {
            if (!shape.enabled || !shape.gameObject.activeInHierarchy) continue;
            var point = shape.bounds.center; point.y = 0;
            if (!Physics.OverlapSphere(point, .55f, ~0, QueryTriggerInteraction.Collide).Contains(shape) ||
                !fixture.arenaLayout.AllowsSpawnOverlap(shape) || !Clear(point, out _)) continue;
            fixture.arenaLayout.allowResonanceAndAmplifierOverlap = false;
            bool rejected = !fixture.arenaLayout.AllowsSpawnOverlap(shape);
            fixture.arenaLayout.allowResonanceAndAmplifierOverlap = true;
            if (rejected) { resonancePoint = point; found = true; break; }
        }
        Check(found, "Arrival overlaps a real Resonance collider and the explicit permission is opt-in");
        Vector3 corePoint = core.transform.position; corePoint.y = 0;
        Check(Clear(corePoint, out var coreReason), "A live Amplifier permits an enemy arrival at its position: " + coreReason);
        var coreShape = core.GetComponentInChildren<Collider>();
        Check(coreShape && fixture.arenaLayout.AllowsSpawnOverlap(coreShape), "Amplifier colliders have explicit overlap permission regardless of their layer");
        fixture.arenaLayout.allowResonanceAndAmplifierOverlap = false;
        Check(!fixture.arenaLayout.AllowsSpawnOverlap(coreShape), "Other layouts retain the default Amplifier overlap policy");
        fixture.arenaLayout.allowResonanceAndAmplifierOverlap = true;
        Vector3 cloudPoint = new Vector3(0, 0, cloud.cloud.radius * .43f);
        Check(cloud.DensityAt(cloudPoint) > .2f && Clear(cloudPoint, out _), "Enemy placement is valid within the dense Orbital Cloud");
        var blocker = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); blocker.transform.position = cloudPoint;
        blocker.layer = LayerMask.NameToLayer("Obstacle"); blocker.transform.SetParent(cloud.transform, true); Physics.SyncTransforms();
        Check(Clear(cloudPoint, out _), "Cloud-owned collider hierarchies cannot reject enemy arrivals");
        blocker.transform.SetParent(null, true); Physics.SyncTransforms();
        Check(!Clear(cloudPoint, out _), "Unrelated solid obstacles still reject enemy arrivals");
        blocker.layer = LayerMask.NameToLayer("NoSpawnZone"); Physics.SyncTransforms();
        Check(!Clear(cloudPoint, out _), "Explicit no-spawn zones still reject enemy arrivals");
        blocker.SetActive(false);
        Check(!Clear(players[0].transform.position, out var playerReason) && playerReason == "Player occupies arrival", "Player occupancy remains protected");
        var form = Own(ScriptableObject.CreateInstance<EnemyFormation>()); form.warningSeconds = .2f;
        var slot = new EnemyFormation.Slot { enemy = template.enemy, telegraph = template.telegraph, region = "Arena", facing = Vector2.up };
        form.slots.Add(slot);
        var one = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>());
        one.cues.Add(new EnemyEncounterTimeline.Cue { label = "Environment overlap", formation = form, arrivalSeconds = .4f, allowedLateness = 2 });
        fixture.encounterTimeline = one;
        foreach (var point in new[] { resonancePoint, corePoint, cloudPoint })
        {
            var grid = fixture.arenaBounds.Grid.transform;
            var local = grid.InverseTransformPoint(point); var half = fixture.arenaBounds.Current.halfSizeLocal;
            float sign = Vector3.Dot(fixture.arenaBounds.Current.axisY_WS, Vector3.forward) >= 0 ? 1 : -1;
            slot.position = new Vector2((local.x / half.x + 1) * .5f, (local.y / half.y * sign + 1) * .5f);
            fixture.RestartTimeline(true); Step(fixture, .8f);
            Check(fixture.TotalSpawned == 1 && fixture.CueStates[0].state == "Complete", "Full telegraph and arrival succeed over environment at " + point);
            var enemy = fixtureRoot.GetComponentInChildren<EnemyBase>();
            Check(enemy && (enemy.transform.position - point).sqrMagnitude < .001f, "Overlap arrival stays at the announced Y=0 position");
            fixture.RestartTimeline(true); yield return .1f;
        }
        Object.Destroy(fixtureRoot); yield return .1f;
        Check(pair.ActivePattern == pattern && pair.ActiveCore == core && pair.isActiveAndEnabled && cloud.IsHazardActive,
            "Enemy overlap tests leave the Cloud and active power-up pair running");
        var seen = new HashSet<string>();
        director.TimelineEnemySpawned += enemy => seen.Add(enemy.Definition.id);
        director.timelinePaused = false; Time.timeScale = 2;
        while (director.GameplayAge < 24 && score.IsScoringOpen)
        {
            foreach (var enemy in director.enemyRoot.GetComponentsInChildren<EnemyBase>())
            {
                CheckTargetScope(enemy, players);
                if (enemy.TryGetComponent<CarrierController>(out var carrier) && carrier.TotalLaunched > 0) seen.Add("Carrier children");
            }
            yield return .4f;
        }
        File.WriteAllText(Folder + "/timeline.txt", string.Join("\n", director.CueStates.Select(c => c.label + ": " + c.state + ", " + c.Progress + ", " + c.reason)));
        Check(director.CueStates.Take(2).All(c => c.finished && c.spawned > 0), "Opening Drone and Ranged formations resolve during the real match");
        // Stationary test players do not clear a live 40-enemy wave. Exercise the
        // remaining canonical cues through Composer with fresh space, preserving
        // all authored occupancy rules and the shared timeline asset.
        for (int cue = 2; cue < 6; cue++)
        {
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i]; p.lastActivityTime = Time.time;
                var position = director.arenaLayout.World(new Vector2(i == 0 ? -.42f : .42f, -.15f));
                p.transform.position = position;
                if (p.TryGetComponent<Rigidbody>(out var body)) { body.position = position; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
            }
            Physics.SyncTransforms(); composer.StartPreview(cue);
            while (director.GameplayAge < 12 && score.IsScoringOpen)
            {
                foreach (var enemy in director.enemyRoot.GetComponentsInChildren<EnemyBase>())
                {
                    CheckTargetScope(enemy, players);
                    if (enemy.TryGetComponent<CarrierController>(out var carrier) && carrier.TotalLaunched > 0) seen.Add("Carrier children");
                }
                yield return .4f;
            }
            var state = director.CueStates[0];
            File.AppendAllText(Folder + "/timeline.txt", "\nIsolated " + state.label + ": " + state.state + ", " + state.Progress + ", " + state.reason);
            Check(state.spawned > 0, "Composer plays cue in ORBITAL: " + state.label + " — " + state.reason);
        }
        Check(seen.Count >= 7, "Six enemy types and Carrier children run against the real roster (opening timeline plus isolated later cues): " + string.Join(", ", seen));
        Check(director.CurrentPressure <= source.maxPressure && director.AliveCount <= source.maxAliveTotal, "Shared timeline population limits remain authoritative");
        Check(pair.isActiveAndEnabled && pair.PairsSpawned > 0 && cloud.IsHazardActive, "Environmental systems remain active during the playable encounter sequence");
        director.timelinePaused = true; yield return .1f; float age = director.GameplayAge;
        yield return .3f; Check(director.GameplayAge == age, "Composer pause stops the enemy timeline");
        var rosterIds = players.Select(p => p.GetInstanceID()).ToArray(); var pairBeforeRestart = pair.ActivePattern;
        composer.RestartPlayback(); yield return .1f;
        Check(director.GameplayAge == 0 && director.AliveCount == 0 && players.Select(p => p.GetInstanceID()).SequenceEqual(rosterIds) && pair.ActivePattern == pairBeforeRestart,
            "Composer restart clears only timeline enemies and leaves players/environment intact");
        director.timelinePaused = false;
        score.CloseScoring(); yield return .2f;
        Check(director.GameplayAge == 0 && director.TimelinePendingCount == 0, "Match close prevents further enemy arrivals");
        Check(sourceJson == EditorJsonUtility.ToJson(source), "Shared authored timeline is unchanged by validation");
        Check(errors.Count == 0, "No Unity errors or exceptions during ORBITAL validation");
    }
    private static void CheckTargetScope(EnemyBase enemy, PlayerControllerScript[] players)
    {
        if (!enemy || enemy.IsDead) return;
        if (enemy.SimulationRoot || players.Any(p => !enemy.SharesSimulationWith(p)))
            throw new Exception("Playable enemy cannot target the normal match roster");
    }
}
#endif
