using CommandSystem;
using LabApi.Features.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using UncomplicatedCustomBots.API.Interfaces;
using UncomplicatedCustomBots.Commands.Debug;

namespace UncomplicatedCustomBots.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    internal class DebugCommandBase : ParentCommand
    {

        public DebugCommandBase() => LoadGeneratedCommands();

        public override string Command => "debug";

        public override string Description => "";

        public override string[] Aliases => [];

        public override void LoadGeneratedCommands()
        {
            Subcommands.Add(new SendRayCast());
            Subcommands.Add(new BotDebug());
            Subcommands.Add(new DebugUI());
            Subcommands.Add(new RoomChildren());
            Subcommands.Add(new RoomBounds());
        }

        private List<ISubcommand> Subcommands { get; } = [];

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count == 0)
            {
                response = $"UncomplicatedCustomBots Debug v{Plugin.Instance.Version} by Mr. Baguetter\n\n<size=35>Available commands:</size>";
                foreach (ISubcommand command in Subcommands)
                    response += $"\n- debug {command.Name}{(command.VisibleArgs != string.Empty ? $" {command.VisibleArgs}" : "")} - {command.Description}";

                return true;
            }

            ISubcommand cmd = Subcommands.FirstOrDefault(cmd => cmd.Name == arguments.At(0));

            cmd ??= Subcommands.FirstOrDefault(cmd => cmd.Aliases.Contains(arguments.At(0)));

            if (cmd is null)
            {
                response = "Command not found!";
                return false;
            }

            if (!sender.HasPermissions(cmd.RequiredPermission))
            {
                response = $"You don't have permission to access that command! \n Required permission: {cmd.RequiredPermission}";
                return false;
            }

            if (arguments.Count < cmd.RequiredArgsCount)
            {
                response = $"Wrong usage!\nCorrect usage: ucb {cmd.Name} {cmd.VisibleArgs}";
                return false;
            }

            List<string> args = [.. arguments];
            args.RemoveAt(0);

            return cmd.Execute(args, sender, out response);
        }
    }
}
