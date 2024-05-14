using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features;
using UnityEngine;
using EventTarget = Exiled.Events.Handlers.Server;

namespace UncomplicatedCustomBots.Events.Internal
{
    internal static class Server
    {
        public static void Register()
        {
            EventTarget.WaitingForPlayers += AddRoomNodesOnWaitingForPlayers;
        }

        public static void Unregister()
        {
            EventTarget.WaitingForPlayers -= AddRoomNodesOnWaitingForPlayers;
        }

        public static void AddRoomNodesOnWaitingForPlayers()
        {
            foreach (var room in Room.List)
            {
                var ways = new List<Room>();

                //thats shit i know
                foreach (var way in Room.List)
                {
                    if (Vector3.Distance(room.Position, way.Position) > 15)
                    {
                        continue;
                    }

                    ways.Add(way);
                }

                RoomNode.List.Add(new RoomNode(room, ways.ToArray()));
            }
        }
    }
}
