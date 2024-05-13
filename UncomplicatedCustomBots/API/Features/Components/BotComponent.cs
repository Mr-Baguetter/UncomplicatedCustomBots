using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class BotComponent : NetworkBehaviour
    {
        private Bot _bot;

        public void Initialize(Bot bot)
        {
            _bot ??= bot;
        }

        public void Update()
        {
            _bot?.State.Update();
        }
    }
}
