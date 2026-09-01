using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the central star in the NOVA stage.
/// - Runs repeated "bounce" explosions.
/// - Opens a pre-bounce entry window where players can enter the star.
/// - Tracks which players are inside for that window.
/// - Holds per-team "shield" contributions from the core minigame
///   (registered via NovaCoreMassReward).
///
/// This script does not know about the anomaly system directly;
/// NovaAnomalyAdapter bridges star events to AnomalyManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NovaStarController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Minimum seconds between bounce explosions.")]
    public float minTimeBetweenBounces = 12f;

    [Tooltip("Maximum seconds between bounce explosions.")]
    public float maxTimeBetweenBounces = 18f;

    [Tooltip("How long the entry window stays open before each bounce.")]
    public float entryWindowDuration = 3f;

    [Tooltip("How many bounces before the final supernova. <= 0 means infinite loop.")]
    public int maxBounces = 3;

    [Header("Entry Window")]
    [Tooltip("Trigger collider defining the radius in which players can enter the star. If null, the star's own collider is used.")]
    public Collider entryCollider;

    [Tooltip("Which layers count as player bodies.")]
    public LayerMask playerLayer;

    [Header("Visuals")]
    [Tooltip("Visual ring object that appears during the entry window.")]
    public GameObject entryRingVisual;

    [Header("Eject Settings")]
    [Tooltip("How far from the star center players are pushed when ejected after the minigame.")]
    public float ejectDistance = 5f;

    [Tooltip("Impulse applied outward when players are ejected.")]
    public float ejectImpulse = 8f;


    [Header("Bounce Damage Model")]
    [Tooltip("Baseline damage/intensity of each bounce (arbitrary units).")]
    public float baseBounceIntensity = 1f;

    [Tooltip("Distance falloff curve. x = normalized distance (0 = center, 1 = outer arena radius), y = multiplier.")]
    public AnimationCurve distanceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.5f);

    [Header("Core Shield Influence")]
    [Tooltip("How strongly core 'shield' reduces bounce intensity for a team. EffectiveIntensity = base / (1 + shield * thisFactor).")]
    public float shieldIntensityFactor = 0.25f;

    [Header("Final Supernova")]
    [Tooltip("If true, trigger a final, larger event after the last bounce.")]
    public bool triggerFinalSupernova = true;

    // --- Public events to hook into ---

    /// <summary>
    /// Fired when the entry window opens for this upcoming bounce.
    /// </summary>
    public event Action OnEntryWindowOpened;

    /// <summary>
    /// Fired when the entry window closes; provides the fixed list of players
    /// who entered the star for this bounce.
    /// </summary>
    public event Action<List<PlayerControllerScript>> OnEntryWindowClosed;

    /// <summary>
    /// Fired when the bounce explosion actually happens.
    /// </summary>
    public event Action OnBounceTriggered;

    /// <summary>
    /// Fired once per bounce per player, passing the player and their
    /// effective intensity multiplier (after shields, distance, etc.).
    /// Use this to apply damage / mass loss / knockback elsewhere.
    /// </summary>
    public event Action<PlayerControllerScript, float> OnPlayerBounceHit;

    /// <summary>
    /// Fired after the last bounce if maxBounces > 0 and triggerFinalSupernova is true.
    /// </summary>
    public event Action OnFinalSupernova;


    private readonly HashSet<PlayerControllerScript> _insideSet = new();

    // Generic "is this player's position inside the entry collider right now?"
    private bool IsInsideEntry(PlayerControllerScript pcs)
    {
        if (pcs == null || entryCollider == null) return false;

        Vector3 p = pcs.transform.position;
        Vector3 closest = entryCollider.ClosestPoint(p);

        // If inside, ClosestPoint returns the point itself.
        return (closest - p).sqrMagnitude < 0.0001f;
    }

    private List<PlayerControllerScript> GetPlayersInsideNow()
    {
        var result = new List<PlayerControllerScript>();

        var players = FindObjectsOfType<PlayerControllerScript>();
        foreach (var p in players)
        {
            if (p == null) continue;

            // Optional: if you have eliminated/inactive flags, filter here.
            if (IsInsideEntry(p))
                result.Add(p);
        }

        return result;
    }


    // --- Internal state ---

    private readonly List<PlayerControllerScript> _currentEntrants = new();
    private readonly Dictionary<int, float> _teamShieldPool = new();

    private bool _entryWindowOpen;
    private bool _waitingForMinigame;
    private int _bounceCount;
    private Coroutine _loopRoutine;

    private void Awake()
    {
        if (entryCollider == null)
        {
            entryCollider = GetComponent<Collider>();
        }

        if (entryCollider != null)
        {
            entryCollider.isTrigger = true;
            entryCollider.enabled = false;
        }

        if (entryRingVisual != null)
            entryRingVisual.SetActive(false);
    }

    private void OnEnable()
    {
        if (_loopRoutine == null)
        {
            _loopRoutine = StartCoroutine(BounceLoop());
        }
    }

    private void OnDisable()
    {
        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }
    }

    // ------------------------------------------------------
    // Core loop
    // ------------------------------------------------------

    private IEnumerator BounceLoop()
    {
        while (maxBounces <= 0 || _bounceCount < maxBounces)
        {
            // Wait for the next bounce interval
            float wait = UnityEngine.Random.Range(minTimeBetweenBounces, maxTimeBetweenBounces);
            yield return new WaitForSeconds(wait);

            // Handle entry window + either immediate bounce (no entrants)
            // or wait for minigame completion (entrants present).
            yield return EntryWindowRoutine();
        }

        if (triggerFinalSupernova)
        {
            TriggerFinalSupernova();
        }
    }

