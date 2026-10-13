using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    [InitializeOnLoad]
    public static partial class EmergenceValidation
    {
        const string Key = "MASSIVE.EmergenceValidation";
        static readonly List<string> checks = new(), errors = new();
        static IEnumerator routine;
        static int frame, errorCount;
        static double deadline;
        static string Folder => EmergenceLevelSetup.Evidence;
        static string Report => Folder + "/" + (SessionState.GetInt(Key + ".mode", 0) == 0 ? "one-v-one" : "two-v-two") + ".txt";
        [Serializable] sealed class RootState { public string scene; public int index; public bool active; }
        [Serializable] sealed class SavedState { public string scene; public bool added; public bool background; public RootState[] roots; }
        static EmergenceValidation() => EditorApplication.playModeStateChanged += StateChanged;
        static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); checks.Add("PASS " + label); }

        [MenuItem("MASSIVE/EMERGENCE/Validate Model and Scene")]
        public static string ModelAndScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate authoring in Edit Mode.");
            Directory.CreateDirectory(Folder); checks.Clear();
            var profile = AssetDatabase.LoadAssetAtPath<EmergenceProfile>(EmergenceLevelSetup.ProfilePath);
            var watch = new System.Diagnostics.Stopwatch(); double totalMs = 0; int steps = 0;
            foreach (int seed in new[] { 1729, 731, 4919 })
            {
                var sim = new EmergenceSimulation(profile, new Vector2(13.6f, 5.6f), seed);
                var initial = sim.Particles.Select(p => p.position).ToArray();
                var radii = sim.Particles.Select(p => p.radius).ToArray();
                Check(sim.MaximumPenetration() < .000001f, "seed " + seed + " starts without overlapping particles");
                Check(sim.Particles.Count(p => p.species < 3 && p.position.x < -6) > 100 && sim.Particles.Count(p => p.species < 3 && p.position.x > 6) > 100, "seed " + seed + " starts as a field-wide dispersed mixture");
                float worstPenetration = 0; int worstOverlapping = 0;
                bool recoveredProductivity = false;
                int modelSteps = profile.simulationHz * 60;
                watch.Restart();
                for (int i = 0; i < modelSteps; i++)
                {
                    sim.Step(1f / profile.simulationHz); while (sim.TryDequeue(out _)) { }
                    if (i > modelSteps * 3 / 4 && sim.ProductiveBodies > 0) recoveredProductivity = true;
                    if (i % profile.simulationHz == 0)
                    {
                        var spacing = MeasureSpacing(sim);
                        worstPenetration = Mathf.Max(worstPenetration, spacing.maximumOverlap);
                        worstOverlapping = Mathf.Max(worstOverlapping, spacing.overlappingParticles);
                    }
                    if (i == modelSteps / 2) sim.Disturb(sim.Particles[12].position, 2.8f, profile.repulsorImpulse, profile.disruptedSeconds, Vector2.zero);
                }
                watch.Stop(); totalMs += watch.Elapsed.TotalMilliseconds; steps += modelSteps;
                // Verify the material cycle, not the hard-contact prototype's reward rate.
                // Force-based organisms split and merge differently; report yield for balance review.
                Check(sim.Consumed > 30 && sim.Secreted > 0, "seed " + seed + " assembles, feeds and secretes (" + sim.Secreted + " nuggets)");
                Check(recoveredProductivity, "seed " + seed + " recovers productive activity during the final 15 seconds after disturbance");
                Check(Mathf.Abs(sim.Consumed - sim.StoredMaterial - sim.ExportedMaterial) < .1f, "seed " + seed + " preserves intake / reserve / waste / export accounting");
                Check(sim.Particles.All(p => !float.IsNaN(p.position.x) && !float.IsNaN(p.velocity.y) && Mathf.Abs(p.position.x) <= sim.HalfSize.x + .001f && Mathf.Abs(p.position.y) <= sim.HalfSize.y + .001f), "seed " + seed + " stays finite and bounded");
                float travel = sim.Particles.Take(profile.BodyParticleCount).Select((p, i) => Vector2.Distance(p.position, initial[i])).Average();
                Check(travel > .2f, "seed " + seed + " particles move and self-organize (mean displacement " + travel.ToString("F2") + " units)");
                Check(worstOverlapping < profile.ParticleCount * .05f, "seed " + seed + " keeps at least 95% of particles visually separate in every one-second sample (worst overlap count " + worstOverlapping + ", depth " + worstPenetration.ToString("F4") + ")");
                Check(radii.SequenceEqual(sim.Particles.Select(p => p.radius)), "seed " + seed + " keeps stable visible and repulsive-core sizes");
                Check(sim.Replenished > 0 && sim.Particles.Length == profile.ParticleCount, "seed " + seed + " replenishes food with a fixed population");
            }
            var empty = Object.Instantiate(profile); empty.species[3].count = 0;
            try
            {
                var sim = new EmergenceSimulation(empty, new Vector2(13.6f, 5.6f), 1729);
                for (int i = 0; i < 900; i++) sim.Step(1f / 30);
                Check(sim.Consumed == 0 && sim.Secreted == 0 && sim.StoredMaterial == 0, "starved colonies cannot create nuggets");
            }
            finally { Object.DestroyImmediate(empty); }
            var a = new EmergenceSimulation(profile, new Vector2(13.6f, 5.6f), 100);
            var b = new EmergenceSimulation(profile, new Vector2(13.6f, 5.6f), 100);
            for (int i = 0; i < 90; i++) { a.Step(1f / 30); b.Step(1f / 30); }
            Check(a.Particles.Select(p => p.position).SequenceEqual(b.Particles.Select(p => p.position)), "fixed seed and tick reproduce the same simulation");
            ValidateSoftSpacing(profile);
            var scene = EditorSceneManager.OpenPreviewScene(EmergenceLevelSetup.ScenePath);
            try { ValidateScene(scene); } finally { EditorSceneManager.ClosePreviewScene(scene); }
            string result = "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks) + "\nModel mean ms/tick (Editor, three seeds, includes disturbance): " + (totalMs / steps).ToString("F3");
            File.WriteAllText(Folder + "/model-and-scene.txt", result); Debug.Log("EMERGENCE MODEL / SCENE PASSED"); return result;
        }
        static void ValidateScene(Scene scene)
        {
            var all = EmergenceLevelSetup.All<Transform>(scene);
            var context = EmergenceLevelSetup.All<LevelSceneContext>(scene).Single();
            var field = EmergenceLevelSetup.All<EmergenceField>(scene).Single();
            Check(scene.GetRootGameObjects().Select(g => g.name).SequenceEqual(new[] { "00_Systems", "01_Presentation", "02_Arena", "GameplayObjects", "04_UI", "90_EditorOnly" }), "six universal roots retained");
            Check(context.level && context.level.levelTitle == "EMERGENCE" && context.level.SceneName == "S-10_EMERGENCE" && context.stage.stageId == "STAGE_010", "new level and stage identities");
            Check(context.level.iconPrefab && context.level.instructionsPanelPrefab && context.level.audioProfile, "icon, instructions and audio assigned");
            Check(!all.Any(t => t.name.Contains("PULSAR")) && EmergenceLevelSetup.All<PulsarRhythmSession>(scene).Length == 0 && EmergenceLevelSetup.All<AttractScript>(scene).Length == 0, "PULSAR-specific body and rhythm removed");
            Check(field.profile && field.particleMaterial && field.arena && field.scores && field.nuggetPrefab && field.players.Length == 4, "ecology has explicit arena, scoring, pickup and roster references");
            Check(field.match == context.match, "ecology uses the scene match owner for pre-regulation simulation");
            Check(field.nuggetPrefab.scoreRewardKey == ScoreRewardKeys.MassNugget && field.nuggetPrefab.rewardMultiplier == 1, "metabolic nugget uses standard mass and score economy");
            Check(field.matterNuggletPrefab && field.amplifierNuggletPrefab && field.amplifierNuggetPrefab && field.profile.pickupChances.IsValid,
                "all four weighted pickup types are assigned");
            Check(EmergenceLevelSetup.All<MatchScoreService>(scene).Length == 1 && EmergenceLevelSetup.All<MatchTimerPresenter>(scene).Length == 1 && EmergenceLevelSetup.All<AmplifierGoalCapture>(scene).Length == 2, "single score authority, timer and two goals");
            Check(EmergenceLevelSetup.All<EnemyDirector>(scene).Single().encounterTimeline && EmergenceLevelSetup.All<AmplifierResonanceSpawner>(scene).Single().scoreService == field.scores, "shared enemies and Amplifier encounter retained");
            foreach (var pair in new[] { new[] { "00_Systems/MatchRuntime", "MatchRuntime" }, new[] { "GameplayObjects/Players", "Players_Gameplay" }, new[] { "02_Arena/Surface", "Arena_Planar_Surface" }, new[] { "02_Arena/BoundsAndCollision", "Arena_Planar_Bounds" }, new[] { "02_Arena/Goals", "TeamGoals_Planar" }, new[] { "04_UI/MatchHUD", "MatchHUD" } })
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(all.Single(t => AnimationUtility.CalculateTransformPath(t, null) == pair[0]).gameObject) == "Assets/Prefabs/Levels/" + pair[1] + ".prefab", "shared prefab " + pair[1]);
            Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts");
            int count = 0;
            foreach (var c in EmergenceLevelSetup.All<Component>(scene).Where(c => c))
            {
                var p = new SerializedObject(c).GetIterator();
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0) throw new Exception("Broken reference " + c.name + "." + p.propertyPath);
                    var go = p.objectReferenceValue as GameObject; if (!go && p.objectReferenceValue is Component component) go = component.gameObject;
                    if (go && go.scene.IsValid() && go.scene != scene) throw new Exception("Cross-scene reference " + c.name + "." + p.propertyPath); count++;
                }
            }
            Check(true, count + " serialized reference slots checked");
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
            Check(catalog.Levels.Count(l => l == context.level) == 1 && EditorBuildSettings.scenes.Count(s => s.enabled && s.path == scene.path) == 1, "EMERGENCE appears once in level selection and has one enabled gameplay scene");
            Check(!ShaderUtil.ShaderHasError(field.particleMaterial.shader), "particle shader compiles");
        }

        [MenuItem("MASSIVE/EMERGENCE/Validate Gameplay (1v1 and 2v2)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start validation in Edit Mode.");
            Directory.CreateDirectory(Folder);
            var saved = new SavedState { scene = SceneManager.GetActiveScene().path, background = Application.runInBackground };
            var roots = new List<RootState>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i); if (s.path == EmergenceLevelSetup.ScenePath) continue;
                foreach (var root in s.GetRootGameObjects())
                { roots.Add(new RootState { scene = s.path, index = root.transform.GetSiblingIndex(), active = root.activeSelf }); root.SetActive(false); }
            }
            saved.roots = roots.ToArray();
            var level = SceneManager.GetSceneByPath(EmergenceLevelSetup.ScenePath);
            if (!level.IsValid()) { level = EditorSceneManager.OpenScene(EmergenceLevelSetup.ScenePath, OpenSceneMode.Additive); saved.added = true; }
            SceneManager.SetActiveScene(level);
            SessionState.SetString(Key + ".saved", JsonUtility.ToJson(saved));
            AssetDatabase.DisallowAutoRefresh(); SessionState.SetBool(Key + ".refreshHeld", true);
            StartMode(0);
        }
        static void StartMode(int mode)
        {
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".mode", mode); SessionState.SetBool(Key + ".passed", false);
            EnemyEncounterPreviewMode.Request(mode == 1); EditorApplication.isPlaying = true;
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Other work in the shared Editor may compile. Hold reloads only for this bounded fixture.
                EditorApplication.LockReloadAssemblies(); SessionState.SetBool(Key + ".reloadHeld", true);
                checks.Clear(); errors.Clear(); errorCount = 0; frame = -1; deadline = EditorApplication.timeSinceStartup + 300;
                Application.runInBackground = true; Application.logMessageReceived += Log;
                routine = Flatten(BothModes()); EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (SessionState.GetBool(Key + ".reloadHeld", false)) { SessionState.SetBool(Key + ".reloadHeld", false); EditorApplication.UnlockReloadAssemblies(); }
                EditorApplication.update -= Tick; routine = null;
                Restore();
            }
        }
        public static void Restore()
        {
            if (SessionState.GetBool(Key + ".reloadHeld", false)) { SessionState.SetBool(Key + ".reloadHeld", false); EditorApplication.UnlockReloadAssemblies(); }
            var saved = JsonUtility.FromJson<SavedState>(SessionState.GetString(Key + ".saved", "{}"));
            if (saved.roots != null) foreach (var r in saved.roots)
            {
                var s = SceneManager.GetSceneByPath(r.scene); if (!s.IsValid()) continue;
                var root = s.GetRootGameObjects().FirstOrDefault(g => g.transform.GetSiblingIndex() == r.index); if (root) root.SetActive(r.active);
            }
            var scene = SceneManager.GetSceneByPath(EmergenceLevelSetup.ScenePath);
            if (saved.added && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            var previous = SceneManager.GetSceneByPath(saved.scene); if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            Application.runInBackground = saved.background; SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".refreshHeld", false)) { SessionState.SetBool(Key + ".refreshHeld", false); AssetDatabase.AllowAutoRefresh(); }
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errorCount++; if (errors.Count < 16) errors.Add(message + "\n" + stack); } }
        static IEnumerator Flatten(IEnumerator source)
        {
            while (source.MoveNext()) if (source.Current is IEnumerator nested) { var e = Flatten(nested); while (e.MoveNext()) yield return e.Current; } else yield return source.Current;
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
            try
            {
                frame = Time.frameCount;
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Gameplay validation timed out.");
                if (!routine.MoveNext()) Finish(null);
                else File.WriteAllText(Report, "RUNNING\n" + string.Join("\n", checks));
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception failure)
        {
            routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1;
            bool pass = failure == null && errors.Count == 0;
            File.WriteAllText(Report, (pass ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" + string.Join("\n", checks) + "\n" + failure + "\nConsole errors: " + errorCount + "\n" + string.Join("\n", errors));
            SessionState.SetBool(Key + ".passed", pass); EditorApplication.isPlaying = false;
        }
        static IEnumerator Wait(float seconds) { float end = Time.unscaledTime + seconds; while (Time.unscaledTime < end) yield return null; }
        static IEnumerator Until(Func<bool> condition, float seconds, string label)
        { float end = Time.unscaledTime + seconds; while (!condition() && Time.unscaledTime < end) yield return null; Check(condition(), label); }
        static void Move(PlayerControllerScript player, Vector3 point)
        { var body = player.GetComponent<Rigidbody>(); point.y = body.position.y; body.position = point; body.linearVelocity = Vector3.zero; Physics.SyncTransforms(); }
        static Vector3 Colony(EmergenceField field)
        {
            var ps = field.Simulation.Particles;
            return field.WorldPosition(ps.First(p => p.species < 3).position);
        }
        static IEnumerator BothModes()
        {
            yield return RuntimeChecks();
            File.WriteAllText(Report, "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks) + "\nConsole errors: " + errors.Count);
            // Exercise a real scene reload and a fresh roster without releasing the bounded reload hold.
            SessionState.SetInt(Key + ".mode", 1); checks.Clear(); errors.Clear(); errorCount = 0;
            GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
            // Keep the user's disabled background scenes loaded. Destroying an inactive Attract
            // InputManager can tear down Rewired's static state despite the active persistent manager.
            var buffer = SceneManager.CreateScene("EMERGENCE validation reload buffer");
            var unload = SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(EmergenceLevelSetup.ScenePath));
            while (!unload.isDone) yield return null;
            var load = SceneManager.LoadSceneAsync(EmergenceLevelSetup.ScenePath, LoadSceneMode.Additive);
            while (!load.isDone) yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(EmergenceLevelSetup.ScenePath));
            var cleanup = SceneManager.UnloadSceneAsync(buffer); while (!cleanup.isDone) yield return null;
            yield return RuntimeChecks();
        }
        static IEnumerator RuntimeChecks()
        {
            var scene = SceneManager.GetSceneByPath(EmergenceLevelSetup.ScenePath);
            var context = EmergenceLevelSetup.All<LevelSceneContext>(scene).Single();
            var field = EmergenceLevelSetup.All<EmergenceField>(scene).Single();
            var gm = context.match; var scores = field.scores;
            var settings = new SerializedObject(gm); settings.FindProperty("allPlayersInactiveHoldSeconds").floatValue = 600;
            // Give the shared Editor room to observe pause/resume before the real phase transition.
            // This disposable fixture never saves its longer countdown to the scene or prefab.
            settings.FindProperty("preMatchCountdownSeconds").floatValue = 10; settings.ApplyModifiedPropertiesWithoutUndo();
            yield return Until(() => field.Simulation != null && field.Simulation.Age > 0, 1, "scene startup advances the ecology");
            Check(gm.Phase == MatchRuntimePhase.Preparing || gm.Phase == MatchRuntimePhase.Countdown, "ecology starts before regulation");
            var startupModel = field.Simulation;
            yield return Until(() => gm.Phase == MatchRuntimePhase.Countdown, 15, "normal startup reaches countdown");
            float countdownAge = field.Simulation.Age;
            Time.timeScale = 0; yield return Wait(.2f);
            Check(field.Simulation.Age == countdownAge, "time pause also freezes pre-regulation ecology");
            Time.timeScale = 1; yield return Wait(.25f);
            Check(gm.Phase == MatchRuntimePhase.Countdown && field.Simulation.Age > countdownAge,
                "countdown advances the same ecology (phase=" + gm.Phase + ", age=" + countdownAge.ToString("F3") + " -> " + field.Simulation.Age.ToString("F3") + ")");
            Check(field.NuggetsReleased == 0 && field.ActiveNuggets == 0 && field.Simulation.Secreted == 0 && field.Simulation.PendingCount == 0 &&
                field.Simulation.RewardBuds.All(b => b.phase == 0) && scores.LightScoreMilliElectronVolts == 0 && scores.DarkScoreMilliElectronVolts == 0,
                "countdown cannot start reward buds, emit pickups or award score");
            Check(field.WakeContacts == 0 && field.AttackContacts == 0 && field.RepulsorContacts == 0, "countdown does not apply player disturbances");
            yield return Until(() => gm.Phase == MatchRuntimePhase.Regulation, 20, "normal match reaches regulation");
            Check(field.Simulation == startupModel && field.Simulation.Age > countdownAge, "regulation continues the developed ecosystem without resetting it");
            var players = field.players.Where(p => p.isActiveAndEnabled && !p.IsPseudoPlayer).ToArray();
            Check(players.Length == (SessionState.GetInt(Key + ".mode", 0) == 0 ? 2 : 4) && Rewired.ReInput.isReady && context.roster.IsRosterReady, "real roster and Rewired initialize in requested mode");
            Check(context.stageTitleText.text == "EMERGENCE" && context.stageNumberText.text == "STAGE_010" && context.spawners.activeInHierarchy, "HUD identity and phase-gated spawning");
            // Larger authored Minimum Body values need more time to assemble from the dispersed soup.
            yield return Until(() => field.Simulation.BodyCount > 0 && field.Simulation.Consumed > 0, 45, "live colonies form and feed");
            var director = EmergenceLevelSetup.All<EnemyDirector>(scene).Single();
            yield return Until(() => director.TotalSpawned > 0, 15, "inherited enemy timeline spawns real enemies");
            // Ecology catch-up is capped per frame; a busy shared Editor can advance the
            // unscaled match clock considerably faster than the particle simulation.
            float emissionDeadline = Time.unscaledTime + 60;
            while (field.NuggetsReleased == 0 && Time.unscaledTime < emissionDeadline) yield return null;
            Check(field.NuggetsReleased > 0, "natural metabolism emits shared mass pickups (ecology age=" + field.Simulation.Age.ToString("F2") +
                ", running=" + field.IsRunning + ", phase=" + gm.Phase + ", consumed=" + field.Simulation.Consumed + ", secreted=" + field.Simulation.Secreted + ")");
            Check(field.Simulation.ProductiveBodies > 0, "at least one live productive body");
            ScreenCapture.CaptureScreenshot(Folder + (players.Length == 2 ? "/one-v-one.png" : "/two-v-two.png")); yield return Wait(.3f);
            float age = field.Simulation.Age; int emitted = field.NuggetsReleased;
            scores.SetScoringState(true, false); yield return Wait(.3f);
            Check(field.Simulation.Age == age && field.NuggetsReleased == emitted, "closed match clock pauses chemistry and secretion");
            scores.SetScoringState(true, true); Time.timeScale = 0; yield return Wait(.3f);
            Check(field.Simulation.Age == age, "time pause freezes simulation"); Time.timeScale = 1;
            // Keep existing encounters verified above, then isolate contact assertions from enemy knockback.
            director.gameObject.SetActive(false);
            // Hold secretion so an expired pooled slot cannot legitimately be reused
            // between the expiry event and this fixture's observation of it.
            scores.SetScoringState(true, false);
            var player = players[0];
            foreach (var p in players.Skip(1)) Move(p, new Vector3(11, 0, 4));
            // The random harvest can contain only amplifiers. Exercise fixed-score rules with an explicit matter fixture.
            var pickup = Object.Instantiate(field.nuggetPrefab, field.transform);
            pickup.Eject(new Vector3(0, 0, 4), Vector3.zero, 10);
            // Let the shared spawn collection lock expire before placing the body on the pickup.
            yield return Wait(.4f);
            player.massScore = player.massScoreMax; long before = scores.GetTeamScore(player.teamID);
            Move(player, pickup.transform.position);
            yield return Until(() => pickup.LastScoreAward.accepted, 3, "player body collects metabolic nugget through shared scoring");
            long after = scores.GetTeamScore(player.teamID);
            Check(after > before && pickup.LastMassRestored < .0001f, "full-mass collection still awards score without false mass restoration");
            yield return Wait(.4f); Check(scores.GetTeamScore(player.teamID) == after, "repeated pickup contacts do not duplicate score");
            Move(player, new Vector3(11, 0, -4));
            pickup.Eject(new Vector3(0, 0, 4), Vector3.zero, 1); yield return Wait(1.2f);
            Check(!pickup.gameObject.activeSelf && scores.GetTeamScore(player.teamID) == after, "pooled pickup expires without scoring");
            pickup.Eject(player.transform.position, Vector3.zero, 5); yield return Wait(.7f);
            Check(pickup.LastScoreAward.accepted && scores.GetTeamScore(player.teamID) > after, "pooled reuse starts a fresh single-award life");
            Object.Destroy(pickup.gameObject);
            scores.SetScoringState(true, true);
            var attack = player.GetComponent<PlayerAttackController>();
            int wakes = field.WakeContacts; Move(player, Colony(field)); yield return Wait(.3f);
            Check(field.WakeContacts > wakes, "player body disturbs the soup");
            Move(player, Colony(field) - Vector3.right * .6f); attack.SetAimDirection(Vector3.right);
            int hits = field.AttackContacts, pulses = field.RepulsorContacts;
            attack.BeginAttack(); float end = Time.unscaledTime + 5;
            while (Time.unscaledTime < end && field.RepulsorContacts == pulses)
            {
                attack.RegisterAttackPress();
                if (attack.CurrentStageIndex == 2) Move(player, Colony(field));
                yield return null;
            }
            Check(field.AttackContacts > hits, "real Thrust/Sweep collider disrupts particles");
            Check(field.RepulsorContacts > pulses, "real expanding Repulsor reaches and scatters particles");
            Check(field.Simulation.Disruptions > 0, "combat interrupts colony maturity");
            float stored = field.Simulation.StoredMaterial; var model = field.Simulation;
            field.enabled = false; yield return Wait(.2f); field.enabled = true; yield return Wait(.2f);
            Check(field.Simulation == model && field.Simulation.StoredMaterial <= stored + 12, "disable/re-enable preserves the material ledger and simulation");
            Check(field.ActiveNuggets <= field.profile.nuggetCapacity && field.Simulation.Particles.Length == field.profile.ParticleCount, "particle and pickup populations stay bounded");
            var spacing = MeasureSpacing(field.Simulation);
            Check(spacing.overlappingParticles < field.profile.ParticleCount * .05f, "soft repulsion keeps at least 95% of live particles visually separate after combat (overlapping " + spacing.overlappingParticles + ")");
            var times = new List<float>(); for (int i = 0; i < 60; i++) { times.Add(field.LastStepMilliseconds); yield return null; }
            times.Sort(); checks.Add("MEASURE " + (field.UsesGpu ? "CPU submission ms (GPU execution excluded)" : "live CPU ecology tick ms") + ": median=" + times[times.Count / 2].ToString("F2") + " p95=" + times[(int)(times.Count * .95f)].ToString("F2") + " (Editor, current PC)");
            var gameplay = scene.GetRootGameObjects().Single(g => g.CompareTag("GameplayObjects"));
            age = field.Simulation.Age; gameplay.SetActive(false); yield return Wait(.2f);
            Check(field.Simulation.Age == age && !field.isActiveAndEnabled, "gameplay shutdown stops ecology and its pickups");
            Check(errors.Count == 0, "no runtime Console errors");
        }
    }
}
