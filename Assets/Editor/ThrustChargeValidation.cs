#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.EditorTools;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class ThrustChargeValidation
{
    const string Key = "MASSIVE.ThrustChargeValidation.";
    const string Output = "Library/ThrustChargeValidation";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new(), errors = new();
    static readonly List<Object> owned = new();
    static int frame;
    static double deadline;
    static PlayerTuningProfile tuning;
    const string AttackPath = "Assets/Scripts/Settings/Resources/PlayerTuningProfile.asset";
    static ThrustChargeValidation()
    {
        EditorApplication.update += BeginWhenReady;
        EditorApplication.playModeStateChanged += StateChanged;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Run in an isolated batch project copy.");
        SessionState.SetBool(Key + "Start", true);
    }
    static void BeginWhenReady()
    {
        if (!SessionState.GetBool(Key + "Start", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + "Start", false);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/report.txt", "RUNNING\n");
        SessionState.SetString(Key + "Profile", File.ReadAllText(AttackPath));
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath("efe7778db3b8628419bdbc865721e0a9"));
        // Scene edits are confined to this unsaved, isolated test scene.
        foreach (var lab in Object.FindObjectsByType<EnemyLab>(FindObjectsInactive.Include, FindObjectsSortMode.None)) lab.gameObject.SetActive(false);
        var arena = Object.FindFirstObjectByType<PlayerActionTestArena>(FindObjectsInactive.Include);
        for (var t = arena.transform; t; t = t.parent) t.gameObject.SetActive(true);
        arena.enabled = true; arena.SetMode(PlayerActionTestArena.TestMode.Manual);
        EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Time.captureDeltaTime = .02f;
            tuning = SharedSettingsRuntime.Load<PlayerTuningProfile>();
            deadline = EditorApplication.timeSinceStartup + 240;
            results.Clear(); errors.Clear(); owned.Clear(); routines.Clear(); routines.Push(Checks()); frame = 0;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool passed = SessionState.GetBool(Key + "Passed", false);
            try
            {
                if (passed)
                {
                    AssetDatabase.ImportAsset(AttackPath, ImportAssetOptions.ForceUpdate);
                    var saved = AssetDatabase.LoadAssetAtPath<PlayerTuningProfile>(AttackPath);
                    Check(Mathf.Approximately(saved.combat.thrustChargeMaxSeconds, 2.75f) && Mathf.Approximately(saved.combat.thrustChargeMaxDistanceMultiplier, 2.5f) &&
                        Mathf.Approximately(saved.combat.thrustChargeMinMoveMultiplier, .4f) && Mathf.Approximately(saved.combat.thrustChargeGuideStrokeWidth, .06f), "Charge tuning persists after Play Mode exit and reimport");
                }
            }
            catch (Exception e) { passed = false; results.Add(e.ToString()); }
            finally
            {
                File.WriteAllText(AttackPath, SessionState.GetString(Key + "Profile", ""));
                SessionState.SetBool(Key + "Pending", false);
                AssetDatabase.Refresh();
            }
            WriteReport(passed ? "PASSED" : "FAILED");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routines.Count == 0 || Time.frameCount < frame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
            while (routines.Count > 0)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                frame = Time.frameCount + 1; return;
            }
            Finish(true);
        }
        catch (Exception e) { results.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool passed)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routines.Clear();
        passed &= errors.Count == 0;
        foreach (var item in owned) if (item) Object.Destroy(item);
        owned.Clear();
        WriteReport(passed ? "PASSED" : "FAILED");
        Debug.Log("THRUST CHARGE VALIDATION " + (passed ? "PASSED" : "FAILED"));
        SessionState.SetBool(Key + "Passed", passed);
        Time.captureDeltaTime = 0; EditorApplication.isPlaying = false;
    }
    static void WriteReport(string status) => File.WriteAllText(Output + "/report.txt", status + "\n" + string.Join("\n", results) + "\n" + string.Join("\n", errors));
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); results.Add("PASS " + label); WriteReport("RUNNING"); }
    static IEnumerator Until(Func<bool> predicate, string label)
    {
        float end = Time.time + 40;
        while (!predicate() && Time.time < end) yield return null;
        if (!predicate()) throw new Exception("Timed out: " + label);
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static PlayerControllerScript actor;
    static PlayerAttackController attack;
    static Rigidbody body;
    static PlayerMelee melee;
    static readonly Vector3 Origin = new(1000, 0, 1000);
    static float moved, launchedDamage, launchedDistance;
    static int launches;
    static PlayerInputFrame Frame(bool down = false, bool held = false, bool up = false) => new() {
        attackDown = down, attackHeld = held, attackUp = up, hasAimDirWS = true, aimDirWS = Vector3.right };
    static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static IEnumerator Seconds(float seconds) { float end = Time.time + seconds; while(Time.time < end) yield return null; }
    static IEnumerator Ready()
    {
        actor.ClearScriptedInput(); attack.CancelAttack(); actor.GetComponent<PlayerShieldAbility>().ForceStopShield();
        yield return Seconds(attack.Effective_attackCooldown + .08f);
        body.position = Origin; body.linearVelocity = Vector3.zero;
        actor.SetScriptedInput(Frame()); yield return null; yield return null;
    }
    static IEnumerator StartHold() { actor.SetScriptedInput(Frame(true,true)); yield return Until(()=>attack.IsCharging,"Hold begins"); }
    static IEnumerator Fire(float seconds, bool auto = false)
    {
        yield return Ready(); Vector3 start = body.position;
        yield return StartHold();
        Check(!attack.IsAttacking && attack.CurrentStage == null && !melee.TuningHitbox.enabled, "Holding starts charge without stage or damage collider");
        if (seconds > 0) yield return Until(() => !attack.IsCharging || attack.ChargeElapsed >= seconds, "Requested hold");
        if (!auto) actor.SetScriptedInput(Frame(up: true));
        yield return Until(() => attack.IsAttacking, "Thrust fires");
        float q = auto ? 1f : seconds / 3f;
        Check(Mathf.Abs(launchedDamage - Mathf.Lerp(1,2,q)) < .025f, "Damage at " + seconds + "s = " + launchedDamage.ToString("0.000"));
        Check(Mathf.Abs(launchedDistance - attack.Profile.GetStage(0).TravelDistance * Mathf.Lerp(1,3,q)) < .08f, "Travel at " + seconds + "s = " + launchedDistance.ToString("0.000"));
        Check(actor.visualsController.ThrustChargeVisualOffsetWS == Vector3.zero, "Vibration clears at launch");
        yield return Until(() => !attack.IsAttacking, "Thrust completes"); yield return null;
        moved = Vector3.Distance(body.position, start);
        Check(attack.CurrentDamageMultiplier == 1f, "Damage resets after Thrust");
    }
    static IEnumerator Checks()
    {
        Check(tuning.combat.thrustChargeEnabled && tuning.combat.thrustChargeMaxSeconds == 3 &&
            tuning.combat.thrustChargeMaxDistanceMultiplier == 3 && tuning.combat.thrustChargeMaxDamageMultiplier == 2,
            "Saved global defaults: 3-second charge, 3x distance, 2x damage");
        var root = new GameObject("Charge fixture"); owned.Add(root); root.SetActive(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), root.transform);
        actor = go.GetComponent<PlayerControllerScript>(); actor.ConfigureDemonstration(root.transform, 0, 1); actor.transform.position = Origin;
        var scale = go.GetComponent<PlayerScaleAdjuster>(); scale.UseGlobalModifiers = false; scale.Size = 1;
        body = go.GetComponent<Rigidbody>(); body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        attack = actor.attackController; tuning.combat.lockOnEnabled = false;
        root.SetActive(true); yield return null; yield return null;
        melee = actor.GetComponentInChildren<PlayerMelee>(true);
        attack.OnStageStarted.AddListener(stage => { launches++; launchedDamage = attack.CurrentDamageMultiplier; launchedDistance = (float)Get(attack, "stageTravelDistanceWS"); });
        yield return GuideChecks(root);
        yield return Fire(0); float tap = moved;
        yield return Fire(1.5f); float half = moved;
        int before = launches; yield return Fire(3, true); float full = moved;
        Check(half > tap * 1.85f && half < tap * 2.15f && full > tap * 2.85f && full < tap * 3.15f,
            "Actual Rigidbody travel ~1:2:3 ("+tap.ToString("0.00")+", "+half.ToString("0.00")+", "+full.ToString("0.00")+")");
        yield return Seconds(1);
        Check(launches == before + 1 && !attack.IsCharging && !attack.IsAttacking, "Holding after auto-fire produces exactly one Thrust");
        actor.SetScriptedInput(Frame(up:true)); yield return Seconds(.1f);
        Check(launches == before + 1, "Release after auto-fire cannot fire again");

        yield return Ready(); yield return StartHold(); yield return Until(() => attack.Charge01 >= .2f, "Early vibration");
        Vector3 still = body.position; float early = 0, late = 0;
        for(int i=0;i<10;i++) { early += actor.visualsController.ThrustChargeVisualOffsetWS.sqrMagnitude; yield return null; }
        Capture("charging-early.png"); yield return Until(() => attack.Charge01 >= .8f, "Late vibration");
        for(int i=0;i<10;i++) { late += actor.visualsController.ThrustChargeVisualOffsetWS.sqrMagnitude; yield return null; }
        Capture("charging-late.png");
        Check(late > early * 3 && early > 0, "Visual vibration grows with charge");
        Check(Vector3.Distance(still, body.position) < .001f && !melee.TuningHitbox.enabled, "Vibration never moves Rigidbody or enables damage");
        float charge = attack.Charge01; Time.timeScale=0; for(int i=0;i<8;i++) yield return null;
        Check(Mathf.Approximately(charge,attack.Charge01), "Pause freezes charge"); Time.timeScale=1;
        attack.CancelAttack(); actor.SetScriptedInput(Frame(up:true)); yield return null; yield return null;
        Check(!attack.IsCharging && !attack.IsAttacking && actor.visualsController.ThrustChargeVisualOffsetWS == Vector3.zero, "Cancel clears charge and vibration");
        yield return Ready(); yield return StartHold();
        actor.SetScriptedInput(new PlayerInputFrame { attackHeld=true, shieldDown=true, shieldHeld=true }); yield return null; yield return null;
        Check(!attack.IsCharging && !attack.IsAttacking && actor.shieldOn, "Shield cancels charge");
        yield return Ready(); yield return StartHold(); actor.ExternalStun(.3f); yield return null; yield return null;
        Check(!attack.IsCharging && !attack.IsAttacking, "External stun cancels charge"); yield return Seconds(.35f);
        actor.SetScriptedInput(Frame(up:true)); yield return null; yield return null;
        Check(!attack.IsAttacking, "Release after stun cannot launch");
        yield return Ready(); yield return StartHold(); actor.SetMatchInputLocked(true); yield return null; yield return null;
        Check(!attack.IsCharging, "Input lock cancels charge"); actor.SetMatchInputLocked(false);
        yield return Ready(); yield return StartHold(); attack.enabled=false;
        Check(!attack.IsCharging, "Disable clears charge"); attack.enabled=true;
        yield return Ready(); yield return StartHold(); actor.SetControlMode(PlayerControlMode.Disabled);
        Check(!attack.IsCharging, "Input ownership change clears charge"); actor.SetControlMode(PlayerControlMode.Scripted);
        yield return Ready(); tuning.combat.thrustChargeEnabled=false; actor.SetScriptedInput(Frame(true,true)); yield return null; yield return null;
        Check(attack.IsAttacking && !attack.IsCharging && attack.CurrentDamageMultiplier==1, "Toggle restores immediate uncharged attack"); tuning.combat.thrustChargeEnabled=true;
        yield return Ready(); yield return StartHold(); attack.HandleAttackInput(false,true,false,false);
        Check(!attack.IsCharging && !attack.IsAttacking, "Power-up consumed input cancels charge");

        yield return Ready();
        var victimGo=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"),root.transform); victimGo.SetActive(false);
        var victim=victimGo.GetComponent<PlayerControllerScript>(); victim.ConfigureDemonstration(root.transform,2,2);
        victimGo.transform.position=Origin+Vector3.forward*20; victimGo.GetComponent<Rigidbody>().useGravity=false; victimGo.SetActive(true); yield return null; yield return null;
        Set(victim,"massScore",.9f); Set(victim,"_invulnUntil",0f); Set(victim,"_timeOfLastShrink",float.NegativeInfinity);
        var npcGo=new GameObject("Charge NPC target"); owned.Add(npcGo); npcGo.transform.position=Origin+Vector3.forward*30;
        var enemy=npcGo.AddComponent<EnemyBase>(); var def=ScriptableObject.CreateInstance<EnemyDefinition>(); owned.Add(def);
        def.healthMassEq=20; def.damageTakenPerSwordHit=2; enemy.Init(def,null); enemy.ConfigureDemonstration(root.transform,actor); npcGo.AddComponent<SphereCollider>(); var hurt=npcGo.AddComponent<EnemyHurtbox>();
        actor.SetScriptedInput(Frame(true,true)); yield return Until(()=>attack.IsAttacking,"Full charge damage test");
        PlayerHitResult accepted=default; int hits=0; victim.HitAccepted += hit=>{ accepted=hit; hits++; };
        Call(melee,"ResolvePlayerImpact",victim.GetComponent<Collider>());
        Check(hits==1 && accepted.accepted && Mathf.Abs(accepted.requestedScale01-2)<.001f &&
            Mathf.Abs(accepted.massLost01-2*(float)Get(victim,"massLossPerHit"))<.001f,"Player takes 2x damage in one transaction");
        Call(hurt,"OnTriggerEnter",melee.TuningHitbox);
        Check(Mathf.Abs(enemy.HealthRemaining-16)<.001f,"NPC receives 2x sword damage (health="+enemy.HealthRemaining+")");
        Check(Mathf.Abs(Vector3.Angle(Vector3.right,attack.ConstrainThrustDirectionWS(Vector3.left))-15)<.01f,"Charged Thrust retains +/-15 degree steering");
        yield return Until(()=>attack.TimeInStage>=attack.ComboWindowSeconds(attack.CurrentStage).x,"Thrust combo window");
        actor.SetScriptedInput(Frame(true,true)); yield return Until(()=>attack.CurrentStageIndex==1,"Swipe combo");
        Check(attack.CurrentDamageMultiplier==1 && !attack.IsCharging && (float)Get(attack,"stageDistanceMultiplier")==1,"Swipe resets charge bonuses and starts on press");
        yield return Until(()=>attack.TimeInStage>=attack.ComboWindowSeconds(attack.CurrentStage).x,"Swipe combo window");
        actor.SetScriptedInput(Frame(true,true)); yield return Until(()=>attack.CurrentStageIndex==2,"Repulsor combo");
        Check(attack.CurrentDamageMultiplier==1 && !attack.IsCharging,"Repulsor remains uncharged");
        yield return Ready(); Object.Destroy(victimGo); Object.Destroy(npcGo);
        yield return LatticeChecks(root); Object.Destroy(root); yield return null; tuning.combat.lockOnEnabled=true;
        var lab=Object.FindFirstObjectByType<PlayerActionTestArena>(); lab.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        PlayerDemoGallery gallery=null;
        yield return Until(()=>(gallery=Object.FindFirstObjectByType<PlayerDemoGallery>()) && gallery.IsPlaying,"Gallery starts");
        yield return Until(()=>gallery.Demos.All(d=>d.SuccessfulLoops>=2 || d.LastFailure!=null),"Seven demo loops");
        Check(gallery.Demos.All(d=>d.SuccessfulLoops>=2 && d.LastFailure==null),"All seven demos complete two successful loops");
        lab.SetMode(PlayerActionTestArena.TestMode.Manual); yield return null;
        using(var data=new SerializedObject(tuning)) {
            data.FindProperty("combat").FindPropertyRelative("thrustChargeMaxSeconds").floatValue=2.75f;
            data.FindProperty("combat").FindPropertyRelative("thrustChargeMaxDistanceMultiplier").floatValue=2.5f;
            data.FindProperty("combat").FindPropertyRelative("thrustChargeMinMoveMultiplier").floatValue=.4f;
            data.FindProperty("combat").FindPropertyRelative("thrustChargeGuideStrokeWidth").floatValue=.06f;
            data.ApplyModifiedProperties(); PlayerTuningEditing.Save(tuning); }
    }
    static IEnumerator LatticeChecks(GameObject root)
    {
        var gridGo=new GameObject("Charge LATTICE fixture"); gridGo.SetActive(false); gridGo.transform.SetParent(root.transform);
        gridGo.transform.SetPositionAndRotation(Origin+Vector3.down*.09f,Quaternion.Euler(270,0,0));
        var grid=gridGo.AddComponent<VectorGridGPU>(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(VectorGridGPU.Instance),grid); grid.registerAsDefault=false; grid.size=new Vector2(28,12);
        var field=gridGo.AddComponent<Massive.Lattice.LatticeDisruptionField>(); field.mode=Massive.Lattice.LatticeDisruptionField.FieldMode.Disconnected;
        var motor=actor.gameObject.AddComponent<Massive.Lattice.LatticePlayerMotor>(); motor.field=field;
        gridGo.SetActive(true); yield return Until(()=>field.IsReady,"LATTICE field ready"); yield return Seconds(1.3f);
        yield return Ready(); Vector3 start=body.position;
        actor.SetScriptedInput(Frame(true,true)); yield return Until(()=>attack.Charge01>=.8f,"LATTICE guide");
        attack.GetChargeGuidePath(out _,out var gridDirection,out float gridDistance);
        Check(Vector3.Angle(gridDirection,Vector3.right)<.01f && Mathf.Abs(gridDistance-field.Spacing*3)<.001f,"LATTICE guide uses snapped heading and actual maximum node travel");
        yield return Until(()=>attack.IsAttacking,"Charged LATTICE launch");
        Check(attack.IsLatticeAttack,"Charge uses quantized motion in LATTICE"); yield return Until(()=>!attack.IsAttacking,"LATTICE completes");
        Check(Mathf.Abs(Vector3.Distance(body.position,start)-field.Spacing*3)<.02f,"Full charge advances three LATTICE nodes");
        yield return Ready(); var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(wall);
        wall.transform.position=Origin+Vector3.right*field.Spacing*1.55f; wall.transform.localScale=new Vector3(.025f,2,2); Physics.SyncTransforms();
        actor.SetScriptedInput(Frame(true,true)); yield return Until(()=>attack.IsAttacking,"Blocked LATTICE launch"); yield return Until(()=>!attack.IsAttacking,"Blocked LATTICE completes");
        Check(body.position.x<=Origin.x+field.Spacing+.02f,"Charged grid travel checks intermediate nodes and stops at blockers");
        Object.Destroy(wall); motor.enabled=false; gridGo.SetActive(false);
    }
    static void Capture(string name)
    {
        var go=new GameObject("Charge capture"); var camera=go.AddComponent<Camera>(); camera.enabled=false;
        camera.cameraType=CameraType.Preview; camera.orthographic=true; camera.orthographicSize=1.25f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.03f,.045f); camera.allowHDR=false;
        camera.transform.SetPositionAndRotation(body.position+Vector3.up*20,Quaternion.LookRotation(Vector3.down,Vector3.forward));
        var rt=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32); rt.Create(); camera.targetTexture=rt;
        var pixels=new Texture2D(768,768,TextureFormat.RGBA32,false); var previous=RenderTexture.active;
        camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,768,768),0,0); pixels.Apply();
        File.WriteAllBytes(Output+"/"+name,pixels.EncodeToPNG()); RenderTexture.active=previous;
        camera.targetTexture=null; rt.Release(); Object.Destroy(rt); Object.Destroy(pixels); Object.Destroy(go);
    }
}
#endif
