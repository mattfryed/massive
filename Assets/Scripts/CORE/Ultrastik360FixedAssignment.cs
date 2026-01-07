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

    // Matches "... Player 1", "... Player 2", etc.
    // Matches "... #1" at the end (UltraStik #1, UltraStik 360 #1, etc.)
    private static readonly Regex HashSuffixRegex =
        new Regex(@"#\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Optional fallback: matches "... Player 1"
    private static readonly Regex PlayerSuffixRegex =
        new Regex(@"Player\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static int ExtractUltrastikId(string hardwareName) {
        if(string.IsNullOrWhiteSpace(hardwareName)) return -1;

        // Try "#N" first
        var m = HashSuffixRegex.Match(hardwareName);
        if(m.Success && int.TryParse(m.Groups[1].Value, out var id)) return id;

        // Fallback: "Player N"
        m = PlayerSuffixRegex.Match(hardwareName);
        if(m.Success && int.TryParse(m.Groups[1].Value, out id)) return id;

        return -1;
    }


    private void Awake() {
        DontDestroyOnLoad(gameObject);

        // Run once Rewired is initialized (safer than assuming Awake/Start timing).
        ReInput.InitializedEvent += OnRewiredInitialized;

        // Optional: if something hotplugs / USB hiccups, re-apply mapping.
        ReInput.ControllerConnectedEvent += OnControllerChanged;
        ReInput.ControllerDisconnectedEvent += OnControllerChanged;
    }

    private void OnDestroy() {
        ReInput.InitializedEvent -= OnRewiredInitialized;
        ReInput.ControllerConnectedEvent -= OnControllerChanged;
        ReInput.ControllerDisconnectedEvent -= OnControllerChanged;
    }

    private void OnRewiredInitialized() {
        ApplyUltrastikMapping();
    }

    private void OnControllerChanged(ControllerStatusChangedEventArgs args) {
        if(args.controllerType != ControllerType.Joystick) return;
        ApplyUltrastikMapping();
    }

    private void ApplyUltrastikMapping() {
        if(!ReInput.isReady) return;

        // Build: UltraStik ID (1-4) -> joystick
        var byUltrastikId = new Dictionary<int, Joystick>();

        foreach (var j in ReInput.controllers.Joysticks) {
            Debug.Log($"JOY: '{j.hardwareName}'  guid={j.deviceInstanceGuid}");
        }

        if(byUltrastikId.Count < 4) {
            Debug.LogWarning("Not all 4 UltraStiks detected yet; skipping remap for now.");
            return;
        }



        foreach (var j in ReInput.controllers.Joysticks) {
            var hwName = j.hardwareName ?? string.Empty;

            // Filter down to just UltraStiks (prevents accidental grabs of other gamepads)
            if(hwName.IndexOf("UltraStik", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            


            int id = ExtractUltrastikId(hwName);
            if(id < 1 || id > 4) continue;

            if(byUltrastikId.ContainsKey(id)) {
                Debug.LogWarning($"Duplicate UltraStik ID {id} detected: '{hwName}'. " +
                                 $"Re-check UltraMap Assign ID and replug each stick.");
                continue;
            }

            byUltrastikId[id] = j;
        }

        ForceAssign(rewiredP1, byUltrastikId, 1);
        ForceAssign(rewiredP2, byUltrastikId, 2);
        ForceAssign(rewiredP3, byUltrastikId, 3);
        ForceAssign(rewiredP4, byUltrastikId, 4);
    }



    private static void ForceAssign(int rewiredPlayerId, Dictionary<int, Joystick> ultraById, int ultrastikId) {
        var player = ReInput.players.GetPlayer(rewiredPlayerId);
        if(player == null) return;

        // Removes ALL joysticks from that player. (Perfect for a dedicated cabinet.)
        player.controllers.ClearControllersOfType(ControllerType.Joystick);

        if(!ultraById.TryGetValue(ultrastikId, out var stick)) {
            Debug.LogWarning($"UltraStik Player {ultrastikId} not found. Rewired Player {rewiredPlayerId} has no joystick.");
            return;
        }

        // removeFromOtherPlayers = true prevents any accidental double-assign.
        player.controllers.AddController(stick, true);

        Debug.Log($"Assigned '{stick.hardwareName}' -> Rewired Player {rewiredPlayerId} " +
                  $"(deviceInstanceGuid={stick.deviceInstanceGuid})");
    }
}
