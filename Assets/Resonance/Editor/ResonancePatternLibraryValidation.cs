using System;
using System.Collections.Generic;
using Massive.Multiplier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Resonance.Editor
{
    public static class ResonancePatternLibraryValidation
    {
        [MenuItem("MASSIVE/Resonance/Validate Pattern Library")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run asset checks in Edit Mode.");
            var checks = new List<string>();
            Action<bool, string> check = (ok, name) => { if (!ok) throw new InvalidOperationException(name); checks.Add(name); };
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                int[] expected = { 8, 12, 8, 14 };
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(ResonancePatternLibrarySetup.Template).GetComponent<ResonancePatternController>();
                for (int i = 0; i < expected.Length; i++)
                {
                    int hz = ResonancePatternLibrarySetup.Frequencies[i];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResonancePatternLibrarySetup.PrefabPath(hz));
                    var asset = prefab.GetComponent<ResonancePatternController>();
                    var d = asset.definition;
                    check(d != null && d.arcs.Count == expected[i], hz + " approved collision-arc count");
                    check(prefab.transform.childCount == 0 && prefab.GetComponent<ResonanceManifestation>() != null, hz + " reusable prefab has no serialized generated geometry");
                    check(asset.patternScale == Vector2.one && asset.particles.particleBudget == template.particles.particleBudget
                        && asset.magneticPath == template.magneticPath && asset.playerResponse == template.playerResponse,
                        hz + " footprint and existing interaction/particle tuning preserved");
                    var cue = d.coreImpactSound;
                    check(cue != null && cue.clips.Length == 1 && cue.clips[0] == AssetDatabase.LoadAssetAtPath<AudioClip>(ResonancePatternLibrarySetup.ClipPath(i))
                        && cue.clips[0] != null && Mathf.Abs(cue.clips[0].length - .72f) < .001f,
                        hz + " assigned approved synth impact");
                    check(cue.pitchJitter == Vector2.one && cue.cooldownSeconds > 0f && cue.maxSimultaneous > 0,
                        hz + " exact pitch and bounded playback");
                    foreach (var curve in d.curves)
                    {
                        foreach (var p in curve.points)
                            if (float.IsNaN(p.x) || float.IsNaN(p.y) || Mathf.Abs(p.x) > 14.001f || Mathf.Abs(p.y) > 6.001f)
                                throw new InvalidOperationException(hz + " invalid arena trace: " + p);
                        bool open = curve.kind == ResonanceCurveKind.OpenPolyline;
                        var end = curve.Evaluate(360);
                        if (open)
                        {
                            var p = curve.points[curve.points.Count - 1] + curve.offset;
                            check(Vector3.Distance(end, new Vector3(p.x, 0, p.y)) < .0001f
                                && Vector3.Distance(curve.Evaluate(0), end) > 1f, hz + " " + curve.name + " stays open at its actual endpoint");
                        }
                        else check(Vector3.Distance(curve.Evaluate(-20), curve.Evaluate(340)) < .001f, hz + " closed curve wraps correctly");
                    }
                    check(true, hz + " full trace stays within the 28 × 12 arena");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    try
                    {
                        var pattern = go.GetComponent<ResonancePatternController>();
                        foreach (var mode in new[] { ResonanceRendering.OptionBParticles, ResonanceRendering.OptionAContinuous })
                        {
                            pattern.rendering = mode; pattern.Rebuild();
                            check(pattern.InteractiveSegments.Count == expected[i], hz + " " + mode + " builds every exposed arc");
                            check(pattern.GeneratedRoot.GetComponentsInChildren<Collider>().Length > 0, hz + " " + mode + " builds collisions");
                            var ghost = pattern.GeneratedRoot.transform.Find("Full pattern — impact reveal (visual only)");
                            check(ghost != null && ghost.childCount == d.curves.Count && ghost.GetComponentsInChildren<Collider>().Length == 0,
                                hz + " " + mode + " complete ghost remains visual-only");
                            check(pattern.ParticleCount <= pattern.particles.particleBudget, hz + " " + mode + " respects particle budget");
                            foreach (var segment in pattern.InteractiveSegments)
                            {
                                var arc = segment.Settings;
                                check(arc.CollisionEnvelope(.5f) > 0 && arc.Envelope(.5f) > 0, hz + " " + arc.name + " has a usable center");
                                if (arc.untaperedStart) check(arc.Envelope(0) == 1 && arc.CollisionEnvelope(0) == 1, hz + " start reaches wall at full width");
                                if (arc.untaperedEnd) check(arc.Envelope(1) == 1 && arc.CollisionEnvelope(1) == 1, hz + " end reaches wall at full width");
                            }
                            if (hz == 776)
                            {
                                float central = pattern.InteractiveSegments[0].PathLength;
                                for (int a = 4; a < 12; a++)
                                    check(Mathf.Abs(pattern.InteractiveSegments[a].PathLength / central - .5f) < .002f,
                                        "776 outer segment " + a + " is half central arc length");
                            }
                        }
                    }
                    finally { Object.DestroyImmediate(go); }
                }
                var openArc = new ResonanceArc { sweepDegrees = 360, openPath = true };
                check(openArc.Envelope(0) == 0 && openArc.Envelope(1) == 0 && openArc.Envelope(.5f) == 1, "full open path tapers rather than behaving like a closed ring");
                var legacy = new ResonanceArc { sweepDegrees = 360 };
                check(legacy.Envelope(0) == 1 && legacy.Envelope(1) == 1, "legacy closed-ring envelope unchanged");
                var cycle = AssetDatabase.LoadAssetAtPath<GameObject>(ResonancePatternLibrarySetup.Encounter).GetComponentInChildren<AmplifierResonanceSpawner>();
                check(cycle.patternOrder.Count == 5 && cycle.loopPatternOrder, "shared encounter cycles five patterns");
                check(cycle.patternOrder[0].patternPrefab == template, "345 original prefab and definition retained");
                var originalCue = AssetDatabase.LoadAssetAtPath<AudioCue>(ResonancePatternLibrarySetup.CuePath(345));
                check(originalCue != null && originalCue.clips.Length == 1 && originalCue.clips[0] != null
                    && originalCue.clips[0] == AssetDatabase.LoadAssetAtPath<AudioClip>(ResonancePatternLibrarySetup.AudioFolder + "/massive-resonance-345hz-foundation-impact.wav")
                    && Mathf.Abs(originalCue.clips[0].length - .72f) < .001f, "345 assigned matching short synth impact");
                check(originalCue.pitchJitter == Vector2.one && originalCue.cooldownSeconds > 0f && originalCue.maxSimultaneous > 0,
                    "345 exact pitch and bounded playback");
                foreach (var path in new[] { ResonancePatternLibrarySetup.DefinitionPath(345),
                    "Assets/Resonance/345 Hz Pattern.asset", "Assets/Resonance/345 Hz Pattern - Option B.asset" })
                    check(AssetDatabase.LoadAssetAtPath<ResonancePatternDefinition>(path).coreImpactSound == originalCue,
                        "345 encounter and prototype audio reference: " + path);
                for (int i = 0; i < 4; i++) check(cycle.patternOrder[i + 1].patternPrefab.definition.referenceLabel.StartsWith(ResonancePatternLibrarySetup.Frequencies[i] + " Hz"), "cycle order " + (i + 1));
                var fixture = new GameObject("Append check"); SceneManager.MoveGameObjectToScene(fixture, scene);
                var fixtureCycle = fixture.AddComponent<AmplifierResonanceSpawner>();
                fixtureCycle.patternOrder = new List<ResonanceSpawnEntry>(cycle.patternOrder);
                check(!ResonancePatternLibrarySetup.AppendMissing(fixtureCycle) && fixtureCycle.patternOrder.Count == 5, "repeated installation does not duplicate/reorder entries");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return "PASS: " + checks.Count + " library checks\n" + string.Join("\n", checks);
        }
    }
}
