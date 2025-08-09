using LabApi.Features.Wrappers;
using System.Collections.Generic;
using System.Linq;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.API
{
    public readonly struct RoomTarget : ITarget
    {
        public RoomTarget(Vector3 offset, Room room)
        {
            Offset = offset;
            Room = room;
        }

        public Vector3 Offset { get; }

        public Room Room { get; }

        public Queue<Room> GetWay(Room room) => Room.GetRoomAtPosition(Room.Position).GetRoomNode().GetWay(room);
        public static List<Room> GetAdjacentRooms(Room room) => room.AdjacentRooms.ToList();
        public static List<Room> GetPath(Room roomStart, Room roomEnd) => Room.FindPath(roomStart, roomEnd); 
    }
}