using LabApi.Features.Wrappers;

namespace UncomplicatedCustomBots.API.Features.States
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

        public virtual void Exit() { }
    }
}
