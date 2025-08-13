using CommandSystem;
using CommandSystem.Commands.RemoteAdmin.Dummies;
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
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.User
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class Follow : ICommand
    {
        public string Command { get; } = "follow";
        public string Description { get; } = "Gets bots in a radius around the player to follow them";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 2;
        public string RequiredPermission { get; } = "ucb.follow";
        public string[] Aliases { get; } = ["fol", "f"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            float radius = 30f;
            Player senderPlayer = Player.Get(sender);
            if (senderPlayer == null)
            {
                response = "You must be in-game to use this command!";
                return false;
            }

            List<Bot> botsInRadius = [];
            foreach (Bot bot in Bot.BotList)
            {
                if (bot?.Player != null &&  Vector3.Distance(senderPlayer.Position, bot.Player.Position) <= radius)
                    botsInRadius.Add(bot);
            }

            if (botsInRadius.Count == 0)
            {
                response = $"No bots found within {radius} units of your position.";
                return false;
            }

            int successCount = 0;
            foreach (Bot bot in botsInRadius)
            {
                try
                {
                    if (bot.Player.Faction == senderPlayer.Faction)
                    {
                        if (bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
                        {
                            nav.StopNavigation();
                            nav.enabled = false;
                        }

                        if (!bot.Player.GameObject.TryGetComponent<PlayerFollower>(out var follower))
                            follower = bot.Player.GameObject.AddComponent<PlayerFollower>();

                        follower.enabled = true;
                        follower.Init(senderPlayer.ReferenceHub);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to make bot {bot.Player.Nickname} follow target: {ex.Message}");
                }
            }

            response = $"Successfully made {successCount} bot(s) follow.";
            return true;
        }
    }
}