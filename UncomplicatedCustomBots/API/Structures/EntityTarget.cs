using Exiled.API.Enums;
using Exiled.API.Features.Core.Interfaces;
using Exiled.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Structures
{
    public readonly struct EntityTarget : ITarget
    {
        public EntityTarget(IWorldSpace entity)
        {
            Room = new RoomTarget(Vector3.zero, RoomType.Unknown);
            Entity = entity;
        }

        public EntityTarget(RoomTarget room)
        {
            Room = room;
            Entity = null;
        }

        public RoomTarget Room { get; }

        public IWorldSpace Entity { get; }
    }
}
