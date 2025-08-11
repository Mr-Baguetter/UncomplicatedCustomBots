using CommandSystem;
using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;
using LabApi.Features.Console;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.Commands.Console
{
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    internal class UCBLogs : ParentCommand
    {
        public UCBLogs() => LoadGeneratedCommands();

        public override string Command { get; } = "ucblogs";

        public override string[] Aliases { get; } = [];

        public override string Description { get; } = "Share the UCB Debug logs with the developers.";

        public override void LoadGeneratedCommands() { }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender.LogName is not "SERVER CONSOLE")
            {
                response = "Sorry but this command is reserved to the game console!";
                return false;
            }

            long Start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            response = $"Loading the JSON content to share with the developers...";

            HttpStatusCode Response = LogManager.SendReport(out HttpContent Content, out string data);
            try
            {
                if (Response is HttpStatusCode.OK)
                {
                    Dictionary<string, string> Data = JsonConvert.DeserializeObject<Dictionary<string, string>>(Plugin.HttpManager.RetriveString(Content));
                    Logger.Info($"[ShareTheLog] Data size being sent: {data}");
                    Logger.Info($"[ShareTheLog] Successfully shared the UCB logs with the developers!\nSend this Id to the developers: {Data["id"]}\n\nTook {DateTimeOffset.Now.ToUnixTimeMilliseconds() - Start}ms");
                }
                else
                    Logger.Info($"Failed to share the UCB logs with the developers: Server says: {Response}");
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }
            
/*
            Task.Run(() =>
            {
                HttpStatusCode Response = LogManager.SendReport(out HttpContent Content, out string data);
                try
                {
                    if (Response is HttpStatusCode.OK)
                    {
                        Dictionary<string, string> Data = JsonConvert.DeserializeObject<Dictionary<string, string>>(Plugin.HttpManager.RetriveString(Content));
                        Logger.Info($"[ShareTheLog] Data size being sent: {data}");
                        Logger.Info($"[ShareTheLog] Successfully shared the UCB logs with the developers!\nSend this Id to the developers: {Data["id"]}\n\nTook {DateTimeOffset.Now.ToUnixTimeMilliseconds() - Start}ms");
                    }
                    else
                        Logger.Info($"Failed to share the UCB logs with the developers: Server says: {Response}");
                }
                catch (Exception e) 
                { 
                    Logger.Error(e.ToString()); 
                }
            });
*/

            return true;
        }
    }
}