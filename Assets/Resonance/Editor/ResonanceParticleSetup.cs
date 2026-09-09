using UnityEditor;
using UnityEngine;

namespace Massive.Resonance.Editor
{
    [CustomEditor(typeof(ResonanceRenderComparison))]
    public sealed class ResonanceRenderComparisonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var pair=(ResonanceRenderComparison)target;
            EditorGUILayout.HelpBox("Exactly one complete prototype is active. Both use the same math and interaction code, with independently editable tuning and pattern assets. Switching never stacks their collisions or grid attraction.",MessageType.Info);
            if (GUILayout.Button("Show Option A — Continuous Plasma")) Select(pair,ResonanceRendering.OptionAContinuous);
            if (GUILayout.Button("Show Option B — Particle Grains")) Select(pair,ResonanceRendering.OptionBParticles);
            var active=pair.selected==ResonanceRendering.OptionAContinuous ? pair.optionA : pair.optionB;
            using (new EditorGUI.DisabledScope(active==null))
                if (GUILayout.Button("Select Active Tuning")) Selection.activeObject=active.gameObject;
        }
        private static void Select(ResonanceRenderComparison pair,ResonanceRendering choice)
        {
            if (pair.optionA==null || pair.optionB==null) return;
            Undo.RecordObjects(new Object[]{pair,pair.optionA.gameObject,pair.optionB.gameObject},"Switch Resonance Option");
            pair.Select(choice); EditorUtility.SetDirty(pair);
        }
    }

    public static class ResonanceParticleSetup
    {
        public const string PrefabPath="Assets/Resonance/Resonance 345 Hz - Option B.prefab";
        [MenuItem("MASSIVE/Resonance/Create Particle Option B from Selected")]
        public static void CreateFromSelection()
        {
            var source=Selection.activeGameObject!=null ? Selection.activeGameObject.GetComponent<ResonancePatternController>() : null;
            if (source==null) { Debug.LogWarning("Select the existing Resonance prototype first."); return; }
            Create(source);
        }
        public static ResonanceRenderComparison Create(ResonancePatternController source)
        {
            if (Application.isPlaying || source==null || source.definition==null) throw new System.InvalidOperationException("Create the comparison from a configured prototype in Edit Mode.");
            // Never silently duplicate a comparison or overwrite the existing particle experiment.
            foreach (var existing in Object.FindObjectsByType<ResonanceRenderComparison>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if (existing.optionA==source || existing.optionB==source) { Selection.activeObject=existing.gameObject; return existing; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)
                throw new System.InvalidOperationException("Option B prefab already exists. Instantiate that asset or choose a new explicit destination; it was not overwritten.");
            Shader shader=Shader.Find("MASSIVE/Resonance/Particles");
            if (shader==null) throw new System.InvalidOperationException("Wait for the particle shader to compile first.");
            const string materialPath="Assets/Resonance/Resonance Particles.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,materialPath); }
            var def=Object.Instantiate(source.definition); def.name="345 Hz Pattern - Option B";
            string definitionPath=AssetDatabase.GenerateUniqueAssetPath("Assets/Resonance/345 Hz Pattern - Option B.asset");
            AssetDatabase.CreateAsset(def,definitionPath);
            var go=new GameObject("Resonance 345 Hz — Option B (Particles)"); go.SetActive(false);
            go.transform.SetParent(source.transform.parent,false);
            go.transform.localPosition=source.transform.localPosition; go.transform.localRotation=source.transform.localRotation; go.transform.localScale=source.transform.localScale;
            var clone=go.AddComponent<ResonancePatternController>();
            EditorUtility.CopySerialized(source,clone);
            clone.definition=def; clone.rendering=ResonanceRendering.OptionBParticles; clone.particleMaterial=material;
            Undo.RegisterCreatedObjectUndo(go,"Create Resonance Particle Option B");
            // Save only the new authored controller; transient children are never part of the asset.
            var arena=clone.arenaBounds; var grid=clone.grid; var origin=clone.energyOrigin;
            clone.arenaBounds=null; clone.grid=null; clone.energyOrigin=null;
            go.SetActive(true); clone.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
            clone.enabled=true; clone.arenaBounds=arena; clone.grid=grid; clone.energyOrigin=origin;
            // The reusable prefab should also be enabled, without serializing generated geometry.
            var contents=PrefabUtility.LoadPrefabContents(PrefabPath);
            try { contents.GetComponent<ResonancePatternController>().enabled=true; PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var controls=new GameObject("Resonance — A-B Comparison");
            var pair=controls.AddComponent<ResonanceRenderComparison>(); pair.optionA=source; pair.optionB=clone;
            Undo.RegisterCreatedObjectUndo(controls,"Create Resonance Comparison Controls");
            Undo.RecordObject(source.gameObject,"Show Resonance Particle Option B");
            pair.Select(ResonanceRendering.OptionBParticles); clone.Rebuild();
            Selection.activeObject=controls; EditorUtility.SetDirty(pair);
            return pair;
        }
    }
}
