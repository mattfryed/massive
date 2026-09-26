#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit authored-scene checks and disposable Play Mode integration checks.</summary>
public static class NovaLevelValidation
{
    private static int passed;
    private static int completions;
    private static float frozenClock, frozenEncounter;
    private static AmplifierCoreGameplay frozenCore;
    private static long expectedLight, expectedDark;
    private static AnomalyResult completedResult;
    private static void Check(bool value, string label)
    { if (!value) throw new InvalidOperationException("NOVA validation: " + label); passed++; }
    private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    private static object Field(object target,string name) => target.GetType().GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target);
    private static void Write(object target,string name,object value) => target.GetType().GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(target,value);
    private static Transform Owner(Object value) => value is GameObject go ? go.transform : (value as Component)?.transform;

    [MenuItem("MASSIVE/Levels/Validate NOVA and Planar Template")]
    public static string Authored()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Use Edit Mode for authored checks.");
        passed=0;
        foreach(string path in new[]{NovaLevelSetup.NovaPath,NovaLevelSetup.TemplatePath})
        {
            var s=EditorSceneManager.OpenPreviewScene(path);
            try
            {
                Check(s.GetRootGameObjects().Select(g=>g.name).OrderBy(n=>n).SequenceEqual(new[]{"00_Systems","01_Presentation","02_Arena","04_UI","90_EditorOnly","GameplayObjects"}.OrderBy(n=>n)),path+" six roots");
                Check(All<GameManagerScript>(s).Length==1 && All<MatchScoreService>(s).Length==1,"one match and score authority");
                Check(All<AudioListener>(s).Count(x=>x.enabled&&x.gameObject.activeInHierarchy)==1,"one active listener");
                Check(All<MatchTimerPresenter>(s).Length==1 && All<TeamAmplifierToastPresenter>(s).Length==1,"timer and amplifier feedback");
                var ctx=All<LevelSceneContext>(s).Single();
                Check(ctx.match && ctx.roster && ctx.inputManagerPrefab && ctx.spawners,"context dependencies");
                Check(ctx.stageTitleText && ctx.stageNumberText,"level identity HUD bindings");
                Check(!(bool)Field(ctx.match,"engageDeathSphereOnEnd"),"legacy end-screen callback disabled");
                Check(Field(ctx.match,"scoreService")==All<MatchScoreService>(s).Single(),"explicit score binding");
                Check(All<PlayerRepulsorAOE>(s).Length==4 && All<PlayerRepulsorFeedback>(s).Length==4 && All<PlayerRepulsorGridPulse>(s).Length==4,"four complete Repulsors");
                foreach(var p in All<PlayerControllerScript>(s))
                {
                    var aoe=p.GetComponentInChildren<PlayerRepulsorAOE>(true);
                    Check((Object)Field(aoe,"owner")==p && (Object)Field(aoe,"attackController")==p.GetComponent<PlayerAttackController>() && (Object)Field(aoe,"hitbox")==aoe.GetComponent<SphereCollider>(),"explicit Repulsor bindings for "+p.name);
                }
                Check(All<AmplifierGoalCapture>(s).Length==2 && All<AmplifierGoalTreatments>(s).Length==1,"goals and treatment");
                var encounter=All<AmplifierResonanceSpawner>(s).Single();
                Check(encounter.corePrefab && encounter.spawnRegion && encounter.spawnRegion.arenaBounds && encounter.patternAnchor && encounter.scoreService,"encounter references");
                Check(encounter.patternOrder.Count==1 && encounter.patternOrder[0].patternPrefab && encounter.UseSharedSettings,"shared encounter configuration");
                foreach(var go in All<Transform>(s)) Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject)==0,"missing script at "+go.name);
                foreach(var c in All<Component>(s).Where(c=>c))
                {
                    var p=new SerializedObject(c).GetIterator();
                    while(p.Next(true))
                    {
                        if(p.propertyType!=SerializedPropertyType.ObjectReference)continue;
                        Check(p.objectReferenceValue!=null || p.objectReferenceInstanceIDValue==0,"broken reference "+c.name+"."+p.propertyPath);
                        var owner=Owner(p.objectReferenceValue);
                        Check(!owner || !owner.gameObject.scene.IsValid() || owner.gameObject.scene==s,"reference outside scene: "+c.name+"."+p.propertyPath);
                    }
                }
                if(path==NovaLevelSetup.NovaPath)
                {
                    Check(ctx.level && ctx.anomalies && ctx.stage,"NOVA identity and anomaly");
                    Check(All<NovaScoreSphereBridge>(s).Length==0 && All<NovaCoreRewardsSink>(s).Length==0,"legacy score bridge removed");
                    Check(encounter.spawnRegion.noGoColliders.Length==1 && encounter.spawnRegion.noGoColliders[0].enabled,"stable star exclusion");
                    var end=All<SupernovaEndSequence>(s).Single();
                    Check(end.match==ctx.match && !end.transform.IsChildOf(s.GetRootGameObjects().Single(g=>g.CompareTag("GameplayObjects")).transform),"end effect survives gameplay shutdown");
                }
                else Check(!ctx.level && !ctx.anomalies && All<NovaStarController>(s).Length==0,"template awaits level identity with no NOVA content");
            }
            finally { EditorSceneManager.ClosePreviewScene(s); }
        }
        var profile=AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        Check(profile.RegulationDurationSeconds==120 && profile.TryGetReward(ScoreRewardKeys.NovaCoreCapture,out var rule) && rule.BaseMilliElectronVolts==25 && rule.repeatPolicy==ScoreRepeatPolicy.OncePerSourceToken,"authored NOVA reward and duration");
        return passed+" authored structure/reference assertions passed across NOVA and template.";
    }

    public static string BeginBonusChecks()
    {
        Check(Application.isPlaying,"Play Mode required"); passed=0; completions=0;
        var gm=Object.FindFirstObjectByType<GameManagerScript>();
        Check(gm.Phase==MatchRuntimePhase.Regulation,"direct launch reaches regulation");
        Check(Rewired.ReInput.isReady && Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length==1,"single ready input manager");
        Check(gm.RegulationDurationSeconds==120,"production clock");
        // Keep the disposable fixture alive without cabinet inactivity or natural star entry.
        Write(gm,"autoReturnOnAllPlayersInactive",false);
        var star=Object.FindFirstObjectByType<NovaStarController>(); star.StopAllCoroutines();
        var manager=Object.FindFirstObjectByType<AnomalyManager>(); manager.CancelCurrentAnomaly();
        var encounter=Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        var scores=gm.ScoreService; scores.ResetForMatch(true);
        var players=Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Where(p=>!p.IsPseudoPlayer).OrderBy(p=>p.playerID).ToArray();
        Check(players.Length==(GameFlowContext.Instance.IsTwoVTwo?4:2),"selected roster count");
        foreach(var p in players) scores.RegisterPlayer(p);
        scores.AdvanceTeamAmplifier(1); scores.AdvanceTeamAmplifier(1); scores.AdvanceTeamAmplifier(1);
        manager.OnAnomalyCompleted+=OnCompleted;
        var adapter=Object.FindFirstObjectByType<NovaAnomalyAdapter>();
        manager.TriggerAnomaly(adapter.novaCoreAnomalyDefinition,players,90,players);
        return "Started real NOVA bonus. Wait for intro, then run DuringBonusChecks.";
    }
    private static void OnCompleted(AnomalyDefinition definition,AnomalyResult result){completions++;completedResult=result;}

    public static string DuringBonusChecks()
    {
        var gm=Object.FindFirstObjectByType<GameManagerScript>();
        var minigame=Object.FindFirstObjectByType<NovaCoreMinigame>();
        var bridge=minigame.GetComponent<NovaMinigameScoring>();
        var scores=gm.ScoreService;
        Check(gm.Phase==MatchRuntimePhase.Bonus && Time.timeScale==0,"bonus pauses world");
        Check(scores.IsScoringOpen && !scores.IsChainClockRunning,"bonus scoring open and chain clock paused");
        var participants=Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Where(p=>!p.IsPseudoPlayer).ToArray();
        var light=participants.First(p=>p.teamID==1); var dark=participants.First(p=>p.teamID==2);
        Check(bridge.TryAwardCapture(light,900001,3,out var award) && award.finalMilliElectronVolts==75,"three captures ignore the x8 team multiplier");
        Check(!bridge.TryAwardCapture(light,900001,3,out _),"duplicate capture rejected");
        Check(bridge.TryAwardCapture(dark,900002,2,out award)&&award.finalMilliElectronVolts==50,"opponent receives its own multiplier");
        Check(!bridge.TryAwardInteraction("UNKNOWN_REWARD",light,"missing",1,out _),"unknown reward rejected by authority");
        Check(!bridge.TryAwardCapture(light,900003,0,out _),"zero quantity rejected");
        bridge.SetAcceptingInteractions(false);
        Check(!bridge.TryAwardCapture(light,900004,1,out _),"intro/outro gate rejects rewards");
        bridge.SetAcceptingInteractions(true);
        Check(bridge.AwardedToTeam(1)==scores.LightScoreMilliElectronVolts && bridge.AwardedToTeam(2)==scores.DarkScoreMilliElectronVolts,"receipts equal actual banked energy");
        frozenClock=gm.RegulationRemainingSeconds;
        var encounter=Object.FindFirstObjectByType<AmplifierResonanceSpawner>(); frozenEncounter=encounter.SecondsRemaining; frozenCore=encounter.ActiveCore;
        expectedLight=scores.LightScoreMilliElectronVolts;expectedDark=scores.DarkScoreMilliElectronVolts;
        return passed+" Play Mode assertions passed so far. Run FrozenChecks after a realtime interval.";
    }
    public static string FrozenChecks()
    {
        var gm=Object.FindFirstObjectByType<GameManagerScript>();
        var encounter=Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        Check(gm.Phase==MatchRuntimePhase.Bonus && Mathf.Approximately(gm.RegulationRemainingSeconds,frozenClock),"regulation remains frozen");
        Check(Mathf.Approximately(encounter.SecondsRemaining,frozenEncounter) && encounter.ActiveCore==frozenCore,"encounter preserved while frozen");
        var mini=Object.FindFirstObjectByType<NovaCoreMinigame>();
        // End the real minigame via its production completion path; transitions and
        // results still run naturally using realtime while world physics is paused.
        typeof(NovaCoreMinigame).GetMethod("EndMinigame",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mini,null);
        return "World/encounter pause assertions passed; running natural outro and result hold.";
    }
    public static string CompletedChecks()
    {
        var gm=Object.FindFirstObjectByType<GameManagerScript>();
        var manager=Object.FindFirstObjectByType<AnomalyManager>();
        Check(gm.Phase==MatchRuntimePhase.Regulation && Time.timeScale==1,"bonus resumes regulation and world");
        Check(completions==1 && !manager.IsAnomalyRunning,"one completion and cleanup");
        Check(!Object.FindFirstObjectByType<NovaCoreMinigame>(),"minigame destroyed");
        var wrap=(NovaCoreWrapUpPayload)completedResult.payload;
        Check(wrap.lightAwardedMilliElectronVolts==expectedLight && wrap.darkAwardedMilliElectronVolts==expectedDark && wrap.universalScoringApplied,"result contains exact universal receipts");
        Check(gm.ScoreService.LightScoreMilliElectronVolts==expectedLight && gm.ScoreService.DarkScoreMilliElectronVolts==expectedDark,"completion did not award twice");
        Check(gm.ScoreService.IsChainClockRunning,"chain clock resumed");
        manager.OnAnomalyCompleted-=OnCompleted;
        gm.BeginBonusRound(); manager.CancelCurrentAnomaly(); gm.EndBonusRound();
        Check(Time.timeScale==1,"cancellation restores world clock");
        return passed+" Play Mode integration assertions passed.";
    }

    public static string ExpireMatch()
    {
        var gm=Object.FindFirstObjectByType<GameManagerScript>();
        Write(gm,"_regulationRemainingSeconds",0f);
        return "Expired regulation through the match owner's ordinary Update; inspect PostGame after its realtime effect delay.";
    }
}
#endif
