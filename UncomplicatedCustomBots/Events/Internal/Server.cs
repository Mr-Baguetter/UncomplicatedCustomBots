using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using MEC;
using NetworkManagerUtils.Dummies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using EventTarget = LabApi.Events.Handlers.ServerEvents;

namespace UncomplicatedCustomBots.Events.Internal
{
    internal static class Server
    {
        public static void Register()
        {
            EventTarget.MapGenerated += OnMapGenerated;
            EventTarget.RoundStarting += OnRoundStarted;
        }

        public static void Unregister()
        {
            EventTarget.MapGenerated -= OnMapGenerated;
            EventTarget.RoundStarting -= OnRoundStarted;
        }

        public static void OnMapGenerated(MapGeneratedEventArgs ev)
        {
            foreach (Room room in Room.List)
            {
                List<Room> ways = [];

                foreach (Room way in Room.List)
                {
                    if (Vector3.Distance(room.Position, way.Position) > 15)
                        continue;

                    ways.Add(way);
                }

                RoomNode.List.Add(new RoomNode(room, ways.ToArray()));
            }
        }

        public static void OnRoundStarted(RoundStartingEventArgs ev)
        {
            if (Player.ReadyList.Count() >= Plugin.Instance.Config.MaxPlayers)
                return;

            for (int i = 0; i < Plugin.Instance.Config.MaxBots; i++)
            {
                new Bot();
            }
        }
    }
}