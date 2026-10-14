using System;
using System.Collections.Generic;
using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

namespace Massive.Resonance.Editor
{
    /// <summary>Creates missing assets only. Rerunning never replaces designer tuning or the 345 Hz prototype.</summary>
    public static class ResonancePatternLibrarySetup
    {
        public const string Folder = "Assets/Resonance/Spawn Patterns";
        public const string AudioFolder = "Assets/Audio/Assets/SFX/Resonance";
        public const string Template = Folder + "/345 Hz Particle Encounter.prefab";
        public const string Encounter = "Assets/Prefabs/Levels/AmplifierResonanceEncounter.prefab";
        public static readonly int[] Frequencies = { 404, 776, 1660, 4129 };
        public static readonly string[] Names = { "Crossroads", "Orbit", "Petals", "Channels" };
        const int Samples = 512;

        public static string DefinitionPath(int hz) => Folder + "/" + hz + " Hz Spawn Pattern.asset";
        public static string PrefabPath(int hz) => Folder + "/" + hz + " Hz Particle Encounter.prefab";
        public static string CuePath(int hz) => AudioFolder + "/AC_Resonance_" + hz + "Hz_Impact.asset";
        public static string ClipPath(int index) => AudioFolder + "/massive-resonance-" + Frequencies[index] + "hz-" + Names[index].ToLowerInvariant() + "-impact.wav";

        [MenuItem("MASSIVE/Resonance/Install Missing Pattern Library")]
        public static void InstallMenu() { Debug.Log(Install()); }

