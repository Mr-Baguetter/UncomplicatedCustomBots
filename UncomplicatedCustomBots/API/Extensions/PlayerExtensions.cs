using System.Linq;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.States;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class PlayerExtensions
    {
        public static bool IsBot(this Player player)
        {
            foreach (Bot bot in Bot.BotList.Where(b => b.Player == player))
                return true;

            return false;
        }

        public static bool TryGetBot(this Player player, out Bot bot)
        {
            bot = GetBot(player);
            return bot != null;
        }

        public static Bot GetBot(this Player player) => Bot.BotList.Where(b => b.Player == player).FirstOrDefault();
    }
}