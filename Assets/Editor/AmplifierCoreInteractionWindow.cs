using Massive.Settings;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public sealed class AmplifierCoreInteractionWindow : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("MASSIVE/Amplifier/Core Interaction")]
        public static void Open() => GetWindow<AmplifierCoreInteractionWindow>("Core Interaction");

        private void OnGUI()
        {
            var profile = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            if (!profile) { EditorGUILayout.HelpBox("Shared Amplifier settings are missing.", MessageType.Error); return; }
            EditorGUILayout.LabelField("Amplifier Core — shared across scenes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Possession Shot: Thrust into the Core, aim with the stick, hold Attack to charge distance, then release to shoot. An opposing Thrust starts an Attack / Shield alternation contest. Physical restores the original pushable Core.", MessageType.Info);
            EditorGUILayout.HelpBox("Hold Attack to grow the aim dots in sequence, from the Core to the far end. Charged Dot Scale sets their full size; Dot Charge Spread controls how smoothly growth travels along the line. The white-left / black-right bar appears only during a contest. Marker Clearance and Shell Gap Blending shape the openings around the player indicators.", MessageType.None);
            if (!profile.sharedEnabled) EditorGUILayout.HelpBox("Shared settings are currently disabled on this profile. Enable them in MASSIVE → Shared Settings or use the Core component's local controls.", MessageType.Warning);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (var data = new SerializedObject(profile)) SharedSettingsEditing.DrawGroup(data, "core.possession");
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("Shots coast using Core physics: charge changes drag and approximate distance, not damage or initial speed. The dotted line shows aim only. Tune body drag, mass and wall bounce in MASSIVE → Shared Settings → Amplifier → Core. Player marker and SINGULARITY entry/exit controls are above.", MessageType.None);
        }
    }
}
