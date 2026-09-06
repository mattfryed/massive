#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Scoring;
using Massive.TextAnimation;
using UnityEditor;
using UnityEngine;

public static class MassiveTextAnimationStarterLibrary
{
    private const string RootFolder = "Assets/massive/Text Animation";
    private const string PresetFolder = RootFolder + "/Presets";
    private const string SequenceFolder = RootFolder + "/Sequences";
    private const string GameplayScoreboardPrefab = "Assets/UI/UI.prefab";

    [MenuItem("MASSIVE/Text Animation/Create or Refresh Suggested Preset Library")]
    public static void CreateOrRefreshLibrary()
    {
        EnsureFolder("Assets", "massive");
        EnsureFolder("Assets/massive", "Text Animation");
        EnsureFolder(RootFolder, "Presets");
        EnsureFolder(RootFolder, "Sequences");

        TextAnimationPreset calibrate = CreatePreset("CALIBRATE_IN", ConfigureCalibrate);
        TextAnimationPreset phaseLock = CreatePreset("PHASE_LOCK_IN", ConfigurePhaseLock);
        TextAnimationPreset vectorSweep = CreatePreset("VECTOR_SWEEP_IN", ConfigureVectorSweep);
        TextAnimationPreset pulseStamp = CreatePreset("PULSE_STAMP", ConfigurePulseStamp);
        TextAnimationPreset ionize = CreatePreset("IONIZE", ConfigureIonize);
        TextAnimationPreset fieldShear = CreatePreset("FIELD_SHEAR", ConfigureFieldShear);
        TextAnimationPreset signalBreak = CreatePreset("SIGNAL_BREAK_OUT", ConfigureSignalBreak);
        TextAnimationPreset overload = CreatePreset("OVERLOAD", ConfigureOverload);
        TextAnimationPreset digitStep = CreatePreset("DIGIT_STEP", ConfigureDigitStep);
        CreatePreset("DIGIT_MORPH", ConfigureDigitMorph);
        TextAnimationPreset countdown = CreatePreset("COUNTDOWN_IMPULSE", ConfigureCountdownImpulse);
        CreateSuggestedEnergyTierPromotionPresets();
        CreateSuggestedEnergyTierLoopPresets();

        TextAnimationSequence levelOut = CreateSequence(
            "LEVEL_SELECT_OUT",
            sequence => ConfigureLevelSelectOut(sequence, signalBreak));
        TextAnimationSequence levelIn = CreateSequence(
            "LEVEL_SELECT_IN",
            sequence => ConfigureLevelSelectIn(
                sequence,
                calibrate,
                phaseLock,
                vectorSweep));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = levelIn != null ? levelIn : phaseLock;
        EditorGUIUtility.PingObject(Selection.activeObject);

        Debug.Log(
            "[MASSIVE Text Animation] Suggested library was created or refreshed at '" +
            RootFolder + "'. Duplicate tuned assets before running this command again.");
    }

    [MenuItem("MASSIVE/Text Animation/Refresh Suggested Energy Tier Presets")]
    public static void RefreshSuggestedEnergyTierPresets()
    {
        EnsureFolder("Assets", "massive");
        EnsureFolder("Assets/massive", "Text Animation");
        EnsureFolder(RootFolder, "Presets");

        TextAnimationPreset[] promotions =
            CreateSuggestedEnergyTierPromotionPresets();
        CreateSuggestedEnergyTierLoopPresets();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = promotions != null && promotions.Length > 0
            ? promotions[promotions.Length - 1]
            : null;
        EditorGUIUtility.PingObject(Selection.activeObject);

        Debug.Log(
            "[MASSIVE Text Animation] Suggested energy-tier presets were " +
            "created or refreshed without changing Level Select assets.");
    }

    // Preserve the first package draft's public entry point for editor scripts
    // that may already call it directly.
    public static void CreateLibrary()
    {
        CreateOrRefreshLibrary();
    }

    [MenuItem("MASSIVE/Text Animation/Refresh Suggested Score Digit Morph")]
    public static void RefreshSuggestedScoreDigitMorph()
    {
        EnsureFolder("Assets", "massive");
        EnsureFolder("Assets/massive", "Text Animation");
        EnsureFolder(RootFolder, "Presets");

        TextAnimationPreset preset =
            CreatePreset("DIGIT_MORPH", ConfigureDigitMorph);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);

