using PlayerRoles;
using System.Collections.Generic;
using System.Linq;
using UncomplicatedCustomBots.API.Features;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Managers
{
    internal static class SquadManager
    {
        private static readonly Dictionary<int, List<Bot>> _squads = [];
        private static int _nextMtfSquadId = 1000;
        private static int _nextChaosSquadId = 2000;
        private static int _nextGuardSquadId = 3000;

        public static void AssignToSquad(Bot bot)
        {
            if (!bot.IsSquadBot)
                return;

            int targetSize = bot.IsMtf ? Mathf.Clamp(Plugin.Instance.Config.MtfSquadSize, 2, 4) : bot.IsGuard ? Mathf.Clamp(Plugin.Instance.Config.GuardSquadSize, 2, 4) : Mathf.Clamp(Plugin.Instance.Config.ChaosSquadSize, 2, 4);
            targetSize = targetSize % 2 == 0 ? targetSize : 2;
            int prefix = bot.IsMtf ? 1 : bot.IsGuard ? 3 : 2;

            foreach (KeyValuePair<int, List<Bot>> kvp in _squads)
            {
                if (kvp.Key / 1000 != prefix)
                    continue;

                if (kvp.Value.Count < targetSize && kvp.Value.All(b => b.Player.IsAlive))
                {
                    bot.SquadId = kvp.Key;
                    kvp.Value.Add(bot);
                    LogManager.Info($"Assigned {bot.Player.DisplayName} to existing squad {kvp.Key} ({kvp.Value.Count}/{targetSize})");
                    return;
                }
            }

            int squadId = 0;
            string squadtype = string.Empty;
            if (bot.IsMtf)
            {
                squadId = _nextMtfSquadId++;
                squadtype = "MTF";
            }

            if (bot.IsChaos)
            {
                squadId = _nextChaosSquadId++;
                squadtype = "Chaos";
            }

            if (bot.IsGuard)
            {
                squadId = _nextGuardSquadId++;
                squadtype = "Guard";
            }
                
            _squads[squadId] = [bot];
            bot.SquadId = squadId;
            LogManager.Info($"Created new squad {squadId} for {bot.Player.DisplayName} ({squadtype})");
        }

        public static List<Bot> GetSquadmates(Bot bot)
        {
            if (!bot.IsInSquad || !_squads.TryGetValue(bot.SquadId, out List<Bot>? squad))
                return [];

            List<Bot> result = [];
            foreach (Bot b in squad)
            {
                if (b != bot && b.Player.IsAlive && b.Player.Role != RoleTypeId.Spectator)
                    result.Add(b);
            }
            return result;
        }

        public static Vector3 GetSquadAveragePosition(Bot bot, List<Bot>? cachedMates = null)
        {
            List<Bot> mates = cachedMates ?? GetSquadmates(bot);
            if (mates.Count == 0)
                return bot.Player.Position;

            Vector3 center = bot.Player.Position;
            foreach (Bot mate in mates)
                center += mate.Player.Position;

            return center / (mates.Count + 1);
        }

        public static float GetSquadSpread(Bot bot, List<Bot>? cachedMates = null, Vector3? cachedCenter = null)
        {
            List<Bot> mates = cachedMates ?? GetSquadmates(bot);
            if (mates.Count == 0)
                return 0f;

            Vector3 center = cachedCenter ?? GetSquadAveragePosition(bot, mates);
            float maxDist = 0f;
            foreach (Bot mate in mates)
            {
                float dist = Vector3.Distance(center, mate.Player.Position);
                if (dist > maxDist)
                    maxDist = dist;
            }

            return maxDist;
        }

        public static void RemoveFromSquad(Bot bot)
        {
            if (!bot.IsInSquad || !_squads.TryGetValue(bot.SquadId, out List<Bot>? squad))
                return;

            int squadId = bot.SquadId;
            squad.Remove(bot);
            bot.SquadId = -1;

            if (squad.Count == 0)
                _squads.Remove(squadId);
        }

        public static void Cleanup()
        {
            Bot[] snapshot = Bot.SnapshotBotList();
            foreach (Bot bot in snapshot)
                bot.SquadId = -1;

            _squads.Clear();
            _nextMtfSquadId = 1000;
            _nextChaosSquadId = 2000;
            _nextGuardSquadId = 3000;
        }
    }
}
