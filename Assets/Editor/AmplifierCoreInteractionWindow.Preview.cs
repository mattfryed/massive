using Massive.Settings;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public sealed partial class AmplifierCoreInteractionWindow
    {
        AmplifierCoreInteractionPreview preview;
        [SerializeField] bool previewRunning = true, previewLoop, closeup, showPath = true, arenaOptions;
        [SerializeField] float playbackSpeed = 1f, previewCharge = 1f, aimAngle = 20f, contestBalance = .5f;
        [SerializeField] float arenaWidth = 24f, arenaHeight = 14f;
        [SerializeField] bool previewWalls = true;
        [SerializeField] AmplifierCoreInteractionPreview.Look previewLook = AmplifierCoreInteractionPreview.Look.LightAim;
        double lastPreviewTick;
        [System.NonSerialized] string previewError;

        void OnEnable()
        {
            previewError = null;
            lastPreviewTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += PreviewTick;
            EditorApplication.playModeStateChanged += PreviewPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleasePreview;
            Undo.undoRedoPerformed += RefreshPreviewSettings;
        }
        void OnDisable()
        {
            EditorApplication.update -= PreviewTick;
            EditorApplication.playModeStateChanged -= PreviewPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreview;
            Undo.undoRedoPerformed -= RefreshPreviewSettings;
            ReleasePreview();
        }
        void ReleasePreview() { preview?.Dispose(); preview = null; }
        void PreviewPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { previewRunning = false; ReleasePreview(); }
            lastPreviewTick = EditorApplication.timeSinceStartup; Repaint();
        }
        void RefreshPreviewSettings() { preview?.SyncSettings(true); Repaint(); }
        void PreviewTick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)System.Math.Min(.1, now - lastPreviewTick); lastPreviewTick = now;
            if (!previewRunning || preview == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            preview.Advance(delta * playbackSpeed); Repaint();
        }
        void EnsurePreview(AmplifierSharedProfile profile)
        {
            if (preview != null || !string.IsNullOrEmpty(previewError)) return;
            try
            {
                preview = new AmplifierCoreInteractionPreview(profile);
                preview.SetArena(arenaWidth, arenaHeight, previewWalls);
                preview.SetAppearance(previewLook);
                preview.Charge = previewCharge; preview.AimAngle = aimAngle;
                preview.Loop = previewLoop; preview.ContestBalance = contestBalance;
                lastPreviewTick = EditorApplication.timeSinceStartup;
            }
            catch (ExitGUIException) { ReleasePreview(); throw; }
            catch (System.Exception e) { ReleasePreview(); previewError = e.GetType().Name + ": " + e.Message; }
        }

        void DrawPreview(AmplifierSharedProfile profile, bool wide)
        {
            EditorGUILayout.LabelField("EDIT-MODE PREVIEW", EditorStyles.boldLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Preview stopped for Play Mode. It will be available again when you exit Play Mode.", MessageType.Info); return;
            }
            EnsurePreview(profile);
            if (preview == null)
            {
                EditorGUILayout.HelpBox(previewError ?? "Preparing preview…", MessageType.Warning);
                if (GUILayout.Button("Retry preview")) previewError = null;
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool physical = profile.core.possession.mode == Massive.Multiplier.AmplifierInteractionMode.Physical;
                if (GUILayout.Button(physical ? "Apply impulse" : "Launch")) { preview.Launch(); previewRunning = true; }
                using (new EditorGUI.DisabledScope(physical))
                    if (GUILayout.Button("Charge → launch"))
                    {
                        if (previewLook is not (AmplifierCoreInteractionPreview.Look.LightAim or AmplifierCoreInteractionPreview.Look.DarkAim))
                            preview.SetAppearance(previewLook = AmplifierCoreInteractionPreview.Look.LightAim);
                        preview.Launch(true); previewRunning = true;
                    }
                if (GUILayout.Button(previewRunning ? "Pause" : "Resume", GUILayout.Width(62f))) previewRunning = !previewRunning;
                if (GUILayout.Button("Stop / reset", GUILayout.Width(86f))) { previewRunning = false; preview.Reset(); }
                preview.Loop = previewLoop = GUILayout.Toggle(previewLoop, "Loop", GUILayout.Width(48f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                closeup = GUILayout.Toolbar(closeup ? 1 : 0, new[] { "Arena", "Core close-up" }) == 1;
                showPath = GUILayout.Toggle(showPath, "Travel trace", GUILayout.Width(95f));
                using (new EditorGUI.DisabledScope(previewRunning))
                    if (GUILayout.Button("Step", GUILayout.Width(45f))) { preview.Advance(Time.fixedDeltaTime); Repaint(); }
            }
            var nextLook = (AmplifierCoreInteractionPreview.Look)EditorGUILayout.Popup("Visual state", (int)previewLook, new[]
            {
                "Neutral / available", "Single possession — Light", "Single possession — Dark", "Contested — two players",
                "Time-out warning", "Spawn", "Despawn", "Post-contest — Light wins", "Post-contest — Dark wins", "Ejected — empty Core"
            });
            if (nextLook != previewLook) { previewLook = nextLook; preview.SetAppearance(nextLook); previewRunning = true; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Single possession")) SetPreviewState(AmplifierCoreInteractionPreview.Look.LightAim);
                if (GUILayout.Button("Contested")) SetPreviewState(AmplifierCoreInteractionPreview.Look.Contest);
                if (GUILayout.Button("Post-contest")) SetPreviewState(AmplifierCoreInteractionPreview.Look.PostContestLight);
                if (GUILayout.Button("Replay state")) { preview.Reset(); previewRunning = true; }
            }
            preview.Charge = previewCharge = EditorGUILayout.Slider("Shot charge", previewCharge, 0f, 1f);
            preview.AimAngle = aimAngle = EditorGUILayout.Slider("Aim angle", aimAngle, -180f, 180f);
            playbackSpeed = EditorGUILayout.Slider("Playback speed", playbackSpeed, .1f, 2f);
            if (previewLook == AmplifierCoreInteractionPreview.Look.Contest)
                preview.ContestBalance = contestBalance = EditorGUILayout.Slider("Light / Dark control", contestBalance, 0f, 1f);

            float reserve = (arenaOptions ? 455f : 395f) + (previewLook == AmplifierCoreInteractionPreview.Look.Contest ? 20f : 0f);
            float height = wide ? Mathf.Max(180f, position.height - reserve) : 180f;
            Rect rect = GUILayoutUtility.GetRect(300f, height, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, preview.Render(rect, closeup), ScaleMode.StretchToFill, false);
                preview.DrawOverlays(rect, showPath);
            }
            // Click the arena to aim without hunting for a numeric direction.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                Vector2 uv = Event.current.mousePosition - rect.position;
                var point = preview.Camera.ViewportToWorldPoint(new Vector3(uv.x / rect.width, 1f - uv.y / rect.height, 30f));
                var direction = point - preview.Position;
                aimAngle = preview.AimAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                Event.current.Use(); Repaint();
            }
            string phase = preview.State switch
            {
                AmplifierCoreInteractionPreview.Phase.Preview => previewLook switch
                {
                    AmplifierCoreInteractionPreview.Look.Contest => "Contested — adjust Light / Dark control",
                    AmplifierCoreInteractionPreview.Look.PostContestLight => PostContestPreviewStatus("Light"),
                    AmplifierCoreInteractionPreview.Look.PostContestDark => PostContestPreviewStatus("Dark"),
                    AmplifierCoreInteractionPreview.Look.Ejected => preview.Clock < .8f ? "Possessed — ejection next…" : "Player ejected — Core available",
                    _ => "Ready to launch"
                },
                AmplifierCoreInteractionPreview.Phase.Charging => "Charging — " + preview.DisplayCharge.ToString("P0"),
                AmplifierCoreInteractionPreview.Phase.Flying => "Moving — entry locked",
                AmplifierCoreInteractionPreview.Phase.ReentryDelay => "Stopped — entry in " + preview.ReentryRemaining.ToString("0.00") + "s",
                _ => "Stopped — enterable"
            };
            EditorGUILayout.LabelField((previewRunning ? "" : "PAUSED · ") + phase, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Speed {preview.Speed:0.00} u/s     Travel {preview.Distance:0.00} u     Shot {preview.ShotTime:0.00}s     Bounces {preview.Bounces}", EditorStyles.miniLabel);
            if (preview.StopTime >= 0f) EditorGUILayout.LabelField($"Came to rest at {preview.StopTime:0.00}s", EditorStyles.miniLabel);
            arenaOptions = EditorGUILayout.Foldout(arenaOptions, "Preview arena options", true);
            if (arenaOptions)
            {
                EditorGUI.BeginChangeCheck();
                arenaWidth = EditorGUILayout.Slider("Width", arenaWidth, 8f, 60f);
                arenaHeight = EditorGUILayout.Slider("Height", arenaHeight, 6f, 30f);
                previewWalls = EditorGUILayout.Toggle("Solid walls", previewWalls);
                if (EditorGUI.EndChangeCheck()) { preview.SetArena(arenaWidth, arenaHeight, previewWalls); preview.Reset(); }
            }
            EditorGUILayout.HelpBox("Real Core and state UI at gameplay proportions in a separate physics arena. Text and ring controls are on the right. Click to aim. Arena view shows world scale; close-up magnifies everything together. Fields, goals and contest input need a gameplay test.", MessageType.None);
        }
        void SetPreviewState(AmplifierCoreInteractionPreview.Look state)
        { previewLook = state; preview.SetAppearance(state); previewRunning = true; closeup = true; }

        string PostContestPreviewStatus(string team)
        {
            if (preview.Clock < .8f) return "Contest resolving…";
            if (preview.PostContestProtectionRemaining > 0f) return $"{team} wins — input protection {preview.PostContestProtectionRemaining:0.00}s (aim available)";
            if (preview.PostContestWaitingForRelease) return team + " wins — previewing button release";
            return team + " wins — fresh Attack can charge";
        }
    }
}
