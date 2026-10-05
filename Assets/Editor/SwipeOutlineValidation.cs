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
public static class SwipeOutlineValidation
{
    const string Key = "MASSIVE.SwipeOutlineValidation.";
    const string Output = "Library/SwipeOutlineValidation";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new(), errors = new();
    static readonly List<Object> owned = new();
    static int frame;
    static double deadline;
    static PlayerTuningProfile tuning;
    const string AttackPath = "Assets/Scripts/Player/Actions/PlayerAttackProfile.asset";
    static SwipeOutlineValidation()
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
        var arena = Object.FindFirstObjectByType<PlayerActionTestArena>();
        arena.enabled = true; arena.SetMode(PlayerActionTestArena.TestMode.Manual);
        EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Time.captureDeltaTime = .01f;
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
                    var saved = AssetDatabase.LoadAssetAtPath<PlayerAttackProfile>(AttackPath);
                    Check(!saved.GetStage(1).ShowSwipeHitboxGuide, "Saved Swipe guide toggle survives Play Mode exit and reimport");
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
        Debug.Log("SWIPE OUTLINE VALIDATION " + (passed ? "PASSED" : "FAILED"));
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
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static IEnumerator Checks()
    {
        Check(tuning.attackProfile.GetStage(1).ShowSwipeHitboxGuide, "Swipe outline is enabled in the shared attack profile");
        var root = new GameObject("Swipe outline fixture"); owned.Add(root); root.SetActive(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), root.transform);
        var actor = go.GetComponent<PlayerControllerScript>(); actor.ConfigureDemonstration(root.transform, 0, 1);
        actor.transform.position = new Vector3(1000, 0, 1000);
        var scale = actor.GetComponent<PlayerScaleAdjuster>(); scale.UseGlobalModifiers = false; scale.Size = 1f;
        var rb = actor.GetComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
        var attack = actor.attackController;
        var profile = Object.Instantiate(tuning.attackProfile); owned.Add(profile);
        var swipe = profile.GetStage(1);
        Set(swipe, "duration", 1f); Set(swipe, "activationFrameStart", 6f); Set(swipe, "activationFrameEnd", 42f);
        Set(swipe, "travelDistance", 0f); Set(swipe, "rotationArc", 120f);
        attack.UseSharedSettings = false; Set(attack, "attackProfile", profile); Set(attack, "lockOnEnabled", false);
        root.SetActive(true); yield return null; yield return null;
        actor.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.right });
        var melee = actor.GetComponentInChildren<PlayerMelee>(true);
        var guide = melee.GetComponent<PlayerSwipeHitboxGuide>();
        Check(guide && guide.Hitbox == melee.TuningHitbox, "Existing player prefab automatically binds the guide to its real melee collider");
        Check(!guide.IsVisible, "Idle player has no Swipe outline");
        var arcDriver = melee.GetComponent<AttackSwordArcDriver>();
        Check(arcDriver && arcDriver.enabled, "Canonical actor sword has an enabled arc driver");
        Call(attack, "StartStage", 1);
        Check(!guide.IsVisible && !guide.Hitbox.enabled, "No outline during Swipe windup");
        Check(Mathf.Abs(attack.CurrentWeaponYawOffsetDeg + 60f) < .01f && Mathf.Abs(SwipeYaw(attack, guide) + 60f) < .01f,
            "Swipe begins at the left edge before its hit window opens");
        Capture(actor, guide, "windup.png", false);
        yield return Until(() => guide.IsVisible, "Swipe damage window");
        Check(guide.Hitbox.enabled, "Outline appears when the actual collider enables");
        Check(SwipeYaw(attack, guide) < -50f, "Active Swipe starts on the player-relative left");
        CheckSideContact(actor, guide, -50f, "Left");
        ValidateAlignment(attack, guide, "Early Swipe");
        ValidateGeometry(guide, "Early Swipe");
        Capture(actor, guide, "swipe-early.png", true);
        yield return Until(() => attack.StageNormalizedTime >= .4f, "Mid Swipe");
        Check(guide.IsVisible && Mathf.Abs(SwipeYaw(attack, guide)) < 5f, "Live capsule crosses straight ahead halfway through the hit window");
        ValidateAlignment(attack, guide, "Middle Swipe");
        CheckSideContact(actor, guide, 0f, "Front");
        Capture(actor, guide, "swipe-middle.png", true);
        yield return Until(() => attack.StageNormalizedTime >= .68f, "Late Swipe");
        Check(guide.IsVisible && SwipeYaw(attack, guide) > 50f, "Live capsule reaches the right while damage is still active");
        ValidateAlignment(attack, guide, "Late Swipe");
        CheckSideContact(actor, guide, 50f, "Right");
        ValidateGeometry(guide, "Sweeping collider");
        Capture(actor, guide, "swipe-late.png", true);
        foreach (float size in new[] { .66f, 1.5f })
        {
            scale.Size = size; Physics.SyncTransforms(); ValidateGeometry(guide, "Player size " + size);
        }
        scale.Size = 1f;
        Vector3 oldCenter = guide.Hitbox.center; float oldRadius = guide.Hitbox.radius, oldHeight = guide.Hitbox.height;
        guide.Hitbox.center += new Vector3(.2f, 0f, .1f); guide.Hitbox.radius = .35f; guide.Hitbox.height = 1.9f;
        Physics.SyncTransforms(); ValidateGeometry(guide, "Live collider center/width/length edit");
        guide.Hitbox.center = oldCenter; guide.Hitbox.radius = oldRadius; guide.Hitbox.height = oldHeight;
        Set(swipe, "showSwipeHitboxGuide", false);
        Check(!guide.IsVisible && guide.Hitbox.enabled, "Turning off the guide leaves the damaging collider active");
        Set(swipe, "showSwipeHitboxGuide", true);
        Check(guide.IsVisible, "Guide can be restored immediately during the same Swipe");
        yield return Until(() => attack.StageNormalizedTime > .75f, "Swipe recovery");
        Check(!guide.IsVisible && !guide.Hitbox.enabled && attack.IsAttacking, "Outline disappears when damage ends, before the stage finishes");
        Capture(actor, guide, "recovery.png", false);
        attack.CancelAttack(); Call(attack, "StartStage", 1);
        yield return Until(() => guide.IsVisible, "Second Swipe");
        attack.CancelAttack(); Check(!guide.IsVisible, "Cancellation removes the outline immediately");
        Check(Quaternion.Angle(guide.Hitbox.transform.localRotation, Quaternion.identity) < .01f,
            "Cancellation immediately restores the neutral sword pose");
        // Exercise the actual authored 0.1-second hit window, in a rotated player frame.
        Set(swipe, "duration", tuning.attackProfile.GetStage(1).Duration);
        Set(swipe, "activationFrameStart", tuning.attackProfile.GetStage(1).ActivationFrameStart);
        Set(swipe, "activationFrameEnd", tuning.attackProfile.GetStage(1).ActivationFrameEnd);
        actor.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.forward });
        yield return null; yield return null;
        Call(attack, "StartStage", 1);
        float minYaw = 180f, maxYaw = -180f, maxAlignment = 0f; int samples = 0;
        while (attack.IsAttacking)
        {
            if (guide.IsVisible)
            {
                float yaw = SwipeYaw(attack, guide);
                minYaw = Mathf.Min(minYaw, yaw); maxYaw = Mathf.Max(maxYaw, yaw); samples++;
                maxAlignment = Mathf.Max(maxAlignment, Vector3.Angle(guide.Hitbox.transform.right, attack.CurrentAttackVisualDirectionWS));
            }
            yield return null;
        }
        Check(samples >= 4 && minYaw < -45f && maxYaw > 45f,
            "Authored hit window sweeps both sides with rotated aim (" + samples + " samples, " + minYaw.ToString("0.0") + " to " + maxYaw.ToString("0.0") + " degrees)");
        Check(maxAlignment < .1f, "Physical sword and VFX share the heading throughout the authored hit window");
        Check(Quaternion.Angle(guide.Hitbox.transform.localRotation, Quaternion.identity) < .01f, "Completed Swipe restores neutral pose");
        Set(attack, "swipeDirection", -1); Call(attack, "StartStage", 1);
        Check(SwipeYaw(attack, guide) > 59f, "Reverse Swipe starts at the opposite edge");
        yield return Until(() => guide.IsVisible && SwipeYaw(attack, guide) < -40f, "Reverse Swipe reaches left");
        Check(guide.IsVisible, "Reverse Swipe crosses to the opposite side before damage closes");
        arcDriver.enabled = false;
        Check(Quaternion.Angle(guide.Hitbox.transform.localRotation, Quaternion.identity) < .01f, "Disabling the arc driver restores neutral pose");
        arcDriver.enabled = true; attack.CancelAttack();
        Set(attack, "swipeDirection", 1);
        Call(attack, "StartStage", 0); yield return Until(() => melee.TuningHitbox.enabled, "Thrust collider");
        Check(!guide.IsVisible, "Thrust does not display the Swipe guide");
        Check(Quaternion.Angle(guide.Hitbox.transform.localRotation, Quaternion.identity) < .01f, "Thrust retains a straight hitbox after Swipe");
        attack.CancelAttack(); Call(attack, "StartStage", 2);
        yield return Until(() => actor.GetComponentInChildren<PlayerRepulsorAOE>().IsPulseActive, "Repulsor");
        Check(!guide.IsVisible, "Repulsor keeps its own existing guide");
        Object.Destroy(root); yield return null;

        var lab = Object.FindFirstObjectByType<PlayerActionTestArena>(); lab.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        PlayerDemoGallery gallery = null;
        yield return Until(() => (gallery = Object.FindFirstObjectByType<PlayerDemoGallery>()) && gallery.IsPlaying, "Gallery start");
        yield return Until(() => gallery.Demos.All(d => d.SuccessfulLoops >= 2 || d.LastFailure != null), "Demo loops");
        Check(gallery.Demos.All(d => d.SuccessfulLoops >= 2 && d.LastFailure == null), "All seven existing demos complete two successful loops");
        lab.SetMode(PlayerActionTestArena.TestMode.Manual); yield return null;
        using (var data = new SerializedObject(tuning.attackProfile))
        {
            data.FindProperty("stages").GetArrayElementAtIndex(1).FindPropertyRelative("showSwipeHitboxGuide").boolValue = false;
            data.ApplyModifiedProperties(); PlayerTuningEditing.Save(tuning.attackProfile);
        }
    }
    static float SwipeYaw(PlayerAttackController attack, PlayerSwipeHitboxGuide guide) =>
        Vector3.SignedAngle(attack.ForwardReference.right, guide.Hitbox.transform.right, Vector3.up);
    static void ValidateAlignment(PlayerAttackController attack, PlayerSwipeHitboxGuide guide, string label) =>
        Check(Vector3.Angle(guide.Hitbox.transform.right, attack.CurrentAttackVisualDirectionWS) < .1f, label + ": physical hitbox and VFX heading agree");
    static void CheckSideContact(PlayerControllerScript actor, PlayerSwipeHitboxGuide guide, float degrees, string label)
    {
        Physics.SyncTransforms();
        Vector3 sample = actor.transform.position + Quaternion.AngleAxis(degrees, Vector3.up) * actor.attackController.ForwardReference.right * 1.3f;
        Check(Physics.OverlapSphere(sample, .025f, ~0, QueryTriggerInteraction.Collide).Contains(guide.Hitbox),
            label + " target region intersects the live physical damage collider");
    }
    static void ValidateGeometry(PlayerSwipeHitboxGuide guide, string label)
    {
        Physics.SyncTransforms();
        Check(PlayerSwipeHitboxGuide.TryGetFootprint(guide.Hitbox, out Vector3 a, out Vector3 b, out float radius), label + ": capsule outline is available");
        float perimeter = Vector3.Distance(a, b) * 2f + Mathf.PI * radius * 2f;
        float maxError = 0f;
        for (int i = 0; i < 64; i++)
        {
            Vector3 p = PlayerSwipeHitboxGuide.FootprintPoint(a, b, radius, perimeter * i / 64f);
            Vector3 segment = b - a;
            float t = segment.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector3.Dot(p - a, segment) / segment.sqrMagnitude) : 0f;
            Vector3 outward = (p - Vector3.Lerp(a, b, t)).normalized;
            // Compare against PhysX's own surface query from outside the capsule,
            // so an undersized guide cannot pass merely by being inside the collider.
            Vector3 actualSurface = guide.Hitbox.ClosestPoint(p + outward * .03f);
            maxError = Mathf.Max(maxError, Vector3.Distance(actualSurface, p));
        }
        Check(maxError < .001f, label + ": all 64 outline samples match the physical collider (max error " + maxError.ToString("0.00000") + ")");
    }
    static void Capture(PlayerControllerScript actor, PlayerSwipeHitboxGuide guide, string filename, bool visible)
    {
        var go = new GameObject("Swipe guide capture"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.cameraType = CameraType.Preview; camera.orthographic = true; camera.orthographicSize = 2.2f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .045f); camera.allowHDR = false;
        camera.transform.SetPositionAndRotation(actor.transform.position + Vector3.right * .5f + Vector3.up * 20f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var target = new RenderTexture(896, 896, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
        var pixels = new Texture2D(896, 896, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
        try
        {
            camera.Render(); int baseline = ReadYellow(target, pixels);
            guide.DrawShapes(camera); camera.Render(); int drawn = ReadYellow(target, pixels);
            File.WriteAllBytes(Output + "/" + filename, pixels.EncodeToPNG());
            Check(visible ? drawn - baseline > 80 : Mathf.Abs(drawn - baseline) < 10,
                "Rendered outline " + (visible ? "visible" : "absent") + ": " + filename + " (added yellow pixels " + (drawn - baseline) + ")");
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(go); }
    }
    static int ReadYellow(RenderTexture target, Texture2D pixels)
    {
        RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 896, 896), 0, 0); pixels.Apply();
        return pixels.GetPixels32().Count(c => c.r > 170 && c.g > 170 && c.b < 60);
    }
}
#endif
