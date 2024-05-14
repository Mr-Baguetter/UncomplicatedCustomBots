using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class RoomExtensions
    {
        public static RoomNode GetRoomNode(this Room rome)
        {
            foreach (var roomNode in RoomNode.List)
            {
                if (roomNode.Room != rome)
                {
                    continue;
                }

                return roomNode;
            }

            return null;
        }
    }
}
