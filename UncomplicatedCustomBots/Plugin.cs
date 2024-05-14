using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UncomplicatedCustomBots
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "UncomplicatedCustomBots";

        public override string Prefix => "ucb";

        public override Version RequiredExiledVersion { get; } = new(8, 8, 0);

        public override Version Version { get; } = new(1, 0, 0);

        public override string Author => "SpGerg";

        public override void OnEnabled()
        {
            Events.Internal.Server.Register();

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Events.Internal.Server.Unregister();

            base.OnDisabled();
        }
    }
}
