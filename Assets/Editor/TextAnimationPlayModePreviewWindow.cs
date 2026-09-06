#if UNITY_EDITOR
using System;
using Massive.Scoring;
using Massive.TextAnimation;
using UnityEditor;
using UnityEngine;

public sealed class TextAnimationPlayModePreviewWindow : EditorWindow
{
    private TMPTextAnimator _animator;
    private TextAnimationPreset _preset;
    private string _sampleText = "MASSIVE";
    private bool _replaceText = true;
    private float _intensity = 1f;
    private Vector2 _direction = Vector2.right;
    private bool _useAccent;
    private Color _accent = Color.white;
    private EnergyTierVisualController _tierController;
    private float _tierPauseSeconds = 2.6f;
    private ScoreboardManagerScript _scoreboard;
    private float _scoreMorphHoldSeconds = 0.65f;
    private ScoreSphereScript _scoreSphere;
    private ScoreVoidMetaballsVisual _scoreVoid;
    private int _scorePreviewTeamID = 1;
    private EnergyUnit _scoreRangeStartUnit = EnergyUnit.MilliElectronVolt;
    private float _scoreRangeStartProgress;
    private EnergyUnit _scoreRangeEndUnit = EnergyUnit.TeraElectronVolt;
    private float _scoreRangeEndProgress = 1f;
    private float _scoreRangePosition;
    private bool _animateScoreRangeChanges = true;
    private float _scoreRangePlaybackSeconds = 24f;
    private bool _scoreRangePreviewActive;
    private bool _scoreRangePlaybackRunning;
    private double _scoreRangePlaybackStartedAt;
    private double _lastScoreRangePlaybackApplyAt;
    private MatchScoreService _scoreService;
    private int _multiplierPreviewPlayerID;
    private Vector2 _scrollPosition;

    [MenuItem("MASSIVE/Text Animation/Play Mode Preview")]
    private static void Open()
    {
        GetWindow<TextAnimationPlayModePreviewWindow>("Text Animation");
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        if (EditorApplication.isPlaying)
            RestoreIntegratedScorePreview();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.LabelField("Play Mode Preset Preview", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        _animator = (TMPTextAnimator)EditorGUILayout.ObjectField(
            "Animator", _animator, typeof(TMPTextAnimator), true);
        _preset = (TextAnimationPreset)EditorGUILayout.ObjectField(
            "Preset", _preset, typeof(TextAnimationPreset), false);

        _replaceText = EditorGUILayout.Toggle("Replace Target Text", _replaceText);
        if (_replaceText)
            _sampleText = EditorGUILayout.TextField("Sample Text", _sampleText);

        _intensity = Mathf.Max(0f, EditorGUILayout.FloatField("Intensity", _intensity));
        _direction = EditorGUILayout.Vector2Field("Direction", _direction);
        _useAccent = EditorGUILayout.Toggle("Use Accent Color", _useAccent);
        if (_useAccent)
            _accent = EditorGUILayout.ColorField("Accent", _accent);

        EditorGUILayout.Space(8f);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to preview generated TMP mesh animation safely.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(
                   !EditorApplication.isPlaying || _animator == null || _preset == null))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Play", GUILayout.Height(28f)))
            {
                TextAnimationContext context = TextAnimationContext.Default
                    .WithIntensity(_intensity)
                    .WithDirection(_direction);

                if (_useAccent)
                    context = context.WithAccentColor(_accent);

                if (_replaceText)
                    _animator.SetTextAndPlay(_sampleText, _preset, context);
                else
                    _animator.Play(_preset, context);
            }

            if (GUILayout.Button("Complete", GUILayout.Height(28f)))
                _animator.CompleteCurrent();

            if (GUILayout.Button("Stop", GUILayout.Height(28f)))
                _animator.StopAll(restoreBaseline: true, restoreVisibility: true);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("Energy Tier Showcase", EditorStyles.boldLabel);
        _tierController = (EnergyTierVisualController)EditorGUILayout.ObjectField(
            "Tier Controller",
            _tierController,
            typeof(EnergyTierVisualController),
            true);
        _tierPauseSeconds = Mathf.Max(
            0f,
            EditorGUILayout.FloatField("Pause Between Tiers", _tierPauseSeconds));
        float appliedTierPauseSeconds = Mathf.Max(_tierPauseSeconds, 2.6f);
        EditorGUILayout.LabelField(
            "Applied Idle Hold",
            $"{appliedTierPauseSeconds:0.##} s");

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Play meV to TeV", GUILayout.Height(30f)))
            {
                RestoreIntegratedScorePreview();
                ResolveTierController();
                _tierController?.PlayTierTextShowcase(appliedTierPauseSeconds);
            }

            using (new EditorGUI.DisabledScope(_tierController == null))
            {
                if (GUILayout.Button("Stop Showcase", GUILayout.Height(30f)))
                    _tierController.StopTierTextShowcase();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (_tierController == null)
        {
            EditorGUILayout.HelpBox(
                "The showcase auto-finds an active EnergyTierVisualController when Play is pressed.",
                MessageType.None);
        }

        DrawIntegratedScorePreview();
        DrawMultiplierPreview();

        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("Score Digit Morph Showcase", EditorStyles.boldLabel);
        _scoreboard = (ScoreboardManagerScript)EditorGUILayout.ObjectField(
            "Scoreboard",
            _scoreboard,
            typeof(ScoreboardManagerScript),
            true);
        _scoreMorphHoldSeconds = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                "Hold Between Values",
                _scoreMorphHoldSeconds));

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Play 000 to 999", GUILayout.Height(30f)))
            {
                RestoreIntegratedScorePreview();
                ResolveScoreboard();
                _scoreboard?.PlayScoreDigitMorphShowcase(_scoreMorphHoldSeconds);
            }

