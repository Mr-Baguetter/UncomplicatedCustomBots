using CommandSystem;
using MEC;
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
        public override string[] Aliases { get; } = ["ucbselfupdate"];
        public override string Description { get; } = "Downloads and installs the latest version of UncomplicatedCustomBots, then restarts the server round.";

        public override void LoadGeneratedCommands() { }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender.LogName is not "SERVER CONSOLE")
            {
                response = "Sorry but this command is reserved to the game console!";
                return false;
            }

            response = $"Attempting to update UncomplicatedCustomBots. Check console for details.";
            Timing.RunCoroutine(Updater.UpdatePluginCoroutine(arguments.FirstOrDefault()));
            return true;
        }
    }
}