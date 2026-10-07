#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyFlexiblePlacementSetup
{
    const string Folder = "Library/EnemyFlexiblePlacement";
    static EnemyFlexiblePlacementSetup() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/setup.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/setup.request");
        try { Configure(); }
        catch (Exception e) { File.WriteAllText(Folder + "/setup.txt", "FAILED\n" + e); Debug.LogException(e); }
    };
    [MenuItem("MASSIVE/Encounters/Use flexible formations and wall quadrants in current scene")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
            throw new InvalidOperationException("Save the current scene in Edit Mode before configuring its encounter layout.");
        var directors = Object.FindObjectsByType<EnemyDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(d => d.gameObject.scene == scene && d.encounterTimeline && d.arenaLayout).ToArray();
        if (directors.Length == 0) throw new InvalidOperationException("No encounter Director in the current scene.");
        int formations = 0, ranges = 0;
        foreach (var form in directors.SelectMany(d => d.encounterTimeline.cues)
            .SelectMany(c => new[] { c.formation, c.fallback }.Concat(c.variants)).Where(f => f).Distinct())
        {
            // Preserve the already-flexible Drone / Ranged Drone tuning.
            if (form.integrity == EnemyFormation.Integrity.Flexible) continue;
            Undo.RecordObject(form, "Make encounter formation flexible");
            form.integrity = EnemyFormation.Integrity.Flexible;
            if (form.slots.Any(s => string.IsNullOrEmpty(s.socket)))
            {
                float radius = form.slots.Max(s => s.enemy ? s.enemy.GetSpawnRadiusWorld() : 0f);
                form.maxPositionAdjustment = Mathf.Max(form.maxPositionAdjustment, Mathf.Max(1.5f, Mathf.Ceil((radius + .5f) * 2f) / 2f));
            }
            form.blockedSlotGrace = Mathf.Max(3f, form.blockedSlotGrace);
            EditorUtility.SetDirty(form); AssetDatabase.SaveAssetIfDirty(form); formations++;
        }
        foreach (var layout in directors.Select(d => d.arenaLayout).Distinct())
        {
            Undo.RecordObject(layout, "Use wall quadrants");
            foreach (var mount in layout.sockets)
            {
                // Only migrate the standard top/bottom mounts; authored side-wall
                // fallbacks and custom ranges retain their level-specific geometry.
                if (mount.useRange || !(mount.id == "TopLeft" || mount.id == "TopRight" || mount.id == "BottomLeft" || mount.id == "BottomRight")) continue;
                float x = mount.id.EndsWith("Left") ? -1f : 1f, y = mount.id.StartsWith("Top") ? 1f : -1f;
                mount.useRange = true; mount.rangeStart = new Vector2(0, y); mount.rangeEnd = new Vector2(x, y); ranges++;
            }
            EditorUtility.SetDirty(layout);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save encounter layout.");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/setup.txt", $"COMPLETE\nScene: {scene.path}\nShared formations made flexible: {formations}\nWall quadrants enabled: {ranges}\n");
        Debug.Log($"[Encounter placement] {formations} formations made flexible; {ranges} wall quadrants enabled in {scene.name}.");
    }
}
#endif
