using CommandSystem;
using System;
using Newtonsoft.Json;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.Commands.Console
{
    public class GitHubReleaseInfo
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("assets")]
        public GitHubAssetInfo[] Assets { get; set; }
    }

    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class UpdateCheck : ParentCommand
    {
        public UpdateCheck() => LoadGeneratedCommands();

        public override string Command { get; } = "ucbupdatecheck";
        public override string[] Aliases { get; } = new string[] { "ucbcheckupdate" };
        public override string Description { get; } = "Checks if a new version of UncomplicatedCustomBots is available.";

        public override void LoadGeneratedCommands() { }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender.LogName is not "SERVER CONSOLE")
            {
                response = "Sorry but this command is reserved to the game console!";
                return false;
            }

            Version version = Plugin.Instance.Version;
            response = $"Currently running version {version}. Checking for updates...";

            _ = Updater.CheckForUpdatesAsync();
            return true;
        }
    }
}
