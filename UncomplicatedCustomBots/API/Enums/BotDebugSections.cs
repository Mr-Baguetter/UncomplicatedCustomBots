using System;

namespace UncomplicatedCustomBots.API.Enums
{
    [Flags]
    public enum BotDebugSections
    {
        None = 0,
        PlayerInfo = 1 << 0,
        StateInfo = 1 << 1,
        ComponentInfo = 1 << 2,
        NavigationInfo = 1 << 3,
        All = PlayerInfo | StateInfo | ComponentInfo | NavigationInfo
    }
}
