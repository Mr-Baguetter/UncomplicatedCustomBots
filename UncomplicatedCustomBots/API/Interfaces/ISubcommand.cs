using CommandSystem;
using System;

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

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response);
    }
}
