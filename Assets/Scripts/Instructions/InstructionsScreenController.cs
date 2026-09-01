using System.Collections;
using UnityEngine;
using TMPro;
using Rewired;

public class InstructionsScreenController : MonoBehaviour
{
    [Header("Dynamic Panel Slot")]
    [SerializeField] private Transform dynamicPanelAnchor;
    [Tooltip("If true, clears existing children under the anchor before instantiating the panel/card.")]
    [SerializeField] private bool clearAnchorOnStart = true;

    [Header("Fallback Cards (Prefabs)")]
    [Tooltip("Used if SelectedLevel has no instructionsPanelPrefab.")]
    [SerializeField] private GameObject[] defaultCardPrefabs;
    [SerializeField] private bool randomizeFallback = true;
    [SerializeField] private bool avoidRepeatingLastFallback = true;
    private static int _lastFallbackIndex = -1;

    [Header("Optional Header Text")]
    [SerializeField] private TMP_Text stageText;   // e.g. STAGE_001
    [SerializeField] private TMP_Text titleText;   // e.g. SUPERNOVA
    [SerializeField] private string stagePrefix = "STAGE_";
    [SerializeField] private int stageDigits = 3;

    [Header("Timing")]
    [SerializeField] private float minMandatoryWaitSeconds = 1.5f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Prompt Swap (Option B)")]
    [Tooltip("Visible immediately (ex: GET READY...). Can be null if you don't want a ready prompt.")]
    [SerializeField] private GameObject readyPromptRoot;

    [Tooltip("Optional: add TMPTextTransition to the ready prompt group and assign it here.")]
    [SerializeField] private TMPTextTransition readyPromptTransition;

    [Tooltip("Hidden initially. This becomes visible after the mandatory wait.")]
    [SerializeField] private GameObject pressAnyPromptRoot;

    [Tooltip("Optional: add TMPTextTransition to the press-any group and assign it here.")]
    [SerializeField] private TMPTextTransition pressAnyPromptTransition;

    [Tooltip("Small hold between hiding Ready and showing Press Any (helps readability).")]
    [SerializeField] private float swapHoldSeconds = 0.05f;

    [Tooltip("Safety timeout if a transition never fires its completion event.")]
    [SerializeField] private float transitionTimeoutSeconds = 1.5f;

    [Header("Rewired Actions (Any Player)")]
    [SerializeField] private string swordAction = "Sword";
    [SerializeField] private string shieldAction = "Shield";

    private bool _armed;

    private bool _readyOutDone;
    private bool _pressInDone;

    private void Awake()
    {
        // Hook transition callbacks (optional)
        if (readyPromptTransition != null)
            readyPromptTransition.onOutComplete.AddListener(() => _readyOutDone = true);

        if (pressAnyPromptTransition != null)
            pressAnyPromptTransition.onInComplete.AddListener(() => _pressInDone = true);
    }

    private void Start()
    {
        GameFlowContext.EnsureExists();

        var selected = GameFlowContext.Instance.SelectedLevel;
        if (selected == null)
        {
            // No selected level = flow error; return to Level Select
            SceneFlow.GoToLevelSelect();
            return;
        }

        // Initial prompt state
        if (readyPromptRoot != null) readyPromptRoot.SetActive(true);
        if (pressAnyPromptRoot != null) pressAnyPromptRoot.SetActive(false);

        // Populate header (optional)
        ApplyHeader(selected);

        // Spawn dynamic content (panel prefab or fallback card)
        SpawnDynamicPanel(selected);

        // Begin timing/input gate
        StartCoroutine(RunGateThenWaitForAnyButton());
    }

    private void ApplyHeader(LevelDefinition def)
    {
        if (def == null) return;

        if (stageText != null)
        {
            string digits = def.levelNumber.ToString(new string('0', Mathf.Max(1, stageDigits)));
            stageText.text = $"{stagePrefix}{digits}";
        }

        if (titleText != null)
            titleText.text = def.levelTitle;
    }

    private void SpawnDynamicPanel(LevelDefinition def)
    {
        if (dynamicPanelAnchor == null) return;

        if (clearAnchorOnStart)
        {
            for (int i = dynamicPanelAnchor.childCount - 1; i >= 0; i--)
                Destroy(dynamicPanelAnchor.GetChild(i).gameObject);
        }

        GameObject prefabToSpawn = null;

        // 1) Level-specific panel
        if (def != null && def.instructionsPanelPrefab != null)
        {
            prefabToSpawn = def.instructionsPanelPrefab;
        }
        else
        {
            // 2) Fallback random card
            prefabToSpawn = ChooseFallbackCard();
        }

        if (prefabToSpawn == null) return;

        var go = Instantiate(prefabToSpawn, dynamicPanelAnchor, worldPositionStays: false);

        // Defensive reset (helps if prefab was authored oddly)
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition3D = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }
    }

    private GameObject ChooseFallbackCard()
    {
        if (defaultCardPrefabs == null || defaultCardPrefabs.Length == 0)
            return null;

        if (!randomizeFallback)
            return defaultCardPrefabs[0];

        int n = defaultCardPrefabs.Length;

        if (n == 1)
            return defaultCardPrefabs[0];

        int idx = Random.Range(0, n);

        if (avoidRepeatingLastFallback && _lastFallbackIndex >= 0 && _lastFallbackIndex < n)
        {
            if (idx == _lastFallbackIndex)
                idx = (idx + 1) % n;
        }

        _lastFallbackIndex = idx;
        return defaultCardPrefabs[idx];
    }

    private IEnumerator RunGateThenWaitForAnyButton()
    {
        _armed = false;

        // Mandatory wait
        float start = Now();
        while (Now() - start < minMandatoryWaitSeconds)
            yield return null;

        // Swap prompts (Ready -> PressAny)
        yield return SwapPromptsRoutine();

        // Now accept input
        _armed = true;

        // Wait for ANY button (Sword or Shield) from ANY player
        while (true)
        {
            if (AnyPlayerPressedSwordOrShield())
                break;

            yield return null;
        }

        SceneFlow.GoToSelectedGameplay();
    }

    private IEnumerator SwapPromptsRoutine()
    {
        // OUT: Ready prompt (optional)
        if (readyPromptRoot != null && readyPromptRoot.activeSelf)
        {
            if (readyPromptTransition != null)
            {
                _readyOutDone = false;
                readyPromptTransition.PlayOut();

                float t0 = Now();
                while (!_readyOutDone && (Now() - t0) < transitionTimeoutSeconds)
                    yield return null;
            }

            // Ensure it’s gone
            readyPromptRoot.SetActive(false);
        }

        if (swapHoldSeconds > 0f)
            yield return Wait(swapHoldSeconds);

        // IN: Press-any prompt
        if (pressAnyPromptRoot != null)
        {
            pressAnyPromptRoot.SetActive(true);

            if (pressAnyPromptTransition != null)
            {
                _pressInDone = false;
                pressAnyPromptTransition.PlayIn();

                // Optional: wait for IN to complete before allowing input
                // (Feels nice + prevents accidental instant start)
                float t0 = Now();
                while (!_pressInDone && (Now() - t0) < transitionTimeoutSeconds)
                    yield return null;
            }
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

    private float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;

private object Wait(float seconds) =>
    useUnscaledTime ? (object)new WaitForSecondsRealtime(seconds) : new WaitForSeconds(seconds);

}
