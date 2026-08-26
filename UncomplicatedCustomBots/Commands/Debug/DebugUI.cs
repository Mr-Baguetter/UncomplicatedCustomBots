using CommandSystem;
using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Text;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DebugUI : ISubcommand
    {
        public string Name { get; } = "ui";
        public string Description { get; } = "Sends a raycast from the player's camera and displays the hit information, ignoring the player.";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.ui";
        public string[] Aliases { get; } = ["interface", "debugui"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Player.TryGet(sender, out Player? player))
            {
                response = "This command can only be executed by a player.";
                return false;
            }

            if (!player.GameObject!.TryGetComponent<DebugUIComponent>(out var ui))
            {
                ui = player.GameObject!.AddComponent<DebugUIComponent>();
                ui.Initialize(player);
            }

            if (arguments.Count == 0)
            {
                response = "Started Debug UI with default sections.";
                ui.SetSectionActive(DebugUISections.RaycastInfo, true);
                return true;
            }

            string arg = arguments.At(0).ToLower();

            if (Enum.TryParse<DebugUISections>(arg, true, out DebugUISections section))
            {
                ui.SetSectionActive(section, true);
                response = $"Enabled debug UI section: {section}";
                return true;
            }

            if (arg == "disable" && arguments.Count > 1 && Enum.TryParse<DebugUISections>(arguments.At(1), true, out DebugUISections disableSection))
            {
                ui.SetSectionActive(disableSection, false);
                response = $"Disabled debug UI section: {disableSection}";
                return true;
            }

            response = $"Unknown section. Try: {string.Join(", ", Enum.GetNames(typeof(DebugUISections)))}, or disable PlayerInfo";
            return false;
        }
    }
}