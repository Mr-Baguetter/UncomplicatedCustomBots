using CommandSystem;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using NetworkManagerUtils.Dummies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
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

        public bool Execute(List<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(int.Parse(arguments[0]));
            if (player == null)
            {
                response = "Player not found!";
                return false;
            }
            if (!player.GameObject.TryGetComponent<Navigation>(out var nav))
            {
                Navigation addednav = player.GameObject.AddComponent<Navigation>();
                addednav.Init();
                response = $"Started {player.PlayerId} sucessfuly!";
                return true;
            }

            Bot bot = player.GetBot();

            bot.ChangeState(new WalkingState(bot));
            nav.Init();
            response = $"Started {player.PlayerId} sucessfuly!";
            return true;
        }
    }
}
