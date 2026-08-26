using CommandSystem;
using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class BotUI : ISubcommand
    {
        public string Name { get; } = "botui";
        public string Description { get; } = "Opens a debug UI for monitoring bot behavior.";
        public string VisibleArgs { get; } = "<bot_id|bot_name> [section|disable <section>]";
        public int RequiredArgsCount { get; } = 1;
        public string RequiredPermission { get; } = "debug.botui";
        public string[] Aliases { get; } = ["bui"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Player.TryGet(sender, out Player? player))
            {
                response = "This command can only be executed by a player.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = $"Usage: {VisibleArgs}. Available bots: {string.Join(", ", Bot.BotList.Select(b => $"{b.Player.PlayerId}:{b.Player.DisplayName}"))}";
                return false;
            }

            string botIdentifier = arguments.At(0);
            Bot? targetBot = null;

            if (int.TryParse(botIdentifier, out int botId))
                targetBot = Bot.BotList.FirstOrDefault(b => b.Player.PlayerId == botId);

            if (targetBot == null)
                targetBot = Bot.BotList.FirstOrDefault(b => b.Player.DisplayName.ToLower().Contains(botIdentifier.ToLower()));

            if (targetBot == null)
            {
                response = $"Bot with identifier '{botIdentifier}' not found. Available bots: {string.Join(", ", Bot.BotList.Select(b => $"{b.Player.PlayerId}:{b.Player.DisplayName}"))}";
                return false;
            }

            if (!player.GameObject!.TryGetComponent<BotDebugUIComponent>(out var ui))
            {
                ui = player.GameObject!.AddComponent<BotDebugUIComponent>();
                ui.Initialize(player, targetBot);
            }

            if (arguments.Count == 1)
            {
                response = "Started Debug UI with default sections.";
                ui.SetSectionActive(BotDebugSections.ComponentInfo, true);
                return true;
            }

            string arg = arguments.At(1).ToLower();

            if (Enum.TryParse<BotDebugSections>(arg, true, out var section))
            {
                ui.SetSectionActive(section, true);
                response = $"Enabled debug UI section: {section}";
                return true;
            }

            if (arg == "disable" && arguments.Count > 2 && Enum.TryParse<BotDebugSections>(arguments.At(2), true, out var disableSection))
            {
                ui.SetSectionActive(disableSection, false);
                response = $"Disabled debug UI section: {disableSection}";
                return true;
            }

            response = $"Unknown section. Available sections: {string.Join(", ", Enum.GetNames(typeof(BotDebugSections)))}. Use 'disable <section>' to disable a section.";
            return false;
        }
    }
}