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
    [MenuItem("MASSIVE/Enemies/Drone/Run Spawn Telegraph Validation")]
    public static void StartTelegraphValidation()
    {
        Start(); SessionState.SetBool(Key + "TelegraphOnly", true);
    }

    private static EnemyDirector TelegraphDirector(ArenaBoundsFromVectorGrid bounds, float warning = 3f, float firstDelay = .1f)
    {
        var profile = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(DronePrototypeSetup.ProfilePath)));
        profile.rules.Clear(); profile.maxAliveTotal = 8; profile.maxAliveMelee = 8;
        var rule = profile.batches[0]; rule.telegraphSeconds = warning; rule.firstBatchDelay = firstDelay;
        rule.initialBatchSize = rule.finalBatchSize = 3; rule.intervalWithinBatch = .5f;
        rule.initialBatchDelay = rule.finalBatchDelay = 120f; rule.batchRadius = 2f; rule.maxAlive = 8; rule.blockedBatchTimeout = .5f;
        var go = Own(new GameObject("Telegraph validation director"));
        var director = go.AddComponent<EnemyDirector>(); director.spawnProfile = profile; director.arenaBounds = bounds;
        director.spawnCheckRadiusWorld = .25f; director.minDistanceFromPlayers = 0f; director.borderBufferWorld = .5f;
        director.spawnBlockMask = 1 << LayerMask.NameToLayer("Obstacle"); director.waitForScoring = true;
        return director;
    }

    private static IEnumerator SpawnTelegraphChecks()
    {
        var renderingChecks = SpawnRenderingChecks();
        while (renderingChecks.MoveNext()) yield return renderingChecks.Current;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.TelegraphPath);
        Check(prefab != null && prefab.GetComponent<EnemySpawnTelegraph>() != null, "Production warning prefab is wired");
        Check(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponent<EnemyBase>() == null,
            "Warning has no collision, damage, or score identity");
        Check(prefab.GetComponent<MeshFilter>().sharedMesh.vertexCount == 72, "Warning contains the Drone's 18 unique crystal edges");
        Vector3 authoredScale = prefab.transform.localScale;
        Check(authoredScale.x > 0f && authoredScale.y > 0f && authoredScale.z > 0f && !float.IsInfinity(authoredScale.sqrMagnitude),
            "Warning has a valid positive authored size");
        var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(DronePrototypeSetup.ProfilePath);
        Check(profile.batches[0].telegraphPrefab == prefab.GetComponent<EnemySpawnTelegraph>() && profile.batches[0].WarningSeconds == 3f,
            "Live Drone profile has a three-second warning");
        SpawnGhostChecks(prefab);
        yield return .03f;

        var gridGo = Own(new GameObject("Telegraph arena")); gridGo.SetActive(false); gridGo.transform.rotation = Quaternion.Euler(270, 0, 0);
        var grid = gridGo.AddComponent<VectorGridGPU>(); grid.enabled = false; grid.size = new Vector2(28, 12); gridGo.SetActive(true);
        var bounds = gridGo.AddComponent<ArenaBoundsFromVectorGrid>(); bounds.RefreshNow();
        var service = MatchScoreService.Instance;
        if (service == null) service = Own(new GameObject("Telegraph scoring clock")).AddComponent<MatchScoreService>();
        service.CloseScoring();
        var director = TelegraphDirector(bounds); yield return .1f;
        Check(director.TotalSpawned == 0 && director.ActiveTelegraphCount == 0, "Warnings wait until gameplay opens");
        service.OpenScoring(); yield return .12f;
        var warning = director.GetComponentInChildren<EnemySpawnTelegraph>();
        Check(warning != null && director.TotalSpawned == 0, "One warning appears before the first unit");
        Vector3 anchor = warning.transform.position;
        float announcedAt = Time.time - warning.Age;
        Check(bounds.ContainsWorldPoint(anchor, .5f), "Warning center is inside the arena");
        yield return .4f;
        CaptureTelegraph(warning, "massive-drone-spawn-warning.png", new Color(.035f, .045f, .065f));
        CaptureTelegraph(warning, "massive-drone-spawn-warning-light.png", new Color(.85f, .85f, .85f));
        float age = warning.Age, clock = director.GameplayAge; Quaternion rotation = warning.transform.rotation;
        var ghosts = warning.GetComponentsInChildren<MeshRenderer>().Where(r => r.gameObject != warning.gameObject).ToArray();
        var ghostPositions = ghosts.Select(r => r.transform.position).ToArray();
        service.SetScoringState(true, false); yield return .35f;
        Check(Mathf.Abs(warning.Age - age) < .02f && Mathf.Abs(director.GameplayAge - clock) < .02f &&
            Quaternion.Angle(rotation, warning.transform.rotation) < .1f, "Gameplay pause freezes warning, rotation, and countdown together");
        Check(ghosts.Select((r, i) => Vector3.Distance(r.transform.position, ghostPositions[i])).All(d => d < .0001f),
            "Gameplay pause freezes every outward ghost");
        service.SetScoringState(true, true);
        yield return Mathf.Max(.01f, 3f - warning.Age - .12f);
        Check(director.TotalSpawned == 0, "No unit appears before the full warning interval");
        yield return .22f;
        Check(director.TotalSpawned == 1 && warning && warning.IsCompleting && warning.Defocus > 0f,
            "Cluster warning defocus overlaps the first enemy's spawn animation");
        CaptureTelegraph(warning, "massive-drone-spawn-batch.png", new Color(.035f, .045f, .065f));
        var members = FreezeTelegraphMembers(director);
        Check(members[0].SpawnTime - announcedAt >= 3.3f, "Actual spawn time includes the three-second warning and pause");
        Check(Vector3.Distance(members[0].transform.position, anchor) <= 2.3f, "First Drone arrives within the announced cluster");
        yield return .5f;
        Check(director.TotalSpawned == 2 && warning && warning.IsCompleting, "Cluster defocus continues while later members arrive");
        FreezeTelegraphMembers(director);
        yield return .5f;
        Check(director.TotalSpawned == 3 && !warning, "Cluster spawn cadence continues after its marker has blurred away");
        members = FreezeTelegraphMembers(director).OrderBy(e => e.SpawnTime).ToArray();
        Check(members.All(e => Vector3.Distance(e.transform.position, anchor) <= 2.3f), "Every member uses the same announced cluster center");
        Check(members[1].SpawnTime - members[0].SpawnTime >= .49f && members[2].SpawnTime - members[1].SpawnTime >= .49f,
            "Warning does not bunch the individual spawn intervals");
        yield return .55f;
        Check(!warning && director.ActiveTelegraphCount == 0, "Completed warning fully despawns");
        Object.Destroy(director.gameObject); yield return .05f;

        // Revalidate placement after warning; an obstacle must not cause a hidden relocation.
        director = TelegraphDirector(bounds, .25f); yield return .1f;
        warning = director.GetComponentInChildren<EnemySpawnTelegraph>(); anchor = warning.transform.position;
        var blocker = Own(new GameObject("New obstacle across warned cluster")); blocker.layer = LayerMask.NameToLayer("Obstacle");
        blocker.transform.position = anchor; blocker.AddComponent<BoxCollider>().size = new Vector3(7, 3, 7); Physics.SyncTransforms();
        yield return .6f;
        Check(director.TotalSpawned == 0 && warning && warning.IsCompleting, "Blocked batch is cancelled without spawning at an unannounced location");
        yield return prefab.GetComponent<EnemySpawnTelegraph>().fadeOutSeconds + .1f;
        Check(director.ActiveTelegraphCount == 0, "Blocked-batch warning does not linger");
        Object.Destroy(blocker); Object.Destroy(director.gameObject); yield return .05f;

        director = TelegraphDirector(bounds); yield return .1f;
        Check(director.ActiveTelegraphCount == 1, "New warning starts after the previous owner is removed");
        director.enabled = false; yield return .03f;
        Check(director.ActiveTelegraphCount == 0 && director.GetComponentInChildren<EnemySpawnTelegraph>() == null,
            "Disabling the director cleans all warnings");
        director.enabled = true; yield return .1f;
        Check(director.TotalSpawned == 0, "Re-enabling cannot release an unannounced pending batch");
        Object.Destroy(director.gameObject); yield return .05f;

        director = TelegraphDirector(bounds); yield return .1f; service.CloseScoring(); yield return .05f;
        Check(director.ActiveTelegraphCount == 0 && director.TotalSpawned == 0, "Match end clears incomplete warnings");
        Object.Destroy(director.gameObject); service.OpenScoring(); yield return .05f;

        director = TelegraphDirector(bounds, 3f, 4f); director.spawnProfile.maxAliveTotal = 1;
        yield return .05f;
        Check(director.TrySpawnEnemy(profile.batches[0].enemy, 1, null, 0, out _), "Capacity fixture occupies the final spawn slot");
        FreezeTelegraphMembers(director); yield return 1.15f;
        Check(director.ActiveTelegraphCount == 0 && director.TotalSpawned == 1, "A full population cap suppresses false warnings");
        Object.Destroy(director.gameObject); yield return .05f;

        director = TelegraphDirector(bounds); director.spawnProfile.batches[0].telegraphPrefab = null;
        yield return .25f;
        Check(director.TotalSpawned == 1 && director.ActiveTelegraphCount == 0, "Rules without a warning retain their original timing");
        Object.Destroy(director.gameObject); Object.Destroy(gridGo); service.CloseScoring(); yield return .05f;
    }

    private static EnemyBase[] FreezeTelegraphMembers(EnemyDirector director)
    {
        var members = director.GetComponentsInChildren<EnemyBase>();
        foreach (var member in members)
        {
            var drone = member.GetComponent<DroneController>(); if (drone) drone.enabled = false;
            var body = member.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero; body.constraints = RigidbodyConstraints.FreezeAll;
        }
        return members;
    }

    private static void SpawnGhostChecks(GameObject prefab)
    {
        var randomBefore = Random.state;
        var instance = Own(Object.Instantiate(prefab));
        var warning = instance.GetComponent<EnemySpawnTelegraph>();
        warning.ghostsEnabled = true; warning.ghostCount = 4; warning.ghostLifetime = .9f;
        warning.ghostOpacity = .2f; warning.ghostDistance = .75f; warning.ghostVariation = .25f;
        warning.Begin();
        Check(instance.transform.childCount == 5 && warning.VisibleGhostCount == 0,
            "Four staggered ghosts are created once, initially invisible");
        for (int i = 0; i < 70; i++) warning.Advance(.01f);
        var main = instance.GetComponent<MeshRenderer>();
        var ghosts = instance.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Outward ghost ")).ToArray();
        Check(warning.VisibleGhostCount >= 2, "Multiple outward ghosts overlap during the warning");
        Check(ghosts.All(r => r.sharedMaterial == main.sharedMaterial &&
            r.GetComponent<MeshFilter>().sharedMesh == instance.GetComponent<MeshFilter>().sharedMesh),
            "Ghosts reuse the crystal mesh and glow material");
        var block = new MaterialPropertyBlock(); bool opacityValid = true, driftValid = true;
        foreach (var ghost in ghosts)
        {
            ghost.GetPropertyBlock(block);
            opacityValid &= block.GetFloat("_Opacity") <= warning.ghostOpacity * warning.Opacity && block.GetFloat("_Contrast") == 0f;
            Vector3 offset = ghost.transform.position - instance.transform.position;
            driftValid &= Mathf.Abs(offset.y) < .0001f && offset.magnitude <= .75f * 1.25f + .001f;
        }
        Check(opacityValid, "Ghost opacity is bounded and does not darken the main icon");
        Check(driftValid && ghosts.Count(r => Vector3.Distance(r.transform.position, instance.transform.position) > .01f) >= 2,
            "Ghosts spread outward within the configured radius on the gameplay plane");
        CaptureTelegraph(warning, "massive-drone-spawn-ghosts.png", new Color(.035f, .045f, .065f), 2.2f);
        CaptureTelegraph(warning, "massive-drone-spawn-ghosts-light.png", new Color(.85f, .85f, .85f), 2.2f);
        var directions = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < 600; i++)
        {
            warning.Advance(.02f);
            if (i % 20 == 0 && ghosts[0].enabled)
                directions.Add((ghosts[0].transform.position - instance.transform.position).normalized);
        }
        Check(directions.Any(a => directions.Any(b => Vector3.Dot(a, b) < -.2f)),
            "Successive ghosts choose different directions around the icon");
        Check(instance.transform.childCount == 5 && instance.GetComponentsInChildren<MeshRenderer>().Length == 6,
            "Repeated ghost cycles keep the renderer pool bounded");
        Check(JsonUtility.ToJson(Random.state) == JsonUtility.ToJson(randomBefore),
            "Ghost animation leaves the gameplay random stream untouched");
        warning.ghostsEnabled = false; warning.Advance(.02f);
        Check(warning.VisibleGhostCount == 0 && ghosts.All(r => !r.enabled), "Ghost toggle hides all copies immediately");
        warning.ghostsEnabled = true; warning.Advance(.1f);
        Check(warning.VisibleGhostCount > 0 && instance.transform.childCount == 5, "Re-enabling reuses the existing ghosts");
        warning.Complete(); bool alive = warning.Advance(warning.fadeOutSeconds + .01f);
        Check(!alive && warning.VisibleGhostCount == 0 && ghosts.All(r => !r.enabled),
            "Completing the warning hides all ghosts before destruction");
        SpawnDefocusChecks(prefab);
    }

    private static void SpawnDefocusChecks(GameObject prefab)
    {
        var parent = Own(new GameObject("Scaled enemy root")); parent.transform.localScale = Vector3.one * 2f;
        var instance = Own(Object.Instantiate(prefab, parent.transform));
        var warning = instance.GetComponent<EnemySpawnTelegraph>(); warning.breathScale = 0f;
        warning.ghostCount = 1; warning.ghostLifetime = 2f; warning.ghostVariation = 0f;
        warning.Begin(3f, Vector3.one * 1.5f);
        Check(Vector3.Distance(instance.transform.lossyScale, Vector3.one * 1.5f) < .0001f,
            "Default outline size matches the supplied enemy scale under a scaled parent");
        warning.Advance(.1f);
        var halo = instance.transform.Find("Diffuse spawn glow").GetComponent<MeshRenderer>();
        Check(halo.enabled && warning.DiffuseGlowOpacity > 0f && warning.Opacity == 0f && warning.VisibleGhostCount == 0,
            "Broad glow appears before the wireframe and ghosts");
        float earlyGlow = warning.DiffuseGlowOpacity;
        warning.Advance(.5f);
        var ghost = instance.transform.Find("Outward ghost 1"); float earlyScale = ghost.localScale.x;
        warning.Advance(.4f);
        Check(ghost.localScale.x < earlyScale && ghost.localScale.x >= warning.ghostEndScale,
            "A living ghost shrinks progressively toward its ending scale");
        Check(warning.DiffuseGlowOpacity > earlyGlow, "Broad glow builds strength through the warning");
        var block = new MaterialPropertyBlock(); instance.GetComponent<MeshRenderer>().GetPropertyBlock(block);
        Check(block.GetColor("_Color") == warning.wireframeColor && block.GetColor("_GlowColor") == warning.glowColor,
            "Wireframe and edge glow use independent authored colors");
        ghost.GetComponent<MeshRenderer>().GetPropertyBlock(block);
        Check(block.GetColor("_Color") == warning.ghostColor, "Ghosts use their own authored color");
        float glowSize = halo.transform.localScale.x, opacity = warning.Opacity;
        warning.Complete(); warning.Advance(warning.fadeOutSeconds * .2f);
        Check(warning.Defocus > 0f && Mathf.Abs(warning.Opacity - opacity) < .001f && halo.transform.localScale.x > glowSize,
            "Exit starts with defocus and a spreading halo before fading opacity");
        instance.GetComponent<MeshRenderer>().GetPropertyBlock(block);
        Check(block.GetFloat("_Defocus") == warning.Defocus, "Main wireframe receives the live blur amount");
        ghost.GetComponent<MeshRenderer>().GetPropertyBlock(block);
        Check(block.GetFloat("_Defocus") == warning.Defocus, "Ghosts blur together with the main wireframe");
        warning.Advance(warning.fadeOutSeconds);
        Check(!halo.enabled && warning.DiffuseGlowOpacity == 0f, "Exit cleans the broad glow as well as outlines");
        instance = Own(Object.Instantiate(prefab)); warning = instance.GetComponent<EnemySpawnTelegraph>();
        warning.sizeMultiplierRange = new Vector2(.7f, 1.3f); warning.breathScale = 0f;
        bool inRange = true;
        for (int i = 0; i < 20; i++)
        {
            warning.Begin(3f, Vector3.one * 2f);
            inRange &= instance.transform.localScale.x >= 1.4f && instance.transform.localScale.x <= 2.6f;
        }
        Check(inRange, "Optional size range stays relative to actual enemy size");
        warning.Cancel();
    }

    private static void CaptureTelegraph(EnemySpawnTelegraph warning, string filename, Color background, float viewSize = 1.65f)
    {
        var go = new GameObject("Telegraph capture"); var camera = go.AddComponent<Camera>();
        camera.transform.position = warning.transform.position + new Vector3(0, 5, -2.5f); camera.transform.LookAt(warning.transform.position);
        camera.orthographic = true; camera.orthographicSize = viewSize; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background; camera.cullingMask = 1 << LayerMask.NameToLayer("Enemy");
        var rt = new RenderTexture(700, 700, 24) { antiAliasing = 4 }; var previous = RenderTexture.active;
        var tex = new Texture2D(700, 700, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 700, 700), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), filename), tex.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go); }
    }
}
#endif
