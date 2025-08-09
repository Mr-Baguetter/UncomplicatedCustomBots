using CommandSystem;
using System.Collections.Generic;

namespace UncomplicatedCustomBots.API.Interfaces
{
    internal interface ISubcommand
    {
        public string Name { get; }

        public string VisibleArgs { get; }

        public int RequiredArgsCount { get; }

        public string Description { get; }

        public string[] Aliases { get; }

        public string RequiredPermission { get; }

        public bool Execute(List<string> arguments, ICommandSender sender, out string response);
    }
}
