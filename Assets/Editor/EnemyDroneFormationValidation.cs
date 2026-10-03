#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyDroneFormationValidation
{
    private const string Key = "EnemyDroneFormationValidation.Run", Folder = "Library/EnemyDroneFormationValidation";
    private static IEnumerator routine;
    private static int frame;
    private static float resume;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    static EnemyDroneFormationValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Demonstrations/Validate Drone Formations")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n"); File.WriteAllText(Folder + "/releases.txt", "");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            frame = 0; resume = 0; results.Clear(); errors.Clear(); owned.Clear(); deadline = EditorApplication.timeSinceStartup + 240;
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Application.runInBackground = SessionState.GetBool(Key + "Background", true); SessionState.SetBool(Key, false); }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || Time.frameCount <= frame || Time.time < resume) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            if (routine.MoveNext()) { frame = Time.frameCount; resume = Time.time + (routine.Current is float f ? f : 0); }
            else Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        bool pass = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (pass ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
        Debug.Log("[Drone formation validation] " + (pass ? "PASSED" : "FAILED: " + error));
        foreach (var obj in owned) if (obj) Object.Destroy(obj);
        Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static IEnumerator Checks()
    {
        var lab = Object.FindFirstObjectByType<EnemyLab>();
        Check(lab && lab.timelinePreview, "Player Actions contains Encounter Lab");
        lab.mode = EnemyLab.LabMode.EncounterTimeline;
        var preview = lab.timelinePreview;
        while (!preview.isActiveAndEnabled || preview.Players.Count != 2) yield return null;
        preview.loop = false; preview.actorsMove = preview.actorsAttack = false;
        preview.preset = EnemyEncounterLab.LayoutPreset.Open;
        var d = preview.director;
        d.timelinePaused = true;
        var source = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        string[] names = { "01 Drone Paired Rows", "01b Drone Center Ring", "01c Drone Goal Phalanxes",
            "02 Ranged Drone Paired Rows", "02b Ranged Drone Center Ring", "02c Ranged Drone Goal Phalanxes" };
        var forms = names.Select(n => AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/" + n + ".asset")).ToArray();
        Check(forms.All(f => f && source.cues.Any(c => c.formation == f)), "All six batches are authored and selectable in the timeline");
        var poses = new List<EnemyArenaLayout.Pose[]>();
        for (int f = 0; f < forms.Length; f++)
        {
            var form = forms[f]; var points = new List<EnemyArenaLayout.Pose>();
            Check(form.slots.Count == (f % 3 == 1 ? 10 : 20), form.name + " has the requested count");
            foreach (var slot in form.slots)
            {
                if (!preview.layout.Resolve(slot, false, out var pose, out var reason) || !preview.layout.Clear(pose, slot.enemy.GetSpawnRadiusWorld(), d.borderBufferWorld, out reason))
                    throw new Exception(form.name + " invalid footprint: " + reason);
                if (points.Any(p => Vector3.Distance(p.clearance, pose.clearance) < 1.1f - .001f)) throw new Exception(form.name + " overlaps itself");
                points.Add(pose);
            }
            poses.Add(points.ToArray());
            Check(true, form.name + " fits arena and its own footprints");
            if (f % 3 == 0)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    var row = form.slots.Where(s => Mathf.Sign(s.position.y - .5f) == side).OrderBy(s => s.releaseDelay).ToArray();
                    if (row.Length != 10) throw new Exception("Row count");
                    float prior = -1;
                    for (int i = 0; i < row.Length; i++)
                    {
                        if (Mathf.Abs(row[i].releaseDelay - i * .25f) > .001f || Mathf.Abs(row[i].position.x - .5f) + .001f < prior) throw new Exception("Rows must stagger center-out every .25s");
                        prior = Mathf.Abs(row[i].position.x - .5f);
                    }
                }
                Check(true, form.name + " has ten per row with center-out .25s stagger");
            }
            if (f % 3 == 1)
            {
                var center = preview.layout.World(Vector2.zero);
                Check(points.All(p => Mathf.Abs(Vector3.Distance(p.position, center) - 3.8f) < .001f), form.name + " is a true circle of radius 3.8");
                var groups = form.slots.GroupBy(s => s.releaseDelay).OrderBy(g => g.Key).ToArray();
                Check(groups.Length == 5 && groups.All(g => g.Count() == 2) && groups.Select((g, i) => Mathf.Abs(g.Key - i * .25f) < .001f).All(x => x), form.name + " releases opposite pairs every .25s");
                if (f == 1) Check(Mathf.Abs(points[0].position.z - center.z) < .001f && Mathf.Abs(points[1].position.z - center.z) < .001f, "Drone ring begins at left and right");
            }
            if (f % 3 == 2)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    var half = points.Where(p => Mathf.Sign(p.position.x) == side).ToArray();
                    if (half.Length != 10 || Mathf.Abs(half.Average(p => p.position.x) - side * 5f) > .001f || half.Any(p => Vector3.Dot(p.rotation * Vector3.forward, Vector3.right * side) < .999f))
                        throw new Exception("Phalanx center or facing mismatch");
                }
                Check(true, form.name + " centers at +/-5 units and points toward goals");
            }
        }
        for (int kind = 0; kind < 3; kind++)
        {
            float clearance = poses[kind].Min(a => poses[kind + 3].Min(b => Vector3.Distance(a.clearance, b.clearance)));
            Check(clearance >= 1.1f, forms[kind].name + " / Ranged cross-clearance " + clearance.ToString("0.000"));
        }
        Check(source.maxAliveTotal >= 40 && source.maxPressure >= 60, "Lab budgets allow matching 20 + 20 batches");
        var times = new List<(EnemyDefinition def, float time)>();
        void Spawned(EnemyBase enemy)
        {
            times.Add((enemy.Definition, d.GameplayAge));
            File.AppendAllText(Folder + "/releases.txt", $"{enemy.Definition.name} at {d.GameplayAge:0.0000}s; frame {Time.deltaTime:0.0000}s\n");
        }
        d.TimelineEnemySpawned += Spawned;
        Time.timeScale = 2;
        // Exercise actual warnings, assembly, entrances and normal AI with two live target actors.
        for (int kind = 0; kind < 3; kind++)
        {
            var timeline = Own(Object.Instantiate(source)); timeline.cues.Clear(); timeline.duration = 20;
            for (int type = 0; type < 2; type++) timeline.cues.Add(new EnemyEncounterTimeline.Cue
            { label = forms[kind + 3 * type].name, formation = forms[kind + 3 * type], arrivalSeconds = 3, allowedLateness = 3 });
            d.encounterTimeline = timeline; d.previewCue = -1; d.timelinePaused = false;
            preview.RestartPreview(); times.Clear();
            while (d.GameplayAge < 10)
            {
                CheckScopeBudget(d);
                yield return null;
            }
            File.AppendAllText(Folder + "/releases.txt", string.Join("\n", d.CueStates.Select(c => c.label + ": " + c.state + " " + c.spawned + " " + c.reason)) + "\n");
            Check(d.CueStates.All(c => c.state == "Complete" && c.spawned == c.formation.slots.Count), "Simultaneous " + forms[kind].name + " + Ranged fully release with normal AI");
            foreach (var form in new[] { forms[kind], forms[kind + 3] })
            {
                var events = times.Where(t => t.def == form.slots[0].enemy).ToArray();
                Check(events.Length == form.slots.Count, form.name + " raises one spawn event per member");
                var actualGroups = events.GroupBy(t => t.time).OrderBy(g => g.Key).ToArray();
                var plannedGroups = form.slots.GroupBy(s => s.releaseDelay).OrderBy(g => g.Key).ToArray();
                Check(actualGroups.Length == plannedGroups.Length && actualGroups.Select((g, i) => g.Count() == plannedGroups[i].Count()).All(x => x), form.name + " preserves paired release groups");
                var gaps = actualGroups.Zip(actualGroups.Skip(1), (a, b) => b.Key - a.Key).ToArray();
                Check(gaps.All(gap => gap >= .249f && gap < .4f), form.name + " retains .25s cadence (frame tolerance): " + string.Join(", ", gaps.Select(gap => gap.ToString("0.0000"))));
            }
            Check(d.enemyRoot.GetComponentsInChildren<EnemyFormationEntry>().All(e => !e.enabled), "Coordinated entrances hand back to AI");
        }
        d.TimelineEnemySpawned -= Spawned;
        Check(errors.Count == 0, "No Unity errors or exceptions during formation validation");
    }
    private static void CheckScopeBudget(EnemyDirector d)
    {
        if (d.CurrentPressure > d.encounterTimeline.maxPressure + .01f || d.AliveCount + d.TimelinePendingCount > d.encounterTimeline.maxAliveTotal)
            throw new Exception("Mixed batch exceeded reserved/live budget");
    }
}
#endif
