using System;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    public sealed partial class PlayerTuningWindow : EditorWindow
    {
        [SerializeField] private int tab, selectedStage, playerSlot;
        [SerializeField] private bool frames, loop = true, selectedOnly, sceneVfx, sceneOverlay;
        [SerializeField] private float playbackRate = 1;
        [SerializeField] private PlayerMeleePlasma previewPlayer;
        [SerializeField] private PlayerTuningSnapshot snapshot;
        private PlayerTuningSnapshot comparisonA, comparisonB;
        private PlayerTuningProfile tuning;
        private MeleeVisualProfile visuals;
        private Vector2 scroll;
        private float cursor;
        private bool playing;
        private double lastTick;
        private readonly PlayerMeleePlasmaPreviewGUI.GameplayBindings visualBindings = new PlayerMeleePlasmaPreviewGUI.GameplayBindings();
        private static readonly string[] Tabs = { "Attack timeline", "Movement", "Size & reach", "Impact & recovery", "Appearance" };
        private static readonly string[] StageNames = { "Thrust", "Sweep", "Repulsor" };

        [MenuItem("MASSIVE/Player Tuning", false, 5)]
        [MenuItem("MASSIVE/Player/Player Tuning", false, 0)]
        public static void Open()
        {
            var window = GetWindow<PlayerTuningWindow>("Player Tuning");
            window.minSize = new Vector2(650, 640);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(650, 640);
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.hierarchyChanged += FindPreviewPlayer;
            Undo.undoRedoPerformed += Repaint;
            SceneView.duringSceneGui += DrawSceneOverlay;
            FindPreviewPlayer();
        }
        private void OnDisable()
        {
            Stop();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.hierarchyChanged -= FindPreviewPlayer;
            Undo.undoRedoPerformed -= Repaint;
            SceneView.duringSceneGui -= DrawSceneOverlay;
            visualBindings.Dispose();
            if (comparisonA) comparisonA.ReleaseTemporary();
            if (comparisonB) comparisonB.ReleaseTemporary();
        }
        private void PlayModeChanged(PlayModeStateChange state) { Stop(); FindPreviewPlayer(); }
        private void FindPreviewPlayer()
        {
            if (previewPlayer && previewPlayer.gameObject.scene.IsValid()) return;
            foreach (var p in Object.FindObjectsByType<PlayerMeleePlasma>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
            {
                var owner = p.GetComponent<PlayerControllerScript>();
                if (!owner || owner.IsPseudoPlayer || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(p.gameObject)) continue;
                if (!previewPlayer || owner.playerID == 0) previewPlayer = p;
                if (owner.playerID == 0) break;
            }
        }
        private void Stop()
        {
            playing = false; cursor = 0;
            if (previewPlayer && !Application.isPlaying) previewPlayer.StopPreview();
            Repaint();
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(.1f, (float)(now - lastTick)); lastTick = now;
            if (!playing || !tuning || !tuning.attackProfile || EditorApplication.isCompiling) return;
            cursor += dt * playbackRate;
            float end = PreviewLength();
            if (cursor > end + .2f)
            {
                if (loop) cursor = 0;
                else { cursor = end; playing = false; }
            }
            SampleScene(); Repaint();
        }
        private void SampleScene()
        {
            if (!sceneVfx || !selectedOnly || !previewPlayer || Application.isPlaying) return;
            var controller = previewPlayer.GetComponent<PlayerAttackController>();
            if (!controller || controller.Profile != tuning.attackProfile) return;
            previewPlayer.ScrubPreviewSeconds(selectedStage, cursor);
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
        private float PreviewLength()
        {
            if (!tuning || !tuning.attackProfile) return 1;
            if (selectedOnly)
            {
                var s = tuning.attackProfile.GetStage(selectedStage);
                return s == null ? 1 : Mathf.Max(s.Duration, PlayerTuningEditing.TailStart(s) + PlayerTuningEditing.Tail(s, visuals));
            }
            float start = 0, end = 0;
            for (int i = 0; i < tuning.attackProfile.Stages.Count; i++)
            {
                var s = tuning.attackProfile.GetStage(i);
                float tailStart = PlayerTuningEditing.TailStart(s);
                if (s.AllowComboCancel && s.EarlyComboHandoff && i + 1 < tuning.attackProfile.Stages.Count)
                    tailStart = Mathf.Min(tailStart, Mathf.Max(s.ComboHandoffSeconds, PlayerTuningEditing.Window(s, tuning).x));
                end = Mathf.Max(end, start + Mathf.Max(s.Duration, tailStart + PlayerTuningEditing.Tail(s, visuals)));
                start += s.AllowComboCancel ? Mathf.Max(s.ComboHandoffSeconds, PlayerTuningEditing.Window(s, tuning).x) : s.Duration;
            }
            return Mathf.Max(.1f, end);
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = Mathf.Min(270, position.width * .36f);
            tuning = PlayerTuningEditing.GetOrCreate();
            visuals = SharedSettingsRuntime.Load<MeleeVisualProfile>();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("PLAYER TUNING", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Application.isPlaying ? "LIVE GAME • edits save globally" : "EDIT MODE • project settings", EditorStyles.miniLabel);
                if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) Undo.PerformUndo();
                if (GUILayout.Button("Redo", EditorStyles.toolbarButton)) Undo.PerformRedo();
            }
            DrawSnapshotToolbar();
            using (var data = new SerializedObject(tuning))
            {
                data.Update();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(data.FindProperty("sharedEnabled"), new GUIContent("Shared player tuning"));
                    EditorGUILayout.PropertyField(data.FindProperty("attackProfile"), new GUIContent("Attack profile"));
                }
                if (data.ApplyModifiedProperties()) PlayerTuningEditing.Save(tuning);
            }
            if (!tuning.sharedEnabled) EditorGUILayout.HelpBox("Shared movement and combat are OFF. Players use local settings. The attack and appearance assets below are still shared by any components that reference them.", MessageType.Warning);
            tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26));
            DrawTransport();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) { DrawTimeline(); DrawStageFields(); DrawSpatialPreview(); }
            else if (tab == 1) DrawMovement();
            else if (tab == 2) { PlayerGlobalModifiersEditing.Draw(true); DrawSpatialPreview(); DrawSceneControls(); }
            else if (tab == 3) DrawImpact();
            else DrawAppearance();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSnapshotToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Store A")) { if (comparisonA) comparisonA.ReleaseTemporary(); comparisonA = PlayerTuningSnapshot.Capture(); }
                using (new EditorGUI.DisabledScope(!comparisonA)) if (GUILayout.Button("Apply A")) { Stop(); comparisonA.Apply(); }
                if (GUILayout.Button("Store B")) { if (comparisonB) comparisonB.ReleaseTemporary(); comparisonB = PlayerTuningSnapshot.Capture(); }
                using (new EditorGUI.DisabledScope(!comparisonB)) if (GUILayout.Button("Apply B")) { Stop(); comparisonB.Apply(); }
                if (GUILayout.Button("Save snapshot…"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save player tuning snapshot", "Player tuning", "asset", "Stores attacks, movement, size, reversal and melee appearance.", "Assets/Editor");
                    if (!string.IsNullOrEmpty(path)) PlayerTuningSnapshot.Capture().SaveAsset(path);
                }
                snapshot = (PlayerTuningSnapshot)EditorGUILayout.ObjectField(snapshot, typeof(PlayerTuningSnapshot), false, GUILayout.MinWidth(70));
                using (new EditorGUI.DisabledScope(!snapshot)) if (GUILayout.Button("Apply", GUILayout.Width(48))) { Stop(); snapshot.Apply(); }
            }
        }
        private void DrawTransport()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? "Pause" : "Play preview", GUILayout.Width(92))) { playing = !playing; if (cursor >= PreviewLength()) cursor = 0; }
                    if (GUILayout.Button("Stop", GUILayout.Width(50))) Stop();
                    loop = GUILayout.Toggle(loop, "Loop", GUILayout.Width(50));
                    EditorGUI.BeginChangeCheck();
                    selectedOnly = GUILayout.Toggle(selectedOnly, "Selected stage", GUILayout.Width(110));
                    if (EditorGUI.EndChangeCheck()) { Stop(); }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Preview speed", GUILayout.Width(88));
                    playbackRate = EditorGUILayout.Slider(playbackRate, .1f, 2f, GUILayout.MinWidth(115));
                }
                EditorGUI.BeginChangeCheck();
                cursor = EditorGUILayout.Slider("Scrub (seconds)", cursor, 0, PreviewLength());
                if (EditorGUI.EndChangeCheck()) { playing = false; SampleScene(); SceneView.RepaintAll(); }
                using (new EditorGUILayout.HorizontalScope())
                {
                    var chosen = (PlayerMeleePlasma)EditorGUILayout.ObjectField("Scene preview player", previewPlayer, typeof(PlayerMeleePlasma), true);
                    if (chosen != previewPlayer) { Stop(); previewPlayer = chosen; }
                    using (new EditorGUI.DisabledScope(!previewPlayer || Application.isPlaying || !selectedOnly))
                    {
                        EditorGUI.BeginChangeCheck();
                        sceneVfx = GUILayout.Toggle(sceneVfx, "Show stage VFX", GUILayout.Width(120));
                        if (EditorGUI.EndChangeCheck()) { if (sceneVfx) SampleScene(); else if (previewPlayer) previewPlayer.StopPreview(); }
                    }
                }
                GUILayout.Label("Preview is visual only. Combo view assumes a queued press at each window opening. Gameplay still requires each press.", EditorStyles.wordWrappedMiniLabel);
                if (sceneVfx && selectedOnly && previewPlayer)
                {
                    var controller = previewPlayer.GetComponent<PlayerAttackController>();
                    if (!controller || controller.Profile != tuning.attackProfile)
                        EditorGUILayout.HelpBox("This player's effective attack profile differs from the edited profile. Choose a player using this profile to preview its effects.", MessageType.Warning);
                }
            }
        }

        private void DrawStageFields()
        {
            var profile = tuning.attackProfile;
            if (!profile || profile.Stages.Count == 0) return;
            selectedStage = Mathf.Clamp(selectedStage, 0, profile.Stages.Count - 1);
            var stage = profile.GetStage(selectedStage);
            EditorGUILayout.LabelField(StageLabel(selectedStage) + " — timing & travel", EditorStyles.boldLabel);
            using (var data = new SerializedObject(profile))
            {
                data.Update();
                var s = data.FindProperty("stages").GetArrayElementAtIndex(selectedStage);
                DrawSeconds(s.FindPropertyRelative("duration"), "Stage duration", .01f);
                Field(s, "animationFrameRate", "Frame ruler rate");
                DrawHitSeconds(s, "activationFrameStart", "Hit begins");
                DrawHitSeconds(s, "activationFrameEnd", "Hit ends");
                if (stage.ActivationFrameEnd / stage.AnimationFrameRate > stage.Duration + .0001f)
                {
                    EditorGUILayout.HelpBox($"Hit end is authored at {stage.ActivationFrameEnd / stage.AnimationFrameRate:0.###}s; the stage ends at {stage.Duration:0.###}s. Gameplay clamps the hit window to that end.", MessageType.Warning);
                    if (GUILayout.Button("Fit hit window to stage"))
                    {
                        s.FindPropertyRelative("activationFrameStart").floatValue = stage.ActivationStartNormalized * stage.Duration * stage.AnimationFrameRate;
                        s.FindPropertyRelative("activationFrameEnd").floatValue = stage.ActivationEndNormalized * stage.Duration * stage.AnimationFrameRate;
                    }
                }
                Field(s, "travelDistance", "Travel distance (size 1)");
                Field(s, "distanceCurve", "Travel over stage");
                Field(s, "rotationArc", "Sweep arc (degrees)");
                EditorGUILayout.LabelField(stage.StageType == AttackStageType.FinisherRepulsor ? "Repulsor is stationary; travel distance is ignored." : $"Average authored travel speed: {stage.TravelDistance / stage.Duration:0.##} units/s. Curve and player size affect actual motion.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(5);
                bool hasNext = selectedStage + 1 < profile.Stages.Count;
                using (new EditorGUI.DisabledScope(!hasNext))
                {
                    Field(s, "allowComboCancel", "Allow next stage");
                    var custom = s.FindPropertyRelative("customComboWindow");
                    bool customValue = EditorGUILayout.Toggle("Custom engagement window", custom.boolValue);
                    if (customValue && !custom.boolValue) PlayerTuningEditing.EnableCustomWindow(s, stage, tuning);
                    if (!customValue) custom.boolValue = false;
                    using (new EditorGUI.DisabledScope(!custom.boolValue || !s.FindPropertyRelative("allowComboCancel").boolValue))
                    {
                        DrawSeconds(s.FindPropertyRelative("comboWindowStartSeconds"), "Accept press from");
                        DrawSeconds(s.FindPropertyRelative("comboWindowEndSeconds"), "Accept press until");
                        Field(s, "earlyComboHandoff", "Allow early handoff");
                        using (new EditorGUI.DisabledScope(!s.FindPropertyRelative("earlyComboHandoff").boolValue))
                            DrawSeconds(s.FindPropertyRelative("comboHandoffSeconds"), "Earliest next stage");
                    }
                }
                if (!hasNext) EditorGUILayout.LabelField("Last stage — no next-stage window.", EditorStyles.miniLabel);
                else EditorGUILayout.HelpBox("The teal bar accepts the next press. The handoff marker is the earliest the queued attack can begin. A later accepted press transitions on the next update. With no accepted press, this stage plays to its full end. Early handoff can truncate damage and travel; lingering visuals do not deal damage.", MessageType.None);
                if (data.ApplyModifiedProperties()) PlayerTuningEditing.Save(profile);
            }
            using (var v = new SerializedObject(visuals))
            {
                v.Update(); DrawSeconds(v.FindProperty(PlayerTuningEditing.TailField(stage)), "Visual aftermath (no damage)");
                if (v.ApplyModifiedProperties()) PlayerTuningEditing.Save(visuals);
            }
            using (var d = new SerializedObject(tuning))
            {
                d.Update(); var combat = d.FindProperty("combat");
                DrawSeconds(combat.FindPropertyRelative("comboInputBuffer"), "Early input buffer");
                DrawSeconds(combat.FindPropertyRelative("attackCooldown"), "Cooldown after combo");
                Field(combat, "useSharedComboWindow", "Fallback: final portion of stage");
                DrawSeconds(combat.FindPropertyRelative("sharedComboWindowSeconds"), "Fallback engagement length");
                if (!combat.FindPropertyRelative("useSharedComboWindow").boolValue)
                { Field(combat, "comboWindowAfterActivationWindow", "Fallback: after hit window"); Field(combat, "comboWindowEndNormalized", "Fallback window end (0–1)"); }
                if (d.ApplyModifiedProperties()) PlayerTuningEditing.Save(tuning);
            }
        }

        private void DrawMovement()
        {
            EditorGUILayout.LabelField("Shared movement — every player, every scene", EditorStyles.boldLabel);
            using (var d = new SerializedObject(tuning))
            {
                d.Update(); var m = d.FindProperty("movement");
                Field(m, "movePower", "Propulsion force"); Field(m, "maxMoveSpeed", "Top speed"); Field(m, "clampSpeed", "Limit top speed");
                Field(m, "lateralFriction", "Sideways braking"); Field(m, "idleBrake", "Stick-release braking");
                Field(m, "reverseBrake", "Ordinary reverse braking"); Field(m, "reverseDotThreshold", "Reverse brake dot threshold");
                Field(m, "moveDeadzone", "Stick deadzone"); Field(m, "attackingMoveScale", "Movement during attack"); Field(m, "shieldMoveMultiplier", "Movement while shielding");
                if (d.ApplyModifiedProperties()) PlayerTuningEditing.Save(tuning);
            }
            EditorGUILayout.HelpBox("Propulsion is a force, so player mass affects acceleration. Sideways and release braking control drift; the reversal cone below can discard existing momentum immediately for ordinary joystick movement.", MessageType.None);
            DrawReversalCone();
            using (var d = new SerializedObject(tuning))
            {
                d.Update(); var c = d.FindProperty("combat");
                EditorGUILayout.LabelField("Lunge aim assistance", EditorStyles.boldLabel);
                Field(c, "lockOnEnabled", "Enable aim assistance"); Field(c, "lockOnConeHalfAngleDeg", "Aim cone half-angle");
                Field(c, "lockOnDirectionBlend", "Aim attraction"); Field(c, "lockOnMaxDistanceOverride", "Target distance (0 = travel)");
                Field(c, "swipeArcCurve", "Swipe rotation over stage"); Field(c, "visualDirectionFollowsCombatFacing", "Aim visuals with live facing");
                if (d.ApplyModifiedProperties()) PlayerTuningEditing.Save(tuning);
            }
        }
        private void DrawImpact()
        {
            if (tuning.attackProfile)
                using (var d = new SerializedObject(tuning.attackProfile))
                {
                    d.Update(); var stages = d.FindProperty("stages");
                    for (int i = 0; i < tuning.attackProfile.Stages.Count; i++)
                    {
                        if (tuning.attackProfile.GetStage(i).StageType != AttackStageType.FinisherRepulsor) continue;
                        var s = stages.GetArrayElementAtIndex(i);
                        EditorGUILayout.LabelField("Repulsor gameplay", EditorStyles.boldLabel);
                        Field(s, "repulsorScale", "Overall attack scale"); Field(s, "repulsorMaxRadius", "Base hit radius");
                        Field(s, "repulsorEnemyDamage", "NPC damage per pulse"); Field(s, "repulsorRadiusCurve", "Expansion over active window");
                    }
                    if (d.ApplyModifiedProperties()) PlayerTuningEditing.Save(tuning.attackProfile);
                }
            using (var d = new SerializedObject(tuning)) SharedSettingsEditing.DrawGroup(d, "repulsor");
            EditorGUILayout.LabelField("Repulsor body & movement recovery — shared", EditorStyles.boldLabel);
            using (var d = new SerializedObject(visuals)) SharedSettingsEditing.DrawGroup(d, "body");
            EditorGUILayout.LabelField("Repulsor grid response — shared", EditorStyles.boldLabel);
            using (var d = new SerializedObject(visuals)) SharedSettingsEditing.DrawGroup(d, "grid");
        }
        private void DrawAppearance()
        {
            if (!visuals) return;
            selectedStage = GUILayout.Toolbar(Mathf.Clamp(selectedStage, 0, 2), StageNames);
            using (var d = new SerializedObject(visuals))
            {
                d.Update();
                EditorGUILayout.PropertyField(d.FindProperty("sharedEnabled"), new GUIContent("Shared melee appearance"));
                PlayerMeleePlasmaPreviewGUI.DrawStyle(d);
                PlayerMeleePlasmaPreviewGUI.DrawTuning(d, visualBindings, selectedStage, previewPlayer);
                if (d.ApplyModifiedProperties()) PlayerTuningEditing.Save(visuals);
            }
        }
        private string StageLabel(int index)
        {
            var s = tuning.attackProfile ? tuning.attackProfile.GetStage(index) : null;
            return (index + 1) + " · " + (s == null ? "Stage" : s.StageType == AttackStageType.PrimaryLunge ? "Thrust" : s.StageType == AttackStageType.ComboSwipe ? "Sweep" : "Repulsor");
        }
        private static void Field(SerializedProperty parent, string name, string label)
        { var p = parent.FindPropertyRelative(name); if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label), true); }
        private void DrawSeconds(SerializedProperty p, string label, float min = 0)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(label + " (s)", p.floatValue);
            if (EditorGUI.EndChangeCheck()) p.floatValue = Mathf.Max(min, value);
        }
        private static void DrawHitSeconds(SerializedProperty s, string field, string label)
        {
            var p = s.FindPropertyRelative(field);
            float fps = Mathf.Max(1, s.FindPropertyRelative("animationFrameRate").floatValue);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUILayout.FloatField(label + " (s)", p.floatValue / fps);
                if (EditorGUI.EndChangeCheck()) p.floatValue = Mathf.Max(0, value) * fps;
                GUILayout.Label($"{p.floatValue:0.#} frames", EditorStyles.miniLabel, GUILayout.Width(82));
            }
        }
    }
}
