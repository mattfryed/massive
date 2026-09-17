using Massive.Player;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMeleePlasma))]
public sealed class PlayerMeleePlasmaEditor : Editor
{
    private int selectedStage;

    public override bool RequiresConstantRepaint()
    {
        var plasma = target as PlayerMeleePlasma;
        return plasma != null && plasma.PreviewActive && plasma.PreviewAnimating;
    }

    public override void OnInspectorGUI()
    {
        var plasma = (PlayerMeleePlasma)target;
        if (GUILayout.Button("Open Melee Visual Preview", GUILayout.Height(28)))
            PlayerMeleePlasmaPreviewWindow.Open(plasma);

        serializedObject.Update();
        PlayerMeleePlasmaPreviewGUI.DrawStyle(serializedObject);
        serializedObject.ApplyModifiedProperties();
        PlayerMeleePlasmaPreviewGUI.DrawTransport(plasma, ref selectedStage);

        serializedObject.Update();
        PlayerMeleePlasmaPreviewGUI.DrawPreviewTiming(serializedObject);
        EditorGUILayout.Space(8);
        PlayerMeleePlasmaPreviewGUI.DrawTuning(serializedObject);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8);
        PlayerMeleePlasmaPreviewGUI.DrawButtons(plasma, selectedStage);
    }
}

public sealed class PlayerMeleePlasmaPreviewWindow : EditorWindow
{
    [SerializeField] private PlayerMeleePlasma previewTarget;
    [SerializeField] private int selectedStage;
    private Vector2 scroll;
    private SerializedObject targetSerialized;
    private PlayerMeleePlasma[] scenePlayers = new PlayerMeleePlasma[0];

    [MenuItem("MASSIVE/Player/Melee Visual Preview")]
    public static void Open()
    {
        Open(null);
    }

    [MenuItem("MASSIVE/Player/Melee Plasma Preview")]
    private static void OpenLegacyMenu()
    {
        Open();
    }

    public static void Open(PlayerMeleePlasma plasma)
    {
        var window = GetWindow<PlayerMeleePlasmaPreviewWindow>("Melee Visual Preview");
        window.minSize = new Vector2(420, 580);
        if (plasma != null) window.SetTarget(plasma);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Melee Visual Preview");
        EditorApplication.hierarchyChanged += RefreshPlayers;
        RefreshPlayers();
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= RefreshPlayers;
        if (previewTarget != null && !Application.isPlaying)
            previewTarget.StopPreview();
        if (targetSerialized != null) targetSerialized.Dispose();
        targetSerialized = null;
    }

    private void OnInspectorUpdate()
    {
        // Animation is advanced by the component, independently of how many UI views are open.
        if (previewTarget != null && previewTarget.PreviewActive) Repaint();
    }

    private void RefreshPlayers()
    {
        scenePlayers = Object.FindObjectsByType<PlayerMeleePlasma>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        if (previewTarget == null)
        {
            PlayerMeleePlasma best = null;
            foreach (var candidate in scenePlayers)
            {
                if (!candidate.gameObject.scene.IsValid() || EditorUtility.IsPersistent(candidate)) continue;
                if (best == null) best = candidate;
                if (candidate.isActiveAndEnabled) { best = candidate; break; }
            }
            SetTarget(best);
        }
        Repaint();
    }

    private void SetTarget(PlayerMeleePlasma value)
    {
        if (previewTarget == value && targetSerialized != null) return;
        if (previewTarget != null && previewTarget != value && !Application.isPlaying)
            previewTarget.StopPreview();
        previewTarget = value;
        if (targetSerialized != null) targetSerialized.Dispose();
        targetSerialized = value != null ? new SerializedObject(value) : null;
        selectedStage = value != null ? Mathf.Clamp(value.PreviewStage, 0, 2) : 0;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        var selected = (PlayerMeleePlasma)EditorGUILayout.ObjectField(
            "Player", previewTarget, typeof(PlayerMeleePlasma), true);
        if (selected != previewTarget) SetTarget(selected);
        if (GUILayout.Button("Find", GUILayout.Width(48))) ShowPlayerMenu();
        if (GUILayout.Button("Select", GUILayout.Width(52)) && previewTarget != null)
            Selection.activeGameObject = previewTarget.gameObject;
        EditorGUILayout.EndHorizontal();

        if (previewTarget == null)
        {
            EditorGUILayout.HelpBox("Choose a scene player with a Player Melee Plasma component. " +
                "Find lists the configured players in the open scenes.", MessageType.Info);
            return;
        }

        if (targetSerialized == null || targetSerialized.targetObject != previewTarget)
            SetTarget(previewTarget);
        targetSerialized.Update();
        PlayerMeleePlasmaPreviewGUI.DrawStyle(targetSerialized);
        targetSerialized.ApplyModifiedProperties();

        // Transport stays visible while the effect controls scroll.
        PlayerMeleePlasmaPreviewGUI.DrawTransport(previewTarget, ref selectedStage);
        targetSerialized.Update();
        PlayerMeleePlasmaPreviewGUI.DrawPreviewTiming(targetSerialized);
        targetSerialized.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        targetSerialized.Update();
        PlayerMeleePlasmaPreviewGUI.DrawTuning(targetSerialized);
        targetSerialized.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        PlayerMeleePlasmaPreviewGUI.DrawButtons(previewTarget, selectedStage);
    }

