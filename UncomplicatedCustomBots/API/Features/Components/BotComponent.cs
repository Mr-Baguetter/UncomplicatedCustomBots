using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class BotComponent : MonoBehaviour
    {
        private Bot _bot;

        public void Initialize(Bot bot)
        {
            _bot = bot;
        }

        public void Update()
        {
            _bot?.Update();
        }
    }
}