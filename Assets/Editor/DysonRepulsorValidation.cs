#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DysonRepulsorValidation
{
    private const string Key = "MASSIVE.DysonRepulsorValidation", Output = "Library/DysonRepulsorValidation";
    private static readonly List<string> results = new(), errors = new();
    private static IEnumerator routine;
    private static int frame;
    private static double deadline;
    private static DysonSphereRepulsorController c;
    private static EnemyBase enemy;
    private static PlayerControllerScript player;
    static DysonRepulsorValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/Enemies/Dyson Repulsor/Validate In Player Actions")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.txt", "RUNNING — waiting for Play Mode\n");
        SessionState.SetBool(Key,true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key,false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 120;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key,false); File.AppendAllText(Output + "/report.txt", "Returned to Edit Mode; runtime fixtures discarded.\n"); }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || Time.frameCount <= frame) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at " + (c ? c.Phase + "/" + c.PhaseAge : "setup"));
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    private static void Finish(Exception e)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/report.txt", (e == null ? "PASSED" : "FAILED") + "\n" + string.Join("\n",results) + "\n" + e + "\n" + string.Join("\n",errors));
        Debug.Log("[Dyson Repulsor validation] " + (e == null ? "PASSED " + results.Count + " checks" : e.ToString()));
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool pass,string message)
    {
        if (!pass) throw new Exception(message); results.Add("PASS " + message);
        File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n",results));
    }
    private static IEnumerator Delay(float seconds)
    { float until = Time.time + seconds; while (Time.time < until) yield return null; }
    private static void Place(Component o, Vector3 point)
    {
        o.transform.position = point;
        if (o.TryGetComponent<Rigidbody>(out var rb)) { rb.position = point; if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
    }
    private static IEnumerator Checks()
    {
        c = Object.FindFirstObjectByType<DysonSphereRepulsorController>(); enemy = c.GetComponent<EnemyBase>();
        Check(c && c.definition && c.panels && c.attackVolume && c.hurtbox && c.ripple, "Alternate prefab references resolve");
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(DysonRepulsorSetup.Folder + "/Enemy_DysonSphere.prefab");
        Check(original.GetComponent<DysonSphereController>() && !original.GetComponent<DysonSphereRepulsorController>()
            && !c.GetComponent<DysonSphereController>(), "Original lunge prefab remains available; alternate has one radial attack owner");
        foreach (var other in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)) if (other != enemy) other.gameObject.SetActive(false);
        foreach (var spawner in Object.FindObjectsByType<Massive.Multiplier.AmplifierResonanceSpawner>(FindObjectsSortMode.None)) spawner.enabled = false;
        while (enemy.IsPaused || !MatchScoreService.Instance || !MatchScoreService.Instance.IsScoringOpen) yield return null;
        player = PlayerControllerScript.ActivePlayers.First(p => !p.IsPseudoPlayer && !p.temporarilyEliminated);
        foreach (var p in PlayerControllerScript.ActivePlayers.ToArray())
        { p.SetControlMode(PlayerControlMode.Scripted); p.ClearScriptedInput(); if (p != player) p.gameObject.SetActive(false); }
        while (player.IsMatchInputLocked || player.IsInvulnerable) yield return null;
        c.attackDistance = .1f; Place(c, Vector3.zero); Place(player, Vector3.right * 3f);
        Check(enemy.Definition == c.definition && enemy.Director == c.sceneDirector && enemy.HealthRemaining == 20f,
            "Alternate retains Dyson health, score definition and authored Director registration");
        while (c.Phase == DysonSphereRepulsorController.AttackPhase.Spawning) yield return null;
        Check(c.panels.Spawn01 >= .999f && !c.attackVolume.enabled, "Existing panel spawn completes before attacks can activate");
        Capture("dyson-idle.png");
        c.attackDistance = 1.5f; Place(c, Vector3.zero); Place(player, Vector3.right * 1.42f);
        while (c.Phase != DysonSphereRepulsorController.AttackPhase.Charge) yield return null;
        Vector3 start = c.transform.position; float mass = player.massScore;
        while (c.PhaseAge < c.chargeSeconds - .05f) yield return null;
        Check(c.panels.BodyScaleMultiplier > .84f && c.panels.BodyScaleMultiplier < .87f,
            "Charge contracts the whole visual body to 85 percent");
        Check(Mathf.Abs(c.DamageRadiusWorld - c.panels.radius * c.chargeScale) < .025f && !c.attackVolume.enabled
            && Mathf.Approximately(player.massScore,mass), "Charging panels close onto the base shell with no damaging hitbox");
        Check(Vector3.Distance(start,c.transform.position) < .03f, "Charge stays stationary instead of preparing a lunge");
        enemy.Pause(true); float clock = c.AnimationClock, age = c.PhaseAge, radius = c.DamageRadiusWorld;
        Quaternion rotation = c.panels.transform.rotation;
        var delay = Delay(.15f); while (delay.MoveNext()) yield return null;
        Check(clock == c.AnimationClock && age == c.PhaseAge && radius == c.DamageRadiusWorld && rotation == c.panels.transform.rotation,
            "Pause freezes contraction, panel vibration, rotation and attack timing");
        Capture("dyson-charge.png"); enemy.Pause(false);
        while (c.Phase != DysonSphereRepulsorController.AttackPhase.Hold) yield return null;
        Check(Mathf.Abs(c.panels.BodyScaleMultiplier-1.15f) < .001f, "Burst reaches 115 percent body scale");
        Check(c.GetComponentsInChildren<ParticleSystem>().All(p => p.main.scalingMode == ParticleSystemScalingMode.Hierarchy
            && p.main.simulationSpace == ParticleSystemSimulationSpace.Local), "Core particles inherit whole-body contraction and rebound");
        Check(Mathf.Abs(c.DamageRadiusWorld-c.panels.radius*1.3f*1.15f) < .025f,
            "Panels extend to 130 percent base radius in every direction, plus whole-body rebound");
        Check(c.attackVolume.enabled && Mathf.Abs(c.attackVolume.radius-c.DamageRadiusWorld) < .00001f
            && Mathf.Abs(c.hurtbox.radius-c.DamageRadiusWorld) < .00001f, "Attack sphere and sword hurtbox match the actual animated panel envelope");
        float renderedRadius = c.panels.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh && m.name.StartsWith("Panel_"))
            .SelectMany(m => m.sharedMesh.vertices.Select(v => Vector3.Distance(m.transform.TransformPoint(v),c.panels.transform.position))).Max();
        Check(Mathf.Abs(renderedRadius-c.DamageRadiusWorld) < .0001f, "Independent rendered-mesh measurement agrees with the damage sphere radius");
        var block = new MaterialPropertyBlock(); var grid = Object.FindFirstObjectByType<VectorGridGPU>();
        PlayerRepulsorGridPulse.WriteGridProperties(grid,block);
        Check(block.GetInt("_RepulsorPulseCount") == 1, "Explosion emits the same outward grid ripple used by Player Repulsor");
        Check(c.TotalHits == 1 && Mathf.Abs(player.massScore-mass+c.definition.damageToPlayerMass01*c.damageMultiplier) < .002f,
            "Expanding panels contact the real player and apply one configured mass hit");
        enemy.Pause(true); PlayerRepulsorGridPulse.WriteGridProperties(grid,block);
        var wave = block.GetVectorArray("_RepulsorPulseOrigins")[0];
        delay = Delay(.15f); while (delay.MoveNext()) yield return null;
        PlayerRepulsorGridPulse.WriteGridProperties(grid,block);
        Check(block.GetVectorArray("_RepulsorPulseOrigins")[0] == wave, "Enemy pause freezes its shared grid ripple without changing player ripple timing");
        Capture("dyson-burst.png"); enemy.Pause(false);
        float holdStart = c.AnimationClock - c.PhaseAge, minScale = 2f, radiusBefore = c.DamageRadiusWorld;
        while (c.Phase == DysonSphereRepulsorController.AttackPhase.Hold) yield return null;
        Check(Mathf.Abs(c.AnimationClock-holdStart-c.panelHoldSeconds) < .045f, "Full extension holds for the configured 0.1 seconds");
        while (c.Phase == DysonSphereRepulsorController.AttackPhase.Return)
        {
            minScale = Mathf.Min(minScale,c.panels.BodyScaleMultiplier);
            CheckRadius(); yield return null;
        }
        Check(minScale < .995f && Mathf.Abs(c.panels.BodyScaleMultiplier-1f) < .0001f,
            "Body rebounds below normal then settles with a rubber-band bounce");
        Check(c.Phase == DysonSphereRepulsorController.AttackPhase.Cooldown && !c.attackVolume.enabled
            && c.TotalHits == 1, "Attack ends at normal breathing with no repeated hit or cooldown damage");
        Check(Vector3.Distance(start,c.transform.position) < .1f, "Entire radial attack completes without forward propulsion");
        float idleRadius = c.DamageRadiusWorld; delay = Delay(.25f); while (delay.MoveNext()) yield return null;
        Check(Mathf.Abs(c.DamageRadiusWorld-idleRadius) > .0005f, "Normal panel breathing resumes after the attack");
        // Late evasion escapes the committed radial area; ripple travel never damages outside the shell.
        Place(c,Vector3.zero); Place(player,Vector3.right*1.42f);
        while (c.Phase != DysonSphereRepulsorController.AttackPhase.Charge) yield return null;
        while (c.PhaseAge < c.chargeSeconds*.8f) yield return null;
        Place(player,Vector3.right*3f); mass = player.massScore; int bursts = c.TotalBursts;
        while (c.TotalBursts == bursts) yield return null;
        while (c.IsDamaging) yield return null;
        Check(Mathf.Approximately(mass,player.massScore), "A player outside the panel sphere takes no damage from the longer visual ripple");
        Place(c,Vector3.zero); Place(player,Vector3.right*1.42f);
        while (c.Phase != DysonSphereRepulsorController.AttackPhase.Charge) yield return null;
        player.SetScriptedInput(new PlayerInputFrame { shieldDown=true,shieldHeld=true,hasAimDirWS=true,aimDirWS=Vector3.left });
        while (c.PhaseAge < c.chargeSeconds*.8f) yield return null;
        Check(player.shieldOn,"Real player shield activates"); mass = player.massScore; bursts = c.TotalBursts;
        while (c.TotalBursts == bursts) yield return null;
        while (c.IsDamaging) yield return null;
        Check(c.TotalBlocks > 0 && Mathf.Approximately(mass,player.massScore), "Shield blocks a radial burst without duplicate mass damage");
        player.ClearScriptedInput();
        enemy.Pause(true); c.ripple.ClearPulses();
        var playerRipple = player.GetComponentInChildren<PlayerRepulsorGridPulse>(true);
        Check(playerRipple != null,"Player retains its original Repulsor ripple component");
        playerRipple.TriggerPulse(); PlayerRepulsorGridPulse.WriteGridProperties(grid,block);
        Check(block.GetInt("_RepulsorPulseCount") == 1,"Original player-triggered ripple still emits through the shared renderer");
        wave = block.GetVectorArray("_RepulsorPulseOrigins")[0];
        delay = Delay(.08f); while (delay.MoveNext()) yield return null;
        PlayerRepulsorGridPulse.WriteGridProperties(grid,block);
        Check(block.GetVectorArray("_RepulsorPulseOrigins")[0].z > wave.z,
            "Player ripple advances on its normal clock while the enemy is paused");
        playerRipple.ClearPulses(); enemy.Pause(false);
        long score = MatchScoreService.Instance.GetTeamScore(player.teamID);
        enemy.TakeDamage(enemy.HealthRemaining,EnemyDamageSource.Sword,player); yield return null;
        Check(enemy.IsDead && !c.attackVolume.enabled && MatchScoreService.Instance.GetTeamScore(player.teamID)>score,
            "Defeat disables damage immediately and awards the existing Dyson score");
        delay = Delay(.25f); while (delay.MoveNext()) yield return null; Capture("dyson-death.png");
        while (enemy) yield return null;
        Check(!c, "Radial enemy and owned effects clean up after panel shatter");
        Check(errors.Count == 0,"No runtime Console errors during radial Dyson validation");
    }
    private static void CheckRadius()
    {
        if (Mathf.Abs(c.attackVolume.radius-c.DamageRadiusWorld) > .00001f)
            throw new Exception("Damage radius detached from panel envelope during return");
    }
    private static void Capture(string file)
    {
        var go = new GameObject("Dyson validation camera"); var camera = go.AddComponent<Camera>();
        var target = new RenderTexture(1000,800,24); camera.targetTexture = target;
        camera.orthographic = true; camera.orthographicSize = 2.1f; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black; camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
        camera.transform.position = c.transform.position + new Vector3(0,8,-3); camera.transform.LookAt(c.transform.position);
        var prev = RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(1000,800,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1000,800),0,0); pixels.Apply();
            File.WriteAllBytes(Output+"/"+file,pixels.EncodeToPNG()); Object.DestroyImmediate(pixels);
        }
        finally { RenderTexture.active=prev; camera.targetTexture=null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(go); }
    }
}
#endif
