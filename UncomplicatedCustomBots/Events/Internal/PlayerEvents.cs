using CommandSystem.Commands.RemoteAdmin.Dummies;
using InventorySystem.Disarming;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using MEC;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.Voice;
using PlayerStatsSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using UnityEngine.AI;
using Utils.Networking;
using static AdminToys.InvisibleInteractableToy;
using EventTarget = LabApi.Events.Handlers.PlayerEvents;
using Logger = LabApi.Features.Console.Logger;

namespace UncomplicatedCustomBots.Events.Internal
{
    internal static class PlayerEvents
    {
        public static void Register()
        {
            EventTarget.Joined += OnPlayerJoined;
            EventTarget.Spawned += OnPlayerSpawned;
            EventTarget.Death += OnPlayerDeath;
            EventTarget.Uncuffed += OnPlayerUncuffed;
            EventTarget.Cuffed += OnPlayerCuffed;
            EventTarget.Hurt += OnPlayerHurt;
        }
        public static void Unregister()
        {
            EventTarget.Joined -= OnPlayerJoined;
            EventTarget.Spawned -= OnPlayerSpawned;
            EventTarget.Death -= OnPlayerDeath;
            EventTarget.Uncuffed -= OnPlayerUncuffed;
            EventTarget.Cuffed -= OnPlayerCuffed;
            EventTarget.Hurt -= OnPlayerHurt;
        }


        public static void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            Timing.CallDelayed(Timing.WaitForOneFrame, () =>
            {
                foreach (Bot bot in Bot.BotList.Where(b => b.Player == ev.Player))
                {
                    LogManager.Info($"Starting bot {bot.Player.DisplayName} - {bot.Player.PlayerId} - {bot.Player.Role}");
                    bot.Start();
                }
                if (ev.Player.IsBot() && !Plugin.Instance.Config.AllowScps && ev.Player.Team == Team.SCPs)
                    ev.Player.SetRole(RoleTypeId.ClassD, RoleChangeReason.RoundStart);
            });
        }

        public static void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            if (!ev.Player.IsBot())
                return;
        }

        public static void OnPlayerJoined(PlayerJoinedEventArgs ev)
        {
            if (!Plugin.Instance.Config.NewPlayersReplaceBots)
                return;

            Player botPlayer = Bot.PlayerList.RandomItem();
            if (botPlayer == null)
                return;

            LogManager.Info($"Replacing {botPlayer.DisplayName} - {botPlayer.PlayerId} with {ev.Player.DisplayName} - {ev.Player.PlayerId}");
            ev.Player.SetRole(botPlayer.Role, RoleChangeReason.LateJoin);
            ev.Player.Position = botPlayer.Position;
            ev.Player.Rotation = botPlayer.Rotation;
            ev.Player.ArtificialHealth = botPlayer.ArtificialHealth;
            ev.Player.MaxArtificialHealth = botPlayer.MaxArtificialHealth;
            ev.Player.Scale = botPlayer.Scale;
            ev.Player.ClearItems();
            foreach (Item item in botPlayer.Items)
                ev.Player.AddItem(item.Type);
            ev.Player.ClearAmmo();
            foreach (var ammo in botPlayer.Ammo)
                ev.Player.SetAmmo(ammo.Key, ammo.Value);
            ev.Player.Health = botPlayer.Health;
            ev.Player.MaxHealth = botPlayer.MaxHealth;
            ev.Player.HumeShield = botPlayer.HumeShield;
            ev.Player.IsDisarmed = botPlayer.IsDisarmed;
            if (ev.Player.IsDisarmed)
                ev.Player.DisarmedBy = botPlayer.DisarmedBy;
            ev.Player.MaxHumeShield = botPlayer.MaxHumeShield;
            ev.Player.HumeShieldRegenRate = botPlayer.HumeShieldRegenRate;
            ev.Player.HumeShieldRegenCooldown = botPlayer.HumeShieldRegenCooldown;
            ev.Player.Gravity = botPlayer.Gravity;
            ev.Player.CurrentItem = botPlayer.CurrentItem;
            ev.Player.StaminaRemaining = botPlayer.StaminaRemaining;
            ev.Player.SendBroadcast($"You replaced a bot!", 5);
            ev.Player.DisableAllEffects();

            NetworkServer.Destroy(botPlayer.GameObject);
        }

        public static void OnPlayerUncuffed(PlayerUncuffedEventArgs ev)
        {
            if (!ev.Target.IsBot())
                return;
            if (!ev.Target.TryGetBot(out Bot bot))
                return;

            UnityEngine.Object.Destroy(bot.Player.GameObject.GetComponent<PlayerFollower>());
            bot.ChangeState(new WalkingState(bot));
        }

        public static void OnPlayerCuffed(PlayerCuffedEventArgs ev)
        {
            if (!ev.Target.IsBot())
                return;
            if (!ev.Target.TryGetBot(out Bot bot))
                return;

            if (bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
            {
                nav.StopNavigation();
                nav.enabled = false;
            }

            bot.Player.GameObject.AddComponent<PlayerFollower>().Init(ev.Player.ReferenceHub);
        }

        public static void OnPlayerHurt(PlayerHurtEventArgs ev)
        {
            if (ev.DamageHandler is not ScpDamageHandler handler)
                return;
            if (handler.Attacker.Role != RoleTypeId.Scp106)
                return;
            if (!ev.Player.IsBot())
                return;

            ev.Player.Kill($"Killed by SCP-106");
        }
    }
}