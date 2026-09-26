#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authors NOVA's bonus-only presentation without altering shared HUD prefabs.</summary>
public static class NovaBonusPresentationSetup
{
    const string PrefabPath = "Assets/Prefabs/Levels/NOVA_BonusHUD.prefab";

    public static string Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name != "S-3_NOVA" || scene.isDirty)
            throw new InvalidOperationException("Open saved NOVA in Edit Mode first.");
        if (Object.FindFirstObjectByType<NovaBonusPresentation>())
            throw new InvalidOperationException("NOVA bonus HUD is already installed.");
        var ui = Object.FindFirstObjectByType<AnomalyUIController>(FindObjectsInactive.Include);
        var font = ui.instructionText.font;
        var root = new GameObject("NOVA_BonusHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        var presenter = root.AddComponent<NovaBonusPresentation>();
        presenter.canvasRect = (RectTransform)root.transform;
        presenter.regulationHudRoots = Array.Empty<GameObject>();
        presenter.footer = Rect("Bonus footer", root.transform, Vector2.zero, new Vector2(1f, 0f));
        presenter.footer.pivot = new Vector2(.5f, 0f);
        presenter.footer.anchoredPosition = new Vector2(0, 18);
        presenter.footer.sizeDelta = new Vector2(-36, 150);
        var light = Panel("Light team", presenter.footer, 0, .275f, Color.white, Color.black);
        var center = Panel("Instructions", presenter.footer, .292f, .708f, Color.black, Color.white);
        var dark = Panel("Dark team", presenter.footer, .725f, 1, Color.black, Color.white);
        Team(light, font, "LIGHT TEAM", Color.black, out presenter.lightCount, out presenter.lightEnergy);
        Team(dark, font, "DARK TEAM", Color.white, out presenter.darkCount, out presenter.darkEnergy);
        Text("Header", center, font, "CAPTURE BONUS ENERGY", Color.white, 22, .025f, .67f, .975f, .95f);
        Text("Body", center, font,
            "Align with incoming particles.\nMatch attack presses to their particle count.\nFlat bonus energy. Multipliers do not apply.",
            Color.white, 18, .025f, .07f, .975f, .67f);
        var effects = Rect("Capture effects", root.transform, Vector2.zero, Vector2.one);
        var feedback = root.AddComponent<NovaCaptureFeedback>();
        feedback.effectsRoot = effects;
        feedback.font = font;
        feedback.circleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Anomalies/NOVA/CircleIcon_UI.mat");
        presenter.captureFeedback = feedback;
        presenter.footer.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var parent = GameObject.Find("04_UI/LevelUI/NOVA").transform;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
        presenter = instance.GetComponent<NovaBonusPresentation>();
        presenter.match = Object.FindFirstObjectByType<GameManagerScript>();
        presenter.worldCamera = Camera.main;
        presenter.arenaBounds = Object.FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
        presenter.lightGoal = GameObject.Find("02_Arena/Goals/TEAM 1 goal")
            .GetComponentInChildren<Massive.Multiplier.AmplifierGoalCapture>().CapturePoint;
        presenter.darkGoal = GameObject.Find("02_Arena/Goals/TEAM 2 goal")
            .GetComponentInChildren<Massive.Multiplier.AmplifierGoalCapture>().CapturePoint;
        presenter.regulationHudRoots = new[] {
            GameObject.Find("04_UI/MatchHUD/Text Objects/TEAM_01"),
            GameObject.Find("04_UI/MatchHUD/Text Objects/TEAM_02"),
            GameObject.Find("04_UI/MatchHUD/Text Objects/Timer"),
            GameObject.Find("04_UI/LevelUI/NOVA/NovaBounceProgress")
        };
        if (presenter.regulationHudRoots.Any(g => !g) || !presenter.arenaBounds || !presenter.captureFeedback.circleMaterial)
            throw new InvalidOperationException("Missing required presentation binding.");
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);

        ui.lowerThirdRoot.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui.lowerThirdRoot);
        ui.lowerThirdRoot = presenter.footer.gameObject;
        ui.lowerHeaderText = presenter.footer.Find("Instructions/Header").GetComponent<TMP_Text>();
        ui.instructionText = presenter.footer.Find("Instructions/Body").GetComponent<TMP_Text>();
        ui.lightTeamCountText = presenter.lightCount;
        ui.darkTeamCountText = presenter.darkCount;
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
        var seq = Object.FindFirstObjectByType<AnomalyUISequencer>(FindObjectsInactive.Include);
        seq.lowerBoxes = Array.Empty<BoxIntroOutro>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(seq);
        var copy = AssetDatabase.LoadAssetAtPath<AnomalyUICopy>("Assets/Scripts/Anomalies/NOVA/NOVA-Anomaly UI Copy.asset");
        copy.lowerDuring.bodyText = "Align with incoming particles.\nMatch attack presses to their particle count.\nFlat bonus energy. Multipliers do not apply.";
        EditorUtility.SetDirty(copy);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Authored NOVA_BonusHUD prefab, bound scene HUD/goals, and replaced NOVA footer refs.";
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static RectTransform Panel(string name, Transform parent, float left, float right, Color fill, Color line)
    {
        var panel = Rect(name, parent, new Vector2(left, 0), new Vector2(right, 1));
        var border = panel.gameObject.AddComponent<Image>(); border.color = line; border.raycastTarget = false;
        var inner = Rect("Background", panel, Vector2.zero, Vector2.one);
        inner.offsetMin = Vector2.one * 2; inner.offsetMax = Vector2.one * -2;
        var image = inner.gameObject.AddComponent<Image>(); image.color = fill; image.raycastTarget = false;
        return panel;
    }

    static void Team(RectTransform panel, TMP_FontAsset font, string team, Color color, out TMP_Text count, out TMP_Text energy)
    {
        Text("Team", panel, font, team, color, 18, .04f, .79f, .96f, .96f);
        Text("Capture label", panel, font, "PARTICLES CAPTURED", color, 14, .03f, .55f, .48f, .75f);
        Text("Energy label", panel, font, "BONUS ENERGY", color, 14, .5f, .55f, .97f, .75f);
        count = Text("Capture count", panel, font, "00", color, 40, .03f, .06f, .48f, .53f);
        energy = Text("Awarded energy", panel, font, "0 meV", color, 30, .5f, .06f, .97f, .53f);
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string copy, Color color, float size,
        float xMin, float yMin, float xMax, float yMax)
    {
        var rect = Rect(name, parent, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = copy; text.color = color;
        text.fontSize = text.fontSizeMax = size; text.fontSizeMin = size * .7f;
        text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
#endif