            using (new EditorGUI.DisabledScope(_scoreboard == null))
            {
                if (GUILayout.Button("Stop / Restore", GUILayout.Height(30f)))
                    _scoreboard.StopScoreDigitMorphShowcase();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (_scoreboard == null)
        {
            EditorGUILayout.HelpBox(
                "The showcase auto-finds an active ScoreboardManagerScript when Play is pressed.",
                MessageType.None);
        }


        EditorGUILayout.EndScrollView();
    }

    private void DrawMultiplierPreview()
    {
        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("Multiplier Integration Preview", EditorStyles.boldLabel);
        _scoreService = (MatchScoreService)EditorGUILayout.ObjectField(
            "Score Service", _scoreService, typeof(MatchScoreService), true);
        _multiplierPreviewPlayerID = Mathf.Max(
            0,
            EditorGUILayout.IntField("Player ID", _multiplierPreviewPlayerID));

        PlayerScoreChain chain = _scoreService != null
            ? _scoreService.GetPlayerChain(_multiplierPreviewPlayerID)
            : null;
        int teamID = chain != null && chain.Player != null ? chain.Player.teamID : 0;
        if (chain != null)
        {
            EditorGUILayout.LabelField(
                "Personal",
                $"x{chain.CurrentMultiplier}  {chain.Progress01:P0} to next tier");
            EditorGUILayout.LabelField(
                "Team Amplifier",
                teamID > 0 ? $"x{_scoreService.GetTeamAmplifierMultiplier(teamID)}" : "—");
        }

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Personal Charge", GUILayout.Height(28f)))
            {
                ResolveScoreService();
                _scoreService?.GetPlayerChain(_multiplierPreviewPlayerID)?.ApplyAward(
                    ScoreChainAwardMode.AdvanceAndRefresh,
                    1f);
            }

            if (GUILayout.Button("Advance Team Amplifier", GUILayout.Height(28f)))
            {
                ResolveScoreService();
                PlayerScoreChain selected = _scoreService?.GetPlayerChain(
                    _multiplierPreviewPlayerID);
                int selectedTeam = selected != null && selected.Player != null
                    ? selected.Player.teamID
                    : _scorePreviewTeamID;
                _scoreService?.AdvanceTeamAmplifier(selectedTeam);
            }

            if (GUILayout.Button("Reset Multipliers", GUILayout.Height(28f)))
            {
                ResolveScoreService();
                _scoreService?.ResetMultipliers();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to exercise player charge and the shared team Amplifier.",
                MessageType.Info);
        }
    }

    private void DrawIntegratedScorePreview()
    {
        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField(
            "Integrated Score / Tier / Sphere Preview",
            EditorStyles.boldLabel);

        int selectedTeam = EditorGUILayout.IntPopup(
            "Team",
            _scorePreviewTeamID,
            new[] { "Light", "Dark" },
            new[] { 1, 2 });

        if (selectedTeam != _scorePreviewTeamID)
        {
            RestoreIntegratedScorePreview();
            _scorePreviewTeamID = selectedTeam;
            _scoreboard = null;
            _scoreSphere = null;
            _scoreVoid = null;
        }

        ScoreboardManagerScript nextScoreboard =
            (ScoreboardManagerScript)EditorGUILayout.ObjectField(
                "Scoreboard", _scoreboard, typeof(ScoreboardManagerScript), true);
        ScoreSphereScript nextSphere =
            (ScoreSphereScript)EditorGUILayout.ObjectField(
                "Sphere Scale Driver", _scoreSphere, typeof(ScoreSphereScript), true);
        ScoreVoidMetaballsVisual nextVoid =
            (ScoreVoidMetaballsVisual)EditorGUILayout.ObjectField(
                "Metaball Sphere", _scoreVoid, typeof(ScoreVoidMetaballsVisual), true);

        if (nextScoreboard != _scoreboard || nextSphere != _scoreSphere || nextVoid != _scoreVoid)
        {
            RestoreIntegratedScorePreview();
            _scoreboard = nextScoreboard;
            _scoreSphere = nextSphere;
            _scoreVoid = nextVoid;
        }

        EditorGUILayout.Space(3f);
        _scoreRangeStartUnit = (EnergyUnit)EditorGUILayout.EnumPopup(
            "Range Start Tier", _scoreRangeStartUnit);
        _scoreRangeStartProgress = EditorGUILayout.Slider(
            "Start Within Tier", _scoreRangeStartProgress, 0f, 1f);
        _scoreRangeEndUnit = (EnergyUnit)EditorGUILayout.EnumPopup(
            "Range End Tier", _scoreRangeEndUnit);
        _scoreRangeEndProgress = EditorGUILayout.Slider(
            "End Within Tier", _scoreRangeEndProgress, 0f, 1f);
        NormalizeScoreRange();

        _animateScoreRangeChanges = EditorGUILayout.Toggle(
            "Animate Changes", _animateScoreRangeChanges);
        _scoreRangePlaybackSeconds = Mathf.Max(
            0.5f,
            EditorGUILayout.FloatField(
                "Play Range Seconds", _scoreRangePlaybackSeconds));

        EditorGUI.BeginChangeCheck();
        float nextPosition = EditorGUILayout.Slider(
            "Score Scrubber", _scoreRangePosition, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            _scoreRangePlaybackRunning = false;
            _scoreRangePosition = nextPosition;

            if (EditorApplication.isPlaying)
                ApplyIntegratedScorePreview(_animateScoreRangeChanges);
        }

        long previewScore = GetScoreRangeValue(_scoreRangePosition);
        EnergyDisplayValue display = EnergyScoreFormatter.GetDisplayValue(previewScore);
        EditorGUILayout.LabelField(
            "Preview Value",
            $"{EnergyScoreFormatter.FormatWithUnit(previewScore, spaceCharacters: false)}  " +
            $"({display.tierProgress01:P0} through {display.unitLabel})");
        EditorGUILayout.LabelField("Raw meV", previewScore.ToString("N0"));

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Apply Scrubber", GUILayout.Height(30f)))
                ApplyIntegratedScorePreview(_animateScoreRangeChanges);

            if (GUILayout.Button(
                    _scoreRangePlaybackRunning ? "Pause Range" : "Play Range",
                    GUILayout.Height(30f)))
            {
                if (_scoreRangePlaybackRunning)
                    _scoreRangePlaybackRunning = false;
                else
                    StartScoreRangePlayback();
            }

            using (new EditorGUI.DisabledScope(!_scoreRangePreviewActive))
            {
                if (GUILayout.Button("Restore Live", GUILayout.Height(30f)))
                    RestoreIntegratedScorePreview();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to drive the live score number, tier effects, and both score-sphere layers together.",
                MessageType.Info);
        }
        else if (_scoreboard == null || _scoreSphere == null || _scoreVoid == null)
        {
            EditorGUILayout.HelpBox(
                "Missing references are auto-resolved by team when Apply Scrubber or Play Range is pressed.",
                MessageType.None);
        }
    }

    private void StartScoreRangePlayback()
    {
        ResolveIntegratedScoreTargets();
        _scoreRangePosition = 0f;
        ApplyIntegratedScorePreview(animateChanges: false);
        _scoreRangePlaybackRunning = true;
        _scoreRangePlaybackStartedAt = EditorApplication.timeSinceStartup;
        _lastScoreRangePlaybackApplyAt = _scoreRangePlaybackStartedAt;
    }

    private void OnEditorUpdate()
    {
        if (!_scoreRangePlaybackRunning)
            return;

        if (!EditorApplication.isPlaying)
        {
            _scoreRangePlaybackRunning = false;
            _scoreRangePreviewActive = false;
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float elapsed = (float)(now - _scoreRangePlaybackStartedAt);
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.5f, _scoreRangePlaybackSeconds));
        _scoreRangePosition = t;

        const double minimumApplyInterval = 0.20;
        if (t >= 1f || now - _lastScoreRangePlaybackApplyAt >= minimumApplyInterval)
        {
            _lastScoreRangePlaybackApplyAt = now;
            ApplyIntegratedScorePreview(animateChanges: true);
        }

        if (t >= 1f)
            _scoreRangePlaybackRunning = false;

        Repaint();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            _scoreRangePlaybackRunning = false;
            _scoreRangePreviewActive = false;
        }
    }

    private void ApplyIntegratedScorePreview(bool animateChanges)
    {
        if (!EditorApplication.isPlaying)
            return;

        ResolveIntegratedScoreTargets();
        long previewScore = GetScoreRangeValue(_scoreRangePosition);

        _tierController?.StopTierTextShowcase();
        _scoreboard?.BeginScorePresentationPreview();
        _scoreSphere?.BeginScorePresentationPreview();
        _scoreVoid?.BeginScorePresentationPreview();

        _scoreboard?.PreviewScorePresentation(previewScore, animateChanges);
        _scoreSphere?.PreviewScorePresentation(previewScore, animateChanges);
        _scoreVoid?.PreviewScorePresentation(previewScore, animateChanges);

        _scoreRangePreviewActive =
            _scoreboard != null || _scoreSphere != null || _scoreVoid != null;
    }

    private void RestoreIntegratedScorePreview()
    {
        _scoreRangePlaybackRunning = false;

        if (!_scoreRangePreviewActive)
            return;

        _scoreboard?.EndScorePresentationPreview();
        _scoreSphere?.EndScorePresentationPreview();
        _scoreVoid?.EndScorePresentationPreview();
        _scoreRangePreviewActive = false;
    }

    private void ResolveIntegratedScoreTargets()
    {
        ResolveScoreboard();
        if (_scoreboard != null && _scoreboard.TeamID != _scorePreviewTeamID)
            _scoreboard = FindScoreboardForTeam(_scorePreviewTeamID);

        if (_scoreSphere == null || _scoreSphere.teamID != _scorePreviewTeamID)
        {
            ScoreSphereScript[] spheres = FindObjectsByType<ScoreSphereScript>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            _scoreSphere = null;
            for (int i = 0; i < spheres.Length; i++)
            {
                if (spheres[i] != null && spheres[i].teamID == _scorePreviewTeamID)
                {
                    _scoreSphere = spheres[i];
                    break;
                }
            }
        }

        if (_scoreVoid == null || _scoreVoid.TeamID != _scorePreviewTeamID)
        {
            ScoreVoidMetaballsVisual[] voids =
                FindObjectsByType<ScoreVoidMetaballsVisual>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            _scoreVoid = null;
            for (int i = 0; i < voids.Length; i++)
            {
                if (voids[i] != null && voids[i].TeamID == _scorePreviewTeamID)
                {
                    _scoreVoid = voids[i];
                    break;
                }
            }
        }
    }

    private ScoreboardManagerScript FindScoreboardForTeam(int teamID)
    {
        ScoreboardManagerScript[] scoreboards =
            FindObjectsByType<ScoreboardManagerScript>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        for (int i = 0; i < scoreboards.Length; i++)
        {
            if (scoreboards[i] != null && scoreboards[i].TeamID == teamID)
                return scoreboards[i];
        }

        return null;
    }

    private void NormalizeScoreRange()
    {
        float start = GetTierCoordinate(
            _scoreRangeStartUnit,
            _scoreRangeStartProgress);
        float end = GetTierCoordinate(
            _scoreRangeEndUnit,
            _scoreRangeEndProgress);

        if (end >= start)
            return;

        _scoreRangeEndUnit = _scoreRangeStartUnit;
        _scoreRangeEndProgress = _scoreRangeStartProgress;
    }

    private long GetScoreRangeValue(float rangePosition)
    {
        float start = GetTierCoordinate(
            _scoreRangeStartUnit,
            _scoreRangeStartProgress);
        float end = GetTierCoordinate(
            _scoreRangeEndUnit,
            _scoreRangeEndProgress);
        return ScoreFromTierCoordinate(Mathf.Lerp(start, end, Mathf.Clamp01(rangePosition)));
    }

    private static float GetTierCoordinate(EnergyUnit unit, float progress01)
    {
        int index = Mathf.Clamp((int)unit, 0, EnergyScoreMath.UnitCount - 1);
        return index + Mathf.Clamp01(progress01);
    }

    private static long ScoreFromTierCoordinate(float coordinate)
    {
        float maximumCoordinate = EnergyScoreMath.UnitCount;
        float clamped = Mathf.Clamp(coordinate, 0f, maximumCoordinate);
        int unitIndex;
        float progress;

        if (clamped >= maximumCoordinate)
        {
            unitIndex = EnergyScoreMath.UnitCount - 1;
            progress = 1f;
        }
        else
        {
            unitIndex = Mathf.Clamp(
                Mathf.FloorToInt(clamped),
                0,
                EnergyScoreMath.UnitCount - 1);
            progress = clamped - unitIndex;
        }

        EnergyUnit unit = (EnergyUnit)unitIndex;
        long factor = EnergyScoreMath.GetFactor(unit);
        long tierStart = unit == EnergyUnit.MilliElectronVolt ? 0L : factor;
        long nextTierStart = EnergyScoreMath.SaturatingMultiply(factor, 1_000L);
        long tierEnd = nextTierStart == long.MaxValue
            ? long.MaxValue
            : Math.Max(tierStart, nextTierStart - 1L);
        double raw = tierStart + (tierEnd - tierStart) * (double)progress;
        return (long)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    private void ResolveTierController()
    {
        if (_tierController != null)
            return;

        _tierController = FindFirstObjectByType<EnergyTierVisualController>(
            FindObjectsInactive.Exclude);
    }

    private void ResolveScoreService()
    {
        if (_scoreService == null)
            _scoreService = FindFirstObjectByType<MatchScoreService>(FindObjectsInactive.Exclude);
    }

    private void ResolveScoreboard()
    {
        if (_scoreboard != null && _scoreboard.TeamID == _scorePreviewTeamID)
            return;

        _scoreboard = FindScoreboardForTeam(_scorePreviewTeamID);
    }
}
#endif