    private void ShowPlayerMenu()
    {
        RefreshPlayers();
        var menu = new GenericMenu();
        bool found = false;
        foreach (var player in scenePlayers)
        {
            if (!player.gameObject.scene.IsValid() || EditorUtility.IsPersistent(player)) continue;
            var candidate = player;
            string label = candidate.gameObject.scene.name + "/" + HierarchyPath(candidate.transform);
            if (!candidate.isActiveAndEnabled) label += " (inactive)";
            menu.AddItem(new GUIContent(label), previewTarget == candidate, () => SetTarget(candidate));
            found = true;
        }
        if (!found) menu.AddDisabledItem(new GUIContent("No configured players in open scenes"));
        menu.ShowAsContext();
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}

internal static class PlayerMeleePlasmaPreviewGUI
{
    private static readonly GUIContent[] StyleNames =
    {
        new GUIContent("Original particles"), new GUIContent("Plasma"),
        new GUIContent("Asset Slashes"), new GUIContent("Off")
    };
    // Display order is independent of the serialized enum values used by existing players.
    private static readonly int[] StyleValues = { 0, 1, 3, 2 };
    private static readonly GUIContent[] SweepNames =
    {
        new GUIContent("Asset prefab — Sword Slash 5"),
        new GUIContent("Volumetric — black & white")
    };
    private static readonly int[] SweepValues = { 0, 1 };
    private static readonly GUIContent[] RepulsorNames =
    {
        new GUIContent("Volumetric outward pulse"), new GUIContent("Plasma ring comparison"),
        new GUIContent("Off")
    };
    private static readonly int[] RepulsorValues = { 0, 1, 2 };
    private static readonly string[] StageNames = { "1 — Thrust", "2 — Sweep", "3 — Repulsor" };

