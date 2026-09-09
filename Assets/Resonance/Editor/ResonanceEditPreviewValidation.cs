using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Resonance.Editor
{
    /// <summary>Isolated authoring checks; no gameplay scene, prefab asset or Play state is changed.</summary>
    public static class ResonanceEditPreviewValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("MASSIVE/Resonance/Validate Edit Mode Formation Authoring")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run formation authoring validation in Edit Mode.");
            var passed = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject sourceRoot = null, destinationRoot = null;
            try
            {
                definition.Set345HzDefaults();
                definition.showCenter = false;
                definition.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = -20, sweepDegrees = 40, thickness = .3f } };
                sourceRoot = new GameObject("Formation authoring source");
                destinationRoot = new GameObject("Formation authoring destination");
                SceneManager.MoveGameObjectToScene(sourceRoot, scene);
                SceneManager.MoveGameObjectToScene(destinationRoot, scene);
                var source = sourceRoot.AddComponent<ResonanceManifestation>();
                var destination = destinationRoot.AddComponent<ResonanceManifestation>();
                var pattern = destination.GetComponent<ResonancePatternController>();
                pattern.definition = definition;
                pattern.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Particles.mat");
                pattern.rendering = ResonanceRendering.OptionBParticles;
                pattern.particles.particleBudget = 1024;
                pattern.Rebuild();
                Check(pattern.ParticleCount > 0 && destinationRoot.GetComponentsInChildren<Renderer>(true).Length > 0,
                    "isolated scene fixture contains real particle renderers", passed);

                source.spawnSeconds = 3.1f; source.despawnSeconds = .8f;
                source.sizeGrowthPower = .35f; source.condensationPower = 2.3f;
                source.dispersalCenter = new Vector2(2, -1); source.dispersalHalfExtents = new Vector2(7, 4);
                source.vibrationStrength = .23f; source.vibrationFrequency = 13f; source.showDispersalArea = false;
                source.spawnPrefabTuningTarget = source;
                source.SetImmediate(false);
                destination.spawnPrefabTuningTarget = destination;
                destination.SetNormalizedProgress(.4f);
                destination.enabled = false;
                string controllerBefore = JsonUtility.ToJson(pattern), definitionBefore = JsonUtility.ToJson(definition);
                string sourceBefore = JsonUtility.ToJson(source);
                var generatedBefore = pattern.GeneratedRoot;
                destination.CopyAnimationSettingsFrom(source);
                Check(AnimationSettingsEqual(source, destination), "copy transfers every animation dial", passed);
                Check(destination.spawnPrefabTuningTarget == destination && !destination.enabled
                    && destination.name == "Formation authoring destination", "copy preserves target link, enabled state and object identity", passed);
                Check(Near(destination.NormalizedProgress, .4f) && destination.IsControlled && !destination.IsAnimating,
                    "copy preserves the held destination pose instead of copying hidden source state", passed);
                Check(JsonUtility.ToJson(pattern) == controllerBefore && JsonUtility.ToJson(definition) == definitionBefore,
                    "copy leaves controller, particle and definition tuning untouched", passed);
                Check(JsonUtility.ToJson(source) == sourceBefore && source.IsHidden,
                    "copy never mutates its source tuning or preview pose", passed);
                Check(pattern.GeneratedRoot == generatedBefore && pattern.ParticleCount > 0,
                    "tuning changes reuse generated particle pools", passed);

                var renderer = destinationRoot.GetComponentInChildren<ParticleSystemRenderer>();
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                Vector4 oldPose = properties.GetVector("_Manifestation");
                destination.sizeGrowthPower = 3.3f; destination.condensationPower = .2f;
                destination.vibrationStrength = .41f; destination.RefreshPreview();
                renderer.GetPropertyBlock(properties); Vector4 refreshedPose = properties.GetVector("_Manifestation");
                Check(oldPose != refreshedPose && Near(destination.NormalizedProgress, .4f) && !destination.IsAnimating,
                    "refresh immediately updates shader tuning while preserving a held scrub pose", passed);
                destination.Advance(10f);
                Check(Near(destination.NormalizedProgress, .4f) && destination.IsTransitioning && !destination.IsAnimating,
                    "a held intermediate pose does not become an active editor animation", passed);
                Check(!pattern.InteractionEnabled && destinationRoot.GetComponentsInChildren<Collider>(true).All(c => !c.enabled),
                    "held preview keeps all pattern interaction gated", passed);

                string tuningBeforeNoOp = JsonUtility.ToJson(destination);
                destination.CopyAnimationSettingsFrom(null); destination.CopyAnimationSettingsFrom(destination);
                Check(JsonUtility.ToJson(destination) == tuningBeforeNoOp && Near(destination.NormalizedProgress, .4f),
                    "null and self-copy are harmless no-ops", passed);

                destination.enabled = true; destination.spawnSeconds = 2f;
                destination.SetImmediate(false); destination.BeginSpawn();
                Check(destination.IsAnimating && destination.NormalizedProgress == 0f,
                    "Form In starts at zero without an immediate stale-clock step", passed);
                double tickStarted = EditorApplication.timeSinceStartup - 1;
                SetEditorClock(destination, tickStarted);
                Invoke(destination, "EditorTick");
                float elapsedProgress = Mathf.Clamp01((float)(GetEditorClock(destination) - tickStarted) / destination.spawnSeconds);
                Check(destination.NormalizedProgress >= .5f && Near(destination.NormalizedProgress, elapsedProgress),
                    "editor callback advances once by actual elapsed time rather than shortening long frames", passed);
                float progressAfterTick = destination.NormalizedProgress;
                Invoke(destination, "Update"); Invoke(destination, "Update");
                Check(Near(destination.NormalizedProgress, progressAfterTick),
                    "ExecuteAlways Update does not double-advance the editor animation clock", passed);

                destination.enabled = false;
                SetEditorClock(destination, EditorApplication.timeSinceStartup - 2);
                Invoke(destination, "EditorTick");
                Check(Near(destination.NormalizedProgress, progressAfterTick) && !pattern.InteractionEnabled,
                    "disabled preview does not tick or reset the interaction gate to idle", passed);
                destination.SetImmediate(false); destination.enabled = true;
                Check(destination.IsHidden && !pattern.InteractionEnabled,
                    "disable and reenable preserve the existing hidden lifecycle contract", passed);
                destination.SetNormalizedProgress(.35f);
                Invoke(destination, "EditorPlayModeChanged", PlayModeStateChange.ExitingEditMode);
                Check(!destination.IsControlled && destination.IsIdle && !destination.IsAnimating
                    && destination.NormalizedProgress == 1f && pattern.InteractionEnabled,
                    "Play entry restores a temporary editor pose without requiring domain reload", passed);
                Check(destinationRoot.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled),
                    "Play-entry reset visibly restores the resting particle pattern", passed);

                destination.spawnPrefabTuningTarget = source;
                string rejectedSourceBefore = JsonUtility.ToJson(source);
                bool rejected = false;
                try { ResonanceManifestationEditor.SaveAnimationTuning(destination); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && JsonUtility.ToJson(source) == rejectedSourceBefore,
                    "Save Animation Tuning rejects nonpersistent scene destinations without modifying them", passed);
                rejected = false;
                try { ResonanceManifestationEditor.SaveAnimationTuning(null); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Save Animation Tuning rejects a missing source", passed);
                return passed.Count + " edit-mode formation authoring checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (sourceRoot != null) UnityEngine.Object.DestroyImmediate(sourceRoot);
                if (destinationRoot != null) UnityEngine.Object.DestroyImmediate(destinationRoot);
                UnityEngine.Object.DestroyImmediate(definition);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static bool AnimationSettingsEqual(ResonanceManifestation a, ResonanceManifestation b)
        {
            return a.spawnSeconds == b.spawnSeconds && a.despawnSeconds == b.despawnSeconds
                && a.sizeGrowthPower == b.sizeGrowthPower && a.condensationPower == b.condensationPower
                && a.dispersalCenter == b.dispersalCenter && a.dispersalHalfExtents == b.dispersalHalfExtents
                && a.vibrationStrength == b.vibrationStrength && a.vibrationFrequency == b.vibrationFrequency
                && a.showDispersalArea == b.showDispersalArea;
        }
        private static void SetEditorClock(ResonanceManifestation target, double time)
        { typeof(ResonanceManifestation).GetField("editorClock", PrivateInstance).SetValue(target, time); }
        private static double GetEditorClock(ResonanceManifestation target)
        { return (double)typeof(ResonanceManifestation).GetField("editorClock", PrivateInstance).GetValue(target); }
        private static void Invoke(ResonanceManifestation target, string method, params object[] args)
        { typeof(ResonanceManifestation).GetMethod(method, PrivateInstance).Invoke(target, args); }
        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .0001f; }
        private static void Check(bool value, string name, List<string> passed)
        { if (!value) throw new InvalidOperationException("Edit preview check failed: " + name); passed.Add(name); }
    }
}
