using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.API.Interfaces
{
    public interface ITarget
    {
        Queue<Room> GetWay(Room room);
    }
}
