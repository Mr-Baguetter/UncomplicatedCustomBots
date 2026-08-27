using CommandSystem;
using LabApi.Features.Wrappers;
using MapGeneration;
using System;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Interfaces;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.API.YamlObjects;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class NavBlockerCreate : ISubcommand
    {
        public string Name { get; } = "navblockcreate";
        public string Description { get; } = "Creates or adds a point to a NavBlocker for the current room (local positions).";
        public string VisibleArgs { get; } = "[roomName]";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.navblockcreate";
        public string[] Aliases { get; } = ["nbcreate", "nbc", "createnavblock"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Player.TryGet(sender, out Player? player))
            {
                response = "This command can only be executed by a player.";
                return false;
            }

            Room? playerRoom = player.CachedRoom ?? Room.GetRoomAtPosition(player.Position);
            if (playerRoom == null || playerRoom.GameObject == null)
            {
                response = "Could not determine your current room.";
                return false;
            }

            string targetRoomName = string.Empty;
            Room? targetRoom = null;

            if (arguments.Count > 0)
            {
                string joined = string.Empty;
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (i > 0)
                        joined += " ";

                    joined += arguments.At(i);
                }

                joined = joined.Trim();
                if (!string.IsNullOrWhiteSpace(joined))
                {
                    targetRoomName = joined;
                    List<Room> found = NavMeshManager.FindRoomsByName(targetRoomName);
                    if (found.Count > 0)
                    {
                        targetRoom = found[0];
                    }
                    else
                    {
                        targetRoom = playerRoom;
                        targetRoomName = joined;
                    }
                }
            }

            targetRoom ??= playerRoom;

            if (string.IsNullOrWhiteSpace(targetRoomName))
            {
                if (targetRoom.Name != RoomName.Unnamed)
                {
                    targetRoomName = targetRoom.Name.ToString();
                }
                else
                    targetRoomName = targetRoom.GameObject.name;
            }

            Vector3 worldPos = player.Position;
            Vector3 localPos = targetRoom.Transform.InverseTransformPoint(worldPos);

            NavBlocker? blocker = null;
            foreach (NavBlocker b in NavMeshManager.SessionNavBlockers)
            {
                if (b.RoomName.Equals(targetRoomName, StringComparison.OrdinalIgnoreCase))
                {
                    blocker = b;
                    break;
                }
            }

            if (blocker == null)
            {
                blocker = new NavBlocker
                {
                    RoomName = targetRoomName,
                    LocalPos = []
                };

                NavMeshManager.SessionNavBlockers.Add(blocker);
            }

            blocker.LocalPos.Add(localPos);
            try
            {
                PrimitiveObjectToy? marker = PrimitiveObjectToy.Create();

                marker.Position = worldPos + Vector3.up * 0.1f;
                marker.Scale = Vector3.one * 0.3f;
                marker.Type = PrimitiveType.Cube;
                marker.Color = Color.red;
                marker.Flags = AdminToys.PrimitiveFlags.Visible;
                marker.GameObject.name = $"NavBlockerPoint_{targetRoomName}_{blocker.LocalPos.Count}";
                marker.Spawn();
            }
            catch (Exception ex)
            {
                LogManager.Debug($"NavBlockerCreate marker spawn failed: {ex.Message}");
            }

            int totalPoints = blocker.LocalPos.Count;
            int totalBlockers = NavMeshManager.SessionNavBlockers.Count;
            response = $"Added point {localPos} (world {worldPos}) to NavBlocker '{targetRoomName}' (room '{targetRoom.GameObject.name}' / {targetRoom.Name}). This blocker now has {totalPoints} point(s). Session has {totalBlockers} blocker(s). Use 'debug navblocksave' to save.";
            return true;
        }
    }
}