    public static void DrawStyle(SerializedObject serialized)
    {
        var property = serialized.FindProperty("visualStyle");
        if (property == null) return;
        EditorGUILayout.LabelField("Melee visual treatment", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int value = EditorGUILayout.IntPopup(new GUIContent("Style", "Choose the existing particles, procedural plasma, or the separate thrust/sweep treatments. Off hides melee presentation only."),
            property.intValue, StyleNames, StyleValues);
        if (EditorGUI.EndChangeCheck()) property.intValue = value;
        if (property.intValue == 3)
        {
            // Keep this comparison beside the fixed transport, above the scrolling tuning fields.
            var sweep = serialized.FindProperty("arcSweepTreatment");
            if (sweep != null)
            {
                EditorGUI.BeginChangeCheck();
                int sweepValue = EditorGUILayout.IntPopup(new GUIContent("Sweep treatment", "Compare the assigned sweep prefab with a black and white 3D density volume. Thrust and finisher keep their own treatments."),
                    sweep.intValue, SweepNames, SweepValues);
                if (EditorGUI.EndChangeCheck()) sweep.intValue = sweepValue;
            }
        }
        if (property.intValue == 1 || property.intValue == 3)
        {
            var repulsor = serialized.FindProperty("repulsorTreatment");
            if (repulsor != null)
            {
                EditorGUI.BeginChangeCheck();
                int repulsorValue = EditorGUILayout.IntPopup(new GUIContent("Repulsor treatment", "Third-stage energy presentation. Body feedback and grid response have separate controls on their own components."),
                    repulsor.intValue, RepulsorNames, RepulsorValues);
                if (EditorGUI.EndChangeCheck()) repulsor.intValue = repulsorValue;
            }
        }
    }

    public static void DrawTransport(PlayerMeleePlasma plasma, ref int selectedStage)
    {
        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        string status = playing ? "GAMEPLAY — NORMAL ATTACK INPUT" :
            !plasma.PreviewActive ? "EDIT PREVIEW STOPPED" :
            plasma.PreviewAnimating ? (plasma.PreviewLoop ? "LOOPING EDIT PREVIEW" : "PLAYING EDIT PREVIEW") :
            "EDIT PREVIEW PAUSED";
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField(status, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(playing
            ? "Use normal attack input in Play Mode. Edit preview stops automatically. Visual settings remain adjustable."
            : "Preview animates only the visual: the player, damage, hitboxes and combo rules are unchanged. Stop removes the preview.",
            MessageType.None);

        bool canPreview = CanPreview(plasma);
        using (new EditorGUI.DisabledScope(!canPreview))
        {
            if (plasma.PreviewActive) selectedStage = Mathf.Clamp(plasma.PreviewStage, 0, 2);
            int nextStage = EditorGUILayout.Popup("Attack stage", selectedStage, StageNames);
            if (nextStage != selectedStage)
            {
                selectedStage = nextStage;
                if (plasma.PreviewActive)
                {
                    bool wasAnimating = plasma.PreviewAnimating;
                    bool wasLooping = plasma.PreviewLoop;
                    if (wasAnimating) plasma.StartPreview(selectedStage, wasLooping);
                    else plasma.ScrubPreview(selectedStage, plasma.PreviewNormalizedTime);
                    RefreshViews();
                }
            }

            EditorGUI.BeginChangeCheck();
            float frame = EditorGUILayout.Slider("Scrub attack", plasma.PreviewNormalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                plasma.ScrubPreview(selectedStage, frame);
                RefreshViews();
            }
            bool repulsorAftermath = selectedStage == 2 &&
                ((int)plasma.visualStyle == 1 || (int)plasma.visualStyle == 3) &&
                plasma.repulsorTreatment == RepulsorVisualTreatment.VolumetricPulse;
            if (repulsorAftermath || ((int)plasma.visualStyle == 3 && (selectedStage == 0 ||
                (selectedStage == 1 && (int)plasma.arcSweepTreatment == 1))))
            {
                string tooltip = selectedStage == 0
                    ? "Inspect the thrust's visual tail after the attack stage ends: 0 is the stage end, 1 is the end of Thrust Linger. This tail deals no damage."
                    : selectedStage == 2
                    ? "Inspect the Repulsor after its active window closes: 0 is the end of the physical pulse, 1 is the end of Pulse Linger. This tail deals no damage."
                    : "Inspect the sweep's visual tail after the damage window closes: 0 is the end of the active window, 1 is the end of World-space Linger. This tail deals no damage.";
                bool matchingPreview = selectedStage == plasma.PreviewStage &&
                    (selectedStage == 0 ? plasma.IsThrustTrailPreview : selectedStage == 2 ? plasma.IsRepulsorPulsePreview : plasma.IsVolumeSweepPreview);
                EditorGUI.BeginChangeCheck();
                float aftermath = EditorGUILayout.Slider(new GUIContent("Scrub lingering trail", tooltip),
                    matchingPreview ? plasma.PreviewAftermathNormalizedTime : 0f, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    plasma.ScrubPreviewAftermath(selectedStage, aftermath);
                    RefreshViews();
                }
            }
        }

        var progress = EditorGUILayout.GetControlRect(false, 17);
        string phase = plasma.PreviewActive ? plasma.PreviewPhase : "Windup → Active → Recovery";
        EditorGUI.ProgressBar(progress, plasma.PreviewActive ? plasma.PreviewNormalizedTime : 0f, phase);
        DrawButtons(plasma, selectedStage);

        if (!playing && !canPreview)
            EditorGUILayout.HelpBox(!plasma.isActiveAndEnabled
                ? "Enable this scene player and its component to preview."
                : EditorUtility.IsPersistent(plasma) ? "Preview a player instance in an open scene."
                : "Select Plasma or Asset Slashes to preview an alternate treatment. Original particles use the existing gameplay emitters.",
                MessageType.Info);
        if (selectedStage == 2 && !plasma.HasRepulsor)
            EditorGUILayout.HelpBox("This player has no Repulsor AOE component. The finisher preview is a visual study; it does not add an area attack.", MessageType.Info);
    }

    public static void DrawButtons(PlayerMeleePlasma plasma, int stageIndex)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        using (new EditorGUI.DisabledScope(!CanPreview(plasma)))
        {
            if (GUILayout.Button("Play stage", EditorStyles.toolbarButton))
            {
                plasma.StartPreview(stageIndex, false);
                RefreshViews();
            }
            bool looping = plasma.PreviewActive && plasma.PreviewLoop && plasma.PreviewAnimating;
            bool loopRequested = GUILayout.Toggle(looping, "Loop stage", EditorStyles.toolbarButton);
            if (loopRequested != looping)
            {
                if (loopRequested) plasma.StartPreview(stageIndex, true);
                else plasma.PausePreview();
                RefreshViews();
            }
        }
        using (new EditorGUI.DisabledScope(!plasma.PreviewActive || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Pause", EditorStyles.toolbarButton))
            {
                plasma.PausePreview();
                RefreshViews();
            }
            if (GUILayout.Button("Stop preview", EditorStyles.toolbarButton))
            {
                plasma.StopPreview();
                RefreshViews();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    public static void DrawPreviewTiming(SerializedObject serialized)
    {
        var style = serialized.FindProperty("visualStyle");
        if (style != null && style.intValue != 1 && style.intValue != 3) return;
        var speed = serialized.FindProperty("previewPlaybackSpeed");
        var delay = serialized.FindProperty("previewRepeatDelay");
        if (speed != null) EditorGUILayout.PropertyField(speed, new GUIContent("Preview playback speed", "Changes only edit-preview time, including effect motion. Gameplay timing is unchanged."));
        if (delay != null) EditorGUILayout.PropertyField(delay, new GUIContent("Loop rest (seconds)", "Pause between repeated stage previews."));
    }

    public static void DrawTuning(SerializedObject serialized)
    {
        var style = serialized.FindProperty("visualStyle");
        int value = style != null ? style.intValue : 0;
        if (value == 3)
        {
            EditorGUILayout.HelpBox("Thrust, Sweep and Repulsor have separate treatments. The Repulsor expands from the player outline during its physical activation window, then briefly fades in world space.", MessageType.None);
            DrawPrefabEffect(serialized, "thrustPrefabEffect", "Thrust", "Prick 5");
            EditorGUILayout.LabelField("Thrust trail — visual only", EditorStyles.miniBoldLabel);
            DrawField(serialized, "thrustWorldEmission", "World-space Emission", "The emitter follows the player's current position and aim. Emitted particles keep their world-space position and heading as the player moves.");
            var refreshes = serialized.FindProperty("thrustEmissionPasses");
            if (refreshes != null)
                EditorGUILayout.IntSlider(refreshes, 1, 5, new GUIContent("Emission Refreshes", "Number of newly emitted main streaks during the lunge. Each starts at the player's current position and aim while older streaks retain their world-space trajectory."));
            DrawSlider(serialized, "thrustLingerSeconds", "Thrust Linger (s)", 0f, 1.5f, "Extra visual lifetime after the thrust attack stage ends. Remaining particles fade without extending the hitbox or dealing damage.");
            DrawSlider(serialized, "thrustFadeCurve", "Thrust Fade Curve", .5f, 4f, "Shapes how the thrust's remaining particles disappear during Thrust Linger.");
            EditorGUILayout.LabelField("Thrust — organic energy glow", EditorStyles.miniBoldLabel);
            DrawField(serialized, "thrustOrganicGlow", "Organic Glow", "Replace the source's soft glow with three-dimensional energy pockets around the emitted streaks. Off restores the original glow for comparison.");
            DrawSlider(serialized, "thrustGlowIntensity", "Glow Intensity", 0f, 2f, "Brightness of the organic energy. Zero hides it without restoring the source glow.");
            DrawSlider(serialized, "thrustGlowBreakup", "Glow Breakup", 0f, 1f, "Break the glow into uneven hot channels and dark gaps instead of a uniform halo.");
            DrawSlider(serialized, "thrustGlowDepth", "Glow Depth", 0f, 1f, "Depth and curl of the energy pockets around each emitted streak.");
            DrawSlider(serialized, "thrustGlowFlow", "Glow Flow", 0f, 4f, "Motion within the organic glow; separate from playback speed and attack timing.");
            DrawField(serialized, "thrustEnergyMaterial", "Energy Material Override", "Optional. The bundled MeleeThrustEnergy material is used when this is empty.");
            EditorGUILayout.Space(8);
            var sweep = serialized.FindProperty("arcSweepTreatment");
            if (sweep != null && sweep.intValue == 1)
                DrawVolumetricSweep(serialized);
            else
                DrawPrefabEffect(serialized, "swipePrefabEffect", "Sweep", "Sword Slash 5");
            EditorGUILayout.Space(8);
            DrawRepulsor(serialized);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Shared placement and attack phases", EditorStyles.boldLabel);
            DrawField(serialized, "surfaceHeight", "Surface Height", "Height of the attack visual above the playing field.");
            DrawField(serialized, "windupOpacity", "Windup Opacity", "Prefab-effect visibility before the damage window opens, including Prick 5. Volumetric tendrils begin at the damage window's start.");
            DrawField(serialized, "recoveryOpacity", "Recovery Opacity", "Prefab-effect visibility during attack recovery. After the thrust stage ends, its remaining particles use Thrust Linger (s) and Thrust Fade Curve. The volumetric sweep uses World-space Linger (s) and Fade Curve.");
            if (sweep != null && sweep.intValue == 1)
                EditorGUILayout.HelpBox("Windup Opacity and Recovery Opacity adjust Prick 5 during its stage; its aftermath uses Thrust Linger (s) and Thrust Fade Curve. Volumetric tendrils begin when the damage window opens and use World-space Linger (s) and Fade Curve.", MessageType.None);
            DrawReferences(serialized);
            return;
        }
        if (value != 1)
        {
            EditorGUILayout.HelpBox(value == 2
                ? "Melee visual effects are off. Attack input, damage, reach and combo rules are unchanged."
                : "The existing melee particle emitters supply this treatment. Their settings remain on the original components.", MessageType.None);
            DrawReferences(serialized);
            return;
        }

        DrawRepulsor(serialized);
        EditorGUILayout.Space(8);
        var iterator = serialized.GetIterator();
        bool children = true;
        while (iterator.NextVisible(children))
        {
            children = false;
            if (iterator.name == "m_Script" || iterator.name == "visualStyle" ||
                iterator.name == "previewPlaybackSpeed" || iterator.name == "previewRepeatDelay" ||
                iterator.name == "thrustPrefabEffect" || iterator.name == "swipePrefabEffect" ||
                iterator.name.StartsWith("thrust", System.StringComparison.Ordinal) ||
                iterator.name == "arcSweepTreatment" || iterator.name.StartsWith("volume", System.StringComparison.Ordinal) ||
                iterator.name == "repulsorTreatment" || iterator.name.StartsWith("repulsorPulse", System.StringComparison.Ordinal)) continue;
            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    private static void DrawRepulsor(SerializedObject serialized)
    {
        EditorGUILayout.LabelField("Repulsor — outward energy pulse", EditorStyles.boldLabel);
        var treatment = serialized.FindProperty("repulsorTreatment");
        if (treatment == null) return;
        if (treatment.intValue == 2)
        {
            EditorGUILayout.HelpBox("Repulsor energy is off. Body pulse and grid response remain independently adjustable on Player Repulsor Feedback and Player Repulsor Grid Pulse.", MessageType.None);
            return;
        }
        if (treatment.intValue == 1)
        {
            EditorGUILayout.HelpBox("Flat plasma ring comparison. Select Volumetric outward pulse above for the new three-dimensional energy treatment.", MessageType.None);
            DrawField(serialized, "plasmaMaterial", "Ring Material", "Material used by the comparison ring.");
            DrawField(serialized, "repulsorBandWidth", "Ring Width", "Thickness of the comparison ring; visual only.");
            return;
        }
        EditorGUILayout.HelpBox("A full-circle volume begins at the player outline when the Repulsor activates, follows the physical pulse radius and briefly lingers at its release position. Radius and expansion timing come from the attack profile. The lingering effect deals no damage.", MessageType.None);
        DrawField(serialized, "repulsorPulseMaterial", "Pulse Material Override", "Optional. The bundled MeleeRepulsor shader supplies the default material automatically.");
        DrawSlider(serialized, "repulsorPulseWidth", "Pulse Width", .035f, .5f, "Radial width of the energy around the physical pulse. Scales with Player Size.");
        DrawSlider(serialized, "repulsorPulseThickness", "Pulse Depth", .025f, .6f, "Three-dimensional thickness above and below the playing plane. Scales with Player Size.");
        DrawSlider(serialized, "repulsorPulseDensity", "Density", .1f, 6f, "Opacity of the dark body and luminous channels.");
        DrawSlider(serialized, "repulsorPulseTurbulence", "Turbulence", 0f, .4f, "Irregular displacement of the ring and its internal flowing strands.");
        DrawSlider(serialized, "repulsorPulseBreakup", "Local Breakup", 0f, 1f, "Contrast between active regions and quieter gaps around the circle.");
        DrawSlider(serialized, "repulsorPulseFlow", "Internal Flow", 0f, 5f, "Motion inside the pulse, independent of physical expansion and preview playback speed.");
        DrawSlider(serialized, "repulsorPulseLinger", "Pulse Linger (s)", 0f, 1f, "Visual lifetime after the active window closes. The pulse remains anchored at its release position.");
        DrawSlider(serialized, "repulsorPulseFade", "Pulse Fade Curve", .5f, 4f, "Shapes the fade of the lingering pulse.");
        EditorGUILayout.LabelField("Black and white layers", EditorStyles.miniBoldLabel);
        DrawField(serialized, "repulsorPulseBlackBody", "Black Body", "Dark three-dimensional matter between the hot channels.");
        DrawField(serialized, "repulsorPulseWhiteEdge", "White Edge", "Fine bright strands near the outer edge.");
        DrawSlider(serialized, "repulsorPulseEdgeIntensity", "Edge Intensity", 0f, 2f, "Brightness of the white edge strands.");
        DrawField(serialized, "repulsorPulseFilaments", "Internal Filaments", "Twisting luminous channels within the volume.");
        DrawSlider(serialized, "repulsorPulseFilamentIntensity", "Filament Intensity", 0f, 2f, "Brightness of the internal channels.");
        DrawField(serialized, "repulsorPulseWisps", "Fine Wisps", "Localized curls surrounding the main pulse.");
        DrawSlider(serialized, "repulsorPulseWispIntensity", "Wisp Intensity", 0f, 1f, "Brightness of the optional fine wisps.");
    }

    private static void DrawVolumetricSweep(SerializedObject serialized)
    {
        EditorGUILayout.LabelField("Sweep — Volumetric black & white", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("New sweep points emit from the player's current position and aim. Emitted pieces retain their position and heading in world space as the player moves. Tendrils can split, rejoin and linger after the damage window closes; the lingering trail deals no damage.", MessageType.None);
        DrawField(serialized, "volumetricArcMaterial", "Volume Material", "Shared material for the black and white 3D crescent. Per-player dials are applied without editing the material asset.");

        EditorGUILayout.LabelField("Volume shape", EditorStyles.miniBoldLabel);
        DrawSlider(serialized, "volumeReachScale", "Reach Scale", .5f, 1.5f, "Visual size relative to the sword capsule's reach. Does not change attack range.");
        DrawSlider(serialized, "volumeArcDegrees", "Arc Angle (degrees)", 60f, 300f, "Angular span of the crescent around the player.");
        DrawSlider(serialized, "volumeRadialWidth", "Radial Width (m)", .05f, .8f, "Width of the curved volume across its radius, in metres.");
        DrawSlider(serialized, "volumeThickness", "Thickness (m)", .03f, 1f, "Depth of the volume above and below its playing plane, in metres.");
        DrawSlider(serialized, "volumeTaper", "End Taper", .3f, 3f, "Shapes how the crescent narrows toward its ends.");

        EditorGUILayout.LabelField("Travelling tendrils", EditorStyles.miniBoldLabel);
        var count = serialized.FindProperty("volumeTendrilCount");
        if (count != null)
            EditorGUILayout.IntSlider(count, 2, 10, new GUIContent("Tendril Count", "Number of main travelling tendrils in the emitted arc."));
        var extra = serialized.FindProperty("volumeExtraTendrils");
        if (extra != null)
            EditorGUILayout.IntSlider(extra, 0, 6, new GUIContent("Extra Fine Tendrils", "Additional finer strands among the main tendrils. Total main and fine tendrils is capped at twelve."));
        DrawSlider(serialized, "volumeTravelDuration", "Arc Travel Time (s)", .12f, 1f, "Time for a tendril to travel through the arc. New points follow the player's current emission position; existing pieces stay in world space. Does not change attack timing.");
        DrawSlider(serialized, "volumeEmissionSpacing", "Emission Spacing (s)", 0f, .12f, "Delay between successive tendril emissions, in seconds.");
        DrawSlider(serialized, "volumeTailLength", "Tail Length", .08f, .8f, "Length of the trailing section behind each moving tendril head.");
        DrawSlider(serialized, "volumeBranching", "Split / Merge Amount", 0f, 1.5f, "How strongly neighboring tendrils separate and reconnect.");
        DrawSlider(serialized, "volumeConvergenceRate", "Junction Frequency", .25f, 6f, "How often neighboring tendrils meet at shared junctions.");
        DrawField(serialized, "volumeCrackle", "Energetic Crackle", "Uneven local kinks and hot channels along the strands. Off removes this additional motion.");
        DrawSlider(serialized, "volumeCrackleAmount", "Crackle Amount", 0f, 1f, "Strength of the randomized kinks and brief hot channels. Zero removes the added crackle.");
        DrawSlider(serialized, "volumeCrackleScale", "Crackle Detail", 2f, 30f, "Spatial detail of the crackle: higher values create smaller, more numerous changes along the arc.");
        DrawSlider(serialized, "volumeCrackleRate", "Crackle Rate", 0f, 25f, "Rate at which local crackle changes. Zero holds its pattern; this does not change attack or propagation speed.");

        EditorGUILayout.LabelField("Lingering trail — visual only", EditorStyles.miniBoldLabel);
        DrawSlider(serialized, "volumeLingerSeconds", "World-space Linger (s)", 0f, 1.5f, "Seconds the emitted trail remains after the damage window ends, independent of attack duration. The lingering trail stays in world space and deals no damage.");
        DrawSlider(serialized, "volumeDissolve", "Fade Curve", .5f, 4f, "Shapes how the remaining density fades during the lingering trail.");

        EditorGUILayout.LabelField("Density and motion", EditorStyles.miniBoldLabel);
        DrawSlider(serialized, "volumeDensity", "Density", .1f, 6f, "Amount of visible matter through the volume.");
        DrawSlider(serialized, "volumeTurbulence", "Turbulence (m)", 0f, .3f, "Amount of irregular displacement in the volume, in metres.");
        DrawSlider(serialized, "volumeNoiseScale", "Noise Scale", 1f, 12f, "Spatial frequency of the volume's internal pattern.");
        DrawSlider(serialized, "volumeFlowSpeed", "Flow Speed", 0f, 5f, "Motion rate of the volume's plasma pattern. Preview playback speed remains a separate control.");
        EditorGUILayout.LabelField("Organic energy glow", EditorStyles.miniBoldLabel);
        DrawField(serialized, "volumeOrganicGlow", "Organic Glow", "Shape the enabled light layers into three-dimensional pockets, dark gaps and internal channels. Off restores the smoother treatment.");
        DrawSlider(serialized, "volumeGlowBreakup", "Glow Breakup", 0f, 1f, "Contrast between hot strands and gaps in the glow.");
        DrawSlider(serialized, "volumeGlowDepth", "Glow Depth", 0f, 1f, "Depth and curl within the luminous volume.");
        DrawSlider(serialized, "volumeGlowFlow", "Glow Flow", 0f, 4f, "Motion of the organic glow's internal pattern, independent of propagation and playback speed.");

        EditorGUILayout.LabelField("Black and white layers", EditorStyles.miniBoldLabel);
        DrawField(serialized, "volumeBlackBody", "Black Body", "Show the dark body of the crescent.");
        DrawField(serialized, "volumeWhiteEdge", "White Edge", "Show the bright edge of the volume.");
        DrawSlider(serialized, "volumeEdgeIntensity", "White Edge Intensity", 0f, 3f, "Brightness of the white edge layer.");
        DrawField(serialized, "volumeFilaments", "Filaments", "Show flowing fine structures inside the volume.");
        DrawSlider(serialized, "volumeFilamentIntensity", "Filament Intensity", 0f, 2f, "Brightness of the internal filament layer.");
        DrawField(serialized, "volumeSatelliteWisp", "Satellite Wisp", "Show the additional smaller wisp beside the main crescent.");
        DrawField(serialized, "volumeSheath", "Plasma Sheath", "Show the surrounding plasma layer that adds weight around the travelling tendrils.");
        DrawSlider(serialized, "volumeSheathIntensity", "Plasma Sheath Intensity", 0f, 1.5f, "Strength of the plasma sheath layer.");
        DrawField(serialized, "volumeCounterflow", "Inner Tributary", "Show the secondary flow within the main sweep.");
        DrawSlider(serialized, "volumeCounterflowIntensity", "Inner Tributary Intensity", 0f, 1.5f, "Strength of the inner tributary layer.");
        DrawField(serialized, "volumeWake", "Fine Wake", "Show the finer trail left behind the main tendrils.");
        DrawSlider(serialized, "volumeWakeIntensity", "Fine Wake Intensity", 0f, 1.5f, "Strength of the fine wake layer.");
    }

    private static void DrawSlider(SerializedObject serialized, string name, string label, float min, float max, string tooltip)
    {
        var property = serialized.FindProperty(name);
        if (property != null) EditorGUILayout.Slider(property, min, max, new GUIContent(label, tooltip));
    }

    private static void DrawReferences(SerializedObject serialized)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Player references", EditorStyles.boldLabel);
        DrawField(serialized, "attackController", "Attack Controller", "Reads the existing combo timing and attack direction. This visual component does not start attacks.");
        DrawField(serialized, "previewProfile", "Preview Profile", "Fallback attack profile for edit preview when no controller profile is available.");
        DrawField(serialized, "meleeExtent", "Melee Extent", "The existing sword capsule provides the baseline visual reach and width. No collider is changed.");
        DrawField(serialized, "legacyMeleeParticles", "Legacy Melee Particles", "Original melee renderers hidden by alternate treatments. An empty list uses the existing automatic detection.");
    }

    private static void DrawPrefabEffect(SerializedObject serialized, string fieldName, string stage, string defaultName)
    {
        var settings = serialized.FindProperty(fieldName);
        if (settings == null) return;
        var prefab = settings.FindPropertyRelative("prefab");
        string source = prefab != null && prefab.objectReferenceValue != null ? prefab.objectReferenceValue.name : defaultName;
        EditorGUILayout.LabelField(stage + " — " + source, EditorStyles.boldLabel);
        DrawRelative(settings, "prefab", "Effect Prefab", "Prefab used only for this attack stage. Its presentation follows the existing attack timing.");

        var tint = settings.FindPropertyRelative("tint");
        if (tint != null)
        {
            var rect = EditorGUILayout.GetControlRect();
            var label = new GUIContent("Tint", "Color multiplier for this stage's effect. HDR colors are supported.");
            EditorGUI.BeginProperty(rect, label, tint);
            EditorGUI.BeginChangeCheck();
            Color color = EditorGUI.ColorField(rect, label, tint.colorValue, true, true, true);
            if (EditorGUI.EndChangeCheck()) tint.colorValue = color;
            EditorGUI.EndProperty();
        }
        var intensity = settings.FindPropertyRelative("intensity");
        if (intensity != null)
            EditorGUILayout.Slider(intensity, 0f, 3f, new GUIContent("Intensity", "Brightness multiplier for this stage's effect."));

        DrawRelative(settings, "reachScale", "Reach Scale", "Scale along the attack direction relative to the sword's baseline reach. Visual tuning only; does not change attack range.");
        DrawRelative(settings, "widthScale", "Width Scale", "Scale across the attack direction. Does not change the hitbox width.");
        DrawRelative(settings, "positionOffset", "Position Offset", "Additional placement offset for this source effect, relative to the attack.");
        DrawRelative(settings, "rotationOffset", "Rotation Offset", "Additional source-effect rotation in degrees, relative to the attack direction.");

        EditorGUILayout.LabelField("Optional layers", EditorStyles.miniBoldLabel);
        DrawRelative(settings, "smoke", "Smoke", "Show this source prefab's smoke layer.");
        DrawRelative(settings, "accentParticles", "Accent Particles", "Show this source prefab's extra sparks and particle accents.");
        DrawRelative(settings, "groundImpact", "Ground Impact", "Show this source prefab's ground-impact layer.");
        DrawRelative(settings, "distortion", "Distortion", "Show this source prefab's distortion layer.");

        // Expanded state is editor UI state, not a change to the serialized attack settings.
        settings.isExpanded = EditorGUILayout.Foldout(settings.isExpanded, "Advanced — source timing and calibration", true);
        if (!settings.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.HelpBox("These are timestamps in the source effect, in seconds. They select the part of its animation fitted to the existing attack phases; they do not change damage timing.", MessageType.None);
            DrawRelative(settings, "referenceReach", "Source Reference Reach", "Authored source-effect length used to calibrate it against the melee capsule. Change this when replacing the prefab with one of a different native size.");
            DrawRelative(settings, "sourceStartTime", "Source Start Time", "First source-effect timestamp sampled for this attack, in seconds.");
            DrawRelative(settings, "sourceActiveEndTime", "Source Active End Time", "Source-effect timestamp mapped to the end of the existing attack's active phase, in seconds.");
            DrawRelative(settings, "sourceRecoveryEndTime", "Source Recovery End Time", "Source-effect timestamp mapped to the end of attack recovery, in seconds.");
        }
    }

    private static void DrawField(SerializedObject serialized, string name, string label, string tooltip)
    {
        var property = serialized.FindProperty(name);
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    private static void DrawRelative(SerializedProperty parent, string name, string label, string tooltip)
    {
        var property = parent.FindPropertyRelative(name);
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    private static bool CanPreview(PlayerMeleePlasma plasma)
    {
        return plasma != null && !EditorApplication.isPlayingOrWillChangePlaymode &&
            plasma.isActiveAndEnabled && plasma.gameObject.scene.IsValid() &&
            !EditorUtility.IsPersistent(plasma) && ((int)plasma.visualStyle == 1 || (int)plasma.visualStyle == 3);
    }

    private static void RefreshViews()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
}
