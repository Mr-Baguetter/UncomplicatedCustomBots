using CommandSystem;
using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Interfaces;

namespace UncomplicatedCustomBots.Commands.Admin
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class Start : ISubcommand
    {
        public string Name { get; } = "start";
        public string Description { get; } = "starts the specified bot";
        public string VisibleArgs { get; } = "<PlayerId> <RoomName>";
        public int RequiredArgsCount { get; } = 2;
        public string RequiredPermission { get; } = "ucb.start";
        public string[] Aliases { get; } = ["s", "trigger"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!int.TryParse(arguments.At(0), out int playerId))
            {
                response = "Invalid player id!";
                return false;
            }

            Player? player = Player.Get(playerId);
            if (player == null)
            {
                response = "Player not found!";
                return false;
            }
            Bot bot = player.GetBot();
            if (bot == null)
            {
                response = "Player is not a bot!";
                return false;
            }

            if (!player.GameObject!.TryGetComponent<Navigation>(out var nav))
                nav = player.GameObject!.AddComponent<Navigation>();

            nav.Init();
            bot.ChangeState(new WalkingState(bot));
            response = $"Started {player.PlayerId} successfully!";
            return true;
        }
    }
}
