public interface IPulsarInputSource
{
    int PlayerCount { get; }
    bool GetSwordDown(int playerIndex);
    bool GetShieldDown(int playerIndex);
}
