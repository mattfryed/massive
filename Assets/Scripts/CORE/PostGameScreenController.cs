using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Massive.Scoring;
using TMPro;
using UnityEngine;
using Rewired;

public class PostGameScreenController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text stageText;       // "STAGE_003"
    [SerializeField] private TMP_Text titleText;       // "SUPERNOVA"
    [SerializeField] private TMP_Text winnerText;      // "LIGHT WINS"
    [SerializeField] private TMP_Text scoreLightText;
    [SerializeField] private TMP_Text scoreDarkText;
    [SerializeField] private TMP_Text scoreLightUnitText;
    [SerializeField] private TMP_Text scoreDarkUnitText;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text modeText;        // "1v1" / "2v2"
    [SerializeField] private TMP_Text countdownText;   // "RETURNING IN 10"

    [Header("Formatting")]
    [SerializeField] private string stagePrefix = "STAGE_";
    [SerializeField] private int stageDigits = 3;
    [SerializeField, Min(1)] private int scoreIntegerDigits = 3;
    [SerializeField, Range(0, 3)] private int scoreFractionDigits = 3;
    [SerializeField] private bool spaceScoreCharacters = true;

    [Header("Return")]
    [SerializeField] private float minHoldSeconds = 1.25f;   // prevents accidental instant skip
    [SerializeField] private float autoReturnSeconds = 10f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Any Button (Rewired)")]
    [SerializeField] private string swordAction = "Sword";
    [SerializeField] private string shieldAction = "Shield";

    [Header("Winner Theme: Text Color")]
    [Tooltip("Assign Winner + Mode + Countdown here (and anything else you want to color-shift).")]
    [SerializeField] private TMP_Text[] themedTexts;

    [SerializeField] private Color lightWinTextColor = Color.black;
    [SerializeField] private Color darkWinTextColor = Color.white;

    [Header("Winner Theme: Void Background Team Override")]
    [Tooltip("Drag BOTH ScoreVoidMetaballsVisuals components here (or their root MonoBehaviours).")]
    [SerializeField] private MonoBehaviour[] voidBackgroundVisuals;

    [Tooltip("LIGHT win sets Team ID Override to this value.")]
    [SerializeField] private int lightTeamIdOverride = 1;

    [Tooltip("DARK win sets Team ID Override to this value.")]
    [SerializeField] private int darkTeamIdOverride = 2;

    [Tooltip("Optional: if your component uses a different backing name, put it here. Leave blank to auto-try common names.")]
    [SerializeField] private string preferredTeamOverrideMemberName = "";

    private bool _armed;

    // --- Fix helpers ---
    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
    private readonly List<Material> _instancedMaterials = new();

    private IEnumerator Start()
    {
        GameFlowContext.EnsureExists();

        if (!GameFlowContext.Instance.HasLastMatchResult)
        {
            // No result? Return to attract safely.
            SceneFlow.GoToChooseMode();
            yield break;
        }

        MatchResult r = GameFlowContext.Instance.LastMatchResult;

        ApplyResult(r);

        // Apply theme immediately...
        ApplyWinnerTheme(r);

        // ...and again next frame so first-frame TMP init / other scripts don't stomp it.
        yield return null;
        ApplyWinnerTheme(r);

        // Continue with normal return routine.
        yield return ReturnRoutine();
    }

    private void OnDestroy()
    {
        // Clean up any runtime-instanced materials we created.
        for (int i = 0; i < _instancedMaterials.Count; i++)
        {
            if (_instancedMaterials[i] != null)
                Destroy(_instancedMaterials[i]);
        }
        _instancedMaterials.Clear();
    }

    private void ApplyResult(MatchResult r)
    {
        if (stageText != null)
        {
            string digits = r.stageNumber.ToString(new string('0', Mathf.Max(1, stageDigits)));
            stageText.text = $"{stagePrefix}{digits}";
        }

        if (titleText != null)
            titleText.text = r.stageTitle;

        if (winnerText != null)
        {
            winnerText.text = r.winner switch
            {
                TeamSide.Light => "LIGHT\nWINS",
                TeamSide.Dark  => "DARK\nWINS",
                _              => "TIE"
            };
        }

        ApplyEnergyScore(scoreLightText, scoreLightUnitText, r.LightScore);
        ApplyEnergyScore(scoreDarkText, scoreDarkUnitText, r.DarkScore);

        if (modeText != null)
            modeText.text = r.mode == GameMode.TwoVTwo ? "2v2" : "1v1";
    }

