// Assets/VectorGridNu/Editor/CreateDefaultGridProfiles.cs
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateDefaultGridProfiles
{
    [MenuItem("MASSIVE/Create Default Grid Profiles")]
    public static void CreateDefaults()
    {
        // Choose destination under Assets
        var folder = EditorUtility.OpenFolderPanel("Choose folder for default profiles", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;
        if (!folder.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("Invalid Folder", "Pick a folder under Assets/.", "OK");
            return;
        }
        string rel = "Assets" + folder.Substring(Application.dataPath.Length);

        // Helpers -------------- //
        GridInteractionModule Mod(
            GridModuleType type, string tag,
            float radius, float strength,
            float innerFrac = 0f,
            bool directional = false,
            bool useVelocity = false,
            bool pullAgainstVel = true)
        {
            return new GridInteractionModule
            {
                tag = tag,
                type = type,
                radius = radius,
                strength = strength,
                innerFrac = Mathf.Clamp(innerFrac, 0f, 0.9f),
                directional = directional,
                useVelocity = useVelocity,
                pullAgainstVelocity = pullAgainstVel,
                fixedDirection = Vector3.right,
                scaleBySpeed = true,
                radiusOverSpeed = AnimationCurve.Linear(0, 1.0f, 10f, 1.2f),
                strengthOverSpeed = AnimationCurve.Linear(0, 0.7f, 10f, 1.2f),
                spinDegPerSec = 180f,
                noiseAmplitude = 0.35f,
                noiseFrequency = 8f,
                duration = 0.18f,
                loopPulse = false,
                envelope = AnimationCurve.EaseInOut(0, 1, 1, 0),
                waveSpeed = 8f,
                waveThickness = 0.7f
            };
        }

        void SaveProfile(string name, System.Action<GridInteractionProfile> init)
        {
            var p = ScriptableObject.CreateInstance<GridInteractionProfile>();
            p.strengthMultiplier = 1f;
            p.radiusMultiplier = 1f;
            init(p);
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(rel, name + ".asset"));
            AssetDatabase.CreateAsset(p, path);
        }

        // Profiles -------------- //

        // 1) Player: directional wake + dash burst + score pulse
        SaveProfile("Player", p =>
        {
            var wake  = Mod(GridModuleType.DirectionalWake, "Wake", 3.0f, 10f);
            wake.useVelocity = true; wake.pullAgainstVelocity = true;

            var dash  = Mod(GridModuleType.BurstRadial, "Dash", 4.5f, 18f);
            dash.scaleBySpeed = false; dash.duration = 0.18f; dash.envelope = AnimationCurve.EaseInOut(0,1,1,0);

            var score = Mod(GridModuleType.Pulse, "Score", 4.5f, 14f);
            score.scaleBySpeed = false; score.duration = 0.5f; score.loopPulse = false;

            p.modules = new[] { wake, dash, score };
        });

        // 2) Celestial Body: steady radial gravity
        SaveProfile("Celestial Body", p =>
        {
            var grav = Mod(GridModuleType.ConstantRadial, "Gravity", 6.0f, 20f, innerFrac: 0.15f);
            grav.scaleBySpeed = false;
            p.modules = new[] { grav };
        });

        // 3) Vortex (anomaly): spinning directional field
        SaveProfile("Vortex (Anomaly)", p =>
        {
            var vox = Mod(GridModuleType.Vortex, "Vortex", 3.5f, 10f);
            vox.scaleBySpeed = false; vox.spinDegPerSec = 180f; vox.directional = true;
            p.modules = new[] { vox };
        });

        // 4) Jiggle (shimmer): local noise jitter
        SaveProfile("Jiggle (Shimmer)", p =>
        {
            var jig = Mod(GridModuleType.Jiggle, "Jiggle", 2.5f, 6f);
            jig.noiseAmplitude = 0.35f; jig.noiseFrequency = 8f;
            p.modules = new[] { jig };
        });

        // 5) Traveling Wave (anomaly): expanding ring band
        SaveProfile("Traveling Wave", p =>
        {
            var wave = Mod(GridModuleType.TravelingWave, "Wave", 1.0f, 12f);
            wave.scaleBySpeed = false;
            wave.waveSpeed = 8f; wave.waveThickness = 0.7f;
            p.modules = new[] { wave };
        });

        // 6) Deposit Pulse (utility): one-shot shockwave
        SaveProfile("Pulse (Deposit)", p =>
        {
            var pulse = Mod(GridModuleType.BurstRadial, "Deposit", 4.5f, 18f);
            pulse.scaleBySpeed = false; pulse.duration = 0.18f; pulse.envelope = AnimationCurve.EaseInOut(0,1,1,0);
            p.modules = new[] { pulse };
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Profiles Created", $"Default multi-module profiles were created in:\n{rel}", "Nice");
    }
}
