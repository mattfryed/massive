using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NovaBounceProgressUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NovaStarController star;
    [SerializeField] private RectTransform slotsRoot;
    [SerializeField] private NovaBounceMarkerSlot slotPrefab;

    [Header("Optional Text")]
    [Tooltip("If assigned, will show BOUNCE X/Y and switch to SUPERNOVA at the end.")]
    [SerializeField] private TMP_Text progressText;

    [Header("Behavior")]
    [Tooltip("If maxBounces <= 0 (infinite), hide the widget.")]
    [SerializeField] private bool hideIfInfinite = true;

    private readonly List<NovaBounceMarkerSlot> _slots = new();

    // Number of bounce explosions that have already happened.
    private int _bouncesCompleted = 0;

    // Track what we built so we only rebuild when maxBounces changes.
    private int _builtForMaxBounces = -1;

    private int MaxBounces => NovaStarController.StageCount;

    private void OnEnable()
    {
        if (star == null)
        {
            Debug.LogWarning("[NovaBounceProgressUI] Star reference missing.", this);
            return;
        }

        star.OnStageChanged += HandleStageChanged;
        star.OnFinalSupernova  += HandleFinalSupernova;

        EnsureBuilt();
        _bouncesCompleted = star.CurrentStage - 1;
        Refresh();
    }

    private void OnDisable()
    {
        if (star == null) return;

        star.OnStageChanged -= HandleStageChanged;
        star.OnFinalSupernova  -= HandleFinalSupernova;
    }

    private void EnsureBuilt()
    {
        int total = MaxBounces;

        if (total <= 0)
        {
            if (hideIfInfinite)
                gameObject.SetActive(false);
            return;
        }

        // Already built for this count?
        if (_builtForMaxBounces == total && _slots.Count == total)
            return;

        _builtForMaxBounces = total;

        // Clamp progress if designer changes maxBounces in inspector.
        _bouncesCompleted = Mathf.Clamp(_bouncesCompleted, 0, total);

        // Rebuild slots
        ClearSlots();

        if (slotsRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("[NovaBounceProgressUI] slotsRoot or slotPrefab missing.", this);
            return;
        }

        for (int i = 0; i < total; i++)
        {
            var slot = Instantiate(slotPrefab, slotsRoot);
            slot.name = $"BounceSlot_{i + 1:00}";

            // Last slot is the special SUPERNOVA marker
            slot.SetIsSupernovaSlot(i == total - 1);

            _slots.Add(slot);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
                Destroy(_slots[i].gameObject);
        }
        _slots.Clear();
    }

    private void HandleStageChanged(int stage)
    {
        // A bounce explosion just occurred → advance.
        int total = MaxBounces;
        if (total <= 0) return;

        _bouncesCompleted = Mathf.Clamp(stage - 1, 0, total);
        Refresh();
    }

    private void HandleFinalSupernova()
    {
        // Optional: snap to completed state.
        int total = MaxBounces;
        if (total <= 0) return;

        _bouncesCompleted = total;
        Refresh();
    }

    private void Refresh()
    {
        int total = MaxBounces;
        if (total <= 0) return;

        EnsureBuilt();

        // Text: "BOUNCE 2/3", then "SUPERNOVA"
        if (progressText != null)
        {
            if (_bouncesCompleted >= total)
            {
                progressText.text = "SUPERNOVA";
            }
            else
            {
                int nextBounceNumber = _bouncesCompleted + 1; // 1-based
                progressText.text = nextBounceNumber == total ? "CORE COLLAPSE 5/5" : $"STELLAR INSTABILITY {nextBounceNumber}/{total}";
            }
        }

        // Slots: past/current/future (last slot always supernova visual)
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            var state =
                (i < _bouncesCompleted) ? NovaBounceMarkerSlot.State.Past :
                (i == _bouncesCompleted) ? NovaBounceMarkerSlot.State.Current :
                NovaBounceMarkerSlot.State.Future;

            slot.SetState(state);
        }
    }
}
