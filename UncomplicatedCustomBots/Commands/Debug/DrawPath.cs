using System;
using System.Collections.Generic;
using CommandSystem;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Interfaces;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DrawPath : ISubcommand
    {
        public string Name { get; } = "drawpath";
        public string Description { get; } = "Draws the path of bots.";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.drawpath";
        public string[] Aliases { get; } = ["path", "drap"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            foreach (Bot bot in Bot.BotList)
            {
                if (!bot.TryGetNavigation(out var nav))
                    continue;
                
                nav.TogglePathVisualization(true);
            }

            if (Player.TryGet(sender, out var player))
                NavMeshManager.DebugDrawNavMesh(player);

            response = "";
            return true;
        }
    }
}