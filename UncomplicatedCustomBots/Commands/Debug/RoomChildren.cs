using CommandSystem;
using LabApi.Features.Wrappers;
using System.Collections.Generic;
using System.Text;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RoomChildren : ISubcommand
    {
        public string Name { get; } = "roomchildren";
        public string Description { get; } = "Gets the children gameobjects in the current room.";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.roomchildren";
        public string[] Aliases { get; } = ["room", "child", "children"];

        public bool Execute(List<string> arguments, ICommandSender sender, out string response)
        {
            if (!Player.TryGet(sender, out Player? player))
            {
                response = "This command can only be executed by a player.";
                return false;
            }

            Room? room = player.CachedRoom;
            if (room == null)
            {
                response = "Current room is null!";
                return false;
            }

            StringBuilder textContent = new();
            foreach (GameObject child in room.GetChildren())
                textContent.AppendLine($"<size=10><b>Name:</b> {child.name}</size>");
                
            response = $"{room.GameObject.name} information: \n <size=8>{textContent}</size>";
            return true;
        }
    }
}