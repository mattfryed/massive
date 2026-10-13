using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Settings;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public sealed partial class AmplifierCoreInteractionWindow : EditorWindow
    {
        private Vector2 scroll;
        [SerializeField] private bool stopping = true, movement = true, shot = true, feedback = true, interaction, appearance;
        [SerializeField] private bool controlText = true, contestRing = true;
        [SerializeField] private bool postContest = true;
        private static readonly HashSet<string> MovedPossessionFields = new()
        {
            "mode", "distanceDrivenShots", "shotSpeed", "minimumRange", "maximumRange", "rollingSpin",
            "settledSpeed", "settlingSeconds", "maximumFlightSeconds", "finalBrakeDrag", "reentryDelay", "fullChargeSeconds",
            "chargeVibration", "chargeVibrationAmount", "chargeVibrationFrequency", "chargeVibrationStart", "chargeVibrationRamp",
            "ejectionRipple", "ejectionRippleSeconds", "ejectionRippleAmplitude", "ejectionRippleWidth", "ejectionRippleFrequency",
            "showContestRing", "contestRingGap", "contestRingWidth", "contestRingOutline", "showRingHalfwayMark",
            "showControlText", "controlTextFont", "controlTextSize", "controlTextColor", "controlTextOffset", "controlTextLineSpacing",
            "showControlTimer", "aimingText", "enteringText", "contestedText", "winnerText", "lightTeamText", "darkTeamText",
            "postContestGraceSeconds", "postContestReleaseSeconds", "winnerAimSeconds"
        };

        [MenuItem("MASSIVE/Amplifier/Core Interaction")]
        public static void Open()
        {
            var window = GetWindow<AmplifierCoreInteractionWindow>("Core Interaction");
            window.minSize = new Vector2(600f, 700f);
            if (window.position.width < 900f) window.position = new Rect(window.position.x, window.position.y, 1080f, 820f);
            window.Show();
        }

        private void OnGUI()
        {
            var profile = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            if (!profile) { EditorGUILayout.HelpBox("Shared Amplifier settings are missing.", MessageType.Error); return; }
            EditorGUILayout.LabelField("Amplifier Core — shared across scenes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Changes save across scenes, including edits made in Play Mode. Cores using local settings keep their own values.", MessageType.Info);
            if (!profile.sharedEnabled) EditorGUILayout.HelpBox("Shared settings are currently disabled on this profile. Enable them in MASSIVE → Shared Settings or use the Core component's local controls.", MessageType.Warning);
            bool wide = position.width >= 900f;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (wide)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * .53f))) DrawPreview(profile, true);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    if (!wide) DrawPreview(profile, false);
                    DrawSettings(profile);
                }
            }
        }

        private void DrawSettings(AmplifierSharedProfile profile)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (var data = new SerializedObject(profile))
            {
                data.Update();
                Field(data, "core.possession.mode", "Interaction Mode");
                bool possession = data.FindProperty("core.possession.mode").enumValueIndex == (int)AmplifierInteractionMode.PossessionShot;
                bool distanceDriven = possession && data.FindProperty("core.possession.distanceDrivenShots").boolValue;

                postContest = EditorGUILayout.Foldout(postContest, "Post-contest input protection", true, EditorStyles.foldoutHeader);
                if (postContest)
                {
                    Field(data, "core.possession.postContestGraceSeconds", "Post-contest Grace (s)");
                    Field(data, "core.possession.postContestReleaseSeconds", "Button Release Time (s)");
                    Field(data, "core.possession.winnerAimSeconds", "Winner Aim Time (s)");
                    EditorGUILayout.HelpBox("Charging is blocked through ejection plus the grace period. Both buttons must stay released, then a fresh Attack press starts charging. Aiming works throughout; its countdown starts after fixed protection. Post-contest preview includes this handoff.", MessageType.None);
                }
                EditorGUILayout.Space();

                contestRing = EditorGUILayout.Foldout(contestRing, "Contest ring", true, EditorStyles.foldoutHeader);
                if (contestRing)
                {
                    Field(data, "core.possession.showContestRing", "Show Contest Ring");
                    using (new EditorGUI.DisabledScope(!data.FindProperty("core.possession.showContestRing").boolValue))
                    {
                        Field(data, "core.possession.contestRingGap", "Gap from Core");
                        Field(data, "core.possession.contestRingWidth", "Ring Thickness");
                        Field(data, "core.possession.contestRingOutline", "White Outline");
                        Field(data, "core.possession.showRingHalfwayMark", "Show 50/50 Tick");
                    }
                    EditorGUILayout.HelpBox("Light starts on the left, Dark on the right. The top is 50/50. Light gains clockwise; Dark gains counterclockwise. Dimensions scale with the Core radius.", MessageType.None);
                }
                EditorGUILayout.Space();
                controlText = EditorGUILayout.Foldout(controlText, "State text & prompts", true, EditorStyles.foldoutHeader);
                if (controlText)
                {
                    Field(data, "core.possession.showControlText", "Show State Text");
                    using (new EditorGUI.DisabledScope(!data.FindProperty("core.possession.showControlText").boolValue))
                    {
                        Field(data, "core.possession.controlTextFont", "Font (blank = project default)");
                        Field(data, "core.possession.controlTextSize", "Text Size");
                        Field(data, "core.possession.controlTextColor", "Text Color");
                        Field(data, "core.possession.controlTextOffset", "Offset / Gap Below");
                        Field(data, "core.possession.controlTextLineSpacing", "Line Spacing");
                        Field(data, "core.possession.showControlTimer", "Show Countdown");
                        Field(data, "core.possession.lightTeamText", "Light Team Label");
                        Field(data, "core.possession.darkTeamText", "Dark Team Label");
                        Field(data, "core.possession.enteringText", "Entering");
                        Field(data, "core.possession.aimingText", "Single Possession");
                        Field(data, "core.possession.contestedText", "Contested");
                        Field(data, "core.possession.winnerText", "Post-contest Winner");
                    }
                    EditorGUILayout.HelpBox("Preview and gameplay use the same text and ring renderer. Text scales with the Core. Offset X moves right; Y is the gap below the Core/ring. Use {team} and {time} in prompts; line breaks are supported.", MessageType.None);
                }
                EditorGUILayout.Space();

                stopping = EditorGUILayout.Foldout(stopping, "Stopping & re-entry", true, EditorStyles.foldoutHeader);
                if (stopping)
                {
                    using (new EditorGUI.DisabledScope(!possession))
                    {
                        Field(data, "core.possession.settledSpeed", "Stop Speed");
                        Field(data, "core.possession.settlingSeconds", "Settling Time (s)");
                        Field(data, "core.possession.reentryDelay", "Re-entry Delay (s)");
                        Field(data, "core.possession.maximumFlightSeconds", "Brake After (s)");
                        Field(data, "core.possession.finalBrakeDrag", "Brake Drag");
                    }
                    EditorGUILayout.HelpBox(possession
                        ? "For less slow drift, raise Stop Speed. The Core rests after staying below that speed for Settling Time, then allows entry after Re-entry Delay. Lower Brake After to start the final slowdown sooner; raise Brake Drag to make it faster."
                        : "Stopping and shot controls apply to Possession Shot mode. Movement & physics below also applies to the original Physical mode.", MessageType.None);
                }

                EditorGUILayout.Space();
                movement = EditorGUILayout.Foldout(movement, "Movement & physics", true, EditorStyles.foldoutHeader);
                if (movement)
                {
                    Field(data, "core.coreScale", "Core Scale");
                    Field(data, "core.mass", "Mass");
                    using (new EditorGUI.DisabledScope(distanceDriven)) Field(data, "core.linearDrag", "Coasting Drag");
                    if (distanceDriven) EditorGUILayout.LabelField("Shot drag is calculated from the travel distances below.", EditorStyles.wordWrappedMiniLabel);
                    Field(data, "core.angularDrag", "Spin Drag");
                    Field(data, "core.maximumPlanarSpeed", "Maximum Speed");
                    Field(data, "core.wallRestitution", "Wall / Resonance Bounce");
                }

                EditorGUILayout.Space();
                shot = EditorGUILayout.Foldout(shot, "Shot travel", true, EditorStyles.foldoutHeader);
                if (shot)
                {
                    using (new EditorGUI.DisabledScope(!possession))
                    {
                        Field(data, "core.possession.distanceDrivenShots", "Drive Shots by Distance");
                        Field(data, "core.possession.fullChargeSeconds", "Full Charge / Auto-fire (s)");
                        Field(data, "core.possession.shotSpeed", "Launch Speed");
                        Field(data, "core.possession.minimumRange", "Zero Charge Distance (units)");
                        Field(data, "core.possession.maximumRange", "Full Charge Distance (units)");
                        Field(data, "core.possession.rollingSpin", "Rolling Spin");
                    }
                    EditorGUILayout.HelpBox(distanceDriven
                        ? "Distances target unobstructed travel, including Stop Speed and Settling Time. Half charge travels halfway between the two distances. Drag is calculated at launch; the Core still coasts, spins and bounces through physics. Collisions, fields or edits during flight change the result."
                        : "Charge adds drag for shorter shots; Coasting Drag is the minimum and can limit the full-charge distance. Enable Drive Shots by Distance to set travel directly. Launch settings affect the next shot; drag and stopping controls also affect a shot already moving.", MessageType.None);
                    if (distanceDriven) DrawDistanceLimits(profile);
                }

                EditorGUILayout.Space();
                feedback = EditorGUILayout.Foldout(feedback, "Charge vibration & ejection ripple", true, EditorStyles.foldoutHeader);
                if (feedback)
                {
                    Field(data, "core.possession.chargeVibration", "Charge Vibration");
                    using (new EditorGUI.DisabledScope(!data.FindProperty("core.possession.chargeVibration").boolValue))
                    {
                        Field(data, "core.possession.chargeVibrationAmount", "Vibration Amount");
                        Field(data, "core.possession.chargeVibrationFrequency", "Vibration Frequency (Hz)");
                        Field(data, "core.possession.chargeVibrationStart", "Begin at Charge (0–1)");
                        Field(data, "core.possession.chargeVibrationRamp", "Vibration Ramp");
                    }
                    Field(data, "core.possession.ejectionRipple", "Ejection Ripple");
                    using (new EditorGUI.DisabledScope(!data.FindProperty("core.possession.ejectionRipple").boolValue))
                    {
                        Field(data, "core.possession.ejectionRippleSeconds", "Ripple Duration (s)");
                        Field(data, "core.possession.ejectionRippleAmplitude", "Ripple Amplitude");
                        Field(data, "core.possession.ejectionRippleWidth", "Ripple Packet Width");
                        Field(data, "core.possession.ejectionRippleFrequency", "Ripple Waves");
                    }
                    EditorGUILayout.HelpBox("Vibration grows with charge without moving the collider. Ejection sends a damped ripple across the Core surface. Preview these in Single possession, Post-contest, or Ejected; Replay state restarts the event.", MessageType.None);
                }
                EditorGUILayout.Space();
                interaction = EditorGUILayout.Foldout(interaction, "Entry, charge, contest & presentation", true, EditorStyles.foldoutHeader);
                if (interaction)
                {
                    EditorGUILayout.HelpBox("Thrust to enter; hold Attack to charge. Release for a shorter shot or keep holding to auto-fire at full charge. Opposing Thrust starts an Attack / Shield alternation contest. The white / black ring appears only during a contest.", MessageType.None);
                    var field = data.FindProperty("core.possession");
                    var end = field.GetEndProperty();
                    field.NextVisible(true);
                    do
                    {
                        if (!MovedPossessionFields.Contains(field.name)) EditorGUILayout.PropertyField(field, true);
                    } while (field.NextVisible(false) && !SerializedProperty.EqualContents(field, end));
                }
                EditorGUILayout.Space();
                appearance = EditorGUILayout.Foldout(appearance, "Core appearance & lifecycle", true, EditorStyles.foldoutHeader);
                if (appearance)
                {
                    var field = data.FindProperty("coreSurface"); var end = field.GetEndProperty();
                    field.NextVisible(true);
                    do { EditorGUILayout.PropertyField(field, true); }
                    while (field.NextVisible(false) && !SerializedProperty.EqualContents(field, end));
                    Field(data, "core.spawnScaleSeconds", "Spawn Growth (s)");
                    Field(data, "core.spawnShellMorphSeconds", "Shell Formation (s)");
                    Field(data, "core.spawnCoreDelaySeconds", "Energy Reveal Delay (s)");
                    Field(data, "core.spawnCoreRevealSeconds", "Energy Reveal (s)");
                    Field(data, "core.despawnScaleSeconds", "Despawn Shrink (s)");
                    Field(data, "core.despawnShellMorphSeconds", "Shell Dissolve (s)");
                    Field(data, "core.despawnCoreHideSeconds", "Energy Hide (s)");
                }
                SharedSettingsEditing.Apply(data);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Field(SerializedObject data, string path, string label)
        {
            var property = data.FindProperty(path);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        }

        private static void DrawDistanceLimits(AmplifierSharedProfile profile)
        {
            var settings = profile.core.possession;
            float speed = AmplifierCoreMotion.LimitSpeed(Vector3.right * Mathf.Max(.1f, settings.shotSpeed), profile.core.maximumPlanarSpeed).magnitude;
            if (speed <= Mathf.Max(.01f, settings.settledSpeed))
                EditorGUILayout.HelpBox("Launch Speed (after Maximum Speed) is already below Stop Speed. Lower Stop Speed or raise the speed limit to allow a normal shot.", MessageType.Warning);
            float reachable = AmplifierCoreMotion.PredictTravel(speed, 0f, Time.fixedDeltaTime, settings, out _);
            if (Mathf.Max(settings.minimumRange, settings.maximumRange) > reachable + .1f)
                EditorGUILayout.HelpBox($"Current speed and braking allow at most about {reachable:0.0} units. Increase Launch Speed / Maximum Speed or Brake After to reach the requested distance.", MessageType.Warning);
        }
    }
}
