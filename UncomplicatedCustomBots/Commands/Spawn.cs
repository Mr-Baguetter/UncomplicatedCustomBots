using CommandSystem;
using Exiled.API.Features;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Extensions;

namespace UncomplicatedCustomBots.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class Spawn : PlayerCommandBase
    {
        public override string Command => "spawn";

        public override string[] Aliases { get; } = new string[0];

        public override string Description => "Spawn bot";

        public override bool Execute(ArraySegment<string> arguments, Player player, out string response)
        {
            var npc = Npc.Spawn("Test", PlayerRoles.RoleTypeId.Scp049);

            npc.Move(API.Enums.DirectionType.Up);
            response = string.Empty;
            return false;
        }
    }
}
