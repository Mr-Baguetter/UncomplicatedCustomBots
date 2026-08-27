using CommandSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UncomplicatedCustomBots.API.Interfaces;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.API.YamlObjects;

namespace UncomplicatedCustomBots.Commands.Debug
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class NavBlockerSave : ISubcommand
    {
        public string Name { get; } = "navblocksave";
        public string Description { get; } = "Saves current session NavBlockers to yaml file(s).";
        public string VisibleArgs { get; } = "[filename] [--rebuild]";
        public int RequiredArgsCount { get; } = 0;
        public string RequiredPermission { get; } = "debug.navblocksave";
        public string[] Aliases { get; } = ["nbsave", "nbs", "savenavblock"];

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (NavMeshManager.SessionNavBlockers.Count == 0)
            {
                response = "No session NavBlockers to save. Use 'debug navblockcreate' to add points first.";
                return false;
            }

            bool doRebuild = false;
            List<string> argsList = [];
            for (int i = 0; i < arguments.Count; i++)
            {
                string a = arguments.At(i);
                if (a.Equals("--rebuild", StringComparison.OrdinalIgnoreCase) || a.Equals("-r", StringComparison.OrdinalIgnoreCase))
                {
                    doRebuild = true;
                }
                else
                    argsList.Add(a);
            }

            string fileName = string.Empty;
            if (argsList.Count > 0)
                fileName = string.Join("_", argsList).Trim();

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"navblock_{DateTime.Now:yyyyMMdd_HHmmss}";

            if (!fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                fileName += ".yml";

            string fullPath = Path.Combine(YamlLoader.Dir(), fileName);

            try
            {
                string yaml;
                if (NavMeshManager.SessionNavBlockers.Count == 1)
                {
                    NavBlocker single = NavMeshManager.SessionNavBlockers[0];
                    yaml = YamlLoader.Serializer.Serialize(single);
                }
                else
                    yaml = YamlLoader.Serializer.Serialize(NavMeshManager.SessionNavBlockers);

                File.WriteAllText(fullPath, yaml);

                string rebuildMsg = string.Empty;
                if (doRebuild)
                {
                    NavMeshManager.BuildWithDelay(0.5f);
                    rebuildMsg = " Rebuild scheduled.";
                }

                int points = NavMeshManager.SessionNavBlockers.Sum(b => b.LocalPos.Count);
                response = $"Saved {NavMeshManager.SessionNavBlockers.Count} NavBlocker(s) ({points} points) to '{fullPath}'.{rebuildMsg} Loaded external count={NavMeshManager.ExternalNavBlockers.Count}.";
                NavMeshManager.SessionNavBlockers.Clear();
                return true;
            }
            catch (Exception ex)
            {
                response = $"Failed to save NavBlockers to '{fullPath}': {ex.Message}";
                return false;
            }
        }
    }
}
