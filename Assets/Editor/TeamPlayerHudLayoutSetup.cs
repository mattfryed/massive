#if UNITY_EDITOR
using Massive.Player;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class TeamPlayerHudLayoutSetup
{
    [MenuItem("MASSIVE/Players/Set Up Adaptive Team HUD")]
    public static void Setup()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerHealthHudSetup.PlayingFieldPath);
        try
        {
            ConfigureTeam(root.transform.Find("UI/Text Objects/TEAM_01"), 1, 2, "LIGHT    MASS");
            ConfigureTeam(root.transform.Find("UI/Text Objects/TEAM_02"), 3, 4, "DARK    MASS");
            PrefabUtility.SaveAsPrefabAsset(root, PlayerHealthHudSetup.PlayingFieldPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureTeam(Transform team, int first, int second, string caption)
    {
        var score = team.Find("SCORE NUMBER").GetComponent<TMP_Text>();
        var title = team.Find("Team side").GetComponent<TMP_Text>();
        title.text = caption;
        title.alignment = TextAlignmentOptions.Flush;
        Vector2 size = title.rectTransform.sizeDelta; size.x = score.rectTransform.sizeDelta.x;
        title.rectTransform.sizeDelta = size;
        Vector3 position = title.transform.localPosition; position.x = score.transform.localPosition.x;
        title.transform.localPosition = position;

        var layout = team.GetComponent<TeamPlayerHudLayout>() ?? team.gameObject.AddComponent<TeamPlayerHudLayout>();
        var settings = new SerializedObject(layout);
        settings.FindProperty("teamScoreText").objectReferenceValue = score;
        Wire(settings.FindProperty("firstPlayer"), team, first);
        Wire(settings.FindProperty("secondPlayer"), team, second);
        settings.ApplyModifiedPropertiesWithoutUndo();
        // Keep the authored prefab in its four-player preview; runtime Automatic resolves the roster.
        layout.SetLayoutMode(PlayerHudLayoutMode.TwoVTwo);
        settings.Update();
        settings.FindProperty("layoutMode").enumValueIndex = (int)PlayerHudLayoutMode.Automatic;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Wire(SerializedProperty panel, Transform team, int player)
    {
        panel.FindPropertyRelative("health").objectReferenceValue =
            team.Find("Player " + player + " health").GetComponent<PlayerHealthPresenter>();
        panel.FindPropertyRelative("multiplier").objectReferenceValue =
            team.Find("Player " + player + " multiplier").GetComponent<PlayerScoreChainPresenter>();
    }
}
#endif