private void ApplyWinnerTheme(MatchResult r)
{
    // 1) Text color theme
    Color c = (r.winner == TeamSide.Light) ? lightWinTextColor :
              (r.winner == TeamSide.Dark)  ? darkWinTextColor  :
              darkWinTextColor;

    System.Collections.Generic.HashSet<TMPTextTransition> toRefresh = null;

    if (themedTexts != null)
    {
        for (int i = 0; i < themedTexts.Length; i++)
        {
            var t = themedTexts[i];
            if (t == null) continue;

            t.color = c;

            // Collect transitions that might be controlling this text
            var tt = t.GetComponent<TMPTextTransition>();
            if (tt != null)
            {
                toRefresh ??= new System.Collections.Generic.HashSet<TMPTextTransition>();
                toRefresh.Add(tt);
            }

            var ttParent = t.GetComponentInParent<TMPTextTransition>();
            if (ttParent != null)
            {
                toRefresh ??= new System.Collections.Generic.HashSet<TMPTextTransition>();
                toRefresh.Add(ttParent);
            }
        }
    }

    // Refresh cached base colors AFTER setting .color
    if (toRefresh != null)
    {
        foreach (var tt in toRefresh)
            tt.RefreshBaseColorsFromCurrent();
    }

    // 2) Void background team override (leave your existing code as-is)
    int teamId = (r.winner == TeamSide.Light) ? lightTeamIdOverride :
                 (r.winner == TeamSide.Dark)  ? darkTeamIdOverride  :
                 -1;

    if (teamId < 0) return;

    if (voidBackgroundVisuals == null || voidBackgroundVisuals.Length == 0) return;

    for (int i = 0; i < voidBackgroundVisuals.Length; i++)
    {
        var mb = voidBackgroundVisuals[i];
        if (mb == null) continue;

        bool ok = TrySetIntMember(mb, teamId, preferredTeamOverrideMemberName);

        if (!ok)
        {
            Debug.LogWarning(
                $"[PostGameScreenController] Could not set Team ID Override on '{mb.name}' ({mb.GetType().Name}). " +
                $"Tell me the backing C# field/property name for the inspector label 'Team ID Override' and I'll lock it in."
            );
        }
        else
        {
            mb.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
            mb.SendMessage("Rebuild", SendMessageOptions.DontRequireReceiver);
            mb.SendMessage("Apply", SendMessageOptions.DontRequireReceiver);
        }
    }
}


    /// <summary>
    /// Applies the winner theme RGB to:
    /// - themedTexts array
    /// - plus winnerText/modeText/countdownText (even if you forgot to add them to themedTexts)
    ///
    /// Preserves each text's current alpha (so typewriter/fades can still drive transparency).
    /// Also neutralizes TMP material FaceColor to white on a per-instance material if needed.
    /// </summary>
    private void ApplyThemeColorToTextSet(Color themeRgb)
    {
        // Always include these, even if not in themedTexts:
        ApplyThemeColorToText(winnerText, themeRgb);
        ApplyThemeColorToText(modeText, themeRgb);
        ApplyThemeColorToText(countdownText, themeRgb);

        if (themedTexts == null) return;
        for (int i = 0; i < themedTexts.Length; i++)
            ApplyThemeColorToText(themedTexts[i], themeRgb);
    }

    private void ApplyThemeColorToText(TMP_Text t, Color themeRgb)
    {
        if (t == null) return;

        // Preserve existing alpha so other effects (typewriter/alpha anims) aren't broken.
        Color c = themeRgb;
        c.a = t.color.a;
        t.color = c;

        // If the material preset has FaceColor tinted black, vertex color can't go white.
        // Ensure FaceColor is neutral white, but do it on a per-instance material (no global side effects).
        Material mat = EnsureWritableFontMaterial(t);
        if (mat != null && mat.HasProperty(FaceColorId))
        {
            Color face = mat.GetColor(FaceColorId);
            face.r = 1f; face.g = 1f; face.b = 1f; // keep alpha as-is
            mat.SetColor(FaceColorId, face);
        }

        // Force update so you can visually confirm immediately.
        t.ForceMeshUpdate();
    }

    private Material EnsureWritableFontMaterial(TMP_Text t)
    {
        if (t == null) return null;

        // fontMaterial can be shared or instanced depending on how the text/preset was authored.
        Material mat = t.fontMaterial;
        Material shared = t.fontSharedMaterial;

        if (mat == null) return null;

        // If it's the shared asset material, instance it so we don't affect other scenes/text.
        if (shared != null && ReferenceEquals(mat, shared))
        {
            var inst = new Material(shared);
            inst.name = shared.name + " (PostGame Instance)";
            t.fontMaterial = inst;
            _instancedMaterials.Add(inst);
            return inst;
        }

        return mat;
    }

    private IEnumerator ReturnRoutine()
    {
        _armed = false;

        // Mandatory hold
        float t0 = Now();
        while (Now() - t0 < minHoldSeconds)
            yield return null;

        _armed = true;

        float end = Now() + autoReturnSeconds;

        while (Now() < end)
        {
            if (_armed && AnyPlayerPressedSwordOrShield())
                break;

            if (countdownText != null)
            {
                float remaining = Mathf.Max(0f, end - Now());
                countdownText.text = $"RETURNING IN {Mathf.CeilToInt(remaining)}";
            }

            yield return null;
        }

        // Cleanup for a fresh attract flow
        GameFlowContext.Instance.ClearLastMatchResult();
        GameFlowContext.Instance.ClearSelection();

        SceneFlow.GoToChooseMode();
    }

    private bool AnyPlayerPressedSwordOrShield()
    {
        if (!ReInput.isReady) return false;

        var players = ReInput.players.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (p.GetButtonDown(swordAction) || p.GetButtonDown(shieldAction))
                return true;
        }

        return false;
    }

    private float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;

    private void ApplyEnergyScore(TMP_Text valueText, TMP_Text unitText, long rawMilliElectronVolts)
    {
        EnergyDisplayValue display = EnergyScoreFormatter.GetDisplayValue(rawMilliElectronVolts);
        string value = EnergyScoreFormatter.FormatValue(
            rawMilliElectronVolts,
            scoreIntegerDigits,
            scoreFractionDigits,
            spaceScoreCharacters);

        if (valueText != null)
            valueText.text = unitText == null ? $"{value} {display.unitLabel}" : value;

        if (unitText != null)
            unitText.text = display.unitLabel;
    }

    /// <summary>
    /// Tries to set an int field/property for "Team ID Override" on an arbitrary MonoBehaviour.
    /// If preferredName is empty, tries common candidates.
    /// </summary>
    private static bool TrySetIntMember(MonoBehaviour target, int value, string preferredName)
    {
        if (target == null) return false;

        var t = target.GetType();

        string[] candidates = string.IsNullOrEmpty(preferredName)
            ? new[]
            {
                "teamIdOverride", "teamIDOverride",
                "TeamIdOverride", "TeamIDOverride",
                "teamId", "teamID"
            }
            : new[] { preferredName };

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < candidates.Length; i++)
        {
            string name = candidates[i];

            var f = t.GetField(name, flags);
            if (f != null && f.FieldType == typeof(int))
            {
                f.SetValue(target, value);
                return true;
            }

            var p = t.GetProperty(name, flags);
            if (p != null && p.CanWrite && p.PropertyType == typeof(int))
            {
                p.SetValue(target, value);
                return true;
            }
        }

        if (!string.IsNullOrEmpty(preferredName))
            return TrySetIntMember(target, value, preferredName: "");

        return false;
    }
}
