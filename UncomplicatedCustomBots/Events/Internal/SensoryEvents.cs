using System;
using System.Collections.Generic;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using PlayerRoles;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Struct;
using UncomplicatedCustomBots.Events.Handlers;
using UnityEngine;
using VoiceChat;

namespace UncomplicatedCustomBots.Events.Internal
{
    internal static class SensoryEvents
    {
        private const float GunshotRange = 40f;
        private const float TeslaRange = 25f;
        private const float GrenadeRange = 30f;
        private const float SpeakingRange = 25f;
        private const float RadioRange = 15f;
        private const float SpeakingThrottleSeconds = 1.0f;

        private static readonly Dictionary<int, float> _lastSpeakingTime = [];

        private static readonly Dictionary<Type, float> doorranges = new()
        {
            [typeof(CheckpointDoor)] = 60f,
            [typeof(PryableDoor)] = 40f,
            [typeof(DoorVariant)] = 12f,
        };

        public static void Register()
        {
            PlayerEvents.ShotWeapon += OnShotWeapon;
            PlayerEvents.InteractedDoor += OnInteractedDoor;
            PlayerEvents.TriggeredTesla += OnTriggeredTesla;
            PlayerEvents.UsingItem += OnUsingItem;
            PlayerEvents.SendingVoiceMessage += OnSpeaking;
            ServerEvents.ProjectileExploded += OnProjectileExploded;
            PlayerMoved.Moved += OnPlayerMoved;
        }

        public static void Unregister()
        {
            PlayerEvents.ShotWeapon -= OnShotWeapon;
            PlayerEvents.InteractedDoor -= OnInteractedDoor;
            PlayerEvents.TriggeredTesla -= OnTriggeredTesla;
            PlayerEvents.UsingItem -= OnUsingItem;
            PlayerEvents.SendingVoiceMessage -= OnSpeaking;
            ServerEvents.ProjectileExploded -= OnProjectileExploded;
            PlayerMoved.Moved -= OnPlayerMoved;
        }

        private static void OnSpeaking(PlayerSendingVoiceMessageEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (ev.Player.TryGetBot(out var speakingBot) && speakingBot.Player == ev.Player)
                return;

            VoiceChatChannel channel = ev.Message.Channel;

            if (channel != VoiceChatChannel.Proximity || channel != VoiceChatChannel.Radio || channel != VoiceChatChannel.Mimicry)
                return;

            float now = Time.time;
            int pid = ev.Player.PlayerId;
            if (_lastSpeakingTime.TryGetValue(pid, out float last) && now - last < SpeakingThrottleSeconds)
                return;
                
            _lastSpeakingTime[pid] = now;

            if (_lastSpeakingTime.Count > 128)
            {
                List<int> toRemove = [];
                foreach (KeyValuePair<int, float> kv in _lastSpeakingTime)
                {
                    if (now - kv.Value > 60f)
                        toRemove.Add(kv.Key);
                }

                foreach (int k in toRemove)
                    _lastSpeakingTime.Remove(k);
            }

            float baseRange = channel switch
            {
                VoiceChatChannel.Radio => RadioRange,
                _ => SpeakingRange,
            };

            Vector3 speakerPos = ev.Player.Position;
            byte priority = SensedEvent.Priorities.ForType(SensedEventType.Speaking);
            Bot[] snapshot = Bot.SnapshotBotList();

            foreach (Bot bot in snapshot)
            {
                if (bot?.Player == null)
                    continue;

                float hearingRange = baseRange;

                if (bot.Player.Role == RoleTypeId.Scp939)
                    hearingRange *= 1.8f;

                float distance = Vector3.Distance(bot.Player.Position, speakerPos);
                if (distance > hearingRange)
                    continue;

                float effectiveRange = bot.Context.AttenuateHearing(hearingRange, bot.Player.Position, speakerPos);

                if (bot.Player.Role == RoleTypeId.Scp939)
                    effectiveRange = Mathf.Max(effectiveRange, hearingRange * 0.8f);

                if (distance <= effectiveRange)
                    bot.Context.AddSensedEvent(SensedEventType.Speaking, speakerPos, priority, distance);
            }
        }

        private static void OnUsingItem(PlayerUsingItemEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (ev.Player.TryGetBot(out var bot) && bot.Player == ev.Player)
                return;

            Broadcast(SensedEventType.UsingItem, ev.Player.Position, 15f);
        }

        private static void OnPlayerMoved(PlayerMovedEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (ev.Player.TryGetBot(out var bot) && bot.Player == ev.Player)
                return;
            
            Broadcast(SensedEventType.Footstep, ev.Player.Position, 15f);
        }

        private static void OnShotWeapon(PlayerShotWeaponEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (ev.Player.TryGetBot(out var bot) && bot.Player == ev.Player)
                return;

            Broadcast(SensedEventType.Gunshot, ev.Player.Position, GunshotRange);
        }

        private static void OnInteractedDoor(PlayerInteractedDoorEventArgs ev)
        {
            if (ev.Player == null || ev.Door == null)
                return;

            if (ev.Player.TryGetBot(out var bot) && bot.Player == ev.Player)
                return;

            Type doorType = ev.Door.Base.GetType();
            if (!doorranges.TryGetValue(doorType, out float range))
            {
                foreach (KeyValuePair<Type, float> kv in doorranges)
                {
                    if (kv.Key.IsAssignableFrom(doorType))
                    {
                        range = kv.Value;
                        break;
                    }
                }
            }
            
            Broadcast(ev.CanOpen ? SensedEventType.DoorOpen : SensedEventType.DoorClose, ev.Door.Position, range);
        }

        private static void OnTriggeredTesla(PlayerTriggeredTeslaEventArgs ev)
        {
            if (ev.Player == null || ev.Tesla == null)
                return;

            if (ev.Player.TryGetBot(out var bot) && bot.Player == ev.Player)
                return;

            Broadcast(SensedEventType.Tesla, ev.Tesla.Position, TeslaRange);
        }

        private static void OnProjectileExploded(ProjectileExplodedEventArgs ev)
        {
            Broadcast(SensedEventType.Grenade, ev.Position, GrenadeRange);
        }

        private static void Broadcast(SensedEventType type, Vector3 position, float hearingRange)
        {
            byte priority = SensedEvent.Priorities.ForType(type);
            Bot[] snapshot = Bot.SnapshotBotList();

            foreach (Bot bot in snapshot)
            {
                if (bot?.Player == null)
                    continue;

                float distance = Vector3.Distance(bot.Player.Position, position);
                if (distance > hearingRange)
                    continue;

                float effectiveRange = bot.Context.AttenuateHearing(hearingRange, bot.Player.Position, position);

                if (distance <= effectiveRange)
                    bot.Context.AddSensedEvent(type, position, priority, distance);
            }
        }
    }
}