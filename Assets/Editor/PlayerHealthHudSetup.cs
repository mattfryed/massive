#if UNITY_EDITOR
using System;
using Massive.Player;
using Massive.Scoring;
using Shapes;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerHealthHudSetup
{
    public const string PlayingFieldPath = "Assets/Prefabs/PLAYING FIELD.prefab";
    public const string NumberMaterialPath = "Assets/Scripts/Player/Health Number.mat";

    [MenuItem("MASSIVE/Players/Set Up Health HUD")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Set up the health HUD in Edit Mode.");
        GameObject root = PrefabUtility.LoadPrefabContents(PlayingFieldPath);
        try
        {
            foreach (PlayerScoreChainPresenter multiplier in root.GetComponentsInChildren<PlayerScoreChainPresenter>(true))
                Configure(multiplier);
            PrefabUtility.SaveAsPrefabAsset(root, PlayingFieldPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Configure(PlayerScoreChainPresenter multiplier)
    {
        string name = "Player " + (multiplier.PlayerID + 1) + " health";
        // Re-running setup preserves authored health-widget placement and style.
        if (multiplier.transform.parent.Find(name) != null) return;
        GameObject widget = Object.Instantiate(multiplier.gameObject, multiplier.transform.parent);
        widget.name = name;
        widget.transform.localPosition = multiplier.transform.localPosition + Vector3.up * 0.35f;
        var copied = widget.GetComponent<PlayerScoreChainPresenter>();
        var copiedSettings = new SerializedObject(copied);
        var text = (TMP_Text)copiedSettings.FindProperty("multiplierText").objectReferenceValue;
        var fill = (Rectangle)copiedSettings.FindProperty("progressRectangle").objectReferenceValue;
        var track = (Rectangle)copiedSettings.FindProperty("progressTrackRectangle").objectReferenceValue;
        Object.DestroyImmediate(copied);

        widget.transform.Find("Player text").GetComponent<TMP_Text>().text = "P" + (multiplier.PlayerID + 1);
        text.transform.parent.parent.name = "Health display";
        text.transform.parent.name = "Health frame";
        text.name = "Health percentage";
        fill.name = "Health fill";
        track.name = "Health track";
        text.text = "50%";
        text.fontSharedMaterial = EnsureNumberMaterial(text.fontSharedMaterial);
        text.UpdateMeshPadding();
        fill.Width = track.Width * 0.5f;

        var presenter = widget.AddComponent<PlayerHealthPresenter>();
        var settings = new SerializedObject(presenter);
        settings.FindProperty("playerID").intValue = multiplier.PlayerID;
        settings.FindProperty("playerSource").objectReferenceValue = multiplier;
        settings.FindProperty("healthText").objectReferenceValue = text;
        settings.FindProperty("healthFill").objectReferenceValue = fill;
        settings.FindProperty("healthTrack").objectReferenceValue = track;
        settings.FindProperty("healthFrame").objectReferenceValue = track.transform.parent.GetComponent<Rectangle>();
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material EnsureNumberMaterial(Material source)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(NumberMaterialPath);
        if (material != null) return material;
        material = new Material(source) { name = "Health Number" };
        material.SetColor("_OutlineColor", Color.white);
        material.SetFloat("_OutlineWidth", 0.14f);
        AssetDatabase.CreateAsset(material, NumberMaterialPath);
        return material;
    }
}
#endif
