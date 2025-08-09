using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Features
{
    public abstract class State
    {
        public State(Bot bot)
        {
            Bot = bot;
        }

        public Bot Bot { get; }

        public Player Player => Bot.Player;

        public abstract void Enter();

        public abstract void Update();

        public abstract void Exit();

        public virtual void OnShot() { }

        public virtual void OnDied() { }
    }
}
