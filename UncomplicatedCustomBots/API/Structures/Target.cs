using Exiled.API.Enums;
using Exiled.API.Features.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Structures
{
    public readonly struct Target
    {
        public Target(IEntity entity)
        {
            Room = new RoomTarget(Vector3.zero, RoomType.Unknown);
            Entity = entity;
        }

        public Target(RoomTarget room)
        {
            Room = room;
            Entity = null;
        }

        public RoomTarget Room { get; }

        public IEntity Entity { get; }
    }
}
