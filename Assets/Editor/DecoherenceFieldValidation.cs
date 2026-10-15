#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Massive.Player;
using Massive.Enemies;
using Massive.Scoring;
using Massive.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Health-scaled erosion, enemy disruption, defense, scoring and roster rendering in a disposable scene.</summary>
[InitializeOnLoad]
public static class DecoherenceFieldValidation
{
    const string Key = "MASSIVE.DecoherenceFieldValidation";
    const string Folder = "Library/DecoherenceFieldValidation";
    static readonly List<string> checks = new(), errors = new();
    static readonly List<Object> owned = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool runInBackground;
    static DecoherenceFieldValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Power-ups/Validate Decoherence Field")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        var original = SceneManager.GetActiveScene();
        string path = "Assets/DecoherenceFieldValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try { if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Could not save fixture."); }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Scene", path); SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "STARTING");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 180;
            runInBackground = Application.runInBackground; Application.runInBackground = true;
            routine = Expand(Checks()); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            (routine as IDisposable)?.Dispose(); routine = null;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            string path = SessionState.GetString(Key + ".Scene", "");
            if (path.StartsWith("Assets/DecoherenceFieldValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key, false);
        }
    }
    static void Log(string text, string stack, LogType type)
    { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(text + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; (routine as IDisposable)?.Dispose(); routine = null;
        foreach (var asset in owned) if (asset) Object.DestroyImmediate(asset);
        owned.Clear(); Application.runInBackground = runInBackground;
        Application.logMessageReceived -= Log;
        bool passed = error == null && errors.Count == 0;
        int passedChecks = 0; foreach (string check in checks) if (check.StartsWith("PASS ", StringComparison.Ordinal)) passedChecks++;
        string report = (passed ? "PASSED" : "FAILED") + " (" + passedChecks + " checks)\n" + string.Join("\n", checks) +
            "\n" + error + "\n" + string.Join("\n", errors);
        File.WriteAllText(Folder + "/report.txt", report);
        Debug.Log("DECOHERENCE FIELD " + report);
        EditorApplication.isPlaying = false;
    }
    static IEnumerator Expand(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
    static IEnumerator Wait(float seconds)
    { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static void Place(PlayerControllerScript player, Vector3 position)
    { player.GetComponent<Rigidbody>().position = position; player.transform.position = position; Physics.SyncTransforms(); }
    static Texture2D Pixels(Camera camera, string name = null)
    {
        var rt = new RenderTexture(800, 800, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(800, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); pixels.Apply();
            if (name != null) File.WriteAllBytes(Folder + "/" + name + ".png", pixels.EncodeToPNG());
            return pixels;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; rt.Release(); Object.DestroyImmediate(rt); }
    }
    static void AppearanceDifference(Camera camera, Action<bool> toggle, string name)
    {
        toggle(false); var before = Pixels(camera);
        toggle(true); var after = Pixels(camera, name);
        int changed = 0;
        try
        {
            var a = before.GetPixels32(); var b = after.GetPixels32();
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].r - b[i].r) > 8) changed++;
            Check(changed > 40, name + " changes visible pixels on the real player (" + changed + ")");
        }
        finally { Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
    }

    static readonly string[] EnemyPaths = {
        "Melee/Drone/ED_Drone", "Ranged/Ranged Drone/ED_RangedDrone", "Melee/Seeker/ED_Seeker",
        "Ranged/Particle Beam Turret/ED_ParticleBeamTurret", "Melee/Carrier/ED_Carrier", "Melee/DysonSphere/ED_DysonSphere" };
    static void Set(object target, string name, object value) => target.GetType().GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    static EnemyDefinition Definition(int index) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/" + EnemyPaths[index] + ".asset");
    static EnemyBase DataEnemy(EnemyDefinition definition, Transform scope, PlayerControllerScript player, Vector3 position, bool compound = false)
    {
        var go = new GameObject(definition.name + " field fixture"); go.transform.SetParent(scope); go.transform.position = position;
        var enemy = go.AddComponent<EnemyBase>(); enemy.ConfigureDemonstration(scope, player); enemy.Init(definition, null);
        var collider = go.AddComponent<SphereCollider>(); collider.radius = .15f; collider.isTrigger = true;
        go.AddComponent<EnemyHurtbox>();
        if (compound) { var child = new GameObject("Second hurtbox"); child.transform.SetParent(go.transform, false); child.AddComponent<SphereCollider>().radius = .15f; child.AddComponent<EnemyHurtbox>(); }
        return enemy;
    }
    static IEnumerator Arm(PlayerControllerScript player)
    {
        var shield = player.GetComponent<PlayerShieldAbility>();
        player.ClearScriptedInput(); shield.ForceStopShield();
        yield return null;
        while (player.powerUps.UsesShieldCharge ? !player.powerUps.CanActivateDecoherence : shield.ActivationCooldownRemaining > 0) yield return null;
        player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true });
        yield return null; yield return null;
        player.SetScriptedInput(new PlayerInputFrame { shieldHeld = true });
        Check(shield.IsActive && player.powerUps.DecoherenceFieldActive, "Ordinary Shield input activates the field");
    }
    static void Stop(PlayerControllerScript player) { player.ClearScriptedInput(); player.GetComponent<PlayerShieldAbility>().ForceStopShield(); }
    static void PlaceEnemy(EnemyBase enemy, Vector3 position)
    { enemy.transform.position = position; Physics.SyncTransforms(); }
    static IEnumerator ChargeChecks(PlayerControllerScript player, DecoherencePowerUpDefinition definition)
    {
        Stop(player); player.powerUps.Clear(); yield return null;
        player.powerUps.Equip(definition, 120f);
        var meter = player.GetComponentInChildren<ParticleAcceleratorVFXModule>(true);
        Check(meter && meter.IsEnergyMeterVisible && Mathf.Approximately(meter.EnergyMeter01, 1), "Uses the Particle Beam energy ring and starts full");
        player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = false }); yield return null; yield return null;
        Check(player.GetComponentInChildren<DecoherenceFieldVisual>().Paint.Deposits.Count == 0, "An already-released press cannot deposit free paint");
        yield return Arm(player);
        var field = player.GetComponentInChildren<DecoherenceFieldVisual>();
        float initialRadius = field.Paint.Deposits[0].radius;
        Check(Mathf.Abs(initialRadius - PlayerScaleAdjuster.BodyRadiusOf(player)) < .001f, "Each activation starts paint at the original player outline");
        float before = player.powerUps.DecoherenceCharge01, began = Time.time;
        yield return Wait(.4f);
        Check(Mathf.Abs(before - player.powerUps.DecoherenceCharge01 - (Time.time - began) / definition.fullMeterPaintSeconds) < .015f, "Charge drains at the configured rate");
        CheckSplitRing(player, meter);
        float grownRadius = field.Paint.Deposits[field.Paint.Deposits.Count - 1].radius;
        Check(grownRadius > initialRadius && Mathf.Abs(grownRadius - definition.fieldRadius * PlayerScaleAdjuster.SizeOf(player) * player.powerUps.DecoherenceCharge01) < .04f,
            "Activation reaches its live charge radius after the short ease");
        yield return Wait(2.9f);
        Check(field.Paint.Deposits[field.Paint.Deposits.Count - 1].radius < grownRadius && Mathf.Approximately(field.Paint.Deposits[0].radius, initialRadius), "New paint shrinks while old paint keeps its original radius");
        Check(player.powerUps.DecoherenceFieldActive, "Charged hold can exceed the ordinary three-second Shield limit");
        Stop(player); float charge = player.powerUps.DecoherenceCharge01; began = Time.time;
        yield return Wait(.3f);
        Check(Mathf.Abs(player.powerUps.DecoherenceCharge01 - charge - (Time.time - began) / definition.emptyToFullRefillSeconds) < .02f, "Release refills at the configured rate");
        Check(Mathf.Abs(meter.EnergyMeter01 - player.powerUps.DecoherenceCharge01) < .01f, "Shared ring tracks the actual remaining charge");
        Check(!meter.transform.Find("Decoherence_ChargeRingEcho").gameObject.activeSelf, "Release recombines the charge ring with the body");
        yield return Wait(definition.paintLifetimeSeconds + .1f);
        Check(field.Paint.Deposits.Count == 0 && field.Paint.Boundary.Count == 0, "Deposits and their outline expire after the configured lifetime");

        definition.fullMeterPaintSeconds = .5f; definition.emptyToFullRefillSeconds = 2f;
        player.powerUps.Equip(definition, 120f); yield return Arm(player); yield return Wait(.53f);
        Check(!player.powerUps.DecoherenceFieldActive && player.powerUps.DecoherenceCharge01 < .15f, "Empty charge ends painting and starts refill");
        player.ClearScriptedInput(); yield return null;
        player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true }); yield return null; yield return null;
        Check(!player.powerUps.DecoherenceFieldActive, "Press below fifteen percent is rejected");
        player.SetScriptedInput(new PlayerInputFrame { shieldHeld = true }); yield return Wait(.4f);
        Check(!player.powerUps.DecoherenceFieldActive && player.powerUps.DecoherenceCharge01 > .15f, "Holding through recovery does not automatically restart the field");
        Check(!meter.transform.Find("Decoherence_ChargeRingEcho").gameObject.activeSelf, "Exhaustion also recombines the charge ring");
        definition.fullMeterPaintSeconds = 6f;
        yield return Arm(player);
        field = player.GetComponentInChildren<DecoherenceFieldVisual>();
        Check(player.powerUps.DecoherenceCharge01 < .5f && player.powerUps.DecoherenceFieldActive, "Fresh press accepts partial charge above the minimum without waiting for full refill");
        int currentStroke = field.Paint.Deposits[field.Paint.Deposits.Count - 1].stroke;
        float restartRadius = 0;
        foreach (var deposit in field.Paint.Deposits) if (deposit.stroke == currentStroke) { restartRadius = deposit.radius; break; }
        Check(Mathf.Abs(restartRadius - PlayerScaleAdjuster.BodyRadiusOf(player)) < .001f, "Partial-charge reactivation restarts the ease from the player outline");
        Stop(player); definition.emptyToFullRefillSeconds = 4f;
        player.powerUps.Clear(); yield return null;
        Check(!meter.IsEnergyMeterVisible && !player.GetComponent<PlayerShieldAbility>().IsActive, "Unequip hides the shared ring and releases the energy-controlled Shield");
        Check(!meter.transform.Find("Decoherence_ChargeRingEcho").gameObject.activeInHierarchy, "Unequip hides the split ring copy");
        var shield = player.GetComponent<PlayerShieldAbility>();
        Check(shield.TryActivate(), "Ordinary Shield can activate after Decoherence unequips"); shield.SetHeld(true);
        yield return Wait(shield.MaxHoldSeconds + .1f);
        Check(!shield.IsActive, "Ordinary Shield retains its original maximum hold");
        player.powerUps.Equip(definition, 120f);
        yield return Arm(player); yield return Wait(.3f);
        var beam = AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>("Assets/Power-ups/Particle Accelerator/PU_ParticleAccelerator.asset");
        Check(beam, "Particle Beam regression fixture exists");
        player.powerUps.Equip(beam, 120f); yield return null; yield return null;
        Check(meter.IsEnergyMeterVisible && !meter.transform.Find("Decoherence_ChargeRingEcho").gameObject.activeSelf &&
            meter.transform.Find("PA_ChargeRing").gameObject.activeInHierarchy, "Switching while split restores the ordinary Particle Beam ring without a lingering copy");
        Stop(player); player.powerUps.Equip(definition, 120f);

        var paint = new DecoherencePaint();
        paint.Paint(Vector3.zero, 1f, 1f, 1, false); paint.Paint(Vector3.right, 1f, 2f, 2, false);
        bool exteriorOnly = true;
        foreach (var arc in paint.Boundary)
        {
            var d = paint.Deposits[arc.deposit]; float a = (arc.from + arc.to) * .5f;
            var point = d.center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * d.radius;
            for (int i = 0; i < paint.Deposits.Count; i++)
                if (i != arc.deposit && Vector3.Distance(point, paint.Deposits[i].center) < paint.Deposits[i].radius - .0001f) exteriorOnly = false;
        }
        Check(exteriorOnly && paint.Boundary.Count > 0, "Merged outline excludes arcs inside overlapping deposits");
        paint.Advance(1.1f);
        Check(!paint.Contains(Vector3.left * .8f) && paint.Contains(Vector3.right), "Each portion of a painted outline expires independently");
        paint.Clear(); paint.Paint(Vector3.zero, 1f, 1f, 1, false);
        paint.Advance(.76f);
        Check(Mathf.Abs(paint.Deposits[0].radius - 1f) < .001f, "Paint keeps its deposited size before the final contraction");
        paint.Advance(.12f);
        Check(Mathf.Abs(paint.Deposits[0].radius - .5f) < .001f && !paint.Contains(Vector3.right * .8f),
            "The last patch shrinks continuously and its damage footprint follows");
        paint.Advance(.11f);
        Check(paint.Deposits.Count == 1 && paint.Deposits[0].radius > 0f && paint.Deposits[0].radius < .01f && paint.Boundary.Count > 0,
            "The final outline reaches a near-zero radius before expiry");
        paint.Advance(.02f);
        Check(paint.Deposits.Count == 0 && paint.Boundary.Count == 0, "No radius or damage footprint remains after expiry");
    }
    static void CheckSplitRing(PlayerControllerScript player, ParticleAcceleratorVFXModule meter)
    {
        // Sample after the body pose, independent of the Editor coroutine's position in the frame.
        meter.SendMessage("LateUpdate");
        var visual = player.visualsController;
        var ring = meter.transform.Find("PA_ChargeRing");
        var echo = meter.transform.Find("Decoherence_ChargeRingEcho");
        Vector3 center = meter.GetVfxOriginWS() + visual.DecoPreJitterWS + visual.RepulsorVisualOffsetWS + visual.ThrustChargeVisualOffsetWS;
        Vector3 offset = visual.DecoSideWS * (.5f * visual.DecoSplitSeparationWorld * visual.DecoSplit01);
        Check(visual.DecoSplit01 > .99f && echo && echo.gameObject.activeInHierarchy &&
            Vector3.Distance(ring.position, center + offset + visual.DecoGhostJitterA_WS) < .0001f &&
            Vector3.Distance(echo.position, center - offset + visual.DecoGhostJitterB_WS) < .0001f,
            "Both charge rings follow their respective body copy, including separation and independent jitter");
        var a = new MaterialPropertyBlock(); var b = new MaterialPropertyBlock();
        ring.GetComponent<Renderer>().GetPropertyBlock(a); echo.GetComponent<Renderer>().GetPropertyBlock(b);
        var dotsA = a.GetVectorArray("_Balls"); var dotsB = b.GetVectorArray("_Balls");
        int count = ring.GetComponent<MetaballSDFInstance>().Count;
        bool same = count == echo.GetComponent<MetaballSDFInstance>().Count && count == Mathf.CeilToInt(meter.EnergyMeter01 * meter.RingDots);
        for (int i = 0; i < count; i++) same &= dotsA[i] == dotsB[i];
        Check(same, "Both split rings display identical remaining energy and fractional dot size");
        int previousDots = meter.RingDots; float previousEnergy = meter.EnergyMeter01;
        try
        {
            Set(meter, "ringDots", 32); meter.SetEnergyMeter(1f, followDecoherenceSplit: true); meter.SendMessage("LateUpdate");
            Check(ring.GetComponent<MetaballSDFInstance>().Count == 32 && echo.GetComponent<MetaballSDFInstance>().Count == 32,
                "Both rings remain complete at the supported 32-dot maximum");
        }
        finally { Set(meter, "ringDots", previousDots); meter.SetEnergyMeter(previousEnergy, followDecoherenceSplit: true); meter.SendMessage("LateUpdate"); }
    }
    static IEnumerator AppearanceChecks(PlayerControllerScript player, DecoherencePowerUpDefinition definition, Camera camera, Transform scope)
    {
        Stop(player); Place(player, Vector3.zero);
        float authoredEase = definition.activationEaseSeconds;
        // Slow only this fixture so early contact can be measured even in a slow Editor.
        definition.activationEaseSeconds = .8f;
        player.powerUps.Equip(definition, 120f);
        float reach = definition.fieldRadius * PlayerScaleAdjuster.SizeOf(player);
        var target = DataEnemy(Definition(4), scope, player, Vector3.right * reach * .8f);
        target.GetComponent<SphereCollider>().radius = reach * .06f;
        Physics.SyncTransforms();
        float health = target.HealthRemaining;
        yield return Arm(player);
        var field = player.GetComponentInChildren<DecoherenceFieldVisual>();
        Check(target.HealthRemaining == health, "An enemy beyond the growing brush receives no premature full-radius damage");
        Object.DestroyImmediate(Pixels(camera, "activation-start"));
        yield return Wait(.12f);
        Object.DestroyImmediate(Pixels(camera, "activation-growing"));
        yield return Wait(.75f);
        Check(target.HealthRemaining < health, "Enemy damage begins when the eased paint actually reaches it");
        Object.DestroyImmediate(Pixels(camera, "activation-grown"));
        float originalZoom = camera.orthographicSize;
        camera.orthographicSize = 1.35f * PlayerScaleAdjuster.SizeOf(player);
        Object.DestroyImmediate(Pixels(camera, "charge-ring-split"));
        Stop(player); Object.Destroy(target.gameObject);
        yield return Wait(.2f);
        Object.DestroyImmediate(Pixels(camera, "charge-ring-rejoined"));
        camera.orthographicSize = originalZoom;
        definition.activationEaseSeconds = authoredEase;

        // Freeze gameplay and render the same merged shape at exactly the same time.
        // Each toggle isolates the new visual layer from player/charge animation.
        field.enabled = false;
        var paint = field.Paint; paint.Clear();
        paint.Paint(Vector3.left * .7f, 1.35f, 8f, 1, false);
        paint.Paint(Vector3.right * .7f, 1.1f, 8f, 2, false);
        Camera.CameraCallback render = cameraToDraw => { if (cameraToDraw == camera) field.DrawShapes(camera); };
        Camera.onPreRender += render;
        int ghosts = definition.ghostOutlineCount;
        float amount = definition.outlineNoiseAmount;
        try
        {
            definition.ghostOutlineCount = 0;
            AppearanceDifference(camera, on => definition.outlineNoiseAmount = on ? amount : 0f, "merged-outline-noise");
            for (int copy = 1; copy <= ghosts; copy++)
            {
                int count = copy;
                AppearanceDifference(camera, on => definition.ghostOutlineCount = on ? count : count - 1, "ghost-outline-" + copy);
            }
            AppearanceDifference(camera, on => { if (on) paint.Advance(.7f); }, "outline-undulation");
            Check(paint.Deposits.Count == 2 && paint.Contains(Vector3.zero) && !paint.Contains(Vector3.right * 2f),
                "Animated ghost outlines leave the painted damage footprint unchanged");
            paint.Clear(); paint.Paint(Vector3.right * 1.5f, 1f, 1f, 1, false);
            Object.DestroyImmediate(Pixels(camera, "recede-full"));
            paint.Advance(.88f); Object.DestroyImmediate(Pixels(camera, "recede-half"));
            paint.Advance(.10f); Object.DestroyImmediate(Pixels(camera, "recede-tip"));
            paint.Advance(.03f); Object.DestroyImmediate(Pixels(camera, "recede-empty"));
        }
        finally
        {
            Camera.onPreRender -= render;
            definition.outlineNoiseAmount = amount; definition.ghostOutlineCount = ghosts;
            field.enabled = true;
        }
        player.powerUps.Equip(definition, 120f); yield return null;
    }
    static int PlayingVoices(AudioSystem audio, AudioClip clip)
    {
        int count = 0;
        foreach (var source in audio.GetComponentsInChildren<AudioSource>())
            if (source.clip == clip && source.isPlaying) count++;
        return count;
    }
    static IEnumerator LifetimeChecks(PlayerControllerScript player, DecoherencePowerUpDefinition definition, Transform scope)
    {
        Stop(player); player.powerUps.Clear(); yield return null;
        definition.secondsPerSwordHit = 1f;
        var durable = Object.Instantiate(Definition(4)); owned.Add(durable); durable.healthMassEq = 50f;
        var enemy = DataEnemy(durable, scope, player, Vector3.right * .7f);
        var beam = AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>("Assets/Power-ups/Particle Accelerator/PU_ParticleAccelerator.asset");
        for (int mode = 0; mode < 3; mode++)
        {
            Place(player, Vector3.zero); player.powerUps.Equip(definition, 120f);
            yield return Arm(player); yield return Wait(.3f);
            var field = player.GetComponentInChildren<DecoherenceFieldVisual>();
            float health = enemy.HealthRemaining;
            if (mode == 0) player.powerUps.OverrideRemainingSeconds(.05f);
            else if (mode == 1) player.powerUps.ForceExpire();
            else player.powerUps.Equip(beam, 120f);
            yield return Wait(.15f);
            Check(field && field.Paint.Deposits.Count > 0 && enemy.HealthRemaining < health && !player.powerUps.DecoherenceFieldActive,
                "Paint survives " + new[] { "natural expiry", "forced expiry", "pickup replacement" }[mode] + " and continues dealing damage");
            Check(!player.GetComponent<PlayerShieldAbility>().IsActive, "Ending the pickup releases active protection");
            if (mode == 2)
            {
                var meter = player.GetComponentInChildren<ParticleAcceleratorVFXModule>(true);
                Check(meter.IsEnergyMeterVisible, "Lingering paint does not hide the replacement beam's charge ring");
                player.powerUps.Equip(definition, 120f); yield return Arm(player);
                Check(player.GetComponentsInChildren<DecoherenceFieldVisual>().Length == 1 &&
                    player.GetComponentInChildren<DecoherenceFieldVisual>() == field,
                    "Reacquiring Decoherence resumes one shared field");
                health = enemy.HealthRemaining; float began = Time.time;
                yield return Wait(.2f);
                Check(Mathf.Abs(health - enemy.HealthRemaining - (Time.time - began)) < .06f,
                    "Reacquisition does not stack old and new field damage");
                player.powerUps.ForceExpire(); yield return Wait(.1f);
            }
            Stop(player); Place(player, Vector3.left * 5);
            health = enemy.HealthRemaining; yield return Wait(.2f);
            Check(enemy.HealthRemaining < health, "Unequipped paint remains at its original world position");
            yield return Wait(definition.paintLifetimeSeconds + .1f);
            Check(!field && !player.GetComponentInChildren<DecoherenceFieldVisual>(), "The final expiry releases the lingering field object");
            health = enemy.HealthRemaining; yield return Wait(.1f);
            Check(Mathf.Approximately(health, enemy.HealthRemaining), "Expired lingering paint cannot keep dealing damage");
        }
        Object.Destroy(enemy.gameObject); Place(player, Vector3.zero);
        for (int mode = 0; mode < 3; mode++)
        {
            player.powerUps.Equip(definition, 120f); yield return Arm(player); yield return Wait(.1f);
            player.powerUps.Equip(beam, 120f);
            var field = player.GetComponentInChildren<DecoherenceFieldVisual>();
            if (mode == 0) player.temporarilyEliminated = true;
            else if (mode == 1) player.gameObject.SetActive(false);
            else player.powerUps.Clear();
            yield return null; yield return null;
            Check(!field, "Lingering paint cleans up on " + new[] { "owner death", "actor disable", "explicit demo clear" }[mode]);
            player.temporarilyEliminated = false; player.gameObject.SetActive(true); Stop(player);
        }
        player.powerUps.Clear(); yield return null;
    }
    static IEnumerator DysonCoreChecks(PlayerControllerScript player, DecoherencePowerUpDefinition definition, Transform scope)
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/Melee/DysonSphere/ED_DysonSphere_Repulsor.asset");
        var go = Object.Instantiate(data.prefab, scope); go.transform.position = Vector3.right * 1.5f;
        var enemy = go.GetComponent<EnemyBase>(); enemy.ConfigureDemonstration(scope, player); enemy.Init(data, null);
        enemy.HoldPosition = true; enemy.AttacksEnabled = false;
        go.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        yield return Wait(3.5f);
        enemy.TakeDamage(enemy.HealthRemaining - .12f, EnemyDamageSource.Environmental);
        yield return Wait(.15f);
        var controller = go.GetComponent<DysonSphereRepulsorController>();
        Check(!enemy.IsDead && controller.panels.ShellRadiusWorld < .001f, "Fractional damage can leave a live Dyson core after its last panel shatters");
        Check(controller.hurtbox.enabled && controller.hurtbox.radius > .01f && controller.attackVolume.radius < .001f,
            "The panel-less core stays hittable without increasing its attack radius");
        player.powerUps.Equip(definition, 120f);
        var field = player.GetComponentInChildren<DecoherenceFieldVisual>();
        var hull = go.GetComponent<SphereCollider>();
        float radius = hull.radius * Mathf.Max(hull.transform.lossyScale.x, hull.transform.lossyScale.y, hull.transform.lossyScale.z);
        field.Paint.Paint(go.transform.position + Vector3.right * radius * .8f, radius * .1f, 1f, 1, false);
        int defeats = 0; enemy.Defeated += hit => { if (hit.source == EnemyDamageSource.Decoherence && hit.creditedPlayer == player) defeats++; };
        yield return Wait(.3f);
        Check(defeats == 1 && (!enemy || enemy.IsDead), "Paint touching only the remaining core finishes the Dyson and credits exactly one defeat");
        player.powerUps.Clear(); if (go) Object.Destroy(go); yield return null;
    }
    static IEnumerator SustainedAudioChecks()
    {
        Check(!AudioSystem.I, "Audio feedback fixture starts without a competing audio service");
        var audio = new GameObject("Sustained damage audio fixture", typeof(AudioListener), typeof(AudioSystem)).GetComponent<AudioSystem>();
        var clip = AudioClip.Create("Silent feedback probe", 48000 * 8, 1, 48000, false); owned.Add(clip);
        var cue = ScriptableObject.CreateInstance<AudioCue>(); owned.Add(cue);
        cue.clips = new[] { clip }; cue.volume = 0; cue.maxSimultaneous = 0; cue.cooldownSeconds = 0;
        var otherCue = Object.Instantiate(cue); owned.Add(otherCue);
        var database = ScriptableObject.CreateInstance<AudioDatabase>(); owned.Add(database);
        database.entries.Add(new AudioDatabase.Entry { assigned = true, value = 910001, name = "FutureEnemyHit", cue = cue });
        database.entries.Add(new AudioDatabase.Entry { assigned = true, value = 910002, name = "OtherEnemyHit", cue = otherCue });
        database.Build(); audio.database = database;
        var aDef = Object.Instantiate(Definition(4)); owned.Add(aDef);
        aDef.healthMassEq = 100; aDef.sfxHit = (AudioEventId)910001; aDef.sfxDeath = (AudioEventId)910002;
        var bDef = Object.Instantiate(Definition(0)); owned.Add(bDef);
        bDef.healthMassEq = 100; bDef.sfxHit = (AudioEventId)910002;
        var first = new GameObject("Carrier feedback probe").AddComponent<EnemyBase>(); first.Init(aDef, null);
        var second = new GameObject("Future drone feedback probe").AddComponent<EnemyBase>(); second.Init(bDef, null);
        int damageEvents = 0; first.Damaged += _ => damageEvents++;
        first.TakeDamage(.1f, EnemyDamageSource.Decoherence, null, EnemyDamageDelivery.Sustained);
        Check(PlayingVoices(audio, clip) == 1, "First sustained contact immediately plays the enemy's own configured cue");
        int calls = 1; float end = Time.unscaledTime + .25f;
        while (Time.unscaledTime < end)
        {
            first.TakeDamage(.1f, EnemyDamageSource.Decoherence, null, EnemyDamageDelivery.Sustained); calls++;
            first.TakeDamage(.1f, EnemyDamageSource.Projectile, null, EnemyDamageDelivery.Sustained); calls++;
            yield return null;
        }
        Check(PlayingVoices(audio, clip) == 1, "Two overlapping sustained sources share one per-enemy audio cadence");
        Check(Mathf.Abs(first.HealthRemaining - (100 - calls * .1f)) < .002f && damageEvents == calls,
            "Audio throttling preserves every damage amount and damage event");
        yield return Wait(.18f);
        first.TakeDamage(.1f, EnemyDamageSource.Projectile, null, EnemyDamageDelivery.Sustained);
        Check(PlayingVoices(audio, clip) == 2, "Sustained audio resumes after the shared interval");
        second.TakeDamage(.1f, EnemyDamageSource.Environmental, null, EnemyDamageDelivery.Sustained);
        Check(PlayingVoices(audio, clip) == 2 && second.HealthRemaining < 100,
            "Crowd spacing applies across different enemy types and different cues without blocking damage");
        yield return Wait(.14f);
        second.TakeDamage(.1f, EnemyDamageSource.Environmental, null, EnemyDamageDelivery.Sustained);
        Check(PlayingVoices(audio, clip) == 3, "An enemy given a new hit cue automatically uses shared sustained feedback");
        first.TakeDamage(.1f, EnemyDamageSource.Sword, null);
        first.TakeDamage(.1f, EnemyDamageSource.ShieldReflect, null);
        Check(PlayingVoices(audio, clip) == 5, "Ordinary impacts bypass sustained-damage cadence");
        first.TakeDamage(200, EnemyDamageSource.Sword, null);
        Check(first.IsDead && PlayingVoices(audio, clip) == 7, "Lethal impact and death cues remain available");

        SceneSfxPolicy.BeginSilence(audio);
        second.TakeDamage(.1f, EnemyDamageSource.Environmental, null, EnemyDamageDelivery.Sustained);
        Check(PlayingVoices(audio, clip) == 0, "Sustained hit sounds obey the existing scene mute policy");
        SceneSfxPolicy.EndSilence(audio);
        second.Init(bDef, null);
        second.TakeDamage(.1f, EnemyDamageSource.Environmental, null, EnemyDamageDelivery.Sustained);
        Check(PlayingVoices(audio, clip) == 1, "A new enemy life resets its sustained audio cadence");
        SceneSfxPolicy.BeginSilence(audio); SceneSfxPolicy.EndSilence(audio);
        Check(!audio.TryPlaySustainedHit((AudioEventId)919999, Vector3.zero) &&
            audio.TryPlaySustainedHit((AudioEventId)910001, Vector3.zero), "Missing audio does not consume the shared sustained budget");
        Object.Destroy(second.gameObject); Object.Destroy(audio.gameObject); yield return null;
    }
    static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + ".Scene", ""), "Disposable scene protects unsaved authored scenes");
        var scope = new GameObject("Decoherence fixtures"); scope.SetActive(false);
        var actorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        var player = Object.Instantiate(actorPrefab, scope.transform).GetComponent<PlayerControllerScript>();
        player.ConfigureDemonstration(scope.transform, 0, 1);
        var opponent = Object.Instantiate(actorPrefab, scope.transform).GetComponent<PlayerControllerScript>();
        opponent.ConfigureDemonstration(scope.transform, 1, 2);
        var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DecoherencePowerUpDefinition>("Assets/Power-ups/Decoherence/PU_Decoherence.asset")); owned.Add(definition);
        scope.SetActive(true); yield return null; yield return null;
        player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        opponent.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        Place(player, Vector3.zero); Place(opponent, Vector3.right * 30);
        player.powerUps.Equip(definition); player.powerUps.OverrideRemainingSeconds(120f);
        var carrier = DataEnemy(Definition(4), scope.transform, player, Vector3.right * 1.5f, true);
        float initial = carrier.HealthRemaining;
        yield return Wait(.15f);
        Check(carrier.HealthRemaining == initial, "Equipping alone causes no field damage");
        yield return Arm(player);
        float contactDeadline = Time.time + definition.activationEaseSeconds + .1f;
        while (carrier.HealthRemaining == initial && Time.time < contactDeadline) yield return null;
        float startHealth = carrier.HealthRemaining, start = Time.time;
        Check(carrier.IsDisrupted, "First contact briefly disrupts an enemy");
        Check(Mathf.Approximately(player.powerUps.ShieldMovementMultiplier(.55f), .9f), "Active field uses its own movement scale");
        yield return Wait(.4f);
        Check(Mathf.Abs(startHealth - carrier.HealthRemaining - (Time.time - start)) < .06f, "One sword-hit equivalent per second; compound colliders do not multiply damage");
        Check(!carrier.IsDisrupted && !carrier.TryDisrupt(.25f, 2f), "Enemy recovers while inside and rejects repeated disruption during immunity");
        Place(player, Vector3.left * 10); Physics.SyncTransforms();
        float afterExit = carrier.HealthRemaining; yield return Wait(.2f);
        Check(carrier.HealthRemaining < afterExit, "Paint keeps damaging at its original position after the owner leaves");
        Check(!player.GetComponentInChildren<DecoherenceFieldVisual>().Paint.Contains(Vector3.left * 5), "Teleport does not paint a bridge across the arena");
        Place(player, Vector3.zero); yield return Wait(.15f);
        Check(carrier.HealthRemaining < afterExit && !carrier.IsDisrupted, "Re-entry continues erosion without another interruption in the same activation");
        carrier.Pause(true); float pausedHealth = carrier.HealthRemaining; yield return Wait(.15f);
        Check(Mathf.Approximately(pausedHealth, carrier.HealthRemaining), "Scene-paused enemies do not take field damage");
        carrier.Pause(false);
        Stop(player); float releasedHealth = carrier.HealthRemaining; yield return Wait(.15f);
        Check(carrier.HealthRemaining < releasedHealth && !player.powerUps.DecoherenceFieldActive, "Release stops painting but deposited ground continues damaging");
        PlaceEnemy(carrier, Vector3.right * 20); releasedHealth = carrier.HealthRemaining; yield return Wait(.15f);
        Check(Mathf.Approximately(releasedHealth, carrier.HealthRemaining), "Leaving painted ground stops damage without restoring health");
        Check(Mathf.Approximately(player.powerUps.ShieldMovementMultiplier(.55f), .55f), "Normal movement scale returns after field ends");
        Object.Destroy(carrier.gameObject); yield return null;

        yield return ChargeChecks(player, definition);
        player.powerUps.Clear(); yield return null;
        player.powerUps.Equip(definition, 120f);

        // Isolate exposure ratios from entry travel time. Growth/contact gets its own
        // test below; ordinary roster and gameplay tests restore the authored ease.
        float authoredEase = definition.activationEaseSeconds;
        definition.activationEaseSeconds = 0f;
        // Accelerate exposure tenfold, preserving all six real health/hit ratios.
        definition.secondsPerSwordHit = .1f;
        var deathTimes = new float[6]; var sources = new EnemyDamageSource[6]; var credits = new PlayerControllerScript[6];
        var timedEnemies = new EnemyBase[6];
        for (int i = 0; i < 6; i++)
        {
            int index = i; float angle = i * Mathf.PI / 3f;
            var enemy = DataEnemy(Definition(i), scope.transform, player, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .8f);
            timedEnemies[i] = enemy;
            enemy.Defeated += hit => { deathTimes[index] = Time.time; sources[index] = hit.source; credits[index] = hit.creditedPlayer; };
        }
        float began = 0; Action<PlayerShieldAbility> onStart = _ => began = Time.time;
        player.GetComponent<PlayerShieldAbility>().ShieldStarted += onStart;
        yield return Arm(player); yield return Wait(2.2f);
        player.GetComponent<PlayerShieldAbility>().ShieldStarted -= onStart;
        for (int i = 0; i < 6; i++) checks.Add("Timing detail " + i + ": death=" + deathTimes[i] + ", start=" + began +
            ", health=" + (timedEnemies[i] ? timedEnemies[i].HealthRemaining.ToString() : "destroyed") + ", active=" + player.powerUps.DecoherenceFieldActive);
        for (int i = 0; i < 6; i++)
        {
            float expected = Definition(i).healthMassEq / Definition(i).damageTakenPerSwordHit * .1f;
            Check(deathTimes[i] > 0 && Mathf.Abs(deathTimes[i] - began - expected) < .08f,
                Definition(i).name + " dies at its sword-hit exposure ratio (" + (deathTimes[i] - began).ToString("F3") + "s, expected " + expected.ToString("F3") + "s)");
            Check(sources[i] == EnemyDamageSource.Decoherence && credits[i] == player, Definition(i).name + " defeat preserves source and player attribution");
        }
        Stop(player); definition.secondsPerSwordHit = 1f; definition.activationEaseSeconds = authoredEase; player.powerUps.Equip(definition, 120f);

        var durable = Object.Instantiate(Definition(4)); owned.Add(durable); durable.healthMassEq = 50f;
        var target = DataEnemy(durable, scope.transform, player, Vector3.right * 1.2f);
        var friendly = DataEnemy(durable, scope.transform, player, Vector3.left); friendly.OwnerTeamId = 1;
        var isolatedScope = new GameObject("Other simulation");
        var isolated = DataEnemy(durable, isolatedScope.transform, null, Vector3.forward);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Field blocking wall";
        wall.transform.position = new Vector3(.6f, .3f, 0); wall.transform.localScale = new Vector3(.2f, 2f, 1.8f); Physics.SyncTransforms();
        yield return Arm(player); yield return Wait(.3f);
        Check(target.HealthRemaining == 50f, "Solid walls block field damage");
        Check(friendly.HealthRemaining == 50f && isolated.HealthRemaining == 50f, "Teammates and other simulation scopes are excluded");
        Object.Destroy(wall); yield return null; yield return Wait(.2f);
        Check(target.HealthRemaining < 50f, "Removing the wall restores nearby contact");
        Stop(player); Object.Destroy(target.gameObject); Object.Destroy(friendly.gameObject); Object.Destroy(isolatedScope);
        player.powerUps.Equip(definition, 120f);

        // Real player defense and per-enemy sword damage overrides.
        var overrideEnemy = DataEnemy(durable, scope.transform, player, Vector3.right * 1.5f);
        Set(overrideEnemy.GetComponent<EnemyHurtbox>(), "damageTakenPerSwordHitOverride", 2f);
        Place(opponent, Vector3.left * 1.5f); opponent.massScore = .9f;
        yield return Arm(player); yield return Wait(definition.activationEaseSeconds);
        float hp = overrideEnemy.HealthRemaining; start = Time.time; float mass = opponent.massScore;
        yield return Wait(.3f);
        Check(Mathf.Abs(hp - overrideEnemy.HealthRemaining - 2f * (Time.time - start)) < .08f, "Per-enemy sword-hit override controls erosion rate");
        Check(opponent.massScore < mass && !opponent.IsExternallyStunned, "Opponents receive separately tuned chip damage without aura stun");
        opponent.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true }); yield return null; yield return null;
        opponent.SetScriptedInput(new PlayerInputFrame { shieldHeld = true }); mass = opponent.massScore; yield return Wait(.2f);
        Check(Mathf.Approximately(opponent.massScore, mass), "Held opponent shield blocks field chip damage");
        Stop(opponent); Place(opponent, Vector3.right * 30);
        player.temporarilyEliminated = true; hp = overrideEnemy.HealthRemaining; yield return Wait(.15f);
        Check(Mathf.Approximately(hp, overrideEnemy.HealthRemaining) && !player.powerUps.DecoherenceFieldActive, "Owner death disables field damage");
        player.temporarilyEliminated = false; Stop(player); Object.Destroy(overrideEnemy.gameObject);

        // Canonical roster bodies, actual mesh fragments, and shield phase regression.
        var camera = new GameObject("Decoherence validation camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 3.4f; camera.aspect = 1;
        camera.transform.SetPositionAndRotation(Vector3.up * 20, Quaternion.Euler(90, 0, 0));
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var light = new GameObject("Fixture light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
        yield return AppearanceChecks(player, definition, camera, scope.transform);
        definition.secondsPerSwordHit = 100f;
        for (int i = 0; i < 6; i++)
        {
            player.powerUps.Equip(definition, 120f);
            var go = Object.Instantiate(Definition(i).prefab, scope.transform);
            go.transform.position = Vector3.right * 1.1f;
            var enemy = go.GetComponent<EnemyBase>(); enemy.ConfigureDemonstration(scope.transform, player); enemy.Init(Definition(i), null);
            enemy.HoldPosition = true; enemy.AttacksEnabled = false;
            var rb = go.GetComponent<Rigidbody>(); if (rb) rb.constraints = RigidbodyConstraints.FreezeAll;
            yield return Wait(4f);
            yield return Arm(player); float health = enemy.HealthRemaining; yield return Wait(.35f);
            Check(enemy.HealthRemaining < health, "Canonical " + Definition(i).name + " accepts field damage");
            var effect = enemy.GetComponent<EnemyDecoherenceVisual>();
            Check(effect && effect.VisibleAmount > .5f, "Canonical " + Definition(i).name + " shows decoherence feedback");
            Object.DestroyImmediate(Pixels(camera, "field-roster-" + i));
            Stop(player); yield return Wait(definition.paintLifetimeSeconds + .3f);
            Check(effect.VisibleAmount < .01f, "Canonical " + Definition(i).name + " re-coheres after painted ground expires");
            Object.Destroy(go); yield return null;
        }
        player.powerUps.Equip(definition, 120f);
        yield return Arm(player);
        Check(player.powerUps.TryHandleShieldImpact(opponent, player.shield.GetComponent<Collider>(), Vector3.right) && opponent.IsExternallyStunned,
            "Original melee phase-through and stun remain functional");
        yield return LifetimeChecks(player, definition, scope.transform);
        yield return DysonCoreChecks(player, definition, scope.transform);

        // Exercise the actual reward adapter/service, not only the defeat event.
        var economy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset")); owned.Add(economy);
        var scoreObject = new GameObject("Field scoring fixture"); scoreObject.SetActive(false);
        var score = scoreObject.AddComponent<MatchScoreService>(); Set(score, "profile", economy); scoreObject.SetActive(true);
        Set(player, "isPseudoPlayer", false); score.RegisterPlayer(player); score.ResetForMatch(true);
        int awards = 0; score.TeamScoreChanged += snapshot => awards++;
        definition.secondsPerSwordHit = .1f; player.powerUps.Equip(definition);
        var scoredEnemy = DataEnemy(Definition(0), scope.transform, player, Vector3.forward * .8f, true);
        int defeats = 0; scoredEnemy.Defeated += _ => defeats++;
        yield return Arm(player); yield return Wait(.3f);
        Check(score.GetTeamScore(1) > 0 && score.GetTeamScore(2) == 0, "Decoherence defeat awards real energy to the attacking team");
        Check(defeats == 1 && awards == 1, "One enemy life awards score exactly once despite compound colliders");
        Set(player, "isPseudoPlayer", true); Stop(player); player.powerUps.Clear();
        yield return SustainedAudioChecks();
        Check(errors.Count == 0, "No runtime errors during field validation");
    }
}
#endif