        public static string Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install pattern assets in Edit Mode.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Template) == null)
                throw new InvalidOperationException("The existing 345 Hz encounter template is missing.");
            // Check the approved media before creating any assets.
            for (int i = 0; i < Frequencies.Length; i++)
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(i)) == null)
                    throw new InvalidOperationException("Import the approved short impact clip first: " + ClipPath(i));

            for (int i = 0; i < Frequencies.Length; i++)
            {
                int hz = Frequencies[i];
                var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(CuePath(hz));
                if (cue == null)
                {
                    var importer = AssetImporter.GetAtPath(ClipPath(i)) as AudioImporter;
                    if (importer != null)
                    {
                        var settings = importer.defaultSampleSettings;
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.compressionFormat = AudioCompressionFormat.PCM;
                        importer.defaultSampleSettings = settings;
                        importer.forceToMono = false;
                        importer.loadInBackground = false;
                        importer.SaveAndReimport();
                    }
                    cue = ScriptableObject.CreateInstance<AudioCue>();
                    cue.clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(i)) };
                    cue.volume = .8f;
                    cue.volumeJitter = new Vector2(.94f, 1f);
                    cue.pitchJitter = Vector2.one; // Keep the authored frequency intact.
                    cue.cooldownSeconds = .12f;
                    cue.maxSimultaneous = 3;
                    cue.spatialBlend = 0f; // Same screen-space SFX convention as other arena impacts.
                    AssetDatabase.CreateAsset(cue, CuePath(hz));
                }
                var definition = AssetDatabase.LoadAssetAtPath<ResonancePatternDefinition>(DefinitionPath(hz));
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
                    Populate(definition, hz);
                    definition.coreImpactSound = cue;
                    AssetDatabase.CreateAsset(definition, DefinitionPath(hz));
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(hz)) == null)
                {
                    if (!AssetDatabase.CopyAsset(Template, PrefabPath(hz)))
                        throw new InvalidOperationException("Could not copy the encounter template for " + hz);
                    var root = PrefabUtility.LoadPrefabContents(PrefabPath(hz));
                    try
                    {
                        root.name = hz + " Hz Particle Encounter";
                        var controller = root.GetComponent<ResonancePatternController>();
                        controller.definition = definition;
                        controller.patternScale = Vector2.one;
                        controller.layoutFit = ResonanceLayoutFit.Stretch;
                        controller.rotationDegrees = 0f;
                        controller.particles.seed = hz;
                        controller.Rebuild();
                        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(hz));
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            var encounter = PrefabUtility.LoadPrefabContents(Encounter);
            try
            {
                if (AppendMissing(encounter.GetComponentInChildren<AmplifierResonanceSpawner>(true)))
                    PrefabUtility.SaveAsPrefabAsset(encounter, Encounter);
            }
            finally { PrefabUtility.UnloadPrefabContents(encounter); }
            return "Resonance library installed: 345 → 404 → 776 → 1660 → 4129. Existing assets/tuning preserved.";
        }

        public static bool AppendMissing(AmplifierResonanceSpawner spawner)
        {
            if (spawner == null) return false;
            bool changed = false;
            if (spawner.patternOrder == null) spawner.patternOrder = new List<ResonanceSpawnEntry>();
            foreach (int hz in Frequencies)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(hz));
                if (prefab == null) throw new InvalidOperationException("Install the pattern library first.");
                var pattern = prefab.GetComponent<ResonancePatternController>();
                if (spawner.patternOrder.Exists(e => e != null && e.patternPrefab == pattern)) continue;
                spawner.patternOrder.Add(new ResonanceSpawnEntry { label = pattern.definition.referenceLabel, patternPrefab = pattern });
                changed = true;
            }
            return changed;
        }

        [MenuItem("MASSIVE/Resonance/Add Missing Patterns to Selected Spawn Cycle")]
        static void AddToSelected()
        {
            var cycle = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<AmplifierResonanceSpawner>() : null;
            if (cycle == null || EditorUtility.IsPersistent(cycle) || Application.isPlaying)
                throw new InvalidOperationException("Select a scene spawn-cycle object in Edit Mode.");
            Undo.RecordObject(cycle, "Add Resonance patterns");
            if (!AppendMissing(cycle)) return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cycle);
            EditorUtility.SetDirty(cycle);
        }

        // Artistic traces of the approved arena study, not a physical plate-frequency solver.
        public static void Populate(ResonancePatternDefinition d, int hz)
        {
            int index = Array.IndexOf(Frequencies, hz);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(hz));
            d.referenceLabel = hz + " Hz — " + Names[index];
            d.showCenter = false;
            d.curves.Clear(); d.arcs.Clear();
            if (hz == 404)
            {
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                {
                    int c = Trace(d, "Diagonal oval " + sx + "," + sz, t =>
                    {
                        Vector2 p = Ellipse(6.5f, 3f, 5.1f, 1.7f, t, 16f);
                        return new Vector2(p.x * sx, p.y * sz);
                    }, false);
                    Arc(d, c, 130f / 360f, 233f / 360f, "Inner facing");
                    Arc(d, c, 310f / 360f, 413f / 360f, "Opposite side");
                }
            }
            else if (hz == 776)
            {
                int circle = Trace(d, "Central orbit", t => Ellipse(0, 0, 4.55f, 4.55f, t), false);
                Trace(d, "Center ghost", t => Ellipse(0, 0, .48f, .48f, t), false);
                for (int i = 0; i < 4; i++) Arc(d, circle, (i * 90f + 17f) / 360f, (i * 90f + 73f) / 360f, "Central quadrant " + i);
                float half = 4.55f * 56f * Mathf.Deg2Rad * .5f;
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                {
                    int c = Corner(d, sx, sz, 5.8f, 3.1f);
                    float fraction = half / Length(d.curves[c]);
                    ArcByDistance(d, c, 0, fraction, "Horizontal wall end", true, false);
                    ArcByDistance(d, c, 1f - fraction, 1f, "Vertical wall end", false, true);
                }
            }
            else if (hz == 1660)
            {
                Arc(d, Trace(d, "East petal", t => Ellipse(7.2f, 0, 2.8f, 1.6f, t), false), .3f, .7f, "East inner petal");
                Arc(d, Trace(d, "West petal", t => Ellipse(-7.2f, 0, 2.8f, 1.6f, t), false), -.2f, .2f, "West inner petal");
                Arc(d, Trace(d, "North petal", t => Ellipse(0, 3.5f, 3.6f, 1.45f, t), false), 205f / 360, 335f / 360, "North inner petal");
                Arc(d, Trace(d, "South petal", t => Ellipse(0, -3.5f, 3.6f, 1.45f, t), false), 25f / 360, 155f / 360, "South inner petal");
                Trace(d, "Center ghost", t => Ellipse(0, 0, .4f, .4f, t), false);
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                    Arc(d, Corner(d, sx, sz, 7.5f, 3.5f), .3f, .7f, "Outer rounded middle");
            }
            else
            {
                for (int band = -4; band <= 4; band++)
                {
                    int b = band;
                    int c = Trace(d, "Channel " + band, t =>
                    {
                        float x = -14f + 28f * t;
                        float wave = (.16f + .14f * Mathf.Exp(-x * x / 35f)) * Mathf.Cos(x * .83f);
                        return new Vector2(x, b * 1.23f + Math.Sign(b) * wave);
                    }, true);
                    float[] ends = Mathf.Abs(band) == 2 ? new[] { -11.4f, -6.4f, -3.6f, 3.6f, 6.4f, 11.4f }
                        : Mathf.Abs(band) == 4 ? new[] { -14f, -11.4f, -6.4f, -3.6f, 3.6f, 6.4f, 11.4f, 14f } : new float[0];
                    for (int a = 0; a < ends.Length; a += 2)
                        Arc(d, c, (ends[a] + 14f) / 28f, (ends[a + 1] + 14f) / 28f, "Band " + band + " segment " + (a / 2 + 1), ends[a] == -14f, ends[a + 1] == 14f);
                }
            }
            d.revision++;
        }

        static Vector2 Ellipse(float x, float z, float rx, float rz, float t, float rotation = 0f)
        {
            float a = t * Mathf.PI * 2f, r = rotation * Mathf.Deg2Rad;
            float u = Mathf.Cos(a) * rx, v = Mathf.Sin(a) * rz;
            return new Vector2(x + u * Mathf.Cos(r) - v * Mathf.Sin(r), z + u * Mathf.Sin(r) + v * Mathf.Cos(r));
        }
        static int Corner(ResonancePatternDefinition d, int sx, int sz, float rx, float rz)
            => Trace(d, "Corner " + sx + "," + sz, t => new Vector2(sx * (14f + rx * Mathf.Cos((180f + 90f * t) * Mathf.Deg2Rad)),
                sz * (6f + rz * Mathf.Sin((180f + 90f * t) * Mathf.Deg2Rad))), true);

        static int Trace(ResonancePatternDefinition d, string name, Func<float, Vector2> sample, bool open)
        {
            var c = new ResonanceCurve { name = name, kind = open ? ResonanceCurveKind.OpenPolyline : ResonanceCurveKind.ClosedPolyline };
            for (int i = 0; i < Samples + (open ? 1 : 0); i++) c.points.Add(sample(i / (float)Samples));
            d.curves.Add(c); return d.curves.Count - 1;
        }
        public static float Length(ResonanceCurve c)
        {
            float length = 0f;
            int n = c.kind == ResonanceCurveKind.OpenPolyline ? c.points.Count - 1 : c.points.Count;
            for (int i = 0; i < n; i++) length += Vector2.Distance(c.points[i], c.points[(i + 1) % c.points.Count]);
            return length;
        }
        static float DistanceFraction(ResonanceCurve c, float parameter)
        {
            bool open = c.kind == ResonanceCurveKind.OpenPolyline;
            float loops = open ? 0f : Mathf.Floor(parameter);
            float t = open ? Mathf.Clamp01(parameter) : Mathf.Repeat(parameter, 1f);
            float point = t * Samples, length = 0f;
            for (int i = 0; i < Samples; i++)
                length += Vector2.Distance(c.points[i], c.points[(i + 1) % c.points.Count]) * Mathf.Clamp01(point - i);
            return loops + length / Mathf.Max(.00001f, Length(c));
        }
        static void Arc(ResonancePatternDefinition d, int c, float from, float to, string name, bool wallStart = false, bool wallEnd = false)
        {
            float a = DistanceFraction(d.curves[c], from), b = DistanceFraction(d.curves[c], to);
            var arc = ArcByDistance(d, c, a, b, name, wallStart, wallEnd);
            float trimStart = wallStart ? 0 : wallEnd ? .14f : .07f;
            float trimEnd = wallEnd ? 1 : wallStart ? .86f : .93f;
            arc.collisionStart = (DistanceFraction(d.curves[c], Mathf.Lerp(from, to, trimStart)) - a) / (b - a);
            arc.collisionEnd = (DistanceFraction(d.curves[c], Mathf.Lerp(from, to, trimEnd)) - a) / (b - a);
        }
        static ResonanceArc ArcByDistance(ResonancePatternDefinition d, int c, float from, float to, string name, bool wallStart = false, bool wallEnd = false)
        {
            var arc = new ResonanceArc {
                name = d.curves[c].name + " / " + name, curveIndex = c, startDegrees = from * 360f, sweepDegrees = (to - from) * 360f,
                thickness = .48f, colliderThickness = .15f, independentCollisionProfile = true,
                collisionStart = wallStart ? 0f : wallEnd ? .14f : .07f, collisionEnd = wallEnd ? 1f : wallStart ? .86f : .93f,
                endTaperFraction = .2f, collisionTaperFraction = .2f, untaperedStart = wallStart, untaperedEnd = wallEnd
            };
            d.arcs.Add(arc); return arc;
        }
    }
}
