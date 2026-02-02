namespace Game.Tools.Proto;

/// <summary>
/// Bitmask-based deploy target filtering.
/// </summary>
public static class DeployTargetHelper
{
    public const int All = 0;
    public const int Client = 1;
    public const int Server = 2;
    public const int Realtime = 4;
    public const int ClientServer = Client | Server;           // 3
    public const int ServerRealtime = Server | Realtime;       // 6
    public const int ClientRealtime = Client | Realtime;       // 5
    public const int ClientServerRealtime = Client | Server | Realtime; // 7

    /// <summary>
    /// Returns true if the field/table with the given <paramref name="bitmask"/>
    /// should be included for the specified <paramref name="targetBit"/>.
    /// A bitmask of 0 (ALL) means include everywhere.
    /// </summary>
    public static bool ShouldInclude(int bitmask, int targetBit)
    {
        if (bitmask == All)
        {
            return true;
        }

        return (bitmask & targetBit) != 0;
    }
}
