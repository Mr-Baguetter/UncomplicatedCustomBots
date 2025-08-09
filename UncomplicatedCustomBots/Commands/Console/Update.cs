using CommandSystem;
using System;
using System.Linq;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.Commands.Console
{
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class Update : ParentCommand
    {
        public Update() => LoadGeneratedCommands();

        public override string Command { get; } = "ucbupdate";
        public override string[] Aliases { get; } = { "ucbselfupdate" };
        public override string Description { get; } = "Downloads and installs the latest version of UncomplicatedCustomBots, then restarts the server round.";

        public override void LoadGeneratedCommands() { }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender.LogName is not "SERVER CONSOLE")
            {
                response = "Sorry but this command is reserved to the game console!";
                return false;
            }

            Version version = Plugin.Instance.Version;
            response = $"Attempting to update UncomplicatedCustomBots from version {version}. Check console for details.";
            _ = Updater.UpdatePluginAsync(version, arguments.FirstOrDefault());
            return true;
        }
    }
}