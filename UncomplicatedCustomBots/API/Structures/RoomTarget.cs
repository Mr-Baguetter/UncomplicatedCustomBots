using Exiled.API.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Structures
{
    public readonly struct RoomTarget
    {
        public RoomTarget(Vector3 offset, RoomType room)
        {
            Offset = offset;
            Room = room;
        }

        public Vector3 Offset { get; }

        public RoomType Room { get; }
    }
}
