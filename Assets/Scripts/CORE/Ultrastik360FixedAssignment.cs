using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Rewired;

public sealed class Ultrastik360FixedAssignment : MonoBehaviour {

    [Header("Rewired Player IDs (left-to-right cabinet order)")]
    [SerializeField] private int rewiredP1 = 0;
    [SerializeField] private int rewiredP2 = 1;
    [SerializeField] private int rewiredP3 = 2;
    [SerializeField] private int rewiredP4 = 3;

    [Header("Debug / Safety")]
    [SerializeField] private bool verboseLogs = true;
    [SerializeField] private bool requireAllFourBeforeAssign = true;

    // Matches "... #1"
    private static readonly Regex HashSuffixRegex =
        new Regex(@"#\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches "... Player 1"
    private static readonly Regex PlayerSuffixRegex =
        new Regex(@"Player\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void Awake() {
        DontDestroyOnLoad(gameObject);

        if(verboseLogs) Debug.Log("[UltraStikAssign] Awake");

        ReInput.InitializedEvent += OnRewiredInitialized;
        ReInput.ControllerConnectedEvent += OnControllerChanged;
        ReInput.ControllerDisconnectedEvent += OnControllerChanged;
    }

    private void Start() {
        // Critical: ensures mapping happens even if this object loads after Rewired init.
        StartCoroutine(WaitForRewiredThenApply());
    }

    private IEnumerator WaitForRewiredThenApply() {
        while(!ReInput.isReady) yield return null;
        if(verboseLogs) Debug.Log("[UltraStikAssign] Rewired ready (Start) -> applying mapping");
        ApplyUltrastikMapping();
    }

    private void OnDestroy() {
        ReInput.InitializedEvent -= OnRewiredInitialized;
        ReInput.ControllerConnectedEvent -= OnControllerChanged;
        ReInput.ControllerDisconnectedEvent -= OnControllerChanged;
    }

    private void OnRewiredInitialized() {
        if(verboseLogs) Debug.Log("[UltraStikAssign] Rewired initialized event -> applying mapping");
        ApplyUltrastikMapping();
    }

    private void OnControllerChanged(ControllerStatusChangedEventArgs args) {
        if(args.controllerType != ControllerType.Joystick) return;
        if(!ReInput.isReady) return;

        // Do it next frame to allow Rewired's joystick list to settle.
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame() {
        yield return null;
        if(verboseLogs) Debug.Log("[UltraStikAssign] Controller change -> applying mapping");
        ApplyUltrastikMapping();
    }

    private static int ExtractUltrastikId(string hardwareName) {
        if(string.IsNullOrWhiteSpace(hardwareName)) return -1;

        var m = HashSuffixRegex.Match(hardwareName);
        if(m.Success && int.TryParse(m.Groups[1].Value, out var id)) return id;

        m = PlayerSuffixRegex.Match(hardwareName);
        if(m.Success && int.TryParse(m.Groups[1].Value, out id)) return id;

        return -1;
    }

    private static bool LooksLikeUltrastik(string hardwareName) {
        if(string.IsNullOrEmpty(hardwareName)) return false;

        // Match what your screenshots show: "Ultimarc Ultra-Stik Player N"
        return hardwareName.IndexOf("Ultimarc", System.StringComparison.OrdinalIgnoreCase) >= 0
            && hardwareName.IndexOf("Ultra", System.StringComparison.OrdinalIgnoreCase) >= 0
            && hardwareName.IndexOf("Stik", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ApplyUltrastikMapping() {
        if(!ReInput.isReady) return;

        var byUltrastikId = new Dictionary<int, Joystick>();

        foreach (var j in ReInput.controllers.Joysticks) {
            if(verboseLogs) {
                Debug.Log($"[UltraStikAssign] Found joystick: '{j.hardwareName}' " +
                          $"rewiredId={j.id} systemId={j.systemId} guid={j.deviceInstanceGuid}");
            }

            var hwName = j.hardwareName ?? string.Empty;

            if(!LooksLikeUltrastik(hwName)) continue;

            int id = ExtractUltrastikId(hwName);
            if(id < 1 || id > 4) continue;

            if(byUltrastikId.ContainsKey(id)) {
                Debug.LogWarning($"[UltraStikAssign] Duplicate UltraStik ID {id}: '{hwName}'. Check UltraMap IDs.");
                continue;
            }

            byUltrastikId[id] = j;
        }

        if(requireAllFourBeforeAssign && byUltrastikId.Count < 4) {
            Debug.LogWarning($"[UltraStikAssign] Only detected {byUltrastikId.Count}/4 UltraStiks; skipping remap.");
            return;
        }

        ForceAssign(rewiredP1, byUltrastikId, 1);
        ForceAssign(rewiredP2, byUltrastikId, 2);
        ForceAssign(rewiredP3, byUltrastikId, 3);
        ForceAssign(rewiredP4, byUltrastikId, 4);
    }

    private static void ForceAssign(int rewiredPlayerId, Dictionary<int, Joystick> ultraById, int ultrastikId) {
        var player = ReInput.players.GetPlayer(rewiredPlayerId);
        if(player == null) {
            Debug.LogWarning($"[UltraStikAssign] Rewired Player {rewiredPlayerId} not found.");
            return;
        }

        // Clear any existing joystick assignment for this player
        player.controllers.ClearControllersOfType(ControllerType.Joystick);

        if(!ultraById.TryGetValue(ultrastikId, out var stick)) {
            Debug.LogWarning($"[UltraStikAssign] UltraStik {ultrastikId} not found; Player {rewiredPlayerId} gets no joystick.");
            return;
        }

        // removeFromOtherPlayers = true prevents duplicates
        player.controllers.AddController(stick, true);

        Debug.Log($"[UltraStikAssign] Assigned '{stick.hardwareName}' -> Rewired Player {rewiredPlayerId}");
    }
}
