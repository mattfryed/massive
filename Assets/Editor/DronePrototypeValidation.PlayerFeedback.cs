#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DronePrototypeValidation
{
    [MenuItem("MASSIVE/Players/Run Enemy Hit Feedback Validation")]
    public static void StartPlayerFeedbackValidation()
    {
        Start();
        SessionState.SetBool(Key + "FeedbackOnly", true);
    }

    private static IEnumerator EnemyHitFeedbackChecks()
    {
        var paletteChecks = PlayerPrefabPaletteChecks();
        while (paletteChecks.MoveNext()) yield return paletteChecks.Current;
        var p = Player(2, 1, new Vector3(0, 0, -15));
        p.gameObject.tag = "Player";
        p.gameObject.SetActive(false);
        var body = p.GetComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var quad = new GameObject("Feedback visual"); quad.transform.SetParent(p.transform, false);
        var visual = quad.AddComponent<PlayerVisualController>();
        visual.blobMat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players.prefab")
            .GetComponentInChildren<PlayerVisualController>(true).blobMat;
        var shape = new GameObject("Visuals"); shape.transform.SetParent(quad.transform, false);
        visual.visuals = shape.transform; visual.idleWobble = 0f; visual.idleTeardrop = 0f;
        visual.teardropMoveWeight = 0f; p.visualsController = visual;
        p.gameObject.SetActive(true);
        var d = Drone(new Vector3(20, 0, -15), false);
        var source = d.gameObject;
        yield return .1f;
        CapturePlayerFeedback(p, "massive-player-hit-before.png");
        Color fill = visual.blobMat.GetColor("_FillColor"), outline = visual.blobMat.GetColor("_OutlineColor");
        Vector3 start = body.position;
        source.transform.position = start + Vector3.right * 2f;
        var result = p.ApplyExternalMassDelta(-.04f, source);
        Check(result.accepted && visual.EnemyDamageFeedbackActive, "Accepted enemy damage starts feedback");
        Check(body.linearVelocity.x < -1f && Mathf.Abs(body.linearVelocity.z) < .001f, "Recoil points away from attacker");
        Check(Mathf.Abs(visual.blobMat.GetFloat("_DamageAngle")) < .01f, "Right-side hit dents the right side");
        Check(visual.blobMat.GetFloat("_OutlineHalf") > visual.outlineHalf * 1.5f, "Visible outline pulse at impact");
        float droneImpulse = visual.blobMat.GetFloat("_DamageImpulse");
        CapturePlayerFeedback(p, "massive-player-hit-impact.png");
        yield return .12f;
        CapturePlayerFeedback(p, "massive-player-hit-ripple.png");
        Check(body.position.x < start.x - .035f, "Recoil visibly moves an idle player");
        Check(visual.blobMat.GetColor("_FillColor") == fill && visual.blobMat.GetColor("_OutlineColor") == outline, "Damage preserves team colors");
        yield return .5f;
        Check(!visual.EnemyDamageFeedbackActive && visual.blobMat.GetFloat("_DamageImpulse") == 0f &&
            Mathf.Abs(visual.blobMat.GetFloat("_OutlineHalf") - visual.outlineHalf) < .0001f,
            "Pulse restores authored outline and shape: active=" + visual.EnemyDamageFeedbackActive + " impulse=" + visual.blobMat.GetFloat("_DamageImpulse") +
            " outline=" + visual.blobMat.GetFloat("_OutlineHalf") + " base=" + visual.outlineHalf);
        Check(Vector3.Distance(body.position, start) < .4f && body.linearVelocity.magnitude < .15f, "Idle recoil settles within a small displacement");
        CapturePlayerFeedback(p, "massive-player-hit-recovered.png");

        p.massScore = .8f; body.linearVelocity = Vector3.zero;
        source.transform.position = p.transform.position + Vector3.forward * 2f;
        p.ApplyExternalMassDelta(-.08f, source);
        Check(visual.blobMat.GetFloat("_DamageImpulse") > droneImpulse * 1.9f, "Larger damage increases deformation");
        Check(body.linearVelocity.z < -1.9f && Mathf.Abs(body.linearVelocity.x) < .001f, "Forward hit recoils backwards in XZ");
        Check(Mathf.Abs(visual.blobMat.GetFloat("_DamageAngle") - Mathf.PI * .5f) < .01f, "Forward hit uses local XZ shader angle");
        for (int i = 0; i < 8; i++) p.ApplyExternalMassDelta(-.005f, source);
        Check(body.linearVelocity.magnitude <= 2.001f && visual.blobMat.GetFloat("_DamageImpulse") <= 1f, "Rapid swarm hits cannot stack speed or distortion");
        yield return .5f;
        p.teamID = 2; yield return .04f;
        Check(visual.blobMat.GetColor("_FillColor") == fill && visual.blobMat.GetColor("_OutlineColor") == outline,
            "Child visual preserves its existing palette when the parent belongs to the dark team");
        source.transform.position = p.transform.position + Vector3.left * 2f;
        p.ApplyExternalMassDelta(-.04f, source);
        CapturePlayerFeedback(p, "massive-player-hit-team2.png");
        p.SetScriptedInput(new PlayerInputFrame { move = Vector2.up });
        yield return .1f;
        Check(!p.IsStunned && Mathf.Abs(body.linearVelocity.z) > .1f, "Steering stays responsive during recoil");
        p.ClearScriptedInput(); yield return .5f;

        // A real wall must stop recoil; it is not a transform displacement.
        body.linearVelocity = Vector3.zero; start = body.position;
        var wall = Own(new GameObject("Feedback collision wall"));
        wall.transform.position = start + Vector3.left * .42f;
        wall.AddComponent<BoxCollider>().size = new Vector3(.1f, 2f, 4f);
        Physics.SyncTransforms(); source.transform.position = start + Vector3.right * 2f;
        p.massScore = .8f; p.ApplyExternalMassDelta(-.08f, source);
        yield return .35f;
        Check(body.position.x > wall.transform.position.x + .27f, "Recoil respects a solid wall");
        Object.Destroy(wall); yield return .1f;

        p.massScore = .8f; body.linearVelocity = Vector3.zero;
        p.ApplyExternalMassDelta(.05f, source);
        p.ApplyExternalMassDelta(-.01f); // Environmental drains have no enemy source.
        Check(!visual.EnemyDamageFeedbackActive && body.linearVelocity == Vector3.zero, "Healing and sourceless drains do not create an enemy hit");
        var invuln = new SerializedObject(p); invuln.FindProperty("respawnInvulnSeconds").floatValue = .6f; invuln.ApplyModifiedPropertiesWithoutUndo();
        typeof(PlayerControllerScript).GetField("_invulnUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, Time.time + 1f);
        result = p.TryApplyHit(source);
        Check(!result.accepted && !visual.EnemyDamageFeedbackActive && body.linearVelocity == Vector3.zero, "Rejected invulnerable hit has no feedback");
        typeof(PlayerControllerScript).GetField("_invulnUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, -1f);

        // Exercise actual Drone contact gates, not a synthetic damage result.
        d.enabled = true; d.spawnGraceSeconds = 0f; d.detectionRange = 0f; d.idleSpeed = 0f;
        source.transform.position = p.transform.position + Vector3.right * 2f;
        p.SetScriptedInput(new PlayerInputFrame { shieldHeld = true }); yield return .04f;
        float mass = p.massScore;
        InvokeFeedbackContact(d, "ResolveContact", p.GetComponent<Collider>());
        Check(p.massScore == mass && !visual.EnemyDamageFeedbackActive, "Shielded Drone contact produces no damage pulse");
        p.ClearScriptedInput(); yield return .04f;
        InvokeFeedbackContact(d, "ResolveContact", p.GetComponent<Collider>());
        Check(p.massScore < mass && visual.EnemyDamageFeedbackActive, "Actual Drone contact routes directional feedback");
        yield return .5f;

        var contactSource = Drone(p.transform.position + Vector3.left * 2f, false);
        var touch = contactSource.gameObject.AddComponent<EnemyTouchDamage>();
        InvokeFeedbackContact(touch, "TryDamage", p.GetComponent<Collider>());
        Check(visual.EnemyDamageFeedbackActive && body.linearVelocity.x > 0f, "Generic enemy touch preserves its hit source");
        Remove(contactSource); yield return .5f;

        var dysonGo = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Enemy System/Enemy Types/Melee/DysonSphere/Enemy_DysonSphere.prefab"), p.transform.position + Vector3.right * 2f, Quaternion.identity));
        var dyson = dysonGo.GetComponent<DysonSphereController>(); dyson.enabled = false;
        dysonGo.GetComponent<EnemyBase>().Init(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
            "Assets/Enemy System/Enemy Types/Melee/DysonSphere/ED_DysonSphere.asset"), null);
        dyson.SendMessage("BeginLunge");
        InvokeFeedbackContact(dyson, "HandleImpact", p.GetComponent<Collider>());
        Check(visual.EnemyDamageFeedbackActive && body.linearVelocity.x < 0f, "Dyson lunge routes directional feedback");
        Object.Destroy(dysonGo); yield return .5f;

        // Projectile direction survives both center overlap and same-frame destruction.
        var projectileGo = Own(new GameObject("Feedback projectile")); projectileGo.transform.position = p.transform.position;
        projectileGo.AddComponent<SphereCollider>().isTrigger = true;
        var projectile = projectileGo.AddComponent<EnemyProjectileBase>(); projectile.useRigidbody = false; projectile.speed = 0f;
        projectile.Init(null, Vector3.back);
        InvokeFeedbackContact(projectile, "TryHandlePlayer", p.GetComponent<Collider>());
        Check(visual.EnemyDamageFeedbackActive && body.linearVelocity.z < 0f, "Projectile impact uses travel direction even at player center");
        yield return .05f;
        Check(!projectile && visual.EnemyDamageFeedbackActive, "Feedback remains valid after source despawns");
        p.SetWorldGameplaySuppressed(true);
        Check(!visual.EnemyDamageFeedbackActive && body.linearVelocity == Vector3.zero, "World suppression clears feedback and recoil");
        p.SetWorldGameplaySuppressed(false); yield return .05f;
        var lethalSource = Drone(p.transform.position + Vector3.right * 2f, false);
        p.massScore = .5f; p.ApplyExternalMassDelta(-.04f, lethalSource.gameObject);
        Check(visual.EnemyDamageFeedbackActive, "Pulse active before lethal hit");
        p.ApplyExternalMassDelta(-1f, lethalSource.gameObject);
        Check(p.temporarilyEliminated && !visual.EnemyDamageFeedbackActive, "Death presentation takes over cleanly");
        Remove(lethalSource); Object.Destroy(p.gameObject); yield return .05f;
    }

    private static void InvokeFeedbackContact(MonoBehaviour target, string method, Collider other)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new object[] { other });
    }

    private static IEnumerator PlayerPrefabPaletteChecks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players.prefab");
        var template = Array.Find(prefab.GetComponentsInChildren<PlayerControllerScript>(true), item => item.teamID == 2);
        Check(template != null && template.visualsController != null, "Production dark-team player prefab is available");
        var holder = Own(new GameObject("Dark player palette validation")); holder.SetActive(false);
        holder.transform.position = new Vector3(0, 0, -15);
        var go = Object.Instantiate(template.gameObject, holder.transform);
        go.transform.localPosition = Vector3.zero; go.SetActive(true);
        var player = go.GetComponent<PlayerControllerScript>(); player.SetControlMode(PlayerControlMode.Scripted);
        var settings = new SerializedObject(player);
        settings.FindProperty("playMatchSpawnOnSceneLoad").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        holder.SetActive(true);
        yield return .15f;
        var visual = player.visualsController;
        CapturePlayerFeedback(player, "massive-dark-player-palette.png");
        Check(visual.blobMat.GetColor("_FillColor") == Color.black && visual.blobMat.GetColor("_OutlineColor") == Color.white,
            "Production dark-team player keeps its black body and white outline; actual fill=" + visual.blobMat.GetColor("_FillColor"));
        Check(visual.blobMat != template.visualsController.blobMat, "Production player owns its runtime material");
        var source = Drone(player.transform.position + Vector3.right * 2f, false);
        player.ApplyExternalMassDelta(-.04f, source.gameObject);
        Check(visual.EnemyDamageFeedbackActive && visual.blobMat.GetColor("_FillColor") == Color.black &&
            visual.blobMat.GetColor("_OutlineColor") == Color.white, "Real prefab retains its dark palette during enemy impact");
        CapturePlayerFeedback(player, "massive-dark-player-palette-impact.png");
        yield return .45f;
        Check(!visual.EnemyDamageFeedbackActive && visual.blobMat.GetColor("_FillColor") == Color.black,
            "Real prefab retains its dark palette after impact recovery");
        Remove(source); Object.Destroy(holder); yield return .05f;
    }

    private static void CapturePlayerFeedback(PlayerControllerScript player, string filename)
    {
        var go = new GameObject("Player feedback capture"); var camera = go.AddComponent<Camera>();
        camera.transform.position = player.transform.position + Vector3.up * 3f;
        camera.transform.rotation = Quaternion.Euler(90, 0, 0); camera.orthographic = true; camera.orthographicSize = .75f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f, .23f, .27f);
        camera.cullingMask = 0; // Player command buffer draws the same production shader independently of layers.
        var rt = new RenderTexture(600, 600, 24) { antiAliasing = 4 };
        var tex = new Texture2D(600, 600, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 600, 600), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), filename), tex.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go); }
    }
}
#endif
