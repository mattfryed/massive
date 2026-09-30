#if UNITY_EDITOR
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Apply derived mount/dimension transforms on the editor thread, as part of the same Undo group.
public abstract class ParticleBeamTurretInspector : Editor
{
    protected virtual void OnEnable() { Undo.undoRedoPerformed += Refresh; }
    protected virtual void OnDisable() { Undo.undoRedoPerformed -= Refresh; }
    public override void OnInspectorGUI()
    {
        if (!DrawDefaultInspector()) return;
        if (!Application.isPlaying)
            foreach (Component component in targets)
                Undo.RecordObjects(component.GetComponentsInChildren<Transform>(true).Cast<Object>()
                    .Concat(component.GetComponentsInChildren<Collider>(true)).ToArray(), "Update turret geometry and mount");
        Refresh();
    }
    private void Refresh()
    {
        foreach (Component component in targets)
        {
            if (!component || EditorUtility.IsPersistent(component)) continue;
            var controller = component.GetComponent<ParticleBeamTurretController>();
            var visual = component.GetComponent<ParticleBeamTurretVisuals>();
            if (!Application.isPlaying && controller) controller.ApplyWallMount();
            if (visual) { visual.ApplyDimensions(); visual.Rebuild(); }
            if (Application.isPlaying) continue;
            foreach (var item in component.GetComponentsInChildren<Transform>(true).Cast<Object>()
                .Concat(component.GetComponentsInChildren<Collider>(true)))
                if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
        SceneView.RepaintAll();
    }
}
[CustomEditor(typeof(ParticleBeamTurretController)), CanEditMultipleObjects]
public sealed class ParticleBeamTurretControllerInspector : ParticleBeamTurretInspector { }
[CustomEditor(typeof(ParticleBeamTurretVisuals)), CanEditMultipleObjects]
public sealed class ParticleBeamTurretVisualsInspector : ParticleBeamTurretInspector { }
#endif
