using CommandSystem;
using LabApi.Features.Wrappers;
using MEC;
using NetworkManagerUtils.Dummies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Interfaces;

namespace UncomplicatedCustomBots.Commands.Admin
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class Spawn : ISubcommand
    {
        public string Name { get; } = "spawn";
        public string Description { get; } = "Spawns a bot";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "ucb.spawn";
        public string[] Aliases { get; } = ["s", "sp"];

        public bool Execute(List<string> arguments, ICommandSender sender, out string response)
        {
            ReferenceHub npc = DummyUtils.SpawnDummy($"{arguments[1]}");
            Bot bot = new(npc);
            bot.Start();

            response = string.Empty;
            return false;
        }
    }
}
