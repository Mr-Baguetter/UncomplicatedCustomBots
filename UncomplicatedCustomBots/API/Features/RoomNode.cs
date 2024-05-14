using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Features
{
    public class RoomNode
    {
        public static List<RoomNode> List { get; } = new List<RoomNode>();

        public RoomNode(Room room, Room[] ways)
        {
            Room = room;
            Ways = ways;
        }

        public Room Room { get; }

        public Room[] Ways { get; }

        public Queue<Room> GetWay(Room room)
        {
            foreach (var way in Ways)
            {
                var result = GetWay(way, new Queue<Room>());

                if (result is null)
                {
                    continue;
                }

                return result;
            }

            return null;
        }

        public Queue<Room> GetWay(Room room, Queue<Room> result)
        {
            if (Room.Type == room.Type)
            {
                result.Enqueue(room);
                return result;
            }

            foreach (var way in Ways)
            {
                var rooms = GetWay(way, result);

                if (rooms is null)
                {
                    return null;
                }

                return rooms;
            }

            return null;
        }
    }
}
