#if UNITY_EDITOR
using System;
using System.Collections;
using Massive.Player;
using Massive.Scoring;
using Shapes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerHealthHudValidation
{
    private const string Key = "MASSIVE.HealthHud.Validation.";
    private static IEnumerator run;
    private static float resumeAt;
    private static int resumeFrame, passed;
    private static double deadline;
    public static string LastReport => SessionState.GetString(Key + "Report", "Not run");
    static PlayerHealthHudValidation() { EditorApplication.playModeStateChanged += OnState; }

    [MenuItem("MASSIVE/Players/Run Health HUD Validation")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Wait for Edit Mode and compilation.");
        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/HealthHudValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Health HUD camera");
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            new GameObject("Health HUD light").AddComponent<Light>().type = LightType.Directional;
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Cannot save fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", path);
        SessionState.SetString(Key + "Report", "Running");
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + "Background", Application.runInBackground);
            Application.runInBackground = true;
            passed = 0; run = Checks(); resumeAt = 0f; resumeFrame = 0;
            deadline = EditorApplication.timeSinceStartup + 30d;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; run = null;
            Application.runInBackground = SessionState.GetBool(Key + "Background", Application.runInBackground);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/HealthHudValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key + "Pending", false);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAILED: timed out"); return; }
        if (!EditorApplication.isPlaying || run == null || Time.unscaledTime < resumeAt || Time.frameCount < resumeFrame) return;
        try
        {
            if (run.MoveNext())
            {
                resumeAt = Time.unscaledTime + (run.Current is float seconds ? seconds : 0.02f);
                resumeFrame = Time.frameCount + 2;
                return;
            }
            Finish(passed + " health HUD checks passed (isolated Play Mode).");
        }
        catch (Exception ex) { Finish("FAILED after " + passed + " checks: " + ex); }
    }

    private static void Finish(string report)
    {
        SessionState.SetString(Key + "Report", report);
        Debug.Log("[Health HUD Validation] " + report);
        EditorApplication.update -= Tick;
        run = null;
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        passed++;
    }

    private static PlayerControllerScript CreatePlayer(int id, float spawnHealth = 0.5f, bool pseudo = false)
    {
        var go = new GameObject("Health test player " + id);
        go.SetActive(false);
        var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        var player = go.AddComponent<PlayerControllerScript>();
        player.playerID = id; player.teamID = id < 2 ? 1 : 2;
        player.SetControlMode(PlayerControlMode.Disabled);
        var settings = new SerializedObject(player);
        settings.FindProperty("playMatchSpawnOnSceneLoad").boolValue = false;
        settings.FindProperty("isPseudoPlayer").boolValue = pseudo;
        settings.FindProperty("spawnMassScore01").floatValue = spawnHealth;
        settings.FindProperty("deathFxSeconds").floatValue = 0.15f;
        settings.FindProperty("respawnDelaySeconds").floatValue = 0.25f;
        settings.FindProperty("respawnFxSeconds").floatValue = 0.15f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(true);
        return player;
    }

    private static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + "Scene", ""), "Isolated fixture required");
        var field = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHealthHudSetup.PlayingFieldPath);
        var authored = field.GetComponentsInChildren<PlayerHealthPresenter>(true);
        Check(authored.Length == 4 && field.GetComponentsInChildren<PlayerScoreChainPresenter>(true).Length == 4,
            "Four health widgets and four existing multiplier widgets are present");
        var widgets = new PlayerHealthPresenter[4];
        var players = new PlayerControllerScript[4];
        var texts = new TMP_Text[4];
        var fills = new Rectangle[4];
        var tracks = new Rectangle[4];
        foreach (var source in authored)
        {
            int id = source.PlayerID;
            var sourceSettings = new SerializedObject(source);
            var multiplier = (PlayerScoreChainPresenter)sourceSettings.FindProperty("playerSource").objectReferenceValue;
            Check(multiplier.PlayerID == id && source.transform.localPosition.y > multiplier.transform.localPosition.y &&
                  Mathf.Approximately(source.transform.localPosition.x, multiplier.transform.localPosition.x),
                "Health widget is aligned above its own player's multiplier");
            widgets[id] = Object.Instantiate(source.gameObject).GetComponent<PlayerHealthPresenter>();
            var settings = new SerializedObject(widgets[id]);
            texts[id] = (TMP_Text)settings.FindProperty("healthText").objectReferenceValue;
            fills[id] = (Rectangle)settings.FindProperty("healthFill").objectReferenceValue;
            tracks[id] = (Rectangle)settings.FindProperty("healthTrack").objectReferenceValue;
        }
        yield return 0.04f;
        Check(texts[0].text == "--" && !fills[0].enabled, "Unbound health does not invent a full or live player");
        for (int i = 0; i < 4; i++) players[i] = CreatePlayer(i);
        yield return 0.6f;
        for (int i = 0; i < 4; i++)
            Check(widgets[i].Player == players[i] && texts[i].text == "50%" &&
                  Mathf.Abs(fills[i].Width / tracks[i].Width - 0.5f) < 0.002f,
                "Late player registration binds the correct widget and shows authored 50% spawn health");

        players[0].ApplyExternalMassDelta(-0.25f);
        yield return 0.5f;
        Check(texts[0].text == "25%" && Mathf.Abs(fills[0].Width / tracks[0].Width - 0.25f) < 0.002f && texts[1].text == "50%",
            "Damage changes only the victim's exact health and fill");

        players[0].massScore = 0.15f;
        yield return 0.04f;
        Check(!widgets[0].IsLowHealth && fills[0].Color == Color.white,
            "Exactly 15% is outside the low health warning");
        players[0].massScore = 0.149f;
        yield return 0.04f;
        var frame = tracks[0].transform.parent.GetComponent<Rectangle>();
        Check(widgets[0].IsLowHealth && fills[0].Color.r > fills[0].Color.g * 4f &&
              frame.Color == fills[0].Color && texts[0].color == fills[0].Color &&
              fills[1].Color == Color.white && !widgets[1].IsLowHealth,
            "Below 15% turns only that player's fill, frame, and number red");
        float brightest = 0f, dimmest = 1f;
        float until = Time.unscaledTime + 0.8f;
        while (Time.unscaledTime < until)
        {
            brightest = Mathf.Max(brightest, fills[0].Color.r);
            dimmest = Mathf.Min(dimmest, fills[0].Color.r);
            yield return 0.03f;
        }
        Check(brightest > 0.9f && dimmest < 0.3f && dimmest > 0f,
            "Low health actually blinks between readable bright and dim red phases");
        widgets[0].enabled = false;
        Check(!widgets[0].IsLowHealth && fills[0].Color == Color.white, "Disable restores the normal palette");
        widgets[0].enabled = true;
        Check(widgets[0].IsLowHealth && fills[0].Color.r > 0.9f, "Re-enable restarts the low-health warning brightly");
        players[0].ApplyExternalMassDelta(0.02f);
        yield return 0.04f;
        Check(!widgets[0].IsLowHealth && fills[0].Color == Color.white && frame.Color == Color.white &&
              texts[0].color == Color.black, "Healing above the threshold immediately restores normal colors");
        players[0].ApplyExternalMassDelta(2f);
        yield return 0.6f;
        Check(texts[0].text == "100%" && Mathf.Approximately(fills[0].Width, tracks[0].Width), "Healing to the cap fills the entire frame");
        players[0].massScore = 0.001f;
        yield return 0.75f;
        Check(texts[0].text == "1%" && fills[0].Width > 0f && fills[0].Width < tracks[0].Width * 0.002f,
            "Tiny positive health remains alive with no minimum white badge: " + texts[0].text +
            " fill=" + fills[0].Width / tracks[0].Width);
        texts[0].ForceMeshUpdate();
        Check(!texts[0].isTextOverflowing && texts[0].fontSharedMaterial.GetColor("_OutlineColor") == Color.white &&
              texts[0].fontSharedMaterial.GetFloat("_OutlineWidth") > 0f,
            "Health number fits and has contrast on empty black track");

        int life = players[0].LifeSequence;
        players[0].ApplyExternalMassDelta(-1f);
        yield return 0.08f;
        Check(players[0].temporarilyEliminated && texts[0].text == "0%" && fills[0].Width == 0f && !fills[0].enabled,
            "Actual lethal damage immediately empties the bar");
        Check(!widgets[0].IsLowHealth && frame.Color == Color.white,
            "Death stops blinking and retains a steady empty frame");
        yield return 0.35f;
        Check(texts[0].text == "0%", "Death display stays empty while the respawn is forming");
        yield return 0.7f;
        Check(!players[0].temporarilyEliminated && players[0].LifeSequence > life && texts[0].text == "50%" &&
              Mathf.Abs(fills[0].Width / tracks[0].Width - 0.5f) < 0.002f,
            "Actual respawn restores the authored health and HUD");

        widgets[0].enabled = false;
        players[0].ApplyExternalMassDelta(-0.1f);
        widgets[0].enabled = true;
        Check(texts[0].text == "40%" && Mathf.Abs(widgets[0].DisplayedHealth01 - 0.4f) < 0.001f,
            "Re-enabling immediately resynchronizes health");
        players[0].massScoreMin = 0.2f; players[0].massScoreMax = 0.8f; players[0].massScore = 0.5f;
        yield return 0.04f;
        Check(texts[0].text == "50%", "Custom health bounds normalize correctly");
        players[0].massScoreMax = players[0].massScoreMin;
        yield return 0.04f;
        Check(texts[0].text == "0%" && !fills[0].enabled, "Invalid zero health range does not produce NaN fill");
        players[0].gameObject.SetActive(false);
        CreatePlayer(0, 0.9f, true);
        yield return 0.04f;
        Check(texts[0].text == "--", "Inactive and pseudo players cannot keep a stale health display");
        var replacement = CreatePlayer(0, 0.7f);
        yield return 0.6f;
        Check(widgets[0].Player == replacement && texts[0].text == "70%", "A replacement player rebinds automatically");
        Check(PlayerHealthPresenter.DisplayPercentage(0.9999f) == 99 && PlayerHealthPresenter.DisplayPercentage(0f) == 0,
            "Zero and one hundred are reserved for actual empty and full health");
        players[0] = replacement;
        IEnumerator layoutChecks = TeamPlayerHudLayoutValidation.Checks(players, Check);
        while (layoutChecks.MoveNext()) yield return layoutChecks.Current;
    }
}
#endif
