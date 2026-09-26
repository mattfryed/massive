#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Disposable Play Mode fixtures exercise natural timing and production capture/completion paths.</summary>
public static class NovaFinaleValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static GameManagerScript match;
    static NovaStarController star;
    static bool initialized, entryChecked, participantsChecked, awardsChecked, collapseChecked, resolved, mixed;
    static float duration;
    static double deadline, resultsSeen;
    static int checks, completions;
    static long regulationLight, regulationDark, bonusLight, bonusDark;
    static readonly List<int> stages=new();
    static Vector3 physicsScale;
    static float colliderRadius;
    static string screenshot;
    static bool sceneReady;
    public static string Status {get; private set;}="Not run";
    static object Read(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Write(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static void Check(bool value,string label)
    { if(!value)throw new InvalidOperationException("NOVA finale validation: "+label); checks++; }

    public static string StartRun(bool fourPlayers, bool mixedEntry, float seconds, string screenshotPath)
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update-=Tick;
        match=null;star=null; initialized=entryChecked=participantsChecked=awardsChecked=collapseChecked=resolved=false;
        resultsSeen=0;
        checks=completions=0; stages.Clear(); duration=seconds; mixed=mixedEntry; screenshot=screenshotPath;
        GameFlowContext.Instance.SetMode(fourPlayers?GameMode.TwoVTwo:GameMode.OneVOne);
        GameFlowContext.Instance.ClearLastMatchResult();
        Status="Running "+(fourPlayers?"2v2":"1v1")+(mixed?" mixed entry":" all late");
        deadline=EditorApplication.timeSinceStartup+90;
        sceneReady=false;
        SceneManager.sceneLoaded-=OnSceneLoaded;
        SceneManager.sceneLoaded+=OnSceneLoaded;
        SceneManager.LoadScene("S-3_NOVA");
        EditorApplication.update+=Tick;
        return Status;
    }

    static void Tick()
    {
        try
        {
            if(!Application.isPlaying){EditorApplication.update-=Tick;SceneManager.sceneLoaded-=OnSceneLoaded;return;}
            if(!sceneReady)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException(Status);
            if(SceneManager.GetActiveScene().name=="S-0_POSTGAME")
            {
                var post=Object.FindFirstObjectByType<PostGameScreenController>();
                if(post) post.StopAllCoroutines();
                if(resultsSeen==0)resultsSeen=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-resultsSeen<2)return;
                var r=GameFlowContext.Instance.LastMatchResult;
                Check(resolved,"explosion occurred before results");
                Check(r.hasBonusBreakdown,"result has breakdown");
                Check(r.regulationLightMilliElectronVolts==regulationLight && r.regulationDarkMilliElectronVolts==regulationDark,"regulation snapshots preserved");
                Check(r.bonusLightMilliElectronVolts==bonusLight && r.bonusDarkMilliElectronVolts==bonusDark,"bonus receipts persisted");
                Check(r.LightScore==regulationLight+bonusLight && r.DarkScore==regulationDark+bonusDark,"combined final score");
                Check(r.winner==(r.LightScore>r.DarkScore?TeamSide.Light:r.DarkScore>r.LightScore?TeamSide.Dark:TeamSide.Tie),"winner from combined score");
                Check(completions==1,"single anomaly completion");
                Check(Time.timeScale==1,"world time restored");
                var labels=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t=>t.name=="REGULATION + BONUS").ToArray();
                Check(labels.Length==2 && labels.All(t=>t.text.Contains("CORE COLLAPSE") && t.color.a>.9f),"visible results breakdown");
                if(!string.IsNullOrEmpty(screenshot)) ScreenCapture.CaptureScreenshot(screenshot);
                Status="PASS: "+checks+" assertions; stages "+string.Join(",",stages)+"; regulation "+regulationLight+"/"+regulationDark+" + bonus "+bonusLight+"/"+bonusDark+" = final "+r.LightScore+"/"+r.DarkScore;
                EditorApplication.update-=Tick; return;
            }
            if(!match)match=Object.FindFirstObjectByType<GameManagerScript>();
            if(!match)return;
            Write(match,"autoReturnOnAllPlayersInactive",false);
            if(!initialized && match.Phase==MatchRuntimePhase.Regulation)Initialize();
            if(!initialized)return;
            var scores=match.ScoreService;
            if(match.Phase==MatchRuntimePhase.FinaleEntry && !entryChecked)
            {
                entryChecked=true;
                Check(star.CurrentStage==5 && star.BurstsReleased==3,"three matter pulses, fifth stage at expiry");
                Check(!Object.FindFirstObjectByType<NovaCoreMinigame>(),"no early minigame");
                Check(!scores.IsScoringOpen && match.RegulationRemainingSeconds==0,"regulation scoring closed");
                Check(star.IsEntryWindowOpen && star.entryRingVisual.activeSelf,"entry ring opens");
                Check(Time.timeScale==1,"movement remains available for entry");
                Check(star.transform.localScale==physicsScale && ((SphereCollider)star.entryCollider).radius==colliderRadius,"physics footprint stable");
                Check(star.rumble.visualRoot.localScale.x>=.99f,"stage four authored scale reached");
                regulationLight=scores.LightScoreMilliElectronVolts; regulationDark=scores.DarkScoreMilliElectronVolts;
                var roster=star.GetRosteredPlayers();
                for(int i=0;i<roster.Count;i++)
                {
                    var p=roster[i]; var body=p.GetComponent<Rigidbody>();
                    body.position=(mixed&&i==0)?star.transform.position:new Vector3((i%2==0?-1:1)*12f,0f,8f);
                    body.linearVelocity=Vector3.zero;
                }
                Check(!scores.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat,roster[0],"AFTER-REGULATION",Vector3.zero,out _),"entry window rejects ordinary score");
                Check(!Object.FindFirstObjectByType<Massive.Levels.LevelSceneContext>().spawners.activeSelf,"ordinary spawners retired");
            }
            if(match.Phase==MatchRuntimePhase.Bonus)
            {
                var mini=Object.FindFirstObjectByType<NovaCoreMinigame>();
                if(!mini)return;
                var participants=(IList)Read(mini,"_participants");
                if(!participantsChecked)
                {
                    participantsChecked=true;
                    Check(participants.Count==(GameFlowContext.Instance.IsTwoVTwo?4:2),"full roster included");
                    int onTime=participants.Cast<object>().Count(p=>!(bool)Read(p,"isLate"));
                    Check(onTime==(mixed?1:0),"empty and mixed on-time snapshots preserve penalties");
                    Check(Time.timeScale==0 && !scores.IsChainClockRunning,"bonus freezes arena and chain clock");
                    Check(!scores.AdvanceTeamAmplifier(2),"bonus cannot advance Amplifier");
                }
                if(!awardsChecked && (bool)Read(mini,"_gameplayEnabled") && (float)Read(mini,"_timeRemaining")<3.5f)
                {
                    var players=star.GetRosteredPlayers();
                    var light=players.First(p=>p.teamID==1); var dark=players.First(p=>p.teamID==2);
                    var chain=light.GetComponent<PlayerScoreChain>(); double multiplier=chain.CurrentMultiplier;
                    var bridge=mini.GetComponent<NovaMinigameScoring>();
                    Check(scores.IsBonusScoringOnly,"bonus award scope");
                    Check(!scores.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat,light,"DURING-BONUS",Vector3.zero,out _),"ordinary rewards blocked during bonus");
                    Check(bridge.TryAwardCapture(light,90001,3,out var award) && award.finalMilliElectronVolts==75 && award.multiplier==1 && award.teamAmplifierMultiplier==1,"flat bonus ignores personal and team multipliers");
                    Check(!bridge.TryAwardCapture(light,90001,3,out _),"duplicate capture blocked");
                    Check(bridge.TryAwardCapture(dark,90002,2,out award)&&award.finalMilliElectronVolts==50,"opponent gets flat score");
                    // Exercise the production particle capture handler, including duplicate callbacks.
                    var state=participants.Cast<object>().First(p=>(PlayerControllerScript)Read(p,"controller")==dark);
                    var particle=Activator.CreateInstance(typeof(NovaCoreMinigame).GetNestedType("CoreParticle",BindingFlags.NonPublic));
                    Write(particle,"lastHitBy",state);Write(particle,"originalSubCount",2);Write(particle,"scoreId",90003);
                    var capture=typeof(NovaCoreMinigame).GetMethod("AwardParticleMass",Flags);
                    capture.Invoke(mini,new[]{particle});capture.Invoke(mini,new[]{particle});
                    Check(bridge.AwardedToTeam(2)==100,"real capture counted once");
                    Check(chain.CurrentMultiplier==multiplier && scores.GetTeamAmplifierMultiplier(1)==8,"bonus leaves multipliers unchanged");
                    bonusLight=bridge.AwardedToTeam(1);bonusDark=bridge.AwardedToTeam(2);
                    awardsChecked=true;
                }
                if(mini.IsFinished && !collapseChecked)
                {
                    collapseChecked=true;
                    Check(awardsChecked && !scores.IsScoringOpen,"scoring shuts before collapse");
                    Check(!Object.FindFirstObjectByType<SupernovaEndSequence>().HasPlayed,"explosion waits for contraction");
                }
            }
            if(match.Phase==MatchRuntimePhase.Resolving && !resolved)
            {
                resolved=true;
                Check(collapseChecked && Object.FindFirstObjectByType<SupernovaEndSequence>().HasPlayed,"explosion follows completed bonus outro");
                Check(!scores.IsScoringOpen && scores.LightScoreMilliElectronVolts==regulationLight+bonusLight,"outro cannot add score");
            }
        }
        catch(Exception e)
        {
            Status="FAIL after "+checks+" assertions: "+e;
            EditorApplication.update-=Tick;
            Debug.LogError(Status);
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name!="S-3_NOVA")return;
        match=null;
        sceneReady=true;
        SceneManager.sceneLoaded-=OnSceneLoaded;
    }

    static void Initialize()
    {
        initialized=true; star=Object.FindFirstObjectByType<NovaStarController>();
        Write(match,"_regulationDurationSeconds",duration);Write(match,"_regulationRemainingSeconds",duration);
        star.entryWindowDuration=1.5f;
        Object.FindFirstObjectByType<NovaAnomalyAdapter>().overrideDuration=5f;
        stages.Add(star.CurrentStage);star.OnStageChanged+=s=>stages.Add(s);
        Object.FindFirstObjectByType<AnomalyManager>().OnAnomalyCompleted+=(d,r)=>completions++;
        star.OnBounceTriggered+=()=>
        {
            int expected=16+(star.CurrentStage-2)*10;
            var pickups=star.matterRoot.GetComponentsInChildren<MatterNuggetScript>(true);
            Check(pickups.Length>=expected,"matter burst populated");
            var burst=pickups.Skip(pickups.Length-expected).ToArray();
            var sum=burst.Aggregate(Vector3.zero,(v,p)=>v+p.GetComponent<Rigidbody>().linearVelocity);
            Check(sum.sqrMagnitude<.001f,"balanced opposite ejection coverage");
            Check(burst.All(p=>p.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude>1),"matter ejected with speed");
        };
        physicsScale=star.transform.localScale;colliderRadius=((SphereCollider)star.entryCollider).radius;
        Check(star.CurrentStage==1 && Mathf.Approximately(star.rumble.visualRoot.localScale.x,.42f),"small stage one visual");
        Check(NovaStarController.StageAt(90,120)==2 && NovaStarController.StageAt(60,120)==3 && NovaStarController.StageAt(30,120)==4,"fractional boundaries");
        Check(NovaStarController.StageAt(225,300)==2 && NovaStarController.StageAt(150,300)==3,"arbitrary timer scales");
        var scores=match.ScoreService; var players=star.GetRosteredPlayers();
        foreach(var p in players)scores.RegisterPlayer(p);
        var light=players.First(p=>p.teamID==1);var dark=players.First(p=>p.teamID==2);
        Check(!scores.TryAwardToPlayer(ScoreRewardKeys.NovaCoreCapture,light,"EARLY-BONUS",Vector3.zero,out _),"bonus reward blocked during regulation");
        Check(scores.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat,light,"REG-LIGHT",Vector3.zero,out _),"regulation reward accepted");
        Check(scores.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat,dark,"REG-DARK",Vector3.zero,out _),"opponent regulation reward accepted");
        scores.AdvanceTeamAmplifier(1);scores.AdvanceTeamAmplifier(1);scores.AdvanceTeamAmplifier(1);
        light.GetComponent<PlayerScoreChain>().ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh,100f);
        // Freeze decay in this fixture so the bonus math encounters a live maximum personal multiplier.
        scores.SetScoringState(true,false);
    }
}
#endif
