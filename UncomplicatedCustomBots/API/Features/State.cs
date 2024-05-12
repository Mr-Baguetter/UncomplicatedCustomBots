using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Features
{
    public abstract class State
    {
        public abstract void Enter();

        public abstract void Update();

        public abstract void Exit();
    }
}
