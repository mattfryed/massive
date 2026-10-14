using System;
using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Resonance.Editor
{
    /// <summary>Explicit, disposable Play Mode fixture. Uses real encounter/Core/goal/audio components,
    /// but no GameManager, match-history recording or changes to scene assets.</summary>
    public static class ResonancePatternLibraryPlayValidation
    {
        public static string Result { get; private set; } = "Not run";
        const string FixtureName = "Resonance library validation (runtime only)";
        static AmplifierResonanceSpawner cycle;
        static AmplifierGoalCapture[] goals;
        static ResonanceSegment segment;
        static int stage, pair, beforeImpact;
        static double began, stageTime;
        static Vector3 contactPoint, incoming;
        static bool previousBackground;
        static readonly List<string> checks = new List<string>();

        public static string CreateFixture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first; the fixture never edits or replaces an authoring scene.");
            if (SceneManager.GetSceneByName(FixtureName).IsValid()) throw new InvalidOperationException("Fixture already exists; stop Play Mode to remove it.");
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            // Disable only runtime copies; stopping Play restores the untouched authoring scene,
            // including any unsaved work. No match is started and no history is recorded.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var sceneRoot in SceneManager.GetSceneAt(i).GetRootGameObjects()) sceneRoot.SetActive(false);
            // The How To screen intentionally owns scene silence until destruction,
            // not just disable. Release only its inactive runtime copy for this audio fixture.
            foreach (var carousel in Object.FindObjectsByType<HowToPlayCarousel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!carousel.isActiveAndEnabled) SceneSfxPolicy.EndSilence(carousel);
            var scene = SceneManager.CreateScene(FixtureName);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 30, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true; camera.orthographicSize = 8;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();
            var service = MatchScoreService.Instance;
            if (service == null) service = new GameObject("Isolated score service").AddComponent<MatchScoreService>();
            else service.gameObject.SetActive(true);
            service.Configure(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
            var audio = AudioSystem.I;
            if (audio == null) audio = new GameObject("Isolated shared audio service").AddComponent<AudioSystem>();
            else audio.gameObject.SetActive(true);
            audio.database = AssetDatabase.LoadAssetAtPath<AudioDatabase>("Assets/Audio/Assets/SFX/AudioDatabase_Main.asset");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ResonancePatternLibrarySetup.Encounter), scene);
            var s = root.GetComponentInChildren<AmplifierResonanceSpawner>();
            s.startAutomatically = false; s.waitForScoring = false; s.scoreService = service;
            s.UseSharedSettings = false; s.initialSpawnDelay = 0f; s.respawnDelay = .15f; s.respawnDelayVariation = Vector2.zero;
            s.maximumActiveSeconds = 0; s.fixedRandomSeed = true; s.randomSeed = 404;
            var plane = new GameObject("28 x 12 placement frame").transform;
            plane.rotation = Quaternion.Euler(90, 0, 0);
            s.spawnRegion.explicitPlane = plane; s.spawnRegion.explicitPlaneSize = new Vector2(28, 12);
            s.spawnRegion.drawZones = false;
            for (int team = 1; team <= 2; team++)
            {
                var go = new GameObject("Validation goal " + team);
                go.transform.position = new Vector3(team == 1 ? -15 : 15, 0, 0);
                go.AddComponent<SphereCollider>().isTrigger = true;
                var goal = go.AddComponent<AmplifierGoalCapture>();
                var serialized = new SerializedObject(goal);
                serialized.FindProperty("teamID").intValue = team;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return "Runtime-only fixture created. Call Begin, then stop Play Mode to remove it and restore the original scene.";
        }
        public static string Begin()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != FixtureName)
                throw new InvalidOperationException("Enter Play Mode in the disposable validation fixture first.");
            if (cycle != null) throw new InvalidOperationException("Validation already running.");
            cycle = Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
            goals = Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None);
            if (cycle == null || goals.Length != 2 || AudioSystem.I == null || SceneSfxPolicy.IsSuppressed)
            {
                cycle = null;
                throw new InvalidOperationException("Fixture services are not ready.");
            }
            MatchScoreService.Instance.ResetForMatch(true);
            checks.Clear(); stage = pair = 0; began = stageTime = EditorApplication.timeSinceStartup;
            cycle.PatternPreparing += ConfigureRuntimePattern;
            cycle.StartCycle();
            Result = "Running five-pattern formation / physical impact / sound / capture / wrap check";
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return Result;
        }
        static void ConfigureRuntimePattern(ResonancePatternController p)
        {
            // Only accelerate the disposable runtime copy, never saved prefab/shared settings.
            var m = p.GetComponent<ResonanceManifestation>();
            m.UseSharedSettings = false; m.spawnSeconds = .45f; m.despawnSeconds = .3f;
        }
        static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
        static int Playing(AudioClip clip)
        {
            int n = 0;
            foreach (var source in AudioSystem.I.GetComponentsInChildren<AudioSource>())
                if (source.isPlaying && source.clip == clip) n++;
            return n;
        }
        static void Tick()
        {
            try
            {
                if (!Application.isPlaying || cycle == null) { Finish("Interrupted"); return; }
                double now = EditorApplication.timeSinceStartup;
                if (now - began > 90) throw new TimeoutException("Stage " + stage + " pair " + pair + ": " + cycle.Status);
                var p = cycle.ActivePattern;
                var core = cycle.ActiveCore;
                if (stage == 0)
                {
                    if (cycle.Phase != AmplifierEncounterPhase.Active || core == null || core.IsSpawning) return;
                    Check(cycle.CurrentPatternIndex == pair % 5, "ordered entry " + pair + " = " + p.definition.referenceLabel);
                    Check(p.InteractionEnabled && cycle.ActiveManifestation.IsIdle, "pattern settles before Core/interaction at entry " + pair);
                    if (pair == 5)
                    {
                        Check(cycle.CapturesObserved == 5 && cycle.PairsSpawned == 6, "five real goal captures advance and loop to 345");
                        Finish("PASS: " + checks.Count + " runtime checks\n" + string.Join("\n", checks)); return;
                    }
                    // Check placement before moving the Core for the physical shot.
                    var ignored = cycle.spawnRegion.ignoredColliders;
                    cycle.spawnRegion.ignoredColliders = core.GetComponentsInChildren<Collider>();
                    string why;
                    bool safe = cycle.spawnRegion.IsValidSpawnPoint(core.transform.position, cycle.CorePlacementRadius, p, out why);
                    cycle.spawnRegion.ignoredColliders = ignored;
                    Check(safe, "safe neutral spawn for " + p.definition.referenceLabel + ": " + why);
                    segment = p.InteractiveSegments[0];
                    float unused;
                    Vector3 local = segment.SampleDistance(segment.PathLength * .5f, out unused);
                    Vector3 a = segment.SampleDistance(segment.PathLength * .49f, out unused);
                    Vector3 b = segment.SampleDistance(segment.PathLength * .51f, out unused);
                    Vector3 tangent = segment.transform.TransformDirection((b - a).normalized);
                    Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;
                    contactPoint = segment.transform.TransformPoint(local);
                    if (Vector3.Dot(normal, -contactPoint) < 0) normal = -normal;
                    incoming = (-normal + tangent * .2f).normalized * 8f;
                    // The new possession mode parks an available Core kinematically.
                    // Use its real physics-release path before prescribing the test shot;
                    // do not disable possession or alter the gameplay prefab/profile.
                    if (core.UsesPossession)
                        typeof(AmplifierCoreGameplay).GetMethod("WakeForGoalField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .Invoke(core, null);
                    Check(!core.Body.isKinematic, "Core released into live physics for " + p.definition.referenceLabel);
                    core.Body.position = contactPoint + normal * (cycle.CorePlacementRadius + p.magneticReach + .65f);
                    core.Body.linearVelocity = incoming; core.Body.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms(); beforeImpact = p.CoreImpactCount;
                    stageTime = now; stage = 1; return;
                }
                if (stage == 1)
                {
                    if (p.CoreImpactCount == beforeImpact)
                    {
                        if (now - stageTime > 5) throw new TimeoutException("No physical Core turnaround for " + p.definition.referenceLabel);
                        return;
                    }
                    Check(p.CoreImpactCount > beforeImpact, "physical Core contact triggers reveal: " + p.definition.referenceLabel);
                    // Stop the remainder of the magnetic release before parking the Core.
                    // Otherwise the driver can propel it from center into a second arc,
                    // contaminating the subsequent player-only audio assertion.
                    foreach (var driver in p.GetComponentsInChildren<ResonanceInteractionDriver>()) driver.ReleaseCoreTurn(core);
                    core.Body.linearVelocity = Vector3.zero;
                    core.Body.position = new Vector3(0, 0, 0); Physics.SyncTransforms();
                    var cue = p.definition.coreImpactSound;
                    Check(cue != null && cue.clips.Length == 1 && cue.clips[0] != null,
                        "every frequency, including 345 Hz, has an impact cue");
                    if (cue != null)
                    {
                        Check(Playing(cue.clips[0]) == 1, "correct pooled impact sound plays once: " + p.definition.referenceLabel);
                        p.NotifyCoreContact(core.Body, segment, contactPoint, incoming, true);
                        Check(Playing(cue.clips[0]) == 1 && p.CoreImpactCount == beforeImpact + 1, "compound/duplicate contact suppressed");
                    }
                    stageTime = now; stage = 2; return;
                }
                if (stage == 2)
                {
                    if (now - stageTime < 1.1) return;
                    var cue = p.definition.coreImpactSound;
                    if (cue != null)
                    {
                        p.NotifyPlayerPassage(12345, contactPoint - Vector3.right, contactPoint + Vector3.right, Vector3.right * 8, .5f, .02f);
                        Check(Playing(cue.clips[0]) == 0, "player passage does not play Core audio");
                        SceneSfxPolicy.BeginSilence(cycle);
                        p.NotifyCoreContact(core.Body, segment, contactPoint, incoming, true);
                        Check(Playing(cue.clips[0]) == 0, "Core audio honors shared scene silence");
                        SceneSfxPolicy.EndSilence(cycle);
                        p.SetInteractionEnabled(false);
                        int count = p.CoreImpactCount;
                        Check(!p.NotifyCoreImpact(core.Body) && p.CoreImpactCount == count && Playing(cue.clips[0]) == 0, "dissolved/disabled pattern cannot play impact");
                        p.SetInteractionEnabled(true);
                    }
                    int team = pair % 2 + 1;
                    var goal = Array.Find(goals, g => g.TeamID == team);
                    core.Body.linearVelocity = Vector3.zero; core.Body.position = goal.CapturePoint.position;
                    Physics.SyncTransforms(); stageTime = now; stage = 3; return;
                }
                if (stage == 3)
                {
                    if (cycle.CapturesObserved <= pair)
                    {
                        if (now - stageTime > 5) throw new TimeoutException("Goal did not capture entry " + pair);
                        return;
                    }
                    Check(cycle.Phase == AmplifierEncounterPhase.Dissolving && !p.InteractionEnabled, "goal capture starts gated dissolution at entry " + pair);
                    pair++; stage = 0;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e.Message + "\n" + string.Join("\n", checks)); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            if (cycle != null)
            {
                SceneSfxPolicy.EndSilence(cycle);
                cycle.PatternPreparing -= ConfigureRuntimePattern;
                cycle.StopCycle();
            }
            cycle = null; Result = result;
            Application.runInBackground = previousBackground;
        }
    }
}
