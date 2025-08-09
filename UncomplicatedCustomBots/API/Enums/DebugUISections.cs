using System;

namespace UncomplicatedCustomBots.API.Enums
{
    [Flags]
    public enum DebugUISections
    {
        None = 0,
        RaycastInfo = 1 << 0,
        PlayerInfo = 1 << 1,
        ServerInfo = 1 << 2,
        RoundInfo = 1 << 3,
        RoleInfo = 1 << 4,
        ZoneInfo = 1 << 5,
        BotInfo = 1 << 6,
        All = RaycastInfo | PlayerInfo | ServerInfo | RoundInfo | RoleInfo | ZoneInfo | BotInfo
    }
}
