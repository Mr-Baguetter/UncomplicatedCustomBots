using CommandSystem;
using System;
using UncomplicatedCustomBots.API.Managers;
using System.Text.Json.Serialization;
using static UncomplicatedCustomBots.API.Managers.Updater;
using MEC;

namespace UncomplicatedCustomBots.Commands.Console
{
    public class GitHubReleaseInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public GitHubAssetInfo[] Assets { get; set; } = [];
    }

    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class UpdateCheck : ParentCommand
    {
        public UpdateCheck() => LoadGeneratedCommands();

        public override string Command { get; } = "ucbupdatecheck";
        public override string[] Aliases { get; } = ["ucbcheckupdate"];
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

            Timing.RunCoroutine(Updater.CheckForUpdatesCoroutine());
            return true;
        }
    }
}
