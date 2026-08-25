using CommandSystem;
using NetworkManagerUtils.Dummies;
using System.Collections.Generic;
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
            string botName = arguments.Count > 0 ? arguments[0] : "Bot";
            ReferenceHub npc = DummyUtils.SpawnDummy(botName);
            Bot bot = new(npc);
            bot.Start();

            response = $"Spawned bot {botName}!";
            return true;
        }
    }
}
