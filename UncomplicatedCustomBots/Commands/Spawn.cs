using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.Commands
{
    public class Spawn : PlayerCommandBase
    {
        public override string Command => "spawn";

        public override string[] Aliases { get; } = new string[0];

        public override string Description => "Spawn bot";

        public override bool Execute(ArraySegment<string> arguments, Player player, out string response)
        {
            
        }
    }
}
