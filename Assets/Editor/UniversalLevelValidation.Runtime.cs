#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class UniversalLevelValidation
{
    const string RunKey="MASSIVE.UniversalLevelValidation";
    static readonly List<string> runtimeChecks=new(), runtimeErrors=new(), runtimeWarnings=new();
    static IEnumerator routine;
    static double deadline;
    static int lastFrame;
    static bool background;
    static string ReportPath => Folder+"/"+SessionState.GetString(RunKey+".title", "UNKNOWN")+"-"+(SessionState.GetInt(RunKey+".index",0)%2==0?"1v1":"2v2")+".txt";
    static void InitializeRuntime()
    {
        EditorApplication.playModeStateChanged+=RuntimeState;
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder+"/run.request"))return;
            string request=File.ReadAllText(Folder+"/run.request").Trim(); File.Delete(Folder+"/run.request");
            RunAll(int.TryParse(request,out int index)?index:0);
        };
    }
    [MenuItem("MASSIVE/Levels/Validate All Playable Levels (1v1 and 2v2)")]
    public static void RunAll()=>RunAll(0);
    static void RunAll(int first)
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)throw new InvalidOperationException("Start from a saved scene in Edit Mode.");
        Directory.CreateDirectory(Folder);
        SessionState.SetString(RunKey+".restore",scene.path);
        SessionState.SetInt(RunKey+".index",first);
        SessionState.SetString(RunKey+".definitions",string.Join("|",Catalog.Levels.Select(AssetDatabase.GetAssetPath)));
        SessionState.SetBool(RunKey+".held",true); AssetDatabase.DisallowAutoRefresh();
        StartNext();
    }
    static void StartNext()
    {
        string[] definitions=SessionState.GetString(RunKey+".definitions","").Split('|');
        int index=SessionState.GetInt(RunKey+".index",0);
        if(index>=definitions.Length*2)
        {
            SessionState.SetBool(RunKey,false); ReleaseRefresh();
            EditorSceneManager.OpenScene(SessionState.GetString(RunKey+".restore",""));
            File.WriteAllText(Folder+"/runtime-complete.txt","All "+definitions.Length+" playable levels exercised in both modes. Inspect individual reports for failures.");
            Debug.Log("Universal level runtime sweep complete.");return;
        }
        var definition=AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitions[index/2]);
        string path=PathFor(definition);
        SessionState.SetString(RunKey+".definition",definitions[index/2]);
        SessionState.SetString(RunKey+".title",definition.levelTitle);
        SessionState.SetString(RunKey+".scene",path);
        SessionState.SetString(RunKey+".hash",Hash128.Compute(File.ReadAllText(path)).ToString());
        EditorSceneManager.OpenScene(path);
        SessionState.SetBool(RunKey,true); SessionState.SetBool(RunKey+".next",false);
        File.WriteAllText(ReportPath,"ENTERING PLAY MODE\n");
        EnemyEncounterPreviewMode.Request(index%2==1); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartLog()
    {
        if(!SessionState.GetBool(RunKey,false))return;
        runtimeChecks.Clear(); runtimeErrors.Clear(); runtimeWarnings.Clear();
        Application.logMessageReceived-=RuntimeLog; Application.logMessageReceived+=RuntimeLog;
    }
    static void RuntimeLog(string message,string stack,LogType type)
    {
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)runtimeErrors.Add(message+"\n"+stack);
        else if(type==LogType.Warning && !runtimeWarnings.Contains(message))runtimeWarnings.Add(message);
    }
    static void RuntimeState(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(RunKey,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            background=Application.runInBackground; Application.runInBackground=true;
            lastFrame=-1; deadline=EditorApplication.timeSinceStartup+170;
            routine=Flatten(RuntimeChecks()); EditorApplication.update+=RuntimeTick;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(RunKey,false);
            if(SessionState.GetBool(RunKey+".next",false))
            {
                SessionState.SetInt(RunKey+".index",SessionState.GetInt(RunKey+".index",0)+1);
                EditorApplication.delayCall+=StartNext;
            }
            else { ReleaseRefresh(); File.AppendAllText(ReportPath,"\nStopped before fixture completion."); }
        }
    }
    static void ReleaseRefresh()
    {if(SessionState.GetBool(RunKey+".held",false)){SessionState.SetBool(RunKey+".held",false);AssetDatabase.AllowAutoRefresh();}}
    static IEnumerator Flatten(IEnumerator source)
    {
        while(source.MoveNext())
        { if(source.Current is IEnumerator inner) { var nested=Flatten(inner); while(nested.MoveNext())yield return nested.Current; } else yield return source.Current; }
    }
    static void RuntimeTick()
    {
        if(!EditorApplication.isPlaying || routine==null || Time.frameCount==lastFrame)return;
        try {lastFrame=Time.frameCount;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Fixture timed out");if(!routine.MoveNext())FinishRuntime(null);}
        catch(Exception e){FinishRuntime(e);}
    }
    static void RuntimeCheck(bool value,string label)
    {runtimeChecks.Add((value?"PASS ":"FAIL ")+label);File.WriteAllText(ReportPath,"RUNNING\n"+string.Join("\n",runtimeChecks));}
    static void FinishRuntime(Exception failure)
    {
        routine=null; EditorApplication.update-=RuntimeTick; Application.logMessageReceived-=RuntimeLog;
        Application.runInBackground=background; Time.timeScale=1;
        bool passed=failure==null && runtimeErrors.Count==0 && runtimeChecks.All(c=>c.StartsWith("PASS"));
        File.WriteAllText(ReportPath,(passed?"PASSED":"FAILED")+" ("+runtimeChecks.Count+" checks)\n"+string.Join("\n",runtimeChecks)+"\n"+failure+
            "\nConsole errors: "+runtimeErrors.Count+"\n"+string.Join("\n",runtimeErrors)+"\nWarnings:\n"+string.Join("\n",runtimeWarnings));
        Debug.Log(SessionState.GetString(RunKey+".title","")+" universal validation "+(passed?"PASSED":"FAILED"));
        SessionState.SetBool(RunKey+".next",true); EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> condition,float seconds,string label)
    {
        double end=EditorApplication.timeSinceStartup+seconds;
        while(!condition()){if(EditorApplication.timeSinceStartup>end)throw new Exception(label);yield return null;}
        RuntimeCheck(true,label);
    }
    static IEnumerator Wait(float seconds)
    {double end=EditorApplication.timeSinceStartup+seconds;while(EditorApplication.timeSinceStartup<end)yield return null;}
    static void WriteField(object value,string name,object data)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(value,data);
    static EnemyBase[] Alive(Scene scene)=>EnemyBase.ActiveEnemies.Where(e=>e && e.gameObject.scene==scene && !e.IsDead).ToArray();
    static IEnumerator RuntimeChecks()
    {
        var scene=SceneManager.GetActiveScene();
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(SessionState.GetString(RunKey+".definition",""));
        bool two=SessionState.GetInt(RunKey+".index",0)%2==1;
        var match=All<GameManagerScript>(scene).Single(); var context=All<LevelSceneContext>(scene).SingleOrDefault();
        WriteField(match,"autoReturnOnAllPlayersInactive",false);
        var director=All<EnemyDirector>(scene).FirstOrDefault(d=>d.isActiveAndEnabled);
        bool countdownHeld=true;
        double end=EditorApplication.timeSinceStartup+25;
        while(match.Phase!=MatchRuntimePhase.Regulation)
        {
            countdownHeld &= (!director || director.GameplayAge==0 && director.TotalSpawned==0) && !All<PowerUpSpawner>(scene).Any(s=>s.isActiveAndEnabled);
            if(match.IsStartupBlocked || EditorApplication.timeSinceStartup>end)throw new Exception("Startup did not reach regulation: "+match.StartupFailureReason);
            yield return null;
        }
        RuntimeCheck(countdownHeld,"Countdown gates enemies and pickup spawners");
        var score=match.ScoreService; var players=All<PlayerControllerScript>(scene).Where(p=>!p.IsPseudoPlayer && p.isActiveAndEnabled).ToArray();
        RuntimeCheck(players.Length==(two?4:2) && GameFlowContext.Instance.IsTwoVTwo==two,"Correct roster and match mode");
        RuntimeCheck(Rewired.ReInput.isReady && Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length==1,"Exactly one ready input manager");
        RuntimeCheck(All<MatchScoreService>(scene).Length==1 && score.IsScoringOpen && match.RegulationDurationSeconds==score.Profile.RegulationDurationSeconds,"Single score authority and shared match duration");
        RuntimeCheck(context && context.level==level && context.stageTitleText.text==level.levelTitle && context.stageNumberText.text=="STAGE_"+level.levelNumber.ToString("000"),"Correct active level and HUD identity");
        RuntimeCheck(players.All(p=>!p.IsMatchInputLocked && !p.IsSpawning && p.GetComponent<PlayerScoreChain>() && p.GetComponentsInChildren<PlayerRepulsorAOE>().Length==1),"Players unlock with one Repulsor and central score chain");
        var timer=All<MatchTimerPresenter>(scene).Single(); var timerText=(TMPro.TMP_Text)Ref(timer,"timerText");
        RuntimeCheck(timer.isActiveAndEnabled && timerText && timerText.isActiveAndEnabled && !string.IsNullOrWhiteSpace(timerText.text),"Visible shared match timer");
        RuntimeCheck(AudioSystem.I && All<AudioListener>(scene).Count(a=>a.isActiveAndEnabled)==1,"Audio service and single listener");
        var light=players.First(p=>p.teamID==1);
        long before=score.LightScoreMilliElectronVolts;
        bool award=score.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat,light,"UNIVERSAL-AUDIT",light.transform.position,out _);
        long after=score.LightScoreMilliElectronVolts;
        RuntimeCheck(award && after>before && !score.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat,light,"UNIVERSAL-AUDIT",light.transform.position,out _) && score.LightScoreMilliElectronVolts==after,"Central awards credit once and reject duplicate tokens");
        if(director)
        {
            yield return Until(()=>director.CueStates.Count>0,2,"Enemy timeline initializes after regulation opens");
            RuntimeCheck(director.TimelineIsTwoVTwo==two && Mathf.Abs(director.TimelineDurationSeconds-match.EncounterRegulationDurationSeconds)<.01f,"Enemy timeline uses match mode and duration");
            yield return Until(()=>Alive(scene).Length>0,12,"Live enemy wave arrives");
            var victim=Alive(scene).First(); before=score.GetTeamScore(light.teamID);
            victim.TakeDamage(victim.HealthRemaining+1,EnemyDamageSource.Sword,light); after=score.GetTeamScore(light.teamID);
            victim.TakeDamage(100000,EnemyDamageSource.Sword,light);
            RuntimeCheck(after>before && score.GetTeamScore(light.teamID)==after,"Actual enemy defeat scores exactly once");
            score.SetScoringState(true,false);yield return null;yield return null;
            float age=director.GameplayAge;int count=director.TotalSpawned;yield return Wait(.3f);
            RuntimeCheck(Mathf.Abs(director.GameplayAge-age)<.0001f && count==director.TotalSpawned && Alive(scene).All(e=>e.IsPaused),"Paused scoring clock freezes enemy AI and arrivals");
            score.SetScoringState(true,true);yield return null;yield return null;
            RuntimeCheck(director.GameplayAge>age && Alive(scene).All(e=>!e.IsPaused),"Enemy AI and timeline resume");
        }
        else RuntimeCheck(false,"Shared enemy timeline is present");
        var pair=All<AmplifierResonanceSpawner>(scene).Single();
        yield return Until(()=>pair.ActiveCore && pair.ActivePattern,40,"Live Core and Resonance coexist with level mechanics");
        var goals=All<AmplifierGoalCapture>(scene); int team=goals[0].TeamID;
        double amplifier=score.GetTeamAmplifierMultiplier(team);
        RuntimeCheck(goals[0].TryAdvanceTeamAmplifier() && score.GetTeamAmplifierMultiplier(team)>amplifier,"Team goal advances its shared Amplifier authority");
        var pickups=All<PowerUpSpawner>(scene).Single();
        yield return Until(()=>PowerUpPickup.ActivePickups.Any(p=>p && p.gameObject.scene==scene && p.CountsTowardSpawnLimit),16,"Global pickup spawns naturally");
        var pickup=PowerUpPickup.ActivePickups.First(p=>p && p.gameObject.scene==scene && p.CountsTowardSpawnLimit);
        RuntimeCheck(pickups.vectorGrid && pickups.vectorGrid.GetComponent<ArenaBoundsFromVectorGrid>().ContainsWorldPoint(pickup.transform.position),"Pickup is inside its level placement bounds");
        light.GetComponent<PlayerRepulsorGridPulse>().TriggerPulse();yield return null;
        ScreenCapture.CaptureScreenshot(Folder+"/"+level.levelTitle+"-"+(two?"2v2":"1v1")+".png");yield return Wait(.15f);
        if(director && !two)
            for(int i=1;i<=5;i++)
            {
                director.previewCue=i; director.RestartTimeline(true);yield return null;
                yield return Until(()=>director.TotalSpawned>0,12,"Enemy family placement: "+director.encounterTimeline.cues[i].label);
                RuntimeCheck(Alive(scene).Any(e=>e.Definition==director.CueStates[0].formation.slots[0].enemy),"Correct live family for cue "+i);
            }
        bool resolving=false,stopped=false,payload=false;
        match.PhaseChanged+=phase=>
        {
            if(phase!=MatchRuntimePhase.Resolving)return;
            resolving=true;
        };
        WriteField(match,"_regulationRemainingSeconds",.05f);
        bool nova=level.levelTitle=="NOVA";
        if(nova)
        {
            yield return Until(()=>match.Phase==MatchRuntimePhase.FinaleEntry,4,"NOVA enters its unique finale");
            RuntimeCheck(!score.IsScoringOpen && !context.spawners.activeInHierarchy,"NOVA entry retires ordinary scoring and pickups");
            yield return Until(()=>match.Phase==MatchRuntimePhase.Bonus,14,"NOVA starts its Core Collapse bonus");
            RuntimeCheck(Time.timeScale==0 && score.IsBonusScoringOnly && !score.IsChainClockRunning,"NOVA bonus pauses arena with bonus-only scoring");
        }
        yield return Until(()=>resolving, nova?45:5,"Regulation/finale resolves through the real lifecycle");
        yield return null;
        stopped=!scene.GetRootGameObjects().Single(g=>g.CompareTag("GameplayObjects")).activeSelf && !score.IsScoringOpen;
        var result=GameFlowContext.Instance.LastMatchResult;
        payload=GameFlowContext.Instance.HasLastMatchResult && result.stageTitle==level.levelTitle && result.stageNumber==level.levelNumber && result.mode==(two?GameMode.TwoVTwo:GameMode.OneVOne)
            && result.lightMilliElectronVolts==score.LightScoreMilliElectronVolts && result.darkMilliElectronVolts==score.DarkScoreMilliElectronVolts;
        RuntimeCheck(stopped,"Gameplay and scoring stop before leaving the level");
        RuntimeCheck(payload,"Exact score totals, level and mode reach the result");
        yield return Until(()=>SceneManager.GetActiveScene().name==SceneFlow.PostGameScene,8,"Shared PostGame loads");
        yield return null;yield return null;
        var post=Object.FindFirstObjectByType<PostGameScreenController>();
        RuntimeCheck(post && ((TMPro.TMP_Text)Ref(post,"titleText")).text==level.levelTitle && Time.timeScale==1,"PostGame displays the correct level with restored world time");
        yield return Until(()=>SceneManager.GetActiveScene().name==SceneFlow.ChooseModeScene,25,"PostGame returns to Attract normally");
        RuntimeCheck(Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length==1 && !Object.FindFirstObjectByType<GameManagerScript>(),"Navigation retains one input manager and no leaked match owner");
        RuntimeCheck(Hash128.Compute(File.ReadAllText(SessionState.GetString(RunKey+".scene",""))).ToString()==SessionState.GetString(RunKey+".hash",""),"Saved level unchanged by runtime fixtures");
        RuntimeCheck(runtimeErrors.Count==0,"No runtime errors or exceptions");
    }
}
#endif