private IEnumerator EntryWindowRoutine()
{
    // --- OPEN WINDOW ---
    _currentEntrants.Clear();
    _entryWindowOpen = true;
    _waitingForMinigame = false;

    if (entryCollider != null)
        entryCollider.enabled = true;

    if (entryRingVisual != null)
        entryRingVisual.SetActive(true);

    OnEntryWindowOpened?.Invoke();

    // --- WAIT ---
    if (entryWindowDuration > 0f)
        yield return new WaitForSeconds(entryWindowDuration);
    else
        yield return null;

    // --- CLOSE WINDOW ---
    _entryWindowOpen = false;

    // Authoritative snapshot at the exact close moment
    SnapshotEntrantsAtClose();

    if (entryCollider != null)
        entryCollider.enabled = false;

    // Always hide ring when the entry window ends
    if (entryRingVisual != null)
        entryRingVisual.SetActive(false);

    // Notify listeners with the fixed list of on-time players
    OnEntryWindowClosed?.Invoke(new List<PlayerControllerScript>(_currentEntrants));

    bool hasEntrants = _currentEntrants.Count > 0;

    // --- BRANCH ---
    if (!hasEntrants)
    {
        TriggerBounce();
        _bounceCount++;
        _currentEntrants.Clear();
        yield break;
    }

    // Entrants exist → wait for minigame completion
    _waitingForMinigame = true;
    while (_waitingForMinigame)
        yield return null;
}


    // ------------------------------------------------------
    // Entry detection
    // ------------------------------------------------------

    // private void OnTriggerEnter(Collider other)
    // {
    //     if (!_entryWindowOpen || entryCollider == null || other == null)
    //         return;

    //     if (playerLayer.value != 0)
    //     {
    //         if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
    //             return;
    //     }

    //     var pcs = other.GetComponentInParent<PlayerControllerScript>();
    //     if (pcs == null) return;

    //     _insideSet.Add(pcs);
    // }

    private bool IsPlayerInsideEntryCollider(PlayerControllerScript pcs)
{
    if (pcs == null || entryCollider == null) return false;

    Vector3 p = pcs.transform.position;
    Vector3 closest = entryCollider.ClosestPoint(p);

    // If the point is inside the collider, ClosestPoint returns the point itself (within tolerance).
    return (closest - p).sqrMagnitude <= 0.0001f;
}

private void SnapshotEntrantsAtClose()
{
    _currentEntrants.Clear();

#if UNITY_6000_0_OR_NEWER
    var players = FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None);
#else
    var players = FindObjectsOfType<PlayerControllerScript>();
