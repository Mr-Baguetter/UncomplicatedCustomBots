using CommandSystem;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class BotDebug : ISubcommand
    {
        public string Name { get; } = "bot";
        public string Description { get; } = "Debug bot information and components";
        public string VisibleArgs { get; } = "<bot_id> <section|disable> [section_name]";
        public int RequiredArgsCount { get; } = 2;
        public string RequiredPermission { get; } = "debug.bot";
        public string[] Aliases { get; } = ["b"];

        public bool Execute(List<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 2)
            {
                response = $"Usage: {Name} <bot_id> <section|disable> [section_name]\nAvailable sections: {string.Join(", ", Enum.GetNames(typeof(BotDebugSections)))}";
                return false;
            }

            string botIdentifier = arguments[0];
            Bot? targetBot = null;

            if (int.TryParse(botIdentifier, out int botId))
                targetBot = Bot.BotList.FirstOrDefault(b => b.Player.PlayerId == botId);

            targetBot ??= Bot.BotList.FirstOrDefault(b => b.Player.DisplayName.ToLower().Contains(botIdentifier.ToLower()));

            if (targetBot == null)
            {
                response = $"Bot with identifier '{botIdentifier}' not found. Available bots: {string.Join(", ", Bot.BotList.Select(b => $"{b.Player.PlayerId}:{b.Player.DisplayName}"))}";
                return false;
            }

            BotDebugComponent? debugComponent = targetBot.Player?.GameObject?.GetComponent<BotDebugComponent>();
            if (debugComponent == null)
            {
                debugComponent = targetBot.Player?.GameObject?.AddComponent<BotDebugComponent>();
                debugComponent?.Init(targetBot);
            }

            if (debugComponent == null)
            {
                response = "Failed to initialize debug component.";
                return false;
            }

            string botDisplayName = targetBot!.Player!.DisplayName;
            int botPlayerId = targetBot!.Player.PlayerId;

            string action = arguments[1].ToLower();

            if (action == "disable")
            {
                if (arguments.Count < 3)
                {
                    debugComponent?.ActiveSections = BotDebugSections.None;
                    response = $"Disabled all debug sections for bot {targetBot.Player?.DisplayName} (ID: {targetBot.Player?.PlayerId})";
                    return true;
                }

                string sectionName = arguments[2];
                if (Enum.TryParse<BotDebugSections>(sectionName, true, out BotDebugSections disableSection))
                {
                    debugComponent.ActiveSections &= ~disableSection;

                    if (debugComponent.ActiveSections == 0)
                    {
                        debugComponent.ActiveSections = BotDebugSections.None;
                        response = $"Disabled debug section '{disableSection}' for bot {botDisplayName} (ID: {botPlayerId})";
                    }
                    else
                        response = $"Disabled debug section '{disableSection}' for bot {botDisplayName} (ID: {botPlayerId})";

                    return true;
                }

                response = $"Unknown section '{sectionName}'. Available sections: {string.Join(", ", Enum.GetNames(typeof(BotDebugSections)))}";
                return false;
            }

            if (Enum.TryParse<BotDebugSections>(action, true, out BotDebugSections section))
            {
                debugComponent.ActiveSections |= section;
                response = $"Enabled debug section '{section}' for bot {botDisplayName} (ID: {botPlayerId})";
                return true;
            }

            switch (action)
            {
                case "all":
                    debugComponent.ActiveSections = BotDebugSections.PlayerInfo | BotDebugSections.ComponentInfo | BotDebugSections.StateInfo;
                    response = $"Enabled all debug sections for bot {botDisplayName} (ID: {botPlayerId})";
                    return true;

                case "status":
                    List<BotDebugSections> activeFlags = Enum.GetValues(typeof(BotDebugSections)).Cast<BotDebugSections>().Where(flag => flag != BotDebugSections.None && debugComponent.ActiveSections.HasFlag(flag)).ToList();

                    string statusMessage = activeFlags.Any()
                        ? $"Active sections: {string.Join(", ", activeFlags)}"
                        : "No debug sections active";

                    response = $"Debug status for bot {botDisplayName} (ID: {botPlayerId}): {statusMessage}";
                    return true;

                case "list":
                    response = $"Available bots:\n{string.Join("\n", Bot.BotList.Select(b => $"- ID: {b.Player.PlayerId}, Name: {b.Player.DisplayName}, Role: {b.Player.Role.GetFullName()}"))}";
                    return true;

                case "stop":
                    response = $"Stopping component for {botDisplayName}";
                    debugComponent.Stop();
                    return true;

                case "resume":
                    response = $"Resuming component for {botDisplayName}";
                    debugComponent.Resume();
                    return true;

                case "destroy":
                    response = $"Destroying component for {botDisplayName}";
                    UnityEngine.Object.Destroy(debugComponent);
                    return true;

                case "hide":
                    response = $"Hiding logs from component for {botDisplayName}";
                    debugComponent.Hide();
                    return true;

                case "show":
                    response = $"Unhiding logs from component for {botDisplayName}";
                    debugComponent.Show();
                    return true;
            }

            response = $"Unknown action '{action}'. Available actions: {string.Join(", ", Enum.GetNames(typeof(BotDebugSections)))}, all, disable, status, list";
            return false;
        }
    }
}