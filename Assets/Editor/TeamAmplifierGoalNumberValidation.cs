using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Scoring;
using Massive.Settings;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    public static class TeamAmplifierGoalNumberValidation
    {
        // Runs against existing scene bindings; changes only nonserialized preview
        // state and uses a disposable camera. Never changes scores or saves a scene.
        public static string Run(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in edit mode.");
            var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
            var source = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            string before = JsonUtility.ToJson(source);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            var presenters = Object.FindObjectsByType<TeamAmplifierPresenter>(FindObjectsSortMode.None)
                .Where(p => p.gameObject.scene == scene).ToArray();
            Check(presenters.Length == 2, "Both team presenters present");
            Directory.CreateDirectory(output);
            try
            {
                foreach (var presenter in presenters)
                {
                    presenter.PreviewGoalMultiplier(1);
                    Check(!presenter.GoalNumberText || !presenter.GoalNumberText.gameObject.activeInHierarchy, "Team " + presenter.TeamID + " hidden at ×1");
                    float previousHeight = 0;
                    foreach (int level in new[] { 2, 4, 8 })
                    {
                        presenter.PreviewGoalMultiplier(level);
                        var text = presenter.GoalNumberText;
                        Check(text && text.gameObject.activeInHierarchy && text.text == source.goalNumber.prefix + level, "Team " + presenter.TeamID + " displays ×" + level);
                        var core = presenter.GoalBadgeCore;
                        var disc = presenter.GoalBadgeDisc;
                        Check(core && disc && core.gameObject.activeInHierarchy && disc.gameObject.activeInHierarchy, "Rainbow core and white disc accompany the number");
                        Check(core.sharedMaterial.renderQueue < disc.sharedMaterial.renderQueue && disc.sharedMaterial.renderQueue < text.fontSharedMaterial.renderQueue,
                            "Badge renders rainbow core, white disc, then number");
                        Check(core.sharedMaterial.GetVector("_GoalOrigin") == text.fontSharedMaterial.GetVector("_GoalOrigin") &&
                            disc.sharedMaterial.GetVector("_GoalShape") == text.fontSharedMaterial.GetVector("_GoalShape"), "All three layers use the same anchored goal mask");
                        Check(core.sharedMaterial.GetFloat("_GoalClipEnabled") == 1 && core.sharedMaterial.GetFloat("_ZWrite") == 0,
                            "Badge core clips without writing over gameplay depth");
                        Check(!core.GetComponent<Collider>() && !core.GetComponentInParent<Massive.Multiplier.AmplifierCoreGameplay>(), "Badge is visual-only");
                        Check(text.font == (source.goalNumber.font ? source.goalNumber.font : text.font), "Shared font resolves");
                        float height = presenter.GoalNumberGlyphBounds.size.y * text.transform.lossyScale.y;
                        Check(height < disc.transform.lossyScale.y && disc.transform.lossyScale.y < core.transform.lossyScale.y,
                            "Small number fits inside the smaller white disc over the rainbow core");
                        Check(height > previousHeight, "Size grows at ×" + level); previousHeight = height;
                        var material = text.fontSharedMaterial;
                        Check(material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "Goal number shader supported and compiled");
                        Check(material.renderQueue > 2500 && material.renderQueue < 3000, "Number draws after score voids and before border strokes");
                        Vector3 center = text.transform.TransformPoint(presenter.GoalNumberGlyphBounds.center);
                        Vector3 origin = material.GetVector("_GoalOrigin");
                        var right = material.GetVector("_GoalRight");
                        if (source.goalNumber.outwardOffset == 0 && source.goalNumber.verticalOffset == 0)
                        {
                            float offset = source.goalNumber.NumberHorizontalOffsetFor(level, presenter.TeamID);
                            float diameter = material.GetVector("_GoalShape").y * 2 * source.goalNumber.ScaleFor(level);
                            Check(Vector3.Distance(center, origin + (Vector3)right * (diameter * offset)) < .002f, "Number respects its independent horizontal offset");
                        }
                        Check(right.w == (presenter.TeamID == 1 ? -1 : 1), "Mask keeps the appropriate team's outside half");
                    }
                    Check(presenter.GetComponentsInChildren<Renderer>(true).Where(r => (r.gameObject.hideFlags & HideFlags.DontSave) == 0).All(r => r.forceRenderingOff), "Legacy badge renderers suppressed");
                }
                foreach (int level in new[] { 2, 4, 8 })
                {
                    foreach (var presenter in presenters) presenter.PreviewGoalMultiplier(level);
                    Capture(output + "/goal-x" + level + ".png");
                }
                Check(JsonUtility.ToJson(source) == before, "Preview does not change shared settings");
                Check(scene.isDirty == dirty, "Preview leaves scene dirty state unchanged");
            }
            finally { foreach (var presenter in presenters) if (presenter) presenter.PreviewGoalMultiplier(0); }
            Check(presenters.All(p => !p.GoalNumberText || !p.GoalNumberText.gameObject.activeInHierarchy), "Stop preview hides both goal numbers");
            Check(presenters.All(p => !p.GoalBadgeCore.gameObject.activeSelf && !p.GoalBadgeDisc.gameObject.activeSelf), "Stop preview hides both supporting layers");
            string report = "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks);
            File.WriteAllText(output + "/validation.txt", report);
            return report;
        }

        public static void Capture(string path)
        {
            var go = new GameObject("Goal number verification camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            if (Camera.main) camera.CopyFrom(Camera.main);
            camera.transform.SetPositionAndRotation(new Vector3(0, 60, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true; camera.orthographicSize = 10;
            var rt = new RenderTexture(1900, 900, 24) { antiAliasing = 4 };
            var old = RenderTexture.active;
            var image = new Texture2D(1900, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1900, 900), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; camera.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }

        public static string RunTransitions(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in edit mode.");
            var source = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            string before = JsonUtility.ToJson(source);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
            bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;
            var presenters = Object.FindObjectsByType<TeamAmplifierPresenter>(FindObjectsSortMode.None);
            var scorePreset = SharedSettingsRuntime.Load<TextAnimationSharedProfile>().scoreDigitMorphPreset;
            Directory.CreateDirectory(output);
            try
            {
                foreach (var p in presenters)
                {
                    var s = p.GoalNumberSettings;
                    Check(s.animateChanges && s.growthSeconds > 0 && s.firstTextGrowSeconds > 0, "Animation timing enabled");
                    p.PreviewGoalMultiplier(2);
                    float whole = p.GoalBadgeCore.transform.lossyScale.x;
                    float circle = p.GoalBadgeDisc.transform.lossyScale.x;
                    float baseDilate = p.GoalNumberText.fontSharedMaterial.GetFloat("_FaceDilate");
                    p.PreviewGoalTransition(1, 2);
                    Check(Near(p.GoalBadgeCore.transform.lossyScale.x, 0) && Near(p.GoalBadgeDisc.transform.lossyScale.x, 0), "First core and circle start at zero");
                    p.SampleGoalTransition(s.growthSeconds * .5f);
                    Check(p.GoalBadgeCore.transform.lossyScale.x > 0 && p.GoalBadgeCore.transform.lossyScale.x <= whole + .002f, "Core grows toward authored size");
                    Check(p.GoalNumberText.GetComponent<Renderer>().forceRenderingOff, "Text remains absent while badge grows");
                    p.SampleGoalTransition(s.growthSeconds);
                    Check(Near(p.GoalBadgeCore.transform.lossyScale.x, whole) && Near(p.GoalBadgeDisc.transform.lossyScale.x, circle), "Both layers finish before text reveal");
                    float empty = p.GoalNumberText.fontSharedMaterial.GetFloat("_FaceDilate");
                    Check(empty < -1, "SDF reveal begins with zero visible stroke thickness");
                    p.SampleGoalTransition(s.growthSeconds + s.firstTextGrowSeconds * .5f);
                    float growing = p.GoalNumberText.fontSharedMaterial.GetFloat("_FaceDilate");
                    Check(growing > empty && growing < baseDilate, "SDF thickness grows toward the font default");
                    p.SampleGoalTransition(s.growthSeconds + s.firstTextGrowSeconds + .05f);
                    Check(!p.GoalTransitionPlaying && Near(p.GoalNumberText.fontSharedMaterial.GetFloat("_FaceDilate"), baseDilate), "Reveal settles at the unmodified font thickness");

                    foreach (int from in new[] { 2, 4 })
                    {
                        int to = from * 2;
                        p.PreviewGoalMultiplier(from);
                        float startWhole = p.GoalBadgeCore.transform.lossyScale.x;
                        float startCircle = p.GoalBadgeDisc.transform.lossyScale.x;
                        p.PreviewGoalMultiplier(to);
                        float endWhole = p.GoalBadgeCore.transform.lossyScale.x;
                        float endCircle = p.GoalBadgeDisc.transform.lossyScale.x;
                        p.PreviewGoalTransition(from, to);
                        Check(Near(p.GoalBadgeCore.transform.lossyScale.x, startWhole) && Near(p.GoalBadgeDisc.transform.lossyScale.x, startCircle), "Next tier starts from the previous tier's size");
                        Check(p.GoalTextAnimator.CurrentPreset == scorePreset, "Exact shared team-score morph preset reused");
                        p.SampleGoalTransition(scorePreset.durationSeconds * .25f);
                        var morphs = p.GoalNumberText.transform.parent.GetComponentsInChildren<MeshRenderer>()
                            .Where(r => r.sharedMaterial && r.sharedMaterial.HasProperty("_Morph")).ToArray();
                        Check(morphs.Length > 0, "Changing digit uses the SDF morph renderer");
                        Check(morphs.All(r => r.sharedMaterial.GetFloat("_GoalClipEnabled") == 1 && r.sharedMaterial.renderQueue == 2992 &&
                            r.sharedMaterial.GetVector("_GoalShape") == p.GoalNumberText.fontSharedMaterial.GetVector("_GoalShape")), "Morph glyphs retain goal clipping and layer order");
                        p.SampleGoalTransition(Mathf.Max(s.growthSeconds, scorePreset.EstimateTotalSeconds(8)) + .1f);
                        Check(!p.GoalTransitionPlaying && Near(p.GoalBadgeCore.transform.lossyScale.x, endWhole) && Near(p.GoalBadgeDisc.transform.lossyScale.x, endCircle), "Morph and scale settle at the next authored level");
                    }
                    p.PreviewGoalTransition(2, 4);
                    p.SampleGoalTransition(s.growthSeconds * .2f);
                    float interruptedSize = p.GoalBadgeCore.transform.lossyScale.x;
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    typeof(TeamAmplifierPresenter).GetMethod("BeginGoalTransition", flags).Invoke(p, new object[] { 4, 8 });
                    typeof(TeamAmplifierPresenter).GetField("previewMultiplier", flags).SetValue(p, 8);
                    p.RefreshGoalNumber();
                    Check(Near(p.GoalBadgeCore.transform.lossyScale.x, interruptedSize), "Rapid captures continue from the currently rendered size");
                    p.PreviewGoalMultiplier(0);
                    Check(!p.GoalTransitionPlaying && !p.GoalBadgeCore.gameObject.activeSelf && !p.GoalNumberText.gameObject.activeInHierarchy,
                        "Stopping preview cancels the transition and hides every layer");
                }
                Check(JsonUtility.ToJson(source) == before && scene.isDirty == dirty, "Animation previews preserve authored settings and scene state");
            }
            finally { foreach (var p in presenters) if (p) p.PreviewGoalMultiplier(0); }
            string report = "PASSED (" + checks.Count + " transition checks)\n" + string.Join("\n", checks);
            File.WriteAllText(output + "/transition-validation.txt", report);
            return report;
        }

        // Deliberately changes only the disposable Play Mode match. Call after
        // presenters have bound, then exit Play Mode to restore authoring state.
        public static string RunRuntime()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in a temporary Play Mode session.");
            var service = MatchScoreService.Instance;
            if (!service) throw new InvalidOperationException("No match score service.");
            var presenters = Object.FindObjectsByType<TeamAmplifierPresenter>(FindObjectsSortMode.None);
            var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
            service.ResetForMatch(true);
            foreach (var p in presenters)
            {
                Check(p.DisplayedMultiplier == 1 && (!p.GoalNumberText || !p.GoalNumberText.gameObject.activeInHierarchy), "Team " + p.TeamID + " starts hidden");
                for (int i = 0; i < service.Profile.TeamAmplifierSettings.MaxIndex; i++)
                {
                    Check(service.AdvanceTeamAmplifier(p.TeamID), "Amplifier scoring event accepted");
                    Check(p.DisplayedMultiplier == service.GetTeamAmplifierMultiplier(p.TeamID) && p.GoalNumberText && p.GoalNumberText.gameObject.activeInHierarchy,
                        "Scoring event updates visible goal number for team " + p.TeamID);
                    Check(p.GoalBadgeCore && p.GoalBadgeCore.gameObject.activeInHierarchy && p.GoalBadgeDisc && p.GoalBadgeDisc.gameObject.activeInHierarchy,
                        "Scoring event reveals complete layered badge");
                    Check(!p.GoalNumberSettings.animateChanges || p.GoalTransitionPlaying, "Scoring event starts the badge transition");
                }
                int before = p.DisplayedMultiplier;
                service.AdvanceTeamAmplifier(p.TeamID);
                Check(p.DisplayedMultiplier == before, "Capped capture leaves number unchanged");
            }
            service.ResetForMatch(false);
            Check(presenters.All(p => p.DisplayedMultiplier == 1 && !p.GoalNumberText.gameObject.activeInHierarchy), "Match reset hides both team goal numbers");
            Check(presenters.All(p => !p.GoalBadgeCore.gameObject.activeSelf && !p.GoalBadgeDisc.gameObject.activeSelf), "Match reset hides all badge layers");
            return "PASSED (" + checks.Count + " runtime checks)\n" + string.Join("\n", checks);
        }
    }
}
