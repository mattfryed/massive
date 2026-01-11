using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rewired;

[DisallowMultipleComponent]
public class HowToPlayScreenController : MonoBehaviour
{
    [Header("Flow")]
    [Tooltip("If empty, will call SceneFlow.GoToLevelSelect().")]
    [SerializeField] private string nextSceneNameOverride = "";

    [Header("Timing")]
    [SerializeField] private float minMandatoryWaitSeconds = 1.25f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Prompt Roots (Option B)")]
    [Tooltip("Visible immediately. This can say e.g. 'HOW TO PLAY' or any intro text.")]
    [SerializeField] private GameObject introPromptRoot;

    [Tooltip("Hidden at start. After the wait, this becomes visible (e.g. 'PRESS ANY BUTTON TO CONTINUE').")]
    [SerializeField] private GameObject pressAnyPromptRoot;

    [Header("Optional Text Transitions")]
    [Tooltip("Optional: Put TMPTextTransition on the Intro root (or parent). We'll PlayOut before switching.")]
    [SerializeField] private TMPTextTransition introOutTransition;

    [Tooltip("Optional: Put TMPTextTransition on the PressAny root (or parent). " +
             "Recommended: set TMPTextTransition.autoPlayInOnEnable = true and we won't call PlayIn manually.")]
    [SerializeField] private TMPTextTransition pressAnyInTransition;

    [Tooltip("If a transition never fires completion (because it was interrupted), we continue after this many seconds.")]
    [SerializeField] private float transitionTimeoutSeconds = 1.5f;

    [Header("Any Button (Rewired, any player)")]
    [SerializeField] private string swordAction = "Sword";
    [SerializeField] private string shieldAction = "Shield";

    [Header("Editor Fallback (optional)")]
    [SerializeField] private bool allowKeyboardFallback = true;
    [SerializeField] private KeyCode keyboardContinueKey = KeyCode.Return;

    private bool _armed;

    // Transition completion flags
    private bool _introOutDone;

    private void Awake()
    {
        // Ensure initial visibility
        if (introPromptRoot != null) introPromptRoot.SetActive(true);
        if (pressAnyPromptRoot != null) pressAnyPromptRoot.SetActive(false);

        // Wire transition completion (optional)
        if (introOutTransition != null)
        {
            introOutTransition.onOutComplete.AddListener(() => _introOutDone = true);
        }
    }

    private void Start()
    {
        // Not strictly required, but safe in your architecture:
        GameFlowContext.EnsureExists();

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _armed = false;

        // 1) Mandatory wait
        if (minMandatoryWaitSeconds > 0f)
            yield return WaitSeconds(minMandatoryWaitSeconds);

        // 2) Animate OUT intro (optional)
        if (introOutTransition != null)
        {
            _introOutDone = false;
            introOutTransition.PlayOut();

            // wait for completion OR timeout
            float start = Now();
            while (!_introOutDone && (Now() - start) < transitionTimeoutSeconds)
                yield return null;
        }

        // 3) Swap prompt roots
        if (introPromptRoot != null) introPromptRoot.SetActive(false);
        if (pressAnyPromptRoot != null) pressAnyPromptRoot.SetActive(true);

        // NOTE:
        // If you want the press-any text to animate IN, the cleanest setup is:
        // - pressAnyPromptRoot starts inactive
        // - TMPTextTransition on that root has autoPlayInOnEnable = true
        //
        // So we do NOT call pressAnyInTransition.PlayIn() here to avoid double-playing.
        // (If you truly want manual control, disable autoPlayInOnEnable and uncomment below.)
        //
        // if (pressAnyInTransition != null) pressAnyInTransition.PlayIn();

        // 4) Arm input after swap
        _armed = true;

        // 5) Wait for any button
        while (true)
        {
            if (AnyPlayerPressedSwordOrShield())
                break;

            if (allowKeyboardFallback && Input.GetKeyDown(keyboardContinueKey))
                break;

            yield return null;
        }

        // 6) Go to Level Select (or override)
        if (!string.IsNullOrEmpty(nextSceneNameOverride))
        {
            SceneManager.LoadScene(nextSceneNameOverride);
        }
        else
        {
            // Preferred in your current architecture
            SceneFlow.GoToLevelSelect();
        }
    }

    private bool AnyPlayerPressedSwordOrShield()
    {
        if (!_armed) return false;
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

    private IEnumerator WaitSeconds(float seconds)
    {
        if (seconds <= 0f) yield break;

        if (useUnscaledTime) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);
    }

    private float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;
}
