using System.Collections.Generic;

/// <summary>
/// Optional interface for minigames that want to know who was "on-time"
/// (e.g., entered an entry ring before the window closed).
///
/// The AnomalyManager will call this immediately after instantiating the minigame prefab
/// (and before Init/Begin) when TriggerAnomaly(...) is invoked with an onTimeParticipants list.
/// </summary>
public interface IOnTimeParticipantsReceiver
{
    void SetOnTimeParticipants(IReadOnlyList<PlayerControllerScript> onTimeParticipants);
}
