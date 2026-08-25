using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using System.Collections.Generic;

namespace UncomplicatedCustomBots.API.Features
{
    public class ObjectivesHandler
    {
        public static bool TryGetObjective(Bot bot, out Objective objective)
        {
            objective = GetObjective(bot)!;
            return objective != null;
        }

        public static Objective? GetObjective(Bot bot)
        {
            foreach (Objective objectives in Objective.ActiveObjectives)
            {
                if (objectives.Bot == bot)
                    return objectives;
            }

            return null;
        }

        public static bool TryAssignObjective(Bot bot)
        {
            if (bot?.Player == null)
                return false;

            if (GetObjective(bot) != null)
                return true;

            switch (bot.Player.Role)
            {
                case RoleTypeId.ClassD:
                    bool hasKeycard = false;
                    foreach (Item i in bot.Player.Items)
                    {
                        if (i.Type >= ItemType.KeycardJanitor && i.Type <= ItemType.KeycardO5)
                        {
                            hasKeycard = true;
                            break;
                        }
                    }

                    if (!hasKeycard)
                    {
                        List<ItemType> keycards = [ItemType.KeycardJanitor, ItemType.KeycardScientist, ItemType.KeycardGuard, ItemType.KeycardZoneManager];
                        new Objective(bot, 1, "Find Keycard", "Try to find a Keycard to escape", keycards);
                        return true;
                    }

                    new Objective(bot, 2, "Escape", "Reach the surface exit to escape", [], [RoomName.EzGateA, RoomName.EzGateB]);
                    return true;

                case RoleTypeId.Scientist:
                    new Objective(bot, 3, "Escape", "Reach the surface exit to escape", [], [RoomName.EzGateA, RoomName.EzGateB]);
                    return true;
            }

            return false;
        }
    }
}