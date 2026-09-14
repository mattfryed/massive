#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DronePrototypeValidation
{
    [MenuItem("MASSIVE/Enemies/Run Swarm and Telegraph Validation")]
    public static void StartEnemyUpgradeValidation()
    { Start(); SessionState.SetBool(Key + "EnemyUpgradeOnly", true); }

    private static EnemyDirector IndividualDirector(ArenaBoundsFromVectorGrid bounds, float lead = .8f)
    {
        var director = TelegraphDirector(bounds, lead);
        director.spawnProfile.batches[0].telegraphMode = EnemyBatchTelegraphMode.PerEnemy;
        director.spawnProfile.batches[0].intervalWithinBatch = .2f;
        return director;
    }

    private static EnemyDirector DysonDirector(ArenaBoundsFromVectorGrid bounds)
    {
        var director = TelegraphDirector(bounds);
        var source = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(DronePrototypeSetup.ProfilePath);
        var rule = source.rules.First(r => r.enemy.defeatRewardKey == "ENEMY_DYSON_DEFEAT");
        rule.minSecondsSinceMatchStart = 0f; rule.telegraphSeconds = .6f; rule.blockedSpawnTimeout = .25f; rule.maxAlive = 1;
        director.spawnProfile.batches.Clear(); director.spawnProfile.rules.Add(rule);
        director.spawnProfile.initialDelaySeconds = .05f;
        director.spawnProfile.minSpawnDelaySeconds = director.spawnProfile.maxSpawnDelaySeconds = 120f;
        return director;
    }

    private static IEnumerator EnemyUpgradeChecks()
    {
        var gridGo = Own(new GameObject("Enemy upgrade arena")); gridGo.SetActive(false); gridGo.transform.rotation = Quaternion.Euler(270, 0, 0);
        var grid = gridGo.AddComponent<VectorGridGPU>(); grid.enabled = false; grid.size = new Vector2(28, 12); gridGo.SetActive(true);
        var bounds = gridGo.AddComponent<ArenaBoundsFromVectorGrid>(); bounds.RefreshNow();
        var service = MatchScoreService.Instance;
        if (!service) service = Own(new GameObject("Enemy upgrade score service")).AddComponent<MatchScoreService>();
        service.Configure(Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"))));
        service.ResetForMatch(true); service.OpenScoring();

        var director = IndividualDirector(bounds); yield return .12f;
        Check(director.PendingSpawnCount == 1 && director.TotalSpawned == 0, "Per-Drone mode announces the first unit independently");
        yield return .4f;
        var warnings = director.GetComponentsInChildren<EnemySpawnTelegraph>().OrderByDescending(w => w.Age).ToArray();
        Check(warnings.Length == 3 && director.PendingSpawnCount == 3 && director.TotalSpawned == 0, "Individual warnings pipeline before any enemy spawns");
        var positions = warnings.Select(w => w.transform.position).ToArray();
        Check(positions.Select((p, i) => positions.Skip(i + 1).All(q => Vector3.Distance(p, q) >= .49f)).All(v => v),
            "Announced individual positions do not overlap");
        float age = warnings[0].Age;
        service.SetScoringState(true, false); yield return .25f;
        Check(Mathf.Abs(warnings[0].Age - age) < .001f && director.TotalSpawned == 0, "Pause freezes individual warning countdowns");
        service.SetScoringState(true, true);
        yield return Mathf.Max(.01f, .8f - warnings[0].Age - .08f);
        Check(director.TotalSpawned == 0, "Every individual receives the full configured warning lead");
        for (int i = 0; i < 20 && director.TotalSpawned < 3; i++) { yield return .04f; FreezeTelegraphMembers(director); }
        var members = FreezeTelegraphMembers(director).OrderBy(e => e.SpawnTime).ToArray();
        Check(members.Length == 3 && director.PendingSpawnCount == 0, "All three announced units arrive and release reservations");
        Check(members.Select((m, i) => Vector3.Distance(m.transform.position, positions[i]) < .001f).All(v => v),
            "Each Drone spawns exactly at its own marker");
        Check(members[1].SpawnTime - members[0].SpawnTime >= .19f && members[2].SpawnTime - members[1].SpawnTime >= .19f,
            "Individual mode preserves batch spacing");
        Check(warnings[2] && warnings[2].IsCompleting, "The last individual marker fades when its own Drone arrives");
        yield return warnings[2].fadeOutSeconds + .1f;
        Check(director.ActiveTelegraphCount == 0, "Every individual marker and ghost is cleaned up");
        Object.Destroy(director.gameObject); yield return .05f;

        director = IndividualDirector(bounds); director.spawnProfile.maxAliveTotal = 1;
        yield return .4f;
        Check(director.PendingSpawnCount == 1 && director.ActiveTelegraphCount == 1, "Reservations enforce the total population cap before spawning");
        Check(!director.TrySpawnEnemy(director.spawnProfile.batches[0].enemy, 0, null, 0, out _), "Another spawn cannot steal an announced capacity slot");
        director.enabled = false; yield return .05f;
        Check(director.PendingSpawnCount == 0 && director.ActiveTelegraphCount == 0, "Disabling releases all reserved capacity and visuals");
        Object.Destroy(director.gameObject); yield return .05f;

        director = IndividualDirector(bounds, .25f);
        director.spawnProfile.batches[0].initialBatchSize = director.spawnProfile.batches[0].finalBatchSize = 1;
        yield return .1f;
        var marker = director.GetComponentInChildren<EnemySpawnTelegraph>();
        var blocker = Own(new GameObject("Blocked exact spawn")); blocker.layer = LayerMask.NameToLayer("Obstacle");
        blocker.transform.position = marker.transform.position; blocker.AddComponent<BoxCollider>().size = Vector3.one;
        Physics.SyncTransforms(); yield return .8f;
        Check(director.TotalSpawned == 0 && director.PendingSpawnCount == 0, "An obstructed individual spawn cancels without relocation");
        yield return marker.fadeOutSeconds + .1f;
        Check(director.ActiveTelegraphCount == 0, "Cancelled individual warning fades completely");
        Object.Destroy(blocker); Object.Destroy(director.gameObject); yield return .05f;

        var dysonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DysonSpawnIndicatorSetup.PrefabPath);
        var dysonMarker = AssetDatabase.LoadAssetAtPath<GameObject>(DysonSpawnIndicatorSetup.TelegraphPath);
        Check(dysonMarker && dysonMarker.GetComponent<EnemySpawnTelegraph>().ghostsEnabled &&
            dysonMarker.GetComponentsInChildren<Collider>(true).Length == 0, "Dyson has a visual-only warning with the same ghost effects");
        Check(dysonMarker.GetComponent<MeshFilter>().sharedMesh.vertexCount == 480, "Dyson outline follows its 80-face sphere's 120 unique edges");
        Check(dysonPrefab.GetComponent<EnemyScoreReward>().scoreToastPrefab != null, "Dyson prefab is connected to credited-score feedback");
        director = DysonDirector(bounds); yield return .18f;
        marker = director.GetComponentInChildren<EnemySpawnTelegraph>();
        Check(marker && director.TotalSpawned == 0 && director.PendingSpawnCount == 1, "Weighted Dyson spawns announce before appearing");
        Vector3 dysonPosition = marker.transform.position;
        yield return .2f;
        CaptureTelegraph(marker, "massive-dyson-spawn-warning.png", new Color(.035f, .045f, .065f), 2.2f);
        CaptureTelegraph(marker, "massive-dyson-spawn-warning-light.png", new Color(.85f, .85f, .85f), 2.2f);
        service.SetScoringState(true, false); age = marker.Age; yield return .3f;
        Check(director.TotalSpawned == 0 && Mathf.Abs(marker.Age - age) < .001f, "Dyson warning pauses with gameplay");
        service.SetScoringState(true, true); yield return .35f;
        var sphere = director.GetComponentInChildren<EnemyBase>();
        Check(sphere && marker.IsCompleting && director.PendingSpawnCount == 0, "Dyson arrives and completes its warning");
        sphere.GetComponent<DysonSphereController>().enabled = false;
        Check(Vector3.Distance(sphere.transform.position, dysonPosition) < .03f, "Dyson arrives at its exact announced position");
        Check(!director.TrySpawnEnemy(sphere.Definition, 1, null, 0, out _), "Dyson weighted rule cap remains enforced");
        yield return marker.fadeOutSeconds + .1f; Check(director.ActiveTelegraphCount == 0, "Dyson warning and ghosts finish cleanup");
        Object.Destroy(director.gameObject); yield return .05f;

        director = DysonDirector(bounds); yield return .15f;
        service.CloseScoring(); yield return .05f;
        Check(director.TotalSpawned == 0 && director.PendingSpawnCount == 0 && director.ActiveTelegraphCount == 0,
            "Closing the match cancels pending weighted spawns");
        Object.Destroy(director.gameObject); yield return .05f; service.OpenScoring();

        var player = Player(3, 1, new Vector3(11, 0, 0)); service.RegisterPlayer(player); service.ResetForMatch(true);
        service.GetPlayerChain(3).ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 4f); service.AdvanceTeamAmplifier(1);
        var dyson = Own(Object.Instantiate(dysonPrefab)); dyson.GetComponent<DysonSphereController>().enabled = false;
        var enemy = dyson.GetComponent<EnemyBase>(); enemy.Init(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonSpawnIndicatorSetup.DefinitionPath), null);
        string token = enemy.SourceLifeToken;
        enemy.TakeDamage(100f, EnemyDamageSource.Sword, player);
        long awarded = service.GetTeamScore(1);
        Check(awarded == 4000 && EnemyScoreToast.ActiveCount == 1, "Dyson defeat creates one toast for the actual amplified 4 eV award");
        Check(Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).Any(t => t.Amount == awarded && t.label.text == "+4 eV"),
            "Dyson toast displays the correct formatted score");
        Check(!service.TryAwardToPlayer(enemy.Definition.defeatRewardKey, player, token, Vector3.zero, out _), "Dyson defeat cannot be credited twice");
        yield return .9f; Check(EnemyScoreToast.ActiveCount == 0, "Dyson toast expires through the shared pool");
        dyson = Own(Object.Instantiate(dysonPrefab)); dyson.GetComponent<DysonSphereController>().enabled = false;
        enemy = dyson.GetComponent<EnemyBase>(); enemy.Init(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonSpawnIndicatorSetup.DefinitionPath), null);
        enemy.Kill(); yield return .05f;
        Check(service.GetTeamScore(1) == awarded && EnemyScoreToast.ActiveCount == 0, "Administrative Dyson removal produces no toast or points");

        var swarm = new DroneController[8];
        for (int i = 0; i < swarm.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / swarm.Length;
            swarm[i] = Drone(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.2f);
            swarm[i].spawnGraceSeconds = 0f; swarm[i].detectionRange = .1f; swarm[i].decisionInterval = .08f;
        }
        yield return 1f;
        var initialVelocities = swarm.Select(d => d.GetComponent<Rigidbody>().linearVelocity.normalized).ToArray();
        Check(swarm.All(d => d.Target == null && d.GetComponent<Rigidbody>().linearVelocity.magnitude > .1f), "Idle Drones actively swarm without a target");
        bool bounded = true;
        for (int i = 0; i < 45; i++)
        {
            yield return .1f;
            bounded &= swarm.All(d => d.transform.position.magnitude < 6f && Mathf.Abs(d.transform.position.y) < .001f);
        }
        Check(bounded, "Idle swarm remains loosely gathered on the gameplay plane");
        Check(swarm.Select((d, i) => Vector3.Dot(initialVelocities[i], d.GetComponent<Rigidbody>().linearVelocity.normalized)).Count(dot => dot < .5f) >= 4,
            "Swarm members curve and change direction rather than drifting in a straight line");
        var pausedPositions = swarm.Select(d => d.transform.position).ToArray();
        foreach (var drone in swarm) drone.GetComponent<EnemyBase>().Pause(true);
        yield return .3f;
        Check(swarm.Select((d, i) => Vector3.Distance(d.transform.position, pausedPositions[i])).All(distance => distance < .001f), "Swarm movement pauses cleanly");
        foreach (var drone in swarm) { drone.GetComponent<EnemyBase>().Pause(false); drone.detectionRange = 6f; }
        Place(player, swarm[0].transform.position + Vector3.right * 3f); yield return .25f;
        Check(swarm.Any(d => d.Target == player), "Idle swarm transitions into normal player pursuit");
        player.temporarilyEliminated = true; yield return .25f;
        Check(swarm.All(d => d.Target != player), "Losing a target returns Drones to local swarm behavior");
        foreach (var drone in swarm) Object.Destroy(drone.gameObject);
        Object.Destroy(player.gameObject); Object.Destroy(gridGo); service.CloseScoring(); yield return .05f;
    }
}
#endif
