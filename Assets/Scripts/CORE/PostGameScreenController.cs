using System;
using System.Collections;
using System.Reflection;
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

    [Header("Optional UI")]
    [SerializeField] private TMP_Text modeText;        // "1v1" / "2v2"
    [SerializeField] private TMP_Text countdownText;   // "RETURNING IN 10"

    [Header("Formatting")]
    [SerializeField] private string stagePrefix = "STAGE_";
    [SerializeField] private int stageDigits = 3;

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

    private void Start()
    {
        GameFlowContext.EnsureExists();

        if (!GameFlowContext.Instance.HasLastMatchResult)
        {
            // No result? Return to attract safely.
            SceneFlow.GoToChooseMode();
            return;
        }

        MatchResult r = GameFlowContext.Instance.LastMatchResult;

        ApplyResult(r);
        ApplyWinnerTheme(r);

        StartCoroutine(ReturnRoutine());
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

        if (scoreLightText != null) scoreLightText.text = FormatScore0100Spaced(r.Light01);
        if (scoreDarkText  != null) scoreDarkText.text  = FormatScore0100Spaced(r.Dark01);

        if (modeText != null)
            modeText.text = r.mode == GameMode.TwoVTwo ? "2v2" : "1v1";
    }

    private void ApplyWinnerTheme(MatchResult r)
    {
        // 1) Text color theme
        Color c = (r.winner == TeamSide.Light) ? lightWinTextColor :
                  (r.winner == TeamSide.Dark)  ? darkWinTextColor  :
                  darkWinTextColor; // tie fallback (adjust if you want)

        if (themedTexts != null)
        {
            for (int i = 0; i < themedTexts.Length; i++)
            {
                if (themedTexts[i] == null) continue;
                themedTexts[i].color = c;
            }
        }

        // 2) Void background team override
        int teamId = (r.winner == TeamSide.Light) ? lightTeamIdOverride :
                     (r.winner == TeamSide.Dark)  ? darkTeamIdOverride  :
                     -1; // tie => don't change

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
                // If the visual script needs to rebuild, these are safe no-ops if the methods don't exist.
                mb.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
                mb.SendMessage("Rebuild", SendMessageOptions.DontRequireReceiver);
                mb.SendMessage("Apply", SendMessageOptions.DontRequireReceiver);
            }
        }
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

    // Matches your old end-screen formatting style (0100 with spaces).
    private static string FormatScore0100Spaced(float normalized01)
    {
        float newDisplay = Mathf.Round(Mathf.Clamp01(normalized01) * 100f);
        string scoreString = newDisplay.ToString();

        // Insert spaces between digits (legacy style)
        for (int i = 1; i <= scoreString.Length; i += 1)
        {
            scoreString = scoreString.Insert(i, " ");
            i++;
        }

        string precedingZeroes;
        if (newDisplay < 10f)       precedingZeroes = "0 0 0 ";
        else if (newDisplay < 100f) precedingZeroes = "0 0 ";
        else                        precedingZeroes = "0 ";

        return precedingZeroes + scoreString + "/ 0 1 0 0";
    }

    /// <summary>
    /// Tries to set an int field/property for "Team ID Override" on an arbitrary MonoBehaviour.
    /// If preferredName is empty, tries common candidates.
    /// </summary>
    private static bool TrySetIntMember(MonoBehaviour target, int value, string preferredName)
    {
        if (target == null) return false;

        var t = target.GetType();

        // Candidate member names (most likely backing names for an inspector label "Team ID Override")
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

            // Field
            var f = t.GetField(name, flags);
            if (f != null && f.FieldType == typeof(int))
            {
                f.SetValue(target, value);
                return true;
            }

            // Property
            var p = t.GetProperty(name, flags);
            if (p != null && p.CanWrite && p.PropertyType == typeof(int))
            {
                p.SetValue(target, value);
                return true;
            }
        }

        // If preferredName was supplied and failed, fall back to common candidates too
        if (!string.IsNullOrEmpty(preferredName))
            return TrySetIntMember(target, value, preferredName: "");

        return false;
    }
}
