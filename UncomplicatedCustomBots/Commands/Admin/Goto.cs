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
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Interfaces;

namespace UncomplicatedCustomBots.Commands.Admin
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class Goto : ISubcommand
    {
        public string Name { get; } = "goto";
        public string Description { get; } = "makes the specified bot go to the specified room";
        public string VisibleArgs { get; } = "<PlayerId> <RoomName>";
        public int RequiredArgsCount { get; } = 2;
        public string RequiredPermission { get; } = "ucb.goto";
        public string[] Aliases { get; } = ["g", "go"];

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
                response = "Player is not a bot!";
                return false;
            }
            if (!Enum.TryParse<RoomName>(arguments[2], out RoomName roomName))
            {
                response = $"{arguments[1]} is not a valid room!";
                return false;
            }

            nav.SetDestination(Room.Get(roomName).FirstOrDefault());
            response = $"Added {roomName} to path!";
            return true;
        }
    }
}
