#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Public behavior checks; fixtures never save runtime changes into authored assets.</summary>
public static class HiggsValidation
{
    private static int passed;
    private static void Check(bool ok, string label)
    { if (!ok) throw new InvalidOperationException("HIGGS validation failed: " + label); passed++; }
    private static SymmetryKnotCaptureState State(float form=0, float unclaimed=6, float claimed=10, float ramp=2)
        => new SymmetryKnotCaptureState(form, unclaimed, claimed, ramp, .25f, 1);

    [MenuItem("MASSIVE/HIGGS/Validate Capture Rules")]
    public static string RunRules()
    {
        passed = 0;
        var s = State(1);
        Check(s.Tick(.5f,1,true)==0 && s.Phase==SymmetryKnotCaptureState.Stage.Forming, "forming cannot score");
        Check(s.Tick(.5f,1,true)==0, "formation completion cannot pay early");
        Check(s.Tick(2,1,true)==0 && s.Hold01==1, "two-second lock has no warmup income");
        Check(s.Tick(1,1,true)==4, "four ticks per productive second");
        Check(s.Tick(1,3,true)==0 && s.Contested, "any opposing presence blocks income");
        Check(s.Tick(.25f,1,true)==1, "same owner resumes after contest");
        float remaining=s.SecondsRemaining;
        Check(s.Tick(100,1,false)==0 && s.SecondsRemaining==remaining, "pause freezes lifetime and reward");
        s.Tick(.1f,0,true);
        Check(s.OwnerTeam==-1 && s.Hold01==0, "vacating clears ownership and ramp");
        s.Tick(.1f,2,true);
        Check(s.OwnershipChanges==1 && s.Hold01<1, "owner change starts a fresh lock");

        s=State();
        Check(s.Tick(10,1,true)==32 && s.Phase==SymmetryKnotCaptureState.Stage.Retiring, "bounded ten-second claim pays eight productive seconds");
        Check(s.Tick(10,2,true)==0 && s.Phase==SymmetryKnotCaptureState.Stage.Finished, "retirement never scores");
        s=State();
        for(int i=0;i<80;i++) s.Tick(.25f,i%2==0?1:0,true);
        Check(s.Phase==SymmetryKnotCaptureState.Stage.Finished, "re-entry cannot extend lifetime repeatedly");
        s=State();
        Check(s.Tick(8,0,true)==0 && s.Phase==SymmetryKnotCaptureState.Stage.Retiring, "unclaimed timeout");
        s=State(); s.Tick(.1f,1,true); s.Retire();
        Check(s.Tick(10,1,true)==0, "manual retirement has no reward");
        foreach(int fps in new[]{30,60,120})
        {
            s=State(); int total=0;
            for(int i=0;i<fps*12;i++) total+=s.Tick(1f/fps,1,true);
            Check(total==32, "frame partition equivalence at "+fps+" fps (got "+total+")");
        }
        s=State(); s.Tick(2,1,true); s.Tick(.24f,1,true); s.Tick(.01f,3,true);
        Check(s.Tick(.01f,1,true)==0, "contest clears partial tick");
        var profile=AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        Check(profile.RegulationDurationSeconds==120, "player-facing regulation is 120 seconds");
        ScoreRewardRule rule;
        Check(profile.TryGetReward(ScoreRewardKeys.HiggsControlTick,out rule) && rule.BaseMilliElectronVolts==25 &&
            !rule.multiplierEligible && rule.chainEffect==ScoreChainAwardMode.None && rule.repeatPolicy==ScoreRepeatPolicy.Unlimited,
            "authored team-only reward rule");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HiggsStageSetup.KnotPath);
        Check(prefab!=null && Mathf.Approximately(prefab.GetComponent<SymmetryKnotController>().CaptureRadius,1.15f), "authored capture radius");
        var fallback=ScriptableObject.CreateInstance<ScoreEconomyProfile>();
        try
        {
            fallback.ResetToRecommendedDefaults();
            Check(fallback.RulesetVersion>=2 && fallback.TryGetReward(ScoreRewardKeys.HiggsControlTick,out rule) &&
                rule.BaseMilliElectronVolts==25 && !rule.multiplierEligible, "new and fallback profiles retain the versioned HIGGS rule");
        }
        finally { Object.DestroyImmediate(fallback); }
        return passed+" HIGGS capture/configuration checks passed";
    }

    // Run in the actual HIGGS scene after it starts. Uses actual player colliders and the real score service.
    public static string RunPlayChecks()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter HIGGS Play Mode first.");
        passed=0;
        var scores=MatchScoreService.Instance;
        Check(scores!=null,"score service exists");
        var manager=Object.FindFirstObjectByType<HiggsExcitationKnotManager>();
        var encounter=Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        manager.enabled=false; encounter.enabled=false;
        var gm=Object.FindFirstObjectByType<GameManagerScript>(); gm.StopAllCoroutines(); gm.enabled=false;
        scores.ResetForMatch(true);
        var players=Object.FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(p=>!p.IsPseudoPlayer && p.gameObject.scene==manager.gameObject.scene).OrderBy(p=>p.playerID).ToArray();
        Check(players.Length==4,"four authored player actors");
        foreach(var player in players)
        {
            player.gameObject.SetActive(true); player.isActive=true; player.temporarilyEliminated=false;
            player.transform.position=new Vector3(100+player.playerID*10,0,100);
            scores.RegisterPlayer(player);
        }
        var light=players.First(p=>p.teamID==1);
        var ally=players.Last(p=>p.teamID==1);
        var dark=players.First(p=>p.teamID==2);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HiggsStageSetup.KnotPath);
        var knot=Object.Instantiate(prefab).GetComponent<SymmetryKnotController>();
        knot.Initialize(Vector3.zero); knot.enabled=false;
        try
        {
            light.transform.position=new Vector3(knot.CaptureRadius+.1f,0,0); Physics.SyncTransforms();
            Check(knot.EvaluateTeamMask()==0,"overlapping body or weapon outside the ring cannot claim");
            light.transform.position=new Vector3(knot.CaptureRadius-.1f,0,0); Physics.SyncTransforms();
            Check(knot.EvaluateTeamMask()==1,"player centre inside the ring can claim");
            light.transform.position=Vector3.zero; Physics.SyncTransforms();
            Check(knot.EvaluateTeamMask()==1,"actual Light collider claims");
            knot.TickGameplay(1,true); knot.TickGameplay(2,true); knot.TickGameplay(1,true);
            Check(scores.GetTeamScore(1)==100,"productive second banks 100 meV");
            var chain=light.GetComponent<PlayerScoreChain>();
            Check(chain!=null && chain.Charge==0 && chain.CurrentMultiplier==1,"no personal charge");
            chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh,100);
            ally.transform.position=Vector3.zero; Physics.SyncTransforms();
            knot.TickGameplay(1,true);
            Check(scores.GetTeamScore(1)==200,"second ally and personal x16 cannot multiply the stream");
            dark.transform.position=Vector3.zero; Physics.SyncTransforms();
            knot.TickGameplay(1,true);
            Check(knot.EvaluateTeamMask()==3 && scores.GetTeamScore(1)==200 && !knot.IsFlowing,"2v1 contests and stops the flow");
            dark.transform.position=new Vector3(100,0,100); Physics.SyncTransforms();
            scores.AdvanceTeamAmplifier(1); scores.AdvanceTeamAmplifier(1); scores.AdvanceTeamAmplifier(1);
            knot.TickGameplay(1,true);
            Check(scores.GetTeamScore(1)==1000,"capped x8 Amplifier still increases Knot income");
            var mouth=Object.FindObjectsByType<SymmetryKnotGoalMouth>(FindObjectsSortMode.None).First(m=>m.teamID==1);
            Check(mouth.ReceivedMilliElectronVolts==1000,"goal receipt receives the accepted amplified amount");
            var snapshot=scores.GetTeamScore(1); var remaining=knot.State.SecondsRemaining;
            knot.TickGameplay(10,false);
            Check(scores.GetTeamScore(1)==snapshot && remaining==knot.State.SecondsRemaining,"pause adds no income or lifetime consumption");
            light.isActive=false; ally.isActive=false; Physics.SyncTransforms();
            Check(knot.EvaluateTeamMask()==0,"inactive actors cannot control");
            dark.transform.position=Vector3.zero; Physics.SyncTransforms();
            knot.TickGameplay(.25f,true);
            Check(knot.State.OwnerTeam==2 && knot.State.Hold01<1,"Dark takeover ramps independently");
            dark.temporarilyEliminated=true;
            Check(knot.EvaluateTeamMask()==0,"eliminated actor cannot contest");
            scores.CloseScoring(); knot.TickGameplay(10,true);
            Check(scores.GetTeamScore(1)==snapshot && scores.GetTeamScore(2)==0,"closed scoring cannot be bypassed");
            knot.Retire(); knot.TickGameplay(1,true);
            Check(!knot.IsFlowing,"retired conduit has no active flow");
        }
        finally { Object.Destroy(knot.gameObject); }
        return passed+" HIGGS physics/scoring integration checks passed (staged fixture; exit Play Mode)";
    }
}
#endif
