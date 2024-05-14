using Exiled.API.Enums;
using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.API
{
    public readonly struct RoomTarget : ITarget
    {
        public RoomTarget(Vector3 offset, RoomType room)
        {
            Offset = offset;
            Room = room;
        }

        public Vector3 Offset { get; }

        public RoomType Room { get; }

        public Queue<Room> GetWay(Room room) => Exiled.API.Features.Room.Get(Entity.Position).GetRoomNode().GetWay(room);
    }
}
