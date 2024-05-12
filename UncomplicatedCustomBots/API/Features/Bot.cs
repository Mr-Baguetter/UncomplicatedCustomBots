using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Features
{
    public class Bot
    {
        public Bot(Player player)
        {
            Player = player;
        }

        public Player Player { get; private set; }

        private State _state;
    }
}
