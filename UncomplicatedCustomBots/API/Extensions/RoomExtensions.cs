using LabApi.Features.Wrappers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class RoomExtensions
    {
        private static HashSet<string> _blacklistCache = [];
        private static int _blacklistCacheHash = -1;

        private static HashSet<string> GetBlacklistSet()
        {
            List<string> blacklist = Plugin.Instance.Config.BlacklistedRooms;
            int hash = blacklist.Count;
            if (_blacklistCacheHash != hash || _blacklistCache.Count != hash)
            {
                _blacklistCache = new(blacklist);
                _blacklistCacheHash = hash;
            }

            return _blacklistCache;
        }

        public static Room GetRandomRoomByBlacklist()
        {
            HashSet<string> blacklist = GetBlacklistSet();
            List<Room> candidates = [];
            List<Room> fallback = [];

            foreach (Room room in Room.List)
            {
                if (room == null || blacklist.Contains(room.GameObject.name))
                    continue;

                if (room.Name == MapGeneration.RoomName.Unnamed || room.Zone == MapGeneration.FacilityZone.Other)
                {
                    fallback.Add(room);
                }
                else
                    candidates.Add(room);
            }

            List<Room> pool = candidates.Count > 0 ? candidates : fallback;
            if (pool.Count == 0)
                return null!;

            return pool[Random.Range(0, pool.Count)];
        }

        public static Room GetRandomRoom()
        {
            List<Room> all = Room.List.ToList();
            return all.Count > 0 ? all[Random.Range(0, all.Count)] : null!;
        }

        public static List<GameObject> GetChildren(this Room room)
        {
            List<GameObject> gameObjects = [];
            foreach (Transform child in room.GameObject.transform)
                gameObjects.Add(child.gameObject);

            return gameObjects;
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
