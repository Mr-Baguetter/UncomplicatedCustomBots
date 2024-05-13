using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Features
{
    public abstract class State
    {
        public State(Player player)
        {
            Player = player;
        }

        public Player Player { get; }

        public abstract void Enter();

        public abstract void Update();

        public abstract void Exit();
    }
}
