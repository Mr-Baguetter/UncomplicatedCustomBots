using LabApi.Features.Wrappers;
using PlayerRoles;
using System.Linq;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features
{
    public static class Targeting
    {
        public static Player GetTarget(Player bot)
        {
            return Player.List.Where(p => IsValidTarget(bot, p)).OrderBy(p => Vector3.Distance(bot.Position, p.Position)).FirstOrDefault();
        }

        private static bool IsValidTarget(Player bot, Player target)
        {
            if (target == null || target == bot || target.Role == RoleTypeId.Spectator || target.IsGodModeEnabled || target.Faction == bot.Faction)
                return false;

            return true;
        }
        public static Player GetScpTarget(Player bot)
        {
            return Player.List.Where(p => IsScp(p) && IsValidTarget(bot, p)).OrderBy(p => Vector3.Distance(bot.Position, p.Position)).FirstOrDefault();
        }

        private static bool IsScp(Player player)
        {
            return player.Role.GetTeam() == Team.SCPs;
        }
    }
}