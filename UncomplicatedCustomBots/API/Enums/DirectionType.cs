using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Enums
{
    [Flags]
    public enum DirectionType
    {
        None,
        Left,
        Right,
        Back,
        Forward
    }
}
