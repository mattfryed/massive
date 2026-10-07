#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyFormation)), CanEditMultipleObjects]
public sealed class EnemyFormationInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (targets.All(t => EnemyTurretFormationAuthoring.CanFlip((EnemyFormation)t)))
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Flip orientation"))
                {
                    foreach (EnemyFormation formation in targets) EnemyTurretFormationAuthoring.Flip(formation);
                    serializedObject.Update();
                }
            EditorGUILayout.HelpBox("Swaps left/right wall quadrants in this shared formation. To flip one encounter, use its button in the Composer.", MessageType.Info);
        }
        DrawDefaultInspector();
    }
}

[InitializeOnLoad]
public static class EnemyTurretFormationAuthoring
{
    public const string FourQuadrantsPath = EnemyEncounterLabSetup.Folder + "/05c Turret Four Quadrants.asset";
    const string Folder = "Library/EnemyTurretFormations";
    static EnemyTurretFormationAuthoring() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/setup.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/setup.request");
        try
        {
            CreateFourQuadrants();
            File.WriteAllText(Folder + "/setup.txt", "COMPLETE\n" + FourQuadrantsPath);
        }
        catch (Exception e) { File.WriteAllText(Folder + "/setup.txt", "FAILED\n" + e); Debug.LogException(e); }
    };
    public static bool CanFlip(EnemyFormation formation) => formation && formation.slots.Count > 0 && formation.slots.All(s =>
        s != null && s.enemy && s.enemy.prefab && s.enemy.prefab.GetComponent<ParticleBeamTurretController>() && EnemyArenaLayout.FlippedWallMount(s.socket) != null);
    public static void Flip(EnemyFormation formation)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !CanFlip(formation)) return;
        Undo.RegisterCompleteObjectUndo(formation, "Flip turret formation orientation");
        foreach (var slot in formation.slots) slot.socket = EnemyArenaLayout.FlippedWallMount(slot.socket);
        EditorUtility.SetDirty(formation);
        if (AssetDatabase.Contains(formation)) AssetDatabase.SaveAssetIfDirty(formation);
    }
    [MenuItem("MASSIVE/Encounters/Create Four Quadrants turret formation")]
    public static void CreateFourQuadrantsMenu() => Selection.activeObject = CreateFourQuadrants();
    public static EnemyFormation CreateFourQuadrants()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var existing = AssetDatabase.LoadAssetAtPath<EnemyFormation>(FourQuadrantsPath);
        if (existing) return existing;
        var source = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/05 Turret Top Bottom.asset");
        if (!source || !CanFlip(source)) throw new InvalidOperationException("Missing turret formation template.");
        var formation = ScriptableObject.CreateInstance<EnemyFormation>();
        formation.name = "05c Turret Four Quadrants";
        formation.warningSeconds = source.warningSeconds;
        formation.integrity = EnemyFormation.Integrity.Flexible;
        formation.blockedSlotGrace = source.blockedSlotGrace;
        var template = source.slots[0];
        foreach (string mount in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
            formation.slots.Add(new EnemyFormation.Slot { enemy = template.enemy, telegraph = template.telegraph,
                socket = mount, facing = mount.StartsWith("Top") ? Vector2.down : Vector2.up });
        AssetDatabase.CreateAsset(formation, FourQuadrantsPath);
        AssetDatabase.SaveAssetIfDirty(formation);
        return formation;
    }
}
#endif