        Debug.Log(
            "[MASSIVE Text Animation] Suggested score digit morph was " +
            "created or refreshed without changing tier or Level Select presets.");
    }

    [MenuItem("MASSIVE/Text Animation/Assign Suggested Digit Morph To Gameplay Scoreboards")]
    public static void AssignSuggestedDigitMorphToGameplayScoreboards()
    {
        TextAnimationPreset preset = AssetDatabase.LoadAssetAtPath<TextAnimationPreset>(
            $"{PresetFolder}/DIGIT_MORPH.asset");
        if (preset == null)
        {
            EditorUtility.DisplayDialog(
                "Suggested Preset Not Found",
                "Refresh the suggested score digit morph before assigning it.",
                "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(GameplayScoreboardPrefab);
        if (root == null)
            return;

        try
        {
            ScoreboardManagerScript[] scoreboards =
                root.GetComponentsInChildren<ScoreboardManagerScript>(true);
            for (int i = 0; i < scoreboards.Length; i++)
            {
                scoreboards[i].SetScoreDigitMorphPreset(preset);
                EditorUtility.SetDirty(scoreboards[i]);
            }

            PrefabUtility.SaveAsPrefabAsset(root, GameplayScoreboardPrefab);
            Debug.Log(
                $"[MASSIVE Text Animation] Assigned DIGIT_MORPH to " +
                $"{scoreboards.Length} scoreboard(s) in '{GameplayScoreboardPrefab}'.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);
    }

    [MenuItem("MASSIVE/Text Animation/Assign Suggested Presets To Selected Energy Tier Profile")]
    public static void AssignSuggestedTierPresets()
    {
        EnergyTierVisualProfile profile = Selection.activeObject as EnergyTierVisualProfile;
        if (profile == null)
            return;

        EnergyUnit[] units =
        {
            EnergyUnit.MilliElectronVolt,
            EnergyUnit.ElectronVolt,
            EnergyUnit.KiloElectronVolt,
            EnergyUnit.MegaElectronVolt,
            EnergyUnit.GigaElectronVolt,
            EnergyUnit.TeraElectronVolt
        };

        string[] promotionPresetNames =
        {
            "MILLI_TIER_PROMOTION",
            "ELECTRON_TIER_PROMOTION",
            "KILO_TIER_PROMOTION",
            "MEGA_TIER_PROMOTION",
            "GIGA_TIER_PROMOTION",
            "TERA_TIER_PROMOTION"
        };

        string[] activeLoopPresetNames =
        {
            "MILLI_ACTIVE_LOOP",
            "ELECTRON_ACTIVE_LOOP",
            "KILO_ACTIVE_LOOP",
            "MEGA_ACTIVE_LOOP",
            "GIGA_ACTIVE_LOOP",
            "TERA_ACTIVE_LOOP"
        };

        TextAnimationPreset[] promotionPresets =
            LoadPresetSet(promotionPresetNames);
        TextAnimationPreset[] activeLoopPresets =
            LoadPresetSet(activeLoopPresetNames);
        if (promotionPresets == null || activeLoopPresets == null)
            return;

        float[] activeLoopIntensities = { 0.35f, 0.45f, 0.55f, 0.65f, 0.78f, 0.90f };
        float[] activeFontSizeMultipliers = { 1.00f, 1.05f, 1.10f, 1.16f, 1.23f, 1.31f };

        Undo.RecordObject(profile, "Assign Suggested Energy Tier Text Presets");

        for (int i = 0; i < units.Length; i++)
        {
            EnergyTierVisualProfile.TierStyle style = profile.GetStyle(units[i]);
            if (style == null)
            {
                Debug.LogWarning(
                    $"[MASSIVE Text Animation] '{profile.name}' has no row for " +
                    $"{units[i]}; its suggested preset was not assigned.",
                    profile);
                continue;
            }

            style.promotionTextPreset = promotionPresets[i];
            style.promotionTextPresetReplacesGenericMotion = true;
            if (style.promotionTextIntensity <= 0f)
                style.promotionTextIntensity = 1f;
            if (style.promotionTextDirection.sqrMagnitude < 0.000001f)
                style.promotionTextDirection = Vector2.right;
            style.useTierColorAsAnimationAccent = true;
            style.activeIndicatorFontSizeMultiplier = activeFontSizeMultipliers[i];
            style.activeLoopTextPreset = activeLoopPresets[i];
            style.activeLoopTextIntensity = activeLoopIntensities[i];
            style.activeLoopTextDirection = Vector2.right;
            style.useTierColorAsActiveLoopAccent = true;
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(profile);

        Debug.Log(
            $"[MASSIVE Text Animation] Assigned suggested promotion and active-loop presets to " +
            $"'{profile.name}'.",
            profile);
    }

    [MenuItem(
        "MASSIVE/Text Animation/Assign Suggested Presets To Selected Energy Tier Profile",
        true)]
    private static bool CanAssignSuggestedTierPresets()
    {
        return Selection.activeObject is EnergyTierVisualProfile;
    }

    private static TextAnimationPreset[] LoadPresetSet(string[] presetNames)
    {
        TextAnimationPreset[] presets = new TextAnimationPreset[presetNames.Length];
        for (int i = 0; i < presetNames.Length; i++)
        {
            string path = $"{PresetFolder}/{presetNames[i]}.asset";
            presets[i] = AssetDatabase.LoadAssetAtPath<TextAnimationPreset>(path);
            if (presets[i] != null)
                continue;

            EditorUtility.DisplayDialog(
                "Suggested Presets Not Found",
                "Create or refresh the suggested preset library before " +
                "assigning it to an energy-tier profile.",
                "OK");
            return null;
        }

        return presets;
    }

    private static void CreateSuggestedEnergyTierLoopPresets()
    {
        CreatePreset("MILLI_ACTIVE_LOOP", ConfigureMilliActiveLoop);
        CreatePreset("ELECTRON_ACTIVE_LOOP", ConfigureElectronActiveLoop);
        CreatePreset("KILO_ACTIVE_LOOP", ConfigureKiloActiveLoop);
        CreatePreset("MEGA_ACTIVE_LOOP", ConfigureMegaActiveLoop);
        CreatePreset("GIGA_ACTIVE_LOOP", ConfigureGigaActiveLoop);
        CreatePreset("TERA_ACTIVE_LOOP", ConfigureTeraActiveLoop);
    }

    private static TextAnimationPreset[] CreateSuggestedEnergyTierPromotionPresets()
    {
        return new[]
        {
            CreatePreset("MILLI_TIER_PROMOTION", ConfigureMilliTierPromotion),
            CreatePreset("ELECTRON_TIER_PROMOTION", ConfigureElectronTierPromotion),
            CreatePreset("KILO_TIER_PROMOTION", ConfigureKiloTierPromotion),
            CreatePreset("MEGA_TIER_PROMOTION", ConfigureMegaTierPromotion),
            CreatePreset("GIGA_TIER_PROMOTION", ConfigureGigaTierPromotion),
            CreatePreset("TERA_TIER_PROMOTION", ConfigureTeraTierPromotion)
        };
    }

    private static void ConfigureMilliTierPromotion(TextAnimationPreset preset)
    {
        ConfigureCalibrate(preset);
        SetTierScalePeak(preset, 1.06f);
    }

    private static void ConfigureElectronTierPromotion(TextAnimationPreset preset)
    {
        ConfigurePhaseLock(preset);
        SetTierScalePeak(preset, 1.07f);
    }

    private static void ConfigureKiloTierPromotion(TextAnimationPreset preset)
    {
        ConfigureVectorSweep(preset);
        SetTierScalePeak(preset, 1.08f);
    }

    private static void ConfigureMegaTierPromotion(TextAnimationPreset preset)
    {
        ConfigurePulseStamp(preset);
        SetTierScalePeak(preset, 1.09f);
    }

    private static void ConfigureGigaTierPromotion(TextAnimationPreset preset)
    {
        ConfigureFieldShear(preset);
        SetTierScalePeak(preset, 1.10f);
    }

    private static void ConfigureTeraTierPromotion(TextAnimationPreset preset)
    {
        ConfigureOverload(preset);
        SetTierScalePeak(preset, 1.12f);
    }

    private static void SetTierScalePeak(
        TextAnimationPreset preset,
        float scaleAtPeak)
    {
        RootMotionTextAnimationModule rootMotion = null;
        for (int i = 0; i < preset.Modules.Count; i++)
        {
            if (preset.Modules[i] is RootMotionTextAnimationModule existing)
            {
                rootMotion = existing;
                break;
            }
        }

        if (rootMotion == null)
        {
            rootMotion = new RootMotionTextAnimationModule
            {
                curve = TextAnimationMath.Pulse01()
            };
            preset.Modules.Add(rootMotion);
        }

        float safeScale = Mathf.Max(1f, scaleAtPeak);
        rootMotion.scaleAtPeak = new Vector2(safeScale, safeScale);
    }

    private static TextAnimationPreset CreatePreset(
        string fileName,
        Action<TextAnimationPreset> configure)
    {
        string path = $"{PresetFolder}/{fileName}.asset";
        TextAnimationPreset existing =
            AssetDatabase.LoadAssetAtPath<TextAnimationPreset>(path);
        if (existing != null)
        {
            configure?.Invoke(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        TextAnimationPreset preset = ScriptableObject.CreateInstance<TextAnimationPreset>();
        preset.name = fileName;
        configure?.Invoke(preset);
        AssetDatabase.CreateAsset(preset, path);
        EditorUtility.SetDirty(preset);
        return preset;
    }

    private static TextAnimationSequence CreateSequence(
        string fileName,
        Action<TextAnimationSequence> configure)
    {
        string path = $"{SequenceFolder}/{fileName}.asset";
        TextAnimationSequence existing =
            AssetDatabase.LoadAssetAtPath<TextAnimationSequence>(path);
        if (existing != null)
        {
            configure?.Invoke(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        TextAnimationSequence sequence =
            ScriptableObject.CreateInstance<TextAnimationSequence>();
        sequence.name = fileName;
        configure?.Invoke(sequence);
        AssetDatabase.CreateAsset(sequence, path);
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static void ConfigureBase(
        TextAnimationPreset preset,
        float duration,
        float stagger,
        TextAnimationOrder order,
        TextAnimationCompletionMode completion = TextAnimationCompletionMode.RestoreBaseline,
        TextAnimationCharacterSelection selection = TextAnimationCharacterSelection.AllVisible)
    {
        preset.delaySeconds = 0f;
        preset.durationSeconds = duration;
        preset.characterStaggerSeconds = stagger;
        preset.timeMode = TextAnimationTimeMode.Unscaled;
        preset.characterSelection = selection;
        preset.characterOrder = order;
        preset.deterministicSeed = 1337;
        preset.playbackMode = TextAnimationPlaybackMode.OneShot;
        preset.interruption = TextAnimationInterruption.Replace;
        preset.completion = completion;
        preset.playObjectModulesWhenNoCharacters = true;
        preset.markers = new List<TextAnimationMarker>();
        preset.Modules.Clear();
    }

    private static void ConfigureActiveLoopBase(
        TextAnimationPreset preset,
        float duration)
    {
        ConfigureBase(preset, duration, 0f, TextAnimationOrder.Forward);
        preset.playbackMode = TextAnimationPlaybackMode.Loop;
    }

    private static SdfTextAnimationModule ActiveGlow(
        float outer,
        float power)
    {
        return new SdfTextAnimationModule
        {
            glowOuterFromOffset = 0f,
            glowOuterToOffset = outer,
            glowPowerFromOffset = 0f,
            glowPowerToOffset = power,
            forceGlowWhilePlaying = true,
            useContextAccentForGlow = true,
            glowAccentWeight = 1f,
            curve = TextAnimationMath.Pulse01()
        };
    }

    private static void ConfigureMilliActiveLoop(TextAnimationPreset preset)
    {
        ConfigureActiveLoopBase(preset, 3.0f);
        preset.Modules.Add(ActiveGlow(0.035f, 0.025f));
    }

    private static void ConfigureElectronActiveLoop(TextAnimationPreset preset)
    {
        ConfigureActiveLoopBase(preset, 2.6f);
        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 0.25f),
            rotationAmplitudeDegrees = 0.10f,
            scaleAmplitude = new Vector2(0.005f, 0.007f),
            temporalCycles = 1f,
            characterPhaseDegrees = 12f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(ActiveGlow(0.05f, 0.035f));
    }

    private static void ConfigureKiloActiveLoop(TextAnimationPreset preset)
    {
        ConfigureActiveLoopBase(preset, 2.25f);
        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 0.40f),
            rotationAmplitudeDegrees = 0.18f,
            scaleAmplitude = new Vector2(0.007f, 0.010f),
            temporalCycles = 1f,
            characterPhaseDegrees = 18f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 1,
            spacing = 0.8f,
            maximumOpacity = 0.07f,
            opacityFalloff = 0.45f,
            spreadFrom = 0.12f,
            spreadTo = 0.42f,
            useContextDirection = true,
            useContextAccentColor = true,
            curve = TextAnimationMath.Pulse01()
        });
    }

    private static void ConfigureMegaActiveLoop(TextAnimationPreset preset)
    {
        ConfigureActiveLoopBase(preset, 2.0f);
        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 0.50f),
            rotationAmplitudeDegrees = 0.22f,
            scaleAmplitude = new Vector2(0.009f, 0.012f),
            temporalCycles = 1f,
            characterPhaseDegrees = 22f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new RootMotionTextAnimationModule
        {
            scaleAtPeak = new Vector2(1.015f, 1.015f),
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(ActiveGlow(0.07f, 0.05f));
    }

    private static void ConfigureGigaActiveLoop(TextAnimationPreset preset)
    {
        ConfigureActiveLoopBase(preset, 1.75f);
        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 0.75f),
            rotationAmplitudeDegrees = 0.38f,
            scaleAmplitude = new Vector2(0.012f, 0.016f),
            shearAmplitude = 0.018f,
            temporalCycles = 1.15f,
            characterPhaseDegrees = 28f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new NoiseTextAnimationModule
        {
            positionAmplitude = new Vector2(0.18f, 0.12f),
            rotationAmplitudeDegrees = 0.07f,
            frequency = 8f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 2,
            spacing = 0.95f,
            maximumOpacity = 0.09f,
            opacityFalloff = 0.48f,
            spreadFrom = 0.10f,
            spreadTo = 0.48f,
            useContextDirection = true,
            useContextAccentColor = true,
            scaleStepPerCopy = -0.008f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(ActiveGlow(0.09f, 0.065f));
    }

    private static void ConfigureTeraActiveLoop(TextAnimationPreset preset)
    {
        ConfigureActiveLoopBase(preset, 1.5f);
        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 0.95f),
            rotationAmplitudeDegrees = 0.52f,
            scaleAmplitude = new Vector2(0.016f, 0.020f),
            shearAmplitude = 0.028f,
            temporalCycles = 1.30f,
            characterPhaseDegrees = 34f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new NoiseTextAnimationModule
        {
            positionAmplitude = new Vector2(0.30f, 0.18f),
            rotationAmplitudeDegrees = 0.14f,
            scaleAmplitude = new Vector2(0.003f, 0.003f),
            shearAmplitude = 0.006f,
            frequency = 10f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new RootMotionTextAnimationModule
        {
            scaleAtPeak = new Vector2(1.02f, 1.02f),
            shakePositionAmplitude = new Vector2(0.12f, 0.08f),
            shakeRotationAmplitudeDegrees = 0.08f,
            shakeFrequency = 8f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 3,
            spacing = 1.10f,
            maximumOpacity = 0.12f,
            opacityFalloff = 0.52f,
            spreadFrom = 0.08f,
            spreadTo = 0.52f,
            useContextDirection = true,
            useContextAccentColor = true,
            scaleStepPerCopy = -0.012f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(ActiveGlow(0.12f, 0.085f));
    }

    private static void ConfigureCalibrate(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.26f, 0.010f, TextAnimationOrder.Forward);

        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            positionFrom = new Vector2(0f, -5f),
            positionTo = Vector2.zero,
            scaleFrom = new Vector2(0.94f, 0.94f),
            scaleTo = Vector2.one,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new TrackingTextAnimationModule
        {
            fromTracking = 7f,
            toTracking = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            faceDilateFromOffset = -0.10f,
            faceDilateToOffset = 0f,
            softnessFromOffset = 0.04f,
            softnessToOffset = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
    }

    private static void ConfigurePhaseLock(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.34f, 0.018f, TextAnimationOrder.CenterOut);

        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            addContextDirection = true,
            directionDistanceFrom = -14f,
            directionDistanceTo = 0f,
            scaleFrom = new Vector2(0.78f, 1.08f),
            scaleTo = Vector2.one,
            shearXFrom = -0.20f,
            shearXTo = 0f,
            curve = TextAnimationMath.Overshoot01()
        });
        preset.Modules.Add(new TrackingTextAnimationModule
        {
            fromTracking = 14f,
            toTracking = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            faceDilateFromOffset = -0.16f,
            faceDilateToOffset = 0f,
            outlineWidthFromOffset = 0.10f,
            outlineWidthToOffset = 0f,
            glowOuterFromOffset = 0.22f,
            glowOuterToOffset = 0f,
            glowPowerFromOffset = 0.18f,
            glowPowerToOffset = 0f,
            forceGlowWhilePlaying = true,
            useContextAccentForOutline = true,
            outlineAccentWeight = 1f,
            useContextAccentForGlow = true,
            glowAccentWeight = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
    }

    private static void ConfigureVectorSweep(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.30f, 0.014f, TextAnimationOrder.Forward);

        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            addContextDirection = true,
            directionDistanceFrom = -24f,
            directionDistanceTo = 0f,
            scaleFrom = new Vector2(0.90f, 1f),
            scaleTo = Vector2.one,
            shearXFrom = 0.38f,
            shearXTo = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new TrackingTextAnimationModule
        {
            fromTracking = 5f,
            toTracking = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            faceDilateFromOffset = -0.12f,
            faceDilateToOffset = 0f,
            glowOuterFromOffset = 0.12f,
            glowOuterToOffset = 0f,
            forceGlowWhilePlaying = true,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 2,
            spacing = 1.25f,
            maximumOpacity = 0.14f,
            opacityFalloff = 0.45f,
            spreadFrom = 0.20f,
            spreadTo = 0.55f,
            useContextDirection = true,
            useContextAccentColor = true,
            curve = TextAnimationMath.Pulse01()
        });
    }

    private static void ConfigurePulseStamp(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.28f, 0.003f, TextAnimationOrder.CenterOut);

        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0.45f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            scaleFrom = new Vector2(1.42f, 1.42f),
            scaleTo = Vector2.one,
            curve = TextAnimationMath.Overshoot01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            outlineWidthFromOffset = 0.18f,
            outlineWidthToOffset = 0f,
            glowOuterFromOffset = 0.28f,
            glowOuterToOffset = 0f,
            glowPowerFromOffset = 0.25f,
            glowPowerToOffset = 0f,
            forceGlowWhilePlaying = true,
            useContextAccentForOutline = true,
            outlineAccentWeight = 1f,
            useContextAccentForGlow = true,
            glowAccentWeight = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new RootMotionTextAnimationModule
        {
            scaleAtPeak = new Vector2(1.08f, 1.08f),
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 3,
            spacing = 1.40f,
            maximumOpacity = 0.18f,
            opacityFalloff = 0.48f,
            spreadFrom = 0.18f,
            spreadTo = 0.62f,
            useContextDirection = true,
            useContextAccentColor = true,
            curve = TextAnimationMath.Pulse01()
        });
        preset.markers.Add(new TextAnimationMarker
        {
            normalizedTime = 0.22f,
            id = "IMPACT"
        });
    }

    private static void ConfigureIonize(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.40f, 0.010f, TextAnimationOrder.DeterministicRandom);

        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            scaleFrom = new Vector2(0.62f, 0.62f),
            scaleTo = Vector2.one,
            shearXFrom = 0.28f,
            shearXTo = 0f,
            curve = TextAnimationMath.Overshoot01()
        });
        preset.Modules.Add(new NoiseTextAnimationModule
        {
            positionAmplitude = new Vector2(10f, 7f),
            rotationAmplitudeDegrees = 5f,
            scaleAmplitude = new Vector2(0.07f, 0.07f),
            shearAmplitude = 0.12f,
            frequency = 24f,
            useCharacterProgressForEnvelope = true,
            curve = TextAnimationMath.Decay01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            faceDilateFromOffset = -0.22f,
            faceDilateToOffset = 0f,
            softnessFromOffset = 0.22f,
            softnessToOffset = 0f,
            glowOuterFromOffset = 0.36f,
            glowOuterToOffset = 0f,
            glowPowerFromOffset = 0.28f,
            glowPowerToOffset = 0f,
            forceGlowWhilePlaying = true,
            useContextAccentForGlow = true,
            glowAccentWeight = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
    }

    private static void ConfigureFieldShear(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.52f, 0f, TextAnimationOrder.Forward);

        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 8f),
            rotationAmplitudeDegrees = 4.5f,
            scaleAmplitude = new Vector2(0.055f, 0.035f),
            shearAmplitude = 0.16f,
            temporalCycles = 1.35f,
            characterPhaseDegrees = 32f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new NoiseTextAnimationModule
        {
            positionAmplitude = new Vector2(2.5f, 1.5f),
            rotationAmplitudeDegrees = 0.8f,
            frequency = 28f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            outlineWidthFromOffset = 0f,
            outlineWidthToOffset = 0.08f,
            glowOuterFromOffset = 0f,
            glowOuterToOffset = 0.22f,
            glowPowerFromOffset = 0f,
            glowPowerToOffset = 0.18f,
            forceGlowWhilePlaying = true,
            useContextAccentForOutline = true,
            outlineAccentWeight = 0.75f,
            useContextAccentForGlow = true,
            glowAccentWeight = 1f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 4,
            spacing = 1.55f,
            maximumOpacity = 0.23f,
            opacityFalloff = 0.52f,
            spreadFrom = 0.15f,
            spreadTo = 0.70f,
            useContextDirection = true,
            useContextAccentColor = true,
            scaleStepPerCopy = -0.015f,
            curve = TextAnimationMath.Pulse01()
        });
    }

    private static void ConfigureSignalBreak(TextAnimationPreset preset)
    {
        ConfigureBase(
            preset,
            0.26f,
            0.011f,
            TextAnimationOrder.DeterministicRandom,
            TextAnimationCompletionMode.HideTarget);

        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 1f,
            toAlpha = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            addContextDirection = true,
            directionDistanceFrom = 0f,
            directionDistanceTo = 18f,
            scaleFrom = Vector2.one,
            scaleTo = new Vector2(0.72f, 0.82f),
            shearXFrom = 0f,
            shearXTo = 0.34f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new TrackingTextAnimationModule
        {
            fromTracking = 0f,
            toTracking = 8f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            faceDilateFromOffset = 0f,
            faceDilateToOffset = -0.18f,
            softnessFromOffset = 0f,
            softnessToOffset = 0.10f,
            curve = TextAnimationMath.EaseOut01()
        });
    }

    private static void ConfigureOverload(TextAnimationPreset preset)
    {
        ConfigureBase(preset, 0.68f, 0.006f, TextAnimationOrder.DeterministicRandom);

        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            scaleFrom = new Vector2(0.76f, 1.28f),
            scaleTo = Vector2.one,
            shearXFrom = -0.42f,
            shearXTo = 0f,
            curve = TextAnimationMath.Overshoot01()
        });
        preset.Modules.Add(new WaveTextAnimationModule
        {
            positionAmplitude = new Vector2(0f, 4f),
            rotationAmplitudeDegrees = 2f,
            scaleAmplitude = new Vector2(0.04f, 0.03f),
            shearAmplitude = 0.08f,
            temporalCycles = 1.6f,
            characterPhaseDegrees = 51f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new NoiseTextAnimationModule
        {
            positionAmplitude = new Vector2(1.5f, 1f),
            rotationAmplitudeDegrees = 0.7f,
            scaleAmplitude = new Vector2(0.02f, 0.02f),
            shearAmplitude = 0.03f,
            frequency = 26f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            outlineWidthFromOffset = 0.22f,
            outlineWidthToOffset = 0f,
            faceDilateFromOffset = 0.12f,
            faceDilateToOffset = 0f,
            glowOuterFromOffset = 0.55f,
            glowOuterToOffset = 0f,
            glowPowerFromOffset = 0.48f,
            glowPowerToOffset = 0f,
            forceGlowWhilePlaying = true,
            useContextAccentForOutline = true,
            outlineAccentWeight = 1f,
            useContextAccentForGlow = true,
            glowAccentWeight = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new RootMotionTextAnimationModule
        {
            scaleAtPeak = new Vector2(1.10f, 1.10f),
            shakePositionAmplitude = new Vector2(0.8f, 0.6f),
            shakeRotationAmplitudeDegrees = 0.7f,
            shakeFrequency = 24f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.Modules.Add(new GhostTrailTextAnimationModule
        {
            copyCount = 5,
            spacing = 1.70f,
            maximumOpacity = 0.28f,
            opacityFalloff = 0.56f,
            spreadFrom = 0.12f,
            spreadTo = 0.80f,
            useContextDirection = true,
            useContextAccentColor = true,
            scaleStepPerCopy = -0.025f,
            curve = TextAnimationMath.Pulse01()
        });
        preset.markers.Add(new TextAnimationMarker
        {
            normalizedTime = 0.18f,
            id = "OVERLOAD"
        });
    }

    private static void ConfigureDigitStep(TextAnimationPreset preset)
    {
        ConfigureBase(
            preset,
            0.20f,
            0.012f,
            TextAnimationOrder.Forward,
            TextAnimationCompletionMode.RestoreBaseline,
            TextAnimationCharacterSelection.ChangedDigits);

        preset.interruption = TextAnimationInterruption.Replace;
        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            positionFrom = new Vector2(0f, -18f),
            positionTo = Vector2.zero,
            scaleFrom = new Vector2(0.82f, 0.82f),
            scaleTo = Vector2.one,
            curve = TextAnimationMath.Overshoot01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            outlineWidthFromOffset = 0.08f,
            outlineWidthToOffset = 0f,
            curve = TextAnimationMath.EaseOut01()
        });
    }

    private static void ConfigureDigitMorph(TextAnimationPreset preset)
    {
        ConfigureBase(
            preset,
            0.28f,
            0.018f,
            TextAnimationOrder.Reverse,
            TextAnimationCompletionMode.RestoreBaseline,
            TextAnimationCharacterSelection.ChangedDigits);

        preset.interruption = TextAnimationInterruption.Replace;
        preset.Modules.Add(new GlyphMorphTextAnimationModule
        {
            edgeSoftness = 0.85f,
            contourBias = -0.01f,
            curve = TextAnimationMath.EaseOut01()
        });
    }

    private static void ConfigureCountdownImpulse(TextAnimationPreset preset)
    {
        ConfigureBase(
            preset,
            0.38f,
            0f,
            TextAnimationOrder.CenterOut,
            TextAnimationCompletionMode.RestoreBaseline,
            TextAnimationCharacterSelection.DigitsOnly);

        preset.interruption = TextAnimationInterruption.CompleteAndReplace;
        preset.Modules.Add(new AlphaTextAnimationModule
        {
            fromAlpha = 0.20f,
            toAlpha = 1f,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new GlyphTransformTextAnimationModule
        {
            scaleFrom = new Vector2(1.85f, 1.85f),
            scaleTo = Vector2.one,
            curve = TextAnimationMath.Overshoot01()
        });
        preset.Modules.Add(new SdfTextAnimationModule
        {
            outlineWidthFromOffset = 0.20f,
            outlineWidthToOffset = 0f,
            glowOuterFromOffset = 0.30f,
            glowOuterToOffset = 0f,
            glowPowerFromOffset = 0.25f,
            glowPowerToOffset = 0f,
            forceGlowWhilePlaying = true,
            curve = TextAnimationMath.EaseOut01()
        });
        preset.Modules.Add(new RootMotionTextAnimationModule
        {
            scaleAtPeak = new Vector2(1.10f, 1.10f),
            curve = TextAnimationMath.Pulse01()
        });
        preset.markers.Add(new TextAnimationMarker
        {
            normalizedTime = 0.16f,
            id = "COUNT"
        });
    }

    private static void ConfigureLevelSelectOut(
        TextAnimationSequence sequence,
        TextAnimationPreset signalBreak)
    {
        sequence.delayTimeMode = TextAnimationTimeMode.Unscaled;
        sequence.hideTargetsUntilTheirTrackStarts = false;
        sequence.warnAboutMissingSlots = true;
        sequence.tailSeconds = 0.02f;
        sequence.tracks = new List<TextAnimationSequence.Track>
        {
            Track("TITLE", signalBreak, 0f, 1f),
            Track("NUMBER", signalBreak, 0.015f, 0.85f),
            Track("ANOMALY_VALUE", signalBreak, 0.035f, 0.72f),
            Track("ANOMALY_LABEL", signalBreak, 0.050f, 0.60f)
        };
    }

    private static void ConfigureLevelSelectIn(
        TextAnimationSequence sequence,
        TextAnimationPreset calibrate,
        TextAnimationPreset phaseLock,
        TextAnimationPreset vectorSweep)
    {
        sequence.delayTimeMode = TextAnimationTimeMode.Unscaled;
        sequence.hideTargetsUntilTheirTrackStarts = true;
        sequence.warnAboutMissingSlots = true;
        sequence.tailSeconds = 0.02f;
        sequence.tracks = new List<TextAnimationSequence.Track>
        {
            Track("NUMBER", calibrate, 0f, 0.90f),
            Track("TITLE", phaseLock, 0.025f, 1f),
            Track("ANOMALY_LABEL", vectorSweep, 0.070f, 0.55f),
            Track("ANOMALY_VALUE", vectorSweep, 0.095f, 0.78f)
        };
    }

    private static TextAnimationSequence.Track Track(
        string slot,
        TextAnimationPreset preset,
        float delay,
        float intensity)
    {
        return new TextAnimationSequence.Track
        {
            slotId = slot,
            preset = preset,
            startDelay = delay,
            intensityMultiplier = intensity,
            overrideDirection = false,
            direction = Vector2.right,
            overrideAccentColor = false,
            accentColor = Color.white
        };
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
