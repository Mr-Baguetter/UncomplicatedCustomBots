using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class PlayerExtensions
    {
        public static bool IsBot(this Player player)
        {
            if (player == null)
                return false;

            return Bot.TryGetByPlayerId(player.PlayerId, out _);
        }

        public static bool TryGetBot(this Player player, out Bot bot)
        {
            bot = null!;
            if (player == null)
                return false;

            return Bot.TryGetByPlayerId(player.PlayerId, out bot);
        }

        public static Bot GetBot(this Player player)
        {
            if (player == null)
                return null!;
                
            Bot.TryGetByPlayerId(player.PlayerId, out Bot bot);
            return bot!;
        }
    }
}