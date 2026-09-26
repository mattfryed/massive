#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>Targeted authoring migration for the approved NOVA finale.</summary>
public static class NovaFinaleSetup
{
    private const string FeaturePath = "Assets/Prefabs/Levels/NOVA_Feature.prefab";
    private static void Set(Object target, string property, Object value)
    {
        var so=new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo();
        if(PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    [MenuItem("MASSIVE/Levels/Apply NOVA Finale")]
    public static string Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!=NovaLevelSetup.NovaPath || scene.isDirty)
            throw new InvalidOperationException("Open saved NOVA in Edit Mode before this migration.");
        var root=PrefabUtility.LoadPrefabContents(FeaturePath);
        try
        {
            var star=root.GetComponentInChildren<NovaStarController>(true);
            var rumble=star.GetComponent<NovaStarRumble>();
            var visuals=star.transform.Find("Star Visuals");
            if(!visuals)
            {
                visuals=new GameObject("Star Visuals").transform;
                visuals.SetParent(star.transform,false);
                foreach(var ps in star.GetComponentsInChildren<ParticleSystem>(true))
                    ps.transform.SetParent(visuals,true);
                // The visible core shared a transform with a collider. Copy only its rendering.
                foreach(var renderer in star.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).ToArray())
                {
                    var surface=new GameObject(renderer.name+" Visual",typeof(MeshFilter),typeof(MeshRenderer));
                    surface.transform.SetParent(visuals,false);
                    surface.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);
                    surface.transform.localScale=renderer.transform.localScale;
                    surface.GetComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    EditorUtility.CopySerialized(renderer,surface.GetComponent<MeshRenderer>());
                    renderer.enabled=false;
                }
            }
            rumble.visualRoot=visuals; rumble.star=star;
            var audio=star.GetComponent<AudioSource>();
            if(!audio)audio=star.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake=false; audio.loop=false; audio.spatialBlend=0f;
            rumble.telegraphSource=audio;
            rumble.telegraphClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SineVFX/Volumetric3DLasers/Resources/SFX/StartWave_03.mp3");
            star.rumble=rumble;
            star.nuggetPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab").GetComponent<MatterNuggetScript>();
            star.nuggletPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/Mass Nugglet.prefab").GetComponent<MatterNuggetScript>();
            var matter=root.transform.Find("Nova Matter");
            if(!matter){matter=new GameObject("Nova Matter").transform; matter.SetParent(root.transform,false);}
            star.matterRoot=matter;
            PrefabUtility.SaveAsPrefabAsset(root,FeaturePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var live=Object.FindFirstObjectByType<NovaStarController>();
        var gm=Object.FindFirstObjectByType<GameManagerScript>();
        live.match=gm;
        live.roster=Object.FindFirstObjectByType<PlayerRosterController>();
        Set(gm,"regulationFinale",live);
        PrefabUtility.RecordPrefabInstancePropertyModifications(live);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var profile=AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        var so=new SerializedObject(profile);
        so.FindProperty("rulesetVersion").intValue=4;
        var rules=so.FindProperty("rewards");
        for(int i=0;i<rules.arraySize;i++)
        {
            var rule=rules.GetArrayElementAtIndex(i);
            if(rule.FindPropertyRelative("key").stringValue!=ScoreRewardKeys.NovaCoreCapture)continue;
            rule.FindPropertyRelative("multiplierEligible").boolValue=false;
            rule.FindPropertyRelative("ignoreAllMultipliers").boolValue=true;
            rule.FindPropertyRelative("bonusOnly").boolValue=true;
            rule.FindPropertyRelative("chainEffect").intValue=0;
            rule.FindPropertyRelative("chainCharge").floatValue=0;
        }
        so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(profile);

        var def=AssetDatabase.LoadAssetAtPath<AnomalyDefinition>("Assets/Scripts/Anomalies/NOVA/Anomaly-NOVA-core.asset");
        def.shortDescription="ENTER THE CORE BEFORE COLLAPSE";
        EditorUtility.SetDirty(def); AssetDatabase.SaveAssetIfDirty(def);
        var copy=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Scripts/Anomalies/NOVA/NOVA-Anomaly UI Copy.asset");
        var cs=new SerializedObject(copy);
        cs.FindProperty("topWarning.topLabelText").stringValue="FINAL CORE ENTRY";
        cs.FindProperty("topActive.topLabelText").stringValue="CORE COLLAPSE BONUS";
        cs.FindProperty("lowerDuring.headerText").stringValue="CAPTURE BONUS ENERGY";
        cs.FindProperty("lowerDuring.bodyText").stringValue="Align with incoming particles\nMatch attack presses to sub-particle count\n\nFlat bonus energy. Multipliers do not apply.";
        cs.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(copy);
        SetupResults();
        return "NOVA scene, feature prefab, flat bonus rule, anomaly copy and results breakdown saved.";
    }

    private static void SetupResults()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/S-0_POSTGAME.unity",OpenSceneMode.Additive);
        try
        {
            var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PostGameScreenController>(true)).Single();
            var so=new SerializedObject(controller);
            foreach(var side in new[]{"Light","Dark"})
            {
                string field="score"+side+"BreakdownText";
                var source=(TMP_Text)so.FindProperty("score"+side+"Text").objectReferenceValue;
                if(so.FindProperty(field).objectReferenceValue)
                {
                    var existing=(TMP_Text)so.FindProperty(field).objectReferenceValue;
                    existing.rectTransform.anchoredPosition=source.rectTransform.anchoredPosition+new Vector2(0,-1.5f);
                    existing.rectTransform.sizeDelta=source.rectTransform.sizeDelta;
                    existing.rectTransform.pivot=source.rectTransform.pivot;
                    existing.alignment=source.alignment;
                    existing.fontSize=3f;
                    EditorUtility.SetDirty(existing);
                    continue;
                }
                var go=new GameObject("REGULATION + BONUS",typeof(RectTransform),source.GetType());
                go.transform.SetParent(source.transform.parent,false);
                var label=go.GetComponent<TMP_Text>();
                label.font=source.font; label.fontSharedMaterial=source.fontSharedMaterial;
                label.fontSize=3f; label.color=source.color; label.alignment=source.alignment;
                label.enableWordWrapping=false; label.raycastTarget=false;
                label.rectTransform.anchorMin=source.rectTransform.anchorMin;
                label.rectTransform.anchorMax=source.rectTransform.anchorMax;
                label.rectTransform.pivot=source.rectTransform.pivot;
                label.rectTransform.sizeDelta=source.rectTransform.sizeDelta;
                label.rectTransform.anchoredPosition=source.rectTransform.anchoredPosition+new Vector2(0,-1.5f);
                label.text="REGULATION  0 meV\nCORE COLLAPSE  +0 meV";
                go.SetActive(false);
                Set(controller,field,label);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
    }
}
#endif
