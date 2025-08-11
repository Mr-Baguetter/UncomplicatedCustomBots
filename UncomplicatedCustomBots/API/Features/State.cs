using LabApi.Features.Wrappers;

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
    }
}
