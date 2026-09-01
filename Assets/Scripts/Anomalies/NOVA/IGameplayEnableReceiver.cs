/// <summary>
/// Optional interface for minigames (or other systems) that want a standardized
/// "gameplay enabled" toggle (useful for intro/outro transitions).
/// </summary>
public interface IGameplayEnableReceiver
{
    void SetGameplayEnabled(bool enabled);
}
