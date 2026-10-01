#if UNITY_EDITOR
using System.Collections.Generic;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CarrierVisuals)), CanEditMultipleObjects]
public sealed class CarrierVisualsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!DrawDefaultInspector()) return;
        foreach (CarrierVisuals visual in targets)
        {
            var changed = new List<Object>();
            if (visual.shell) changed.Add(visual.shell.transform);
            if (visual.TryGetComponent<CarrierController>(out var carrier))
            {
                foreach (var dock in carrier.docks) if (dock) changed.Add(dock);
                if (carrier.damageTrigger) changed.Add(carrier.damageTrigger);
            }
            Undo.RecordObjects(changed.ToArray(), "Adjust Carrier body scale");
            visual.ApplyBodyScale();
            foreach (var obj in changed) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }
        SceneView.RepaintAll();
    }
}
#endif
