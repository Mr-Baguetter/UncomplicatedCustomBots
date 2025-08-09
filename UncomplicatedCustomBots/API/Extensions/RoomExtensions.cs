using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class RoomExtensions
    {
        public static RoomNode GetRoomNode(this Room rome)
        {
            foreach (RoomNode roomNode in RoomNode.List)
            {
                if (roomNode.Room != rome)
                    continue;

                return roomNode;
            }

            return null;
        }

        public static Room GetRandomRoom()
        {
            List<Room> roomList = Room.List.Where(r => !Plugin.Instance.Config.BlacklistedRooms.Contains(r.GameObject.name)).ToList();
            
            if (roomList.Count == 0)
                return null;

            return roomList[UnityEngine.Random.Range(0, roomList.Count)];
        }

        public static bool TryGetRandomRoom(out Room room) => (room = GetRandomRoom()) != null;

        public static List<GameObject> GetChildren(this Room room)
        {
            List<GameObject> gameObjects = [];
            foreach (Transform child in room.GameObject.transform)
                gameObjects.Add(child.gameObject);

            return gameObjects;
        }

        public static Bounds GetMapBounds(this Room room)
        {
            MeshRenderer[] renderers = room.GameObject.GetComponents<MeshRenderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }
        /// <summary>
        /// Returns the local space position, based on a world space position.
        /// </summary>
        /// <param name="room">The room instance this method extends.</param>
        /// <param name="position">World position.</param>
        /// <returns>Local position, based on the room.</returns>
        public static Vector3 LocalPosition(this Room room, Vector3 position) => room.Transform.InverseTransformPoint(position);

        /// <summary>
        /// Returns the World position, based on a local space position.
        /// </summary>
        /// <param name="room">The room instance this method extends.</param>
        /// <param name="offset">Local position.</param>
        /// <returns>World position, based on the room.</returns>
        public static Vector3 WorldPosition(this Room room, Vector3 offset) => room.Transform.TransformPoint(offset);
    }
}
