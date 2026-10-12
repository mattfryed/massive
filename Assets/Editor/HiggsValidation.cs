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
        s.Tick(1.5f,0,true);
        Check(s.OwnerTeam==-1 && s.Hold01==0, "expired vacancy forgiveness clears ownership and ramp");
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
    [MenuItem("MASSIVE/HIGGS/Validate Vacancy Forgiveness")]
    public static string RunForgivenessRules()
    {
        passed = 0;
        var s = State();
        Check(s.Tick(1,0,true)==0 && s.OwnerTeam==-1, "empty unclaimed Knot never gains control");
        foreach (int team in new[]{1,2})
        {
            s=State(); s.Tick(2,team,true); s.Tick(.5f,team,true);
            Check(s.Tick(1,0,true)==4 && s.OwnerTeam==team && s.Hold01==1 && s.IncomeTicks==6,
                "team "+team+" retains control and growing income during forgiveness");
            float remaining=s.SecondsRemaining;
            Check(s.Tick(100,0,false)==0 && s.OwnerTeam==team && s.SecondsRemaining==remaining,
                "pause freezes forgiveness for team "+team);
            Check(s.Tick(.25f,team,true)==1 && s.IncomeTicks==7,
                "return preserves lock and income for team "+team);
            Check(s.Tick(1.5f,0,true)==6 && s.OwnerTeam==-1 && s.Hold01==0 && s.IncomeTicks==0 &&
                s.AwardTeam==team && s.FirstAwardTickIndex==7,
                "renewed grace ends exactly and keeps final award attribution for team "+team);
            Check(s.Tick(.25f,0,true)==0 && s.AwardTeam==-1,
                "continued vacancy cannot renew grace for team "+team);
            Check(s.Tick(.25f,team,true)==0 && s.Hold01<1 && s.IncomeTicks==0,
                "return after expiry starts a fresh lock for team "+team);
        }
        s=State(); s.Tick(1,1,true);
        Check(s.Tick(.5f,0,true)==0 && s.Hold01==.75f,
            "forgiveness preserves capture progress without early income");
        s=State(); s.Tick(2,1,true); s.Tick(.5f,0,true);
        Check(s.Tick(.25f,2,true)==0 && s.OwnerTeam==2 && s.Hold01<1 && s.IncomeTicks==0,
            "opponent entering alone immediately takes over and starts its lock");
        s=State(); s.Tick(2,1,true); s.Tick(.5f,0,true);
        Check(s.Tick(.25f,3,true)==0 && s.Contested, "opposing presence immediately contests forgiven control");
        Check(s.Tick(.25f,0,true)==0 && s.OwnerTeam==-1 && s.IncomeTicks==0,
            "an emptied contest cannot restore cancelled forgiveness");
        s=State(unclaimed:3,claimed:3); s.Tick(2,1,true); s.Tick(.75f,1,true);
        Check(s.Tick(2,0,true)==1 && s.Phase==SymmetryKnotCaptureState.Stage.Retiring,
            "forgiveness cannot extend the Knot lifetime");
        s=State(); s.Tick(2,1,true); s.Tick(.25f,0,true); s.Retire();
        Check(s.Tick(1,0,true)==0 && s.OwnerTeam==-1, "retirement cancels forgiveness");
        s=new SymmetryKnotCaptureState(0,6,10,2,.25f,1,false,0); s.Tick(2,1,true);
        Check(s.Tick(.25f,0,true)==0 && s.OwnerTeam==-1, "zero forgiveness restores immediate loss");
        foreach(int fps in new[]{30,60,120})
        {
            s=State(); s.Tick(2,1,true); int total=0;
            for(int i=0;i<fps*2;i++) total+=s.Tick(1f/fps,0,true);
            Check(total==6 && s.OwnerTeam==-1, "forgiveness frame partition equivalence at "+fps+" fps");
        }
        s=State(); s.Tick(2,1,true);
        Check(s.Tick(3,0,true)==6 && s.OwnerTeam==-1 && s.FirstAwardTickIndex==0,
            "slow frame crossing grace expiry pays only the retained interval");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HiggsStageSetup.KnotPath);
        Check(Mathf.Approximately(new SerializedObject(prefab.GetComponent<SymmetryKnotController>())
            .FindProperty("vacancyForgivenessSeconds").floatValue,1.5f), "authored forgiveness is 1.5 seconds");
        return passed+" HIGGS vacancy forgiveness checks passed";
    }

    public static string RunPlayChecks()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter HIGGS Play Mode first.");
        passed=0;
        var scores=MatchScoreService.Instance;
        Check(scores!=null,"score service exists");
        var manager=Object.FindFirstObjectByType<HiggsExcitationKnotManager>();
        var encounter=Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        manager.enabled=false; encounter.enabled=false;
        // Keep this deterministic capture fixture independent of live encounter scoring.
        foreach (var enemies in Object.FindObjectsByType<Massive.Enemies.EnemyDirector>(FindObjectsSortMode.None))
            enemies.enabled=false;
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
        // Isolate the original economy/collider regression from optional sustained growth.
        var incomeSettings = new SerializedObject(knot);
        incomeSettings.FindProperty("maximumIncomeMultiplier").floatValue = 1;
        incomeSettings.ApplyModifiedPropertiesWithoutUndo();
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

            // Exercise forgiveness through the actual boundary query, accepted awards,
            // exponential progression, flow and goal receipts, including the final frame.
            incomeSettings.Update();
            incomeSettings.FindProperty("maximumIncomeMultiplier").floatValue=8;
            incomeSettings.ApplyModifiedPropertiesWithoutUndo();
            foreach(var player in players)
            {
                player.isActive=true; player.temporarilyEliminated=false;
                player.transform.position=new Vector3(100+player.playerID*10,0,100);
            }
            scores.ResetForMatch(true); knot.Initialize(Vector3.zero);
            light.transform.position=Vector3.zero; Physics.SyncTransforms();
            long receiptBefore=mouth.ReceivedMilliElectronVolts;
            knot.TickGameplay(1,true); knot.TickGameplay(2,true); knot.TickGameplay(.5f,true);
            light.transform.position=new Vector3(knot.CaptureRadius+.1f,0,0); Physics.SyncTransforms();
            knot.TickGameplay(1,true);
            Check(knot.EvaluateTeamMask()==0 && knot.State.OwnerTeam==1 && knot.IsFlowing &&
                knot.State.IncomeTicks==6 && scores.GetTeamScore(1)==188,
                "empty boundary preserves control, flow and exponential income during grace");
            knot.TickGameplay(100,false);
            Check(knot.State.OwnerTeam==1 && scores.GetTeamScore(1)==188, "pause freezes live grace");
            knot.TickGameplay(.5f,true);
            Check(knot.State.OwnerTeam==-1 && knot.State.IncomeTicks==0 && !knot.IsFlowing &&
                scores.GetTeamScore(1)==276 && mouth.ReceivedMilliElectronVolts-receiptBefore==276,
                "exact grace expiry pays the final growing ticks to the original team and mouth");
            knot.TickGameplay(.5f,true);
            Check(scores.GetTeamScore(1)==276, "empty Knot stops income after forgiveness");
            light.transform.position=Vector3.zero; Physics.SyncTransforms(); knot.TickGameplay(.25f,true);
            Check(knot.State.OwnerTeam==1 && knot.State.Hold01<1 && scores.GetTeamScore(1)==276,
                "late physical return must lock again");
            knot.Initialize(Vector3.zero); scores.ResetForMatch(true);
            knot.TickGameplay(1,true); knot.TickGameplay(2,true);
            light.transform.position=new Vector3(100,0,100); Physics.SyncTransforms(); knot.TickGameplay(.5f,true);
            light.transform.position=Vector3.zero; Physics.SyncTransforms(); knot.TickGameplay(.25f,true);
            Check(knot.State.Hold01==1 && knot.State.IncomeTicks==3 && scores.GetTeamScore(1)==82,
                "physical return within grace continues the existing lock and income");
            light.transform.position=new Vector3(100,0,100); Physics.SyncTransforms(); knot.TickGameplay(.25f,true);
            dark.transform.position=Vector3.zero; Physics.SyncTransforms(); knot.TickGameplay(.25f,true);
            Check(knot.State.OwnerTeam==2 && knot.State.Hold01<1 && scores.GetTeamScore(1)==114 &&
                scores.GetTeamScore(2)==0 && !knot.IsFlowing,
                "opponent physically entering during grace stops original income immediately");
            dark.transform.position=new Vector3(100,0,100); Physics.SyncTransforms();
            scores.CloseScoring(); knot.TickGameplay(1,true);
            Check(scores.GetTeamScore(2)==0 && !knot.IsFlowing, "closed scoring bypasses no grace award gate");
        }
        finally { Object.Destroy(knot.gameObject); }
        return passed+" HIGGS physics/scoring integration checks passed (staged fixture; exit Play Mode)";
    }
}
#endif