#endif

    foreach (var pcs in players)
    {
        if (pcs == null) continue;
        if (!pcs.gameObject.activeInHierarchy) continue;

        if (IsPlayerInsideEntryCollider(pcs))
            _currentEntrants.Add(pcs);
    }
}



    private void OnTriggerExit(Collider other)
    {
        if (!_entryWindowOpen || entryCollider == null || other == null)
            return;

        if (playerLayer.value != 0)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;
        }

        var pcs = other.GetComponentInParent<PlayerControllerScript>();
        if (pcs == null) return;

        // Handle multi-collider rigs: only remove if player is actually outside now.
        if (!IsInsideEntry(pcs))
            _insideSet.Remove(pcs);
    }

    // ------------------------------------------------------
    // Shield registration (from NOVA core minigame)
    // ------------------------------------------------------

    /// <summary>
    /// Called by NovaCoreMassReward when a team captures core mass in
    /// the NOVA anomaly. The star accumulates this per team to reduce
    /// bounce intensity.
    /// </summary>
    public void RegisterCoreCapture(int teamIndex, float shieldAmount)
    {
        if (teamIndex < 0 || shieldAmount <= 0f)
            return;

        if (!_teamShieldPool.TryGetValue(teamIndex, out float current))
            current = 0f;

        _teamShieldPool[teamIndex] = current + shieldAmount;
    }

    // ------------------------------------------------------
    // Bounce + final supernova
    // ------------------------------------------------------

    /// <summary>
    /// Called by NovaAnomalyAdapter once the NOVA core anomaly has
    /// completed and rewards/shields have been applied.
    /// </summary>
    public void TriggerBounceAfterMinigame()
    {
        if (!_waitingForMinigame)
            return;
        // Hide the entry ring now that the minigame is done.
        if (entryRingVisual != null)
            entryRingVisual.SetActive(false);

        // Eject entrants outward from the star so they don't immediately re-enter
        // on the next entry window.
        if (_currentEntrants != null && _currentEntrants.Count > 0)
        {
            foreach (var pcs in _currentEntrants)
            {
                if (pcs == null) continue;

                var rb = pcs.GetComponent<Rigidbody>();
                if (rb == null) continue;

                Vector3 starPos = transform.position;
                Vector3 playerPos = rb.position;

                // Direction from star to player in XZ plane
                Vector3 dir = playerPos - starPos;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    // If they're exactly at the center, pick a random direction
                    dir = UnityEngine.Random.onUnitSphere;
                    dir.y = 0f;
                }
                dir.Normalize();

                // Reposition and push
                rb.position = starPos + dir * ejectDistance;
    #if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = dir * ejectImpulse;
    #else
                rb.velocity = dir * ejectImpulse;
    #endif
            }
        }

        // Now do the bounce
        TriggerBounce();
        _bounceCount++;
        _currentEntrants.Clear();
        _waitingForMinigame = false;

        if (maxBounces > 0 && _bounceCount >= maxBounces)
{
    OnFinalSupernova?.Invoke();
    // optionally: disable this component or stop the bounce loop
    enabled = false;
}

    }


    private void TriggerBounce()
    {
        OnBounceTriggered?.Invoke();

        // For each player in the scene, compute an effective "intensity multiplier"
        // based on distance and shield. Let listeners decide what to do with it.
        var players = FindObjectsOfType<PlayerControllerScript>();
        foreach (var p in players)
        {
            if (p == null) continue;

            // Distance falloff
            Vector3 starPos = transform.position;
            Vector3 playerPos = p.transform.position;
            float distance = Vector3.Distance(starPos, playerPos);

            // TODO: replace this with your actual arena radius.
            float arenaRadius = 10f;
            float normalized = Mathf.Clamp01(distance / arenaRadius);
            float falloff = distanceFalloff.Evaluate(normalized);

            // Shield factor
            float shield = 0f;
            _teamShieldPool.TryGetValue(p.teamID, out shield);
            float shieldFactor = 1f / (1f + Mathf.Max(0f, shield) * shieldIntensityFactor);

            float effectiveIntensity = baseBounceIntensity * falloff * shieldFactor;

            OnPlayerBounceHit?.Invoke(p, effectiveIntensity);
        }
    }

    private void TriggerFinalSupernova()
    {
        OnFinalSupernova?.Invoke();

        // TODO: play final supernova VFX, end the match, etc.
        // This could call into GameManagerScript.EndGame() or similar.
    }
}
