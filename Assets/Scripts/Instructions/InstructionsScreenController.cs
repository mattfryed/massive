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

    [Header("Timing + Prompt")]
    [SerializeField] private float minMandatoryWaitSeconds = 1.5f;
    [SerializeField] private GameObject pressAnyPromptRoot; // set inactive at start
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Rewired Actions (Any Player)")]
    [SerializeField] private string swordAction = "Sword";
    [SerializeField] private string shieldAction = "Shield";

    private bool _armed;

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

        // Hide prompt until we’re armed
        if (pressAnyPromptRoot != null)
            pressAnyPromptRoot.SetActive(false);

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

        float start = Now();
        while (Now() - start < minMandatoryWaitSeconds)
            yield return null;

        _armed = true;

        if (pressAnyPromptRoot != null)
            pressAnyPromptRoot.SetActive(true);

        // Wait for ANY button (Sword or Shield) from ANY player
        while (true)
        {
            if (AnyPlayerPressedSwordOrShield())
                break;

            yield return null;
        }

        SceneFlow.GoToSelectedGameplay();
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
}
