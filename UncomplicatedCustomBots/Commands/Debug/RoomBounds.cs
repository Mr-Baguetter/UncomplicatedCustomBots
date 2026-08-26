using CommandSystem;
using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Text;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RoomBounds : ISubcommand
    {
        public string Name { get; } = "roombounds";
        public string Description { get; } = "Gets the bounds of the current room.";
        public string VisibleArgs { get; } = "";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.roombounds";
        public string[] Aliases { get; } = ["bounds", "bound"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Player.TryGet(sender, out Player? player))
            {
                response = "This command can only be executed by a player.";
                return false;
            }

            Room? room = player.CachedRoom;
            if (room == null)
            {
                response = "Current room is null!";
                return false;
            }

            Bounds bounds = room.Base.WorldspaceBounds;
            
            if (bounds.size == Vector3.zero)
            {
                response = "Could not determine room bounds.";
                return false;
            }

            Vector3[] corners =
            [
                new(bounds.min.x, bounds.min.y, bounds.min.z),
                new(bounds.max.x, bounds.min.y, bounds.min.z),
                new(bounds.min.x, bounds.min.y, bounds.max.z),
                new(bounds.max.x, bounds.min.y, bounds.max.z),

                new(bounds.min.x, bounds.max.y, bounds.min.z),
                new(bounds.max.x, bounds.max.y, bounds.min.z),
                new(bounds.min.x, bounds.max.y, bounds.max.z),
                new(bounds.max.x, bounds.max.y, bounds.max.z)
            ];

            List<PrimitiveObjectToy> createdPrimitives = [];
            
            for (int i = 0; i < corners.Length; i++)
            {
                PrimitiveObjectToy primitive = PrimitiveObjectToy.Create(); 
                primitive.Position = corners[i];
                primitive.Type = PrimitiveType.Sphere;
                primitive.Scale = Vector3.one * 0.5f;
                primitive.GameObject.name = $"RoomBound_Corner_{i}";
                primitive.Color = Color.red;
                primitive.Spawn();
                
                createdPrimitives.Add(primitive);
            }

            PrimitiveObjectToy center = PrimitiveObjectToy.Create(); 
            center.Position = bounds.center;
            center.Type = PrimitiveType.Cube;
            center.Scale = Vector3.one * 0.3f;
            center.GameObject.name = $"RoomBound_Center";
            center.Color = Color.blue;
            center.Spawn();
                
            createdPrimitives.Add(center);

            StringBuilder sb = new();
            sb.AppendLine($"Room: {room.GameObject.name}");
            sb.AppendLine($"Bounds Center: {bounds.center}");
            sb.AppendLine($"Bounds Size: {bounds.size}");
            sb.AppendLine($"Created {createdPrimitives.Count} primitive markers");
            sb.AppendLine("Red spheres mark corners, blue cube marks center");

            response = sb.ToString();
            return true;
        }
    }
}