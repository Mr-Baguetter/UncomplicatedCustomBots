using CommandSystem;
using LabApi.Features.Wrappers;
using System.Collections.Generic;
using System.Text;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class SendRayCast : ISubcommand
    {
        public string Name { get; } = "raycast";
        public string Description { get; } = "Sends a raycast from the player's camera and displays the hit information, ignoring the player.";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.raycast";
        public string[] Aliases { get; } = ["ray", "cast", "sendraycast"];

        public bool Execute(List<string> arguments, ICommandSender sender, out string response)
        {
            if (!Player.TryGet(sender, out Player? player))
            {
                response = "This command can only be executed by a player.";
                return false;
            }

            int layerMask = ~LayerMask.GetMask("Hitbox");

            if (Physics.Raycast(player.Camera.position, player.Camera.forward, out RaycastHit hitInfo, 100f, layerMask))
            {
                StringBuilder textContent = new();
                textContent.AppendLine($"<size=7><b><color=yellow>Raycast Info</color></b></size>");
                textContent.AppendLine($"<size=5><b>Position:</b> {hitInfo.point}</size>");
                textContent.AppendLine($"<size=5><b>Distance:</b> {hitInfo.distance:F2}m</size>");
                textContent.AppendLine($"<size=5><b>Collider Name:</b> {hitInfo.collider.name}</size>");
                textContent.AppendLine($"<size=5><b>Object Name:</b> {hitInfo.transform.name}</size>");
                textContent.AppendLine($"<size=5><b>Object Layer:</b> {LayerMask.LayerToName(hitInfo.transform.gameObject.layer)}</size>");

                TextToy infoToy = TextToy.Create(hitInfo.point);
                infoToy.TextFormat = textContent.ToString();
                infoToy.Position += Vector3.up * 1f;
                infoToy.Position -= Vector3.forward * 1f;
                infoToy.Rotation = player.Rotation;
                infoToy.Spawn();

                response = $"Successfully created raycast information at {hitInfo.point}.";
                return true;
            }

            response = "Raycast did not hit any object.";
            return false;
        }
    }
}