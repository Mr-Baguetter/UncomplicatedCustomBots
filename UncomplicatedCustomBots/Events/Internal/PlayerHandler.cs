using CommandSystem.Commands.RemoteAdmin.Dummies;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerStatsSystem;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Managers;
using EventTarget = LabApi.Events.Handlers.PlayerEvents;

namespace UncomplicatedCustomBots.Events.Internal
{
    internal static class PlayerHandler
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
            Bot.EnsureGlobalCollisionsIgnored();

            if (ev.Player != null && !ev.Player.IsBot() && !ev.Player.IsDestroyed)
            {
                Bot[] bots = Bot.SnapshotBotList();
                Timing.CallDelayed(0.5f, () =>
                {
                    if (ev.Player?.GameObject == null)
                        return;

                    UnityEngine.Collider[] playerColliders = ev.Player.GameObject.GetComponentsInChildren<UnityEngine.Collider>(true);
                    foreach (Bot bot in bots)
                    {
                        if (bot?.Player?.GameObject == null)
                            continue;

                        UnityEngine.Collider[] botColliders = bot.Player.GameObject.GetComponentsInChildren<UnityEngine.Collider>(true);
                        foreach (UnityEngine.Collider a in playerColliders)
                        {
                            if (a == null)
                                continue;

                            foreach (UnityEngine.Collider b in botColliders)
                            {
                                if (b == null)
                                    continue;

                                UnityEngine.Physics.IgnoreCollision(a, b, true);
                            }
                        }
                    }
                });
            }

