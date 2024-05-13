using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Enums;

namespace UncomplicatedCustomBots.API.Interfaces
{
    public interface IWalkState
    {
        DirectionType MoveDirections { get; set; }
    }
}
