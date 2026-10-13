#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PostGameResultsSetup
{
    public const string ScenePath = "Assets/Scenes/S-0_POSTGAME-V2.unity";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    // Applied once to the user's duplicate. Future edits remain ordinary scene authoring.
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var controller = Object.FindFirstObjectByType<PostGameScreenController>();
        if ((bool)typeof(PostGameScreenController).GetField("displayExactMilliElectronVolts", Fields).GetValue(controller)) return;
        var light = scene.GetRootGameObjects().Single(go => go.name == "TEAM_01").transform;
        var dark = scene.GetRootGameObjects().Single(go => go.name == "TEAM_02").transform;
        var first = light.Find("PLAYER 1");
        var template = light.Find("PLAYER 2");
        foreach (Transform child in template)
        {
            var copy = Object.Instantiate(child.gameObject, first, false);
            copy.name = child.name;
        }

        // Preserve the existing Team 2 root and mirror the complete authored Team 1 block.
        foreach (Transform child in dark.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        dark.position = new Vector3(-light.position.x, light.position.y, light.position.z);
        dark.rotation = light.rotation;
        dark.localScale = light.localScale;
        foreach (Transform child in light)
        {
            var copy = Object.Instantiate(child.gameObject, dark, false);
            copy.name = child.name;
        }
        foreach (var rect in dark.GetComponentsInChildren<RectTransform>(true))
        {
            var pos = rect.anchoredPosition; pos.x = -pos.x; rect.anchoredPosition = pos;
            rect.pivot = new Vector2(1f - rect.pivot.x, rect.pivot.y);
        }
        foreach (var text in dark.GetComponentsInChildren<TMP_Text>(true)) text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        dark.Find("PLAYER 1").name = "PLAYER 3";
        dark.Find("PLAYER 2").name = "PLAYER 4";
        var third = (RectTransform)dark.Find("PLAYER 3");
        var fourth = (RectTransform)dark.Find("PLAYER 4");
        (third.anchoredPosition, fourth.anchoredPosition) = (fourth.anchoredPosition, third.anchoredPosition);

        var views = new PostGamePlayerStatsView[4];
        for (int team = 1; team <= 2; team++)
        {
            var root = team == 1 ? light : dark;
            Text(root, "SCORE TITLE").text = team == 1 ? "TOTAL LIGHT MASS" : "TOTAL DARK MASS";
            Text(root, "unit scale").text = "meV";
            var breakdown = Text(root, "REGULATION + BONUS");
            breakdown.rectTransform.anchoredPosition = new Vector2(0, -7.7f);
            breakdown.fontSize = 2.2f;
            breakdown.gameObject.SetActive(false);
            for (int member = 0; member < 2; member++)
            {
                int id = (team - 1) * 2 + member;
                var player = (RectTransform)root.Find("PLAYER " + (id + 1));
                var view = new PostGamePlayerStatsView
                {
                    playerID = id, teamID = team, root = player, heading = player.GetComponent<TMP_Text>(),
                    individualScore = Text(player, "INDIVIDUAL SCORE"), playerKills = Text(player, "PLAYER KILLS"),
                    deaths = Text(player, "DEATHS"), enemiesKilled = Text(player, "ENEMIES KILLED"),
                    distanceTraveled = Text(player, "DISTANCE TRAVELED"),
                    twoPlayerPosition = player.anchoredPosition, soloPosition = new Vector2(0, player.anchoredPosition.y)
                };
                view.heading.text = "PLAYER " + (id + 1);
                // Keep the authored value edges, with bounded columns for full meV contributions.
                var score = view.individualScore;
                var rect = score.rectTransform;
                float edge = team == 1 ? rect.rect.xMax : rect.rect.xMin;
                rect.pivot = new Vector2(team == 1 ? 1 : 0, rect.pivot.y);
                rect.anchoredPosition = new Vector2(edge, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(4.7f, rect.sizeDelta.y);
                score.enableAutoSizing = true; score.fontSizeMin = 1.2f; score.fontSizeMax = 4;
                views[id] = view;
            }
        }
        Set(controller, "displayExactMilliElectronVolts", true);
        Set(controller, "playerStats", views);
        Set(controller, "individualScoreLabel", Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(t => t.name == "INDIVIDUAL SCORE" && t.text == "Individual score").gameObject);
        Set(controller, "scoreLightText", Text(light, "SCORE NUMBER"));
        Set(controller, "scoreDarkText", Text(dark, "SCORE NUMBER"));
        Set(controller, "scoreLightUnitText", Text(light, "unit scale"));
        Set(controller, "scoreDarkUnitText", Text(dark, "unit scale"));
        Set(controller, "scoreLightBreakdownText", Text(light, "REGULATION + BONUS"));
        Set(controller, "scoreDarkBreakdownText", Text(dark, "REGULATION + BONUS"));
        // The copied disc is smaller than the old full-width winner treatment.
        ((TMP_Text)typeof(PostGameScreenController).GetField("winnerText", Fields).GetValue(controller)).fontSize = 8f;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    static TMP_Text Text(Transform root, string name) => root.Find(name).GetComponent<TMP_Text>();

    // One-time additive migration. Run in the disposable validation copy, then
    // copy only the reviewed scene back; never overwrite subsequent authoring.
    public static void AddMultiplierBreakdown()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var controller = Object.FindFirstObjectByType<PostGameScreenController>();
        if ((TMP_Text)typeof(PostGameScreenController).GetField("averageLightAmplifierText", Fields).GetValue(controller)) return;
        foreach (int team in new[] { 1, 2 })
        {
            var root = scene.GetRootGameObjects().Single(go => go.name == "TEAM_0" + team).transform;
            var average = CopyMultiplierText(Text(root, "unit scale"), "AVERAGE AMPLIFIER", .40f);
            average.text = "AVG AMPLIFIER  x1.0";
            Set(controller, team == 1 ? "averageLightAmplifierText" : "averageDarkAmplifierText", average);
        }
        var views = (PostGamePlayerStatsView[])typeof(PostGameScreenController).GetField("playerStats", Fields).GetValue(controller);
        foreach (var view in views)
        {
            view.averageMultiplier = CopyMultiplierText(view.individualScore, "AVERAGE MULTIPLIER", .48f);
            view.averageMultiplier.text = "AVG MULTIPLIER  x1.0";
        }
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static TMP_Text CopyMultiplierText(TMP_Text source, string name, float offset)
    {
        var copy = Object.Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = name;
        var text = copy.GetComponent<TMP_Text>();
        text.rectTransform.anchoredPosition += Vector2.down * offset;
        text.fontSize = 2.4f;
        text.fontSizeMin = 2f;
        text.fontSizeMax = 2.4f;
        text.enableAutoSizing = true;
        text.characterSpacing = 0f;
        text.color = Color.white;
        // Subordinate breakdown labels fade in without changing the score animation.
        var transition = new SerializedObject(copy.GetComponent<TMPTextTransition>());
        transition.FindProperty("inEffects").intValue = (int)TMPTextTransition.Effects.Alpha;
        transition.ApplyModifiedPropertiesWithoutUndo();
        return text;
    }

    static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
}
#endif
