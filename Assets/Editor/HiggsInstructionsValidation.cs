#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Scoring;
using Massive.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HiggsInstructionsValidation
{
    const string Key="MASSIVE.HiggsInstructionsValidation";
    const string Folder=HiggsInstructionsSetup.Folder;
    static readonly List<string> checks=new(), errors=new();
    static double deadline, finishAt;
    static float captureAge;
    static bool entering, capturing, earning, growth, reenabled, background, leaving, secondCapture, outro;
    static int firstKnotID;
    static Vector3 initialPlayerScale;
    static long firstRate;
    static MatchScoreService baselineScore;
    static long baselineLight, baselineDark;
    static int frame;

    static HiggsInstructionsValidation()
    {
        EditorApplication.playModeStateChanged+=State;
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder+"/test.request")) return;
            File.Delete(Folder+"/test.request"); Run();
        };
    }
    [MenuItem("MASSIVE/HIGGS/Validate Instructions Possession Loop")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Run from a saved Edit Mode scene.");
        Directory.CreateDirectory(Folder);
        var prefab=PrefabUtility.LoadPrefabContents(HiggsInstructionsSetup.PanelPath);
        try
        {
            var demo=prefab.GetComponentInChildren<HiggsInstructionsDemo>(true);
            if(!demo || !demo.knotTemplate || !demo.knotRules || !demo.incomeToast || !demo.playerPrefab || !demo.grid || !demo.field || !demo.viewport || !demo.panelFrame)
                throw new InvalidOperationException("Missing instruction demonstration references.");
            if(prefab.GetComponentsInChildren<PlayerControllerScript>(true).Any() || prefab.GetComponentsInChildren<SymmetryKnotController>(true).Any() || prefab.GetComponentsInChildren<HiggsExcitationKnotManager>(true).Any())
                throw new InvalidOperationException("Instruction panel contains live gameplay owners.");
            if(prefab.GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))
                throw new InvalidOperationException("Instruction panel has a missing script.");
            if(demo.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("TEAM SCORE")))
                throw new InvalidOperationException("Preview still contains an extra score footer.");
            var sourceScene=EditorSceneManager.OpenPreviewScene(HiggsInstructionsSetup.GameplayScene);
            try
            {
                var grid=sourceScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VectorGridGPU>(true)).Single();
                var field=sourceScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HiggsFieldGPU>(true)).Single();
                if(demo.grid.gridScale!=grid.gridScale || demo.grid.size!=grid.size || demo.grid.registerAsDefault || demo.field.WorldSizeXZ!=field.WorldSizeXZ)
                    throw new InvalidOperationException("Preview grid/field differs from gameplay proportions.");
            }
            finally { EditorSceneManager.ClosePreviewScene(sourceScene); }
            File.WriteAllText(Folder+"/authored.txt","PASS Explicit demo references\nPASS No live match/knot owners\nPASS No missing scripts\nPASS No team score footer\nPASS Gameplay grid and field proportions\n");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        SessionState.SetString(Key+".restore",SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key+".finished",false);
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/S-0_INSTRUCTIONS.unity");
        EditorApplication.isPlaying=true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Prepare()
    {
        if(!SessionState.GetBool(Key,false)) return;
        checks.Clear(); errors.Clear();
        Application.logMessageReceived+=Log;
        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SelectLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-HIGGS.asset"));
    }
    static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message+"\n"+stack); }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            deadline=EditorApplication.timeSinceStartup+75;
            entering=capturing=earning=growth=reenabled=leaving=secondCapture=outro=false; finishAt=0; frame=-1; firstRate=0;
            background=Application.runInBackground; Application.runInBackground=true;
            EditorApplication.update+=Tick;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false); EditorApplication.update-=Tick;
            Application.logMessageReceived-=Log;
            Application.runInBackground=background;
            if(!SessionState.GetBool(Key+".finished",false))
                File.AppendAllText(Folder+"/runtime.txt","\nINTERRUPTED before fixture completion.\n");
            EditorSceneManager.OpenScene(SessionState.GetString(Key+".restore",""));
        }
    }
    static void Check(bool ok,string text)
    {checks.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines(Folder+"/runtime.txt",checks);}
    static void Capture(string name) => ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Instructions fixture timed out.");
            var demo=Object.FindFirstObjectByType<HiggsInstructionsDemo>();
            if(!demo || !demo.Player || !demo.Knot)return;
            if(!entering && demo.Phase=="Entering")
            {
                entering=true; Check(demo.DisplayedScore==0,"Entry begins outside knot with zero score");
                baselineScore=MatchScoreService.Instance;
                baselineLight=baselineScore ? baselineScore.LightScoreMilliElectronVolts : 0;
                baselineDark=baselineScore ? baselineScore.DarkScoreMilliElectronVolts : 0;
                Check(!demo.GetComponentInChildren<MatchScoreService>(true) && !demo.Player.ParticipatesInMatch && demo.Player.UsesGameplayTuning && demo.Player.SimulationRoot,"Preview player is scoped, uses gameplay tuning and creates no score authority");
                initialPlayerScale=demo.Player.transform.lossyScale; firstKnotID=demo.Knot.GetInstanceID();
                var adjuster=demo.Player.GetComponent<PlayerScaleAdjuster>();
                Check(adjuster.UsesGlobalModifiers && Mathf.Approximately(adjuster.Size,PlayerGlobalModifiers.Current.ForPlayer(0).size),"Player uses the same global size as gameplay");
                File.WriteAllText(Folder+"/proportions.txt","Player root scale: "+initialPlayerScale+"\nPlayer body radius: "+PlayerScaleAdjuster.BodyRadiusOf(demo.Player)+"\nKnot radius: "+demo.knotRules.CaptureRadius+"\nGrid spacing: "+demo.grid.ResolvedGridScale+"\nField: "+demo.field.WorldSizeXZ);
                File.WriteAllLines(Folder+"/loaded-player-context.txt",Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Select(p=>p.name+" / "+p.gameObject.scene.path+" / "+p.gameObject.scene.isLoaded));
                Capture("01-entering");
            }
            if(!capturing && demo.Phase=="Capturing" && demo.CaptureProgress>.4f)
            {
                capturing=true; captureAge=Time.unscaledTime;
                Check(demo.DisplayedScore==0 && demo.incomeToast.RequestedContent==SymmetryKnotIncomeToast.Content.Capturing,"Inside ring captures before any income");
                Capture("02-capturing");
            }
            if(!earning && demo.incomeToast.RequestedContent==SymmetryKnotIncomeToast.Content.Earning && !demo.incomeToast.IsTransitioningOut && !demo.incomeToast.GetComponent<TMPTextTransition>().IsPlaying && demo.DisplayedScore>0)
            {
                earning=true; firstRate=demo.incomeToast.MilliElectronVoltsPerSecond;
                Check(capturing && Time.unscaledTime>captureAge && demo.CaptureProgress==1,"Completed capture activates knot and score display");
                Check(firstRate>0 && !demo.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("TEAM SCORE")),"Only gameplay capture/income feedback is displayed");
                var properties=new MaterialPropertyBlock(); demo.field.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
                Check(demo.field.HeightTexture && demo.field.HeightTexture.IsCreated() && demo.field.HasReservedExcitation && properties.GetTexture("_HiggsHeight")==demo.field.HeightTexture,"Higgs field is running with its own textures and knot excitation");
                Check(VectorGridGPU.Instance!=demo.grid && demo.Player.GetComponentsInChildren<GridInteractor>(true).All(i=>i.grid==demo.grid),"Player deforms the preview grid without replacing the global grid");
                Capture("03-earning");
            }
            if(earning && !growth && demo.incomeToast.MilliElectronVoltsPerSecond>=firstRate*3)
            {
                growth=true; Check(demo.DisplayedScore>firstRate,"Sustained possession grows income and total");
                Capture("04-growing-income");
            }
            if(!leaving && demo.Phase=="Leaving" && Vector3.Distance(demo.Player.transform.position,demo.Knot.transform.position)>demo.knotRules.CaptureRadius+PlayerScaleAdjuster.BodyRadiusOf(demo.Player))
            {
                leaving=true;
                Check(demo.Player.transform.lossyScale==initialPlayerScale && demo.Player.GetComponentInChildren<PlayerVisualController>().enabled,"Player keeps its full gameplay ring and scale while leaving");
                Capture("05-leaving");
            }
            if(!outro && demo.Phase=="Resetting")
            {
                var ring=(Shapes.Disc)new SerializedObject(demo.Knot).FindProperty("controlRingDashed").objectReferenceValue;
                if(ring.Radius < demo.knotRules.CaptureRadius * .3f)
                {
                    outro=true;
                    var visual=demo.Player.GetComponentInChildren<PlayerVisualController>();
                    Check(demo.Player.transform.lossyScale==initialPlayerScale && visual.enabled && visual.outlineHalf>0,"Player outline remains full-size through the knot's final collapse");
                    Capture("07-knot-outro-player-ring");
                }
            }
            if(!secondCapture && demo.CompletedLoops==1 && demo.Phase=="Capturing" && demo.CaptureProgress>.4f)
            {
                secondCapture=true;
                var ring=(Shapes.Disc)new SerializedObject(demo.Knot).FindProperty("controlRingDashed").objectReferenceValue;
                Check(demo.Knot.GetInstanceID()!=firstKnotID && ring.Thickness>0 && Mathf.Approximately(ring.Radius,demo.knotRules.CaptureRadius),"Second encounter has a fresh knot with restored ring thickness and radius");
                Check(demo.Player.transform.lossyScale==initialPlayerScale,"Player scale remains constant across the loop seam");
                Capture("06-second-capture");
            }
            if(!reenabled && demo.CompletedLoops>=2 && demo.Phase=="Entering")
            {
                Check(entering && capturing && earning && growth && leaving && secondCapture && outro,"Two complete entry/capture/scoring/exit loops");
                Check(demo.DisplayedScore==0,"Loop resets displayed score");
                var view=demo.viewport.GetComponent<Camera>();
                Check(view.enabled && view.rect.width>.1f && view.rect.height>.1f && view.rect.xMin>=0 && view.rect.xMax<=1 && view.rect.yMin>=0 && view.rect.yMax<=1,"Close-up camera remains inside the instruction pane");
                demo.gameObject.SetActive(false);
                Check(!demo.viewport.GetComponent<Camera>().enabled && !demo.incomeToast.IsVisible,"Disable hides preview camera and toast");
                demo.gameObject.SetActive(true); reenabled=true;
                Check(demo.DisplayedScore==0 && demo.CompletedLoops==0,"Reopening starts a fresh demonstration");
                finishAt=EditorApplication.timeSinceStartup+6;
            }
            if(reenabled && EditorApplication.timeSinceStartup>=finishAt)
            {
                Check(demo.DisplayedScore>0,"Scoring presentation resumes after reopening");
                Check(MatchScoreService.Instance==baselineScore && (!baselineScore || baselineScore.LightScoreMilliElectronVolts==baselineLight && baselineScore.DarkScoreMilliElectronVolts==baselineDark),"Repeated preview leaves real score authority and totals unchanged");
                Check(errors.Count==0,"No runtime errors or exceptions"); Finish(null);
            }
        }
        catch(Exception e){Finish(e);}
    }
    static void Finish(Exception failure)
    {
        SessionState.SetBool(Key+".finished",true);
        EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
        Application.runInBackground=background;
        File.WriteAllText(Folder+"/runtime.txt",(failure==null && errors.Count==0 && checks.All(c=>c.StartsWith("PASS"))?"PASSED":"FAILED")+"\n"+string.Join("\n",checks)+"\n"+failure+"\nErrors: "+errors.Count+"\n"+string.Join("\n",errors));
        Debug.Log("HIGGS instructions validation complete: "+Folder+"/runtime.txt");
        EditorApplication.isPlaying=false;
    }
}
#endif