            Timing.CallDelayed(Timing.WaitForOneFrame, () =>
            {
                if (ev.Player == null || ev.Player.IsDestroyed)
                    return;

                Bot[] snapshot = Bot.SnapshotBotList();
                foreach (Bot bot in snapshot)
                {
                    if (bot.Player != ev.Player)
                        continue;

                    LogManager.Info($"Starting bot {bot.Player.DisplayName} - {bot.Player.PlayerId} - {bot.Player.Role}");
                    bot.Start();

                    if (bot.IsSquadBot)
                        SquadManager.AssignToSquad(bot);
                }
                
                if (ev.Player.IsBot() && !Plugin.Instance.Config.AllowScps && ev.Player.Team == Team.SCPs)
                    ev.Player.SetRole(RoleTypeId.ClassD, RoleChangeReason.RoundStart);
            });
        }

        public static void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            if (!ev.Player.IsBot())
                return;
                
            LogManager.Debug($"{ev.Player.Nickname} Died - Role: {ev.OldRole.GetFullName()} - Room: {Room.GetRoomAtPosition(ev.OldPosition)!.GameObject.name}, Attacker: {ev.Attacker?.Nickname ?? "null"}, Attacker Is Bot: {ev.Attacker?.IsBot() ?? false}, DamageHandler: {ev.DamageHandler.GetType()}");
        }

        public static void OnPlayerJoined(PlayerJoinedEventArgs ev)
        {
            Bot.EnsureGlobalCollisionsIgnored();

            if (!ev.Player.IsDestroyed)
            {
                Bot[] bots = Bot.SnapshotBotList();
                Timing.CallDelayed(0.5f, () =>
                {
                    if (ev.Player?.GameObject == null)
                        return;

                    UnityEngine.Collider[] playerColliders = ev.Player.GameObject.GetComponentsInChildren<UnityEngine.Collider>(true);
                    foreach (Bot bot in bots)
                    {
                        if (bot?.Player?.GameObject == null)
                            continue;

                        UnityEngine.Collider[] botColliders = bot.Player.GameObject.GetComponentsInChildren<UnityEngine.Collider>(true);
                        foreach (UnityEngine.Collider a in playerColliders)
                        {
                            if (a == null)
                                continue;

                            foreach (UnityEngine.Collider b in botColliders)
                            {
                                if (b == null)
                                    continue;

                                UnityEngine.Physics.IgnoreCollision(a, b, true);
                            }
                        }
                    }
                });
            }

            if (ev.Player == null || ev.Player.IsDestroyed)
                return;

            if (!Plugin.Instance.Config.NewPlayersReplaceBots)
                return;

            if (Bot.PlayerList.Count == 0)
                return;

            Player botPlayer = Bot.PlayerList.RandomItem();
            if (botPlayer == null)
                return;

            Bot bot = botPlayer.GetBot();

            LogManager.Info($"Replacing {botPlayer.DisplayName} - {botPlayer.PlayerId} with {ev.Player.DisplayName} - {ev.Player.PlayerId}");
            ev.Player.SetRole(botPlayer.Role, RoleChangeReason.LateJoin);
            ev.Player.Position = botPlayer.Position;
            ev.Player.Rotation = botPlayer.Rotation;
            ev.Player.ArtificialHealth = botPlayer.ArtificialHealth;
            ev.Player.MaxArtificialHealth = botPlayer.MaxArtificialHealth;
            ev.Player.Scale = botPlayer.Scale;
            ev.Player.ClearItems();
            foreach (Item item in botPlayer.Items)
            {
                ev.Player.AddItem(item.Type);
            }

            ev.Player.ClearAmmo();
            foreach (KeyValuePair<ItemType, ushort> ammo in botPlayer.Ammo)
            {
                ev.Player.SetAmmo(ammo.Key, ammo.Value);
            }

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

            bot?.Destroy();
            NetworkServer.Destroy(botPlayer.GameObject);
        }

        public static void OnPlayerUncuffed(PlayerUncuffedEventArgs ev)
        {
            if (!ev.Target.TryGetBot(out Bot bot))
                return;

            UnityEngine.Object.Destroy(bot!.Player.GameObject!.GetComponent<PlayerFollower>());
            bot.ChangeState(new WalkingState(bot));
        }

        public static void OnPlayerCuffed(PlayerCuffedEventArgs ev)
        {
            if (!ev.Target.TryGetBot(out Bot bot))
                return;

            if (bot!.Player.GameObject!.TryGetComponent<Navigation>(out var nav))
            {
                nav.StopNavigation();
                nav.enabled = false;
            }

            bot.Player.GameObject.AddComponent<PlayerFollower>().Init(ev.Player.ReferenceHub);
        }

        public static void OnPlayerHurt(PlayerHurtEventArgs ev)
        {
            if (!ev.Player.TryGetBot(out Bot bot))
                return;

            if (ev.Attacker == null || ev.Attacker == ev.Player)
                return;

            float damage = (ev.DamageHandler as StandardDamageHandler)?.Damage ?? 0f;
            bot.Context.RecordAttacker(ev.Attacker, damage);
            bot.Context.Target = ev.Attacker;

            if (bot.Player.Role != RoleTypeId.Spectator)
            {
                switch (bot.Player.Role)
                {
                    case RoleTypeId.Scp049 when bot.State is not Scp049State:
                            bot.ChangeState(new Scp049State(bot));
                        break;

                    case RoleTypeId.Scp106 when bot.State is not Scp106State:
                            bot.ChangeState(new Scp106State(bot));
                        break;

                    case RoleTypeId.Scp939 when bot.State is not Scp939State:
                            bot.ChangeState(new Scp939State(bot));
                        break;

                    case RoleTypeId.Scp173 when bot.State is not Scp173State:
                            bot.ChangeState(new Scp173State(bot));
                        break;

                    case RoleTypeId.Scp3114 when bot.State is not Scp3114State:
                        Scp3114Role scp3114 = (bot.Player.RoleBase as Scp3114Role)!;
                        if (!scp3114.Disguised)
                            bot.ChangeState(new Scp3114State(bot));
                        break;

                    default:
                        if (bot.State is not CombatState)
                            bot.ChangeState(new CombatState(bot));

                        break;
                }
            }
        }
    }
}