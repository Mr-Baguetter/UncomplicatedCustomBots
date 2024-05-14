using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.Interfaces;
using Exiled.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.API
{
    public class EntityTarget : ITarget
    {
        public EntityTarget(RoomTarget room)
        {
            Entity = null;
        }

        public IWorldSpace Entity { get; }

        public Queue<Room> GetWay(Room room) => Room.Get(Entity.Position).GetRoomNode().GetWay(room);
    }
}
