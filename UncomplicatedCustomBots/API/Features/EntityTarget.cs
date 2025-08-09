using LabApi.Features.Wrappers;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Interfaces;

namespace UncomplicatedCustomBots.API
{
    public class EntityTarget : ITarget
    {
        public EntityTarget(RoomTarget room)
        {
            Entity = null;
        }

        public IWorldSpace Entity { get; }

        public Queue<Room> GetWay(Room room) => Room.GetRoomAtPosition(Entity.Position).GetRoomNode().GetWay(room);
    }
}
