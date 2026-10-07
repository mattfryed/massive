#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EnemyFlexiblePlacementValidation
{
    static IEnumerator ScalingChecks()
    {
        var rows = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/01 Drone Paired Rows.asset");
        var turret = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/05 Turret Top Bottom.asset");
        fixture.fitRegulation = false; fixture.duration = 20; fixture.twoVTwo.advancedFrom = 0;
        fixture.maxPressure = 0; fixture.populationLimits.Clear(); fixture.maxAliveTotal = 80;
        GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
        Reset(rows); yield return null; yield return Until(9);
        var state = director.CueStates[0];
        Check(director.TimelineIsTwoVTwo && director.TimelineLimits.total == 120, "2v2 captures scaled budget and mode at run start");
        Check(state.spawned == 20 && state.reinforcements != null && state.reinforcements.spawned == 10,
            "2v2 Drone paired rows spawn 20 originals plus 10 fully telegraphed extras");
        Check(state.reinforcements.slots.All(s => s.announced && s.state == "Spawned"), "Every extra arrives at its locked warning position");
        GameFlowContext.Instance.SetMode(GameMode.OneVOne);
        fixture.twoVTwo.totalLimitMultiplier = 2;
        Check(director.TimelineIsTwoVTwo && director.TimelineLimits.total == 120, "Player/roster changes and profile edits cannot change the captured run mode or caps");
        fixture.twoVTwo.totalLimitMultiplier = 1.5f;
        Reset(rows); yield return null; yield return Until(9);
        Check(director.CueStates[0].spawned == 20 && director.CueStates[0].reinforcements == null, "1v1 preserves the original 20-slot formation");

        GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
        fixture.twoVTwo.totalLimitMultiplier = 1; fixture.maxAliveTotal = 21;
        Reset(rows); yield return null; yield return Until(9); state = director.CueStates[0];
        Check(state.spawned == 20 && state.reinforcements.spawned == 0 && state.reinforcements.skipped == 10,
            "One free population slot skips every extra pair without holding the original formation");
        Check(state.reinforcements.slots.All(s => s.reason.Contains("limit")), "Composer exposes population-limit reasons for skipped extras");

        fixture.maxAliveTotal = 24;
        Reset(rows); yield return null; yield return Until(9); state = director.CueStates[0];
        Check(state.spawned == 20 && state.reinforcements.spawned == 4 && state.reinforcements.skipped == 6,
            "Remaining budget admits exactly two complete reinforcement pairs");

        fixture.maxAliveTotal = 40;
        Reset(rows, true); fixture.cues[1].arrivalSeconds = 3; director.RestartTimeline(true);
        yield return null; yield return Until(9);
        Check(director.CueStates.All(c => c.reinforcements != null && c.reinforcements.spawned == 0 && c.reinforcements.skipped == 10),
            "Simultaneous original batches reserve their full budgets before any extras");

        fixture.maxAliveTotal = 80;
        Reset(turret); yield return null; yield return Until(8); state = director.CueStates[0];
        Check(state.spawned == 2 && state.reinforcements.spawned == 2, "2v2 diagonal turret pair expands to four occupied quadrants");
        var extraSlots = EnemyEncounterScaling.Reinforcements(fixture, fixture.cues[0], turret, true);
        Reset(turret); yield return null;
        var blockedRange = layout.sockets.Single(s => s.id == extraSlots[0].socket);
        for (int i = 0; i <= 64; i += 2) Block(layout.WallPose(blockedRange, i / 64f).clearance, Vector3.one * .1f);
        yield return Until(8); state = director.CueStates[0];
        Check(state.spawned == 2 && state.reinforcements.spawned == 0 && state.reinforcements.skipped == 2,
            "One blocked extra quadrant skips both optional turrets while the original diagonal spawns");

        Reset(turret); yield return null; yield return Until(1.6f); state = director.CueStates[0];
        Check(state.reinforcements.slots.All(s => s.announced), "Extra warning appears before the late-obstruction fixture");
        Block(state.reinforcements.slots[0].clearance, Vector3.one * .1f);
        yield return Until(8);
        Check(state.spawned == 2 && state.reinforcements.spawned == 0 && state.reinforcements.skipped == 2 && state.reinforcements.finished,
            "Obstruction after announcement holds then expires the whole pair without moving warnings");

        Reset(turret); fixture.cues[0].overrideSpawnPolicy = true; fixture.cues[0].integrity = EnemyFormation.Integrity.Strict;
        yield return null; yield return Until(8); state = director.CueStates[0];
        Check(state.spawned == 2 && state.reinforcements.spawned == 2, "Strict original formations keep their existing contract while extras release independently");

        Reset(rows); yield return null; yield return Until(1.5f);
        Check(director.TimelinePendingCount == 30, "Pending totals include all original and optional reservations");
        director.RestartTimeline(true);
        Check(director.TimelinePendingCount == 0 && director.CueStates.All(c => c.reinforcements == null), "Restart cancels optional warnings and returns their full budget");
        yield return LabRosterChecks();
        GameFlowContext.Instance.SetMode(GameMode.OneVOne);
    }
    static IEnumerator LabRosterChecks()
    {
        GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
        var go = new GameObject("2v2 Lab roster validation"); go.SetActive(false);
        var previewDirector = go.AddComponent<EnemyDirector>(); previewDirector.enabled = false;
        previewDirector.encounterTimeline = fixture; previewDirector.arenaLayout = layout;
        var lab = go.AddComponent<Massive.Demonstrations.EnemyEncounterLab>(); lab.enabled = false;
        lab.director = previewDirector; lab.layout = layout; lab.manualPlayerControl = true;
        lab.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        go.SetActive(true); lab.RestartPreview(); yield return null;
        Check(lab.Players.Count == 4 && lab.Players.Count(p => p.teamID == 1) == 2 && lab.Players.Count(p => p.teamID == 2) == 2,
            "2v2 Lab creates two real player actors per team");
        Check(lab.Players.Select(p => p.playerID).OrderBy(i => i).SequenceEqual(new[] { 0, 1, 2, 3 }) && lab.Players.All(p => Mathf.Abs(p.transform.position.y) < .001f),
            "Lab actors have distinct input slots and remain at gameplay Y=0");
        Check(lab.ManualPlayer.playerID == 0 && lab.ManualPlayer.ControlMode == (Application.isFocused ? PlayerControlMode.Rewired : PlayerControlMode.Disabled),
            "Manual P1 control remains available with the existing background-input lock");
        GameFlowContext.Instance.SetMode(GameMode.OneVOne); lab.RestartPreview(); yield return null;
        Check(lab.Players.Count == 2 && !previewDirector.TimelineIsTwoVTwo, "Lab restart returns to two actors when previewing 1v1");
        Object.Destroy(go); yield return null;
    }
}
#endif
