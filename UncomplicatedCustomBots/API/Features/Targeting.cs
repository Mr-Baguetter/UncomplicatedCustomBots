using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Extensions;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features
{
    public static class Targeting
    {
        private const float TargetPersistenceMargin = 15f;
        private const float MaxTargetingRange = 60f;
        private const float LineOfSightScore = 30f;
        private const float OutOfSightPenalty = 20f;
        private const float EngagementPenaltyPerBot = 12f;
        private const float CacheDuration = 0.35f;

        private static readonly Dictionary<Bot, CacheEntry> _targetCache = [];
        private static readonly Dictionary<Bot, CacheEntry> _scpCache = [];

        private class CacheEntry
        {
            public float Time;
            public Player? Target;
            public bool Valid;
        }

        public static Player? GetTarget(Bot bot, Player? currentTarget = null)
        {
            if (TryGetCached(_targetCache, bot, out Player? cached))
                return cached;

            Vector3 botPosition = bot.Player.Position;
            Player? best = null;
            float bestScore = float.MinValue;

            Dictionary<Player, int> engagementMap = BuildEngagementMap(bot);
            Player? recentAttacker = bot.Context.PeekRecentAttacker(5f);

            foreach (Player p in Player.List)
            {
                if (!IsValidTarget(bot, p))
                    continue;

                float score = ScoreTarget(bot, p, botPosition, engagementMap, recentAttacker);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            if (currentTarget != null && IsValidTarget(bot, currentTarget))
            {
                float currentScore = ScoreTarget(bot, currentTarget, botPosition, engagementMap, recentAttacker);
                if (currentScore >= bestScore - TargetPersistenceMargin)
                    best = currentTarget;
            }

            if (best != null)
            {
                bot.Context.RememberLastKnownTarget(best);
            }
            else
                bot.Context.RememberLastKnownTarget(currentTarget);

            bot.Context.Target = best;
            CacheResult(_targetCache, bot, best);
            return best;
        }

        public static Player? GetScpTarget(Bot bot)
        {
            if (TryGetCached(_scpCache, bot, out Player? cached))
                return cached;

            Vector3 botPosition = bot.Player.Position;
            Player? closest = null;
            float closestDistance = float.MaxValue;

            foreach (Player p in Player.List)
            {
                if (!IsScp(p) || !IsValidTarget(bot, p))
                    continue;

                float distance = Vector3.Distance(botPosition, p.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = p;
                }
            }

            CacheResult(_scpCache, bot, closest);
            return closest;
        }

        private static Dictionary<Player, int> BuildEngagementMap(Bot exclude)
        {
            Dictionary<Player, int> map = [];
            Bot[] bots = Bot.SnapshotBotList();
            foreach (Bot b in bots)
            {
                if (b == exclude)
                    continue;

                if (b.State is States.CombatState combat && combat.Target != null)
                {
                    if (!map.TryGetValue(combat.Target, out int c))
                    {
                        map[combat.Target] = 1;
                    }
                    else
                        map[combat.Target] = c + 1;
                }
            }
            return map;
        }

        public static void RemoveBot(Bot bot)
        {
            _targetCache.Remove(bot);
            _scpCache.Remove(bot);
        }

        private static float ScoreTarget(Bot bot, Player target, Vector3 botPosition, Dictionary<Player,int> engagementMap, Player? recentAttacker)
        {
            float distance = Vector3.Distance(botPosition, target.Position);
            float score = Mathf.Clamp(MaxTargetingRange - distance, 0f, MaxTargetingRange);

            switch (target.Team)
            {
                case Team.SCPs:
                    score += 45f;
                    break;

                case Team.FoundationForces:
                    score += 35f;
                    break;

                case Team.ChaosInsurgency:
                    score += 30f;
                    break;

                case Team.Scientists:
                    score += 10f;
                    break;

                case Team.ClassD:
                    score += 8f;
                    break;

                case Team.OtherAlive:
                    score += 5f;
                    break;
            }

            if (target.CurrentItem is FirearmItem firearm)
            {
                score += firearm.Type switch
                {
                    ItemType.GunLogicer or ItemType.GunE11SR or ItemType.GunA7 or ItemType.GunAK or ItemType.GunFRMG0 or ItemType.GunShotgun => 25f,
                    _ => 12f,
                };
            }

            score += Mathf.Clamp(100f - target.Health, 0f, 50f) * 0.2f;

            if (distance < MaxTargetingRange && bot.HasLineOfSight(target, CombatExtensions.CombatHitregMask))
            {
                score += LineOfSightScore;
            }
            else
                score -= OutOfSightPenalty;

            if (recentAttacker == target)
                score += 40f;

            if (bot.Context.HasHeardGunshotNear(target.Position, 5f, 15f))
                score += 20f;

            if (bot.Context.HasHeardSpeakingNear(target.Position, 5f, 12f))
                score += bot.Player.Role == RoleTypeId.Scp939 ? 35f : 15f;

            if (bot.Context.LastKnownTargetPosition.HasValue && bot.Context.LastKnownTargetPosition.Value == target.Position)
            {
                float timeSinceSeen = Time.time - bot.Context.LastKnownTargetTime;
                if (timeSinceSeen < 3f)
                {
                    score += 15f;
                }
                else if (timeSinceSeen < 8f)
                    score += 8f;
            }

            if (engagementMap.TryGetValue(target, out int engagementCount) && engagementCount > 0)
                score -= engagementCount * EngagementPenaltyPerBot;

            return score;
        }

        public static bool IsValidTarget(Bot bot, Player target)
        {
            if (target == null || target == bot.Player || target.Role == RoleTypeId.Spectator || target.IsGodModeEnabled)
                return false;

            if (target.Faction == bot.Player.Faction || target.IsDisarmed)
                return false;

            if (target.Role == RoleTypeId.Tutorial && !Plugin.Instance.Config.AttackTutorials)
                return false;

            if (bot.Player.Role == RoleTypeId.ClassD && target.Role == RoleTypeId.Scientist)
                return false;

            return Vector3.Distance(bot.Player.Position, target.Position) <= MaxTargetingRange;
        }

        private static bool IsScp(Player player) => player.Role.GetTeam() == Team.SCPs;

        private static bool TryGetCached(Dictionary<Bot, CacheEntry> cache, Bot bot, out Player? target)
        {
            target = null;

            if (!cache.TryGetValue(bot, out CacheEntry entry) || !entry.Valid)
                return false;

            if (Time.time - entry.Time > CacheDuration)
            {
                entry.Valid = false;
                return false;
            }

            if (entry.Target != null && !IsValidTarget(bot, entry.Target))
            {
                entry.Valid = false;
                return false;
            }

            target = entry.Target;
            return true;
        }

        private static void CacheResult(Dictionary<Bot, CacheEntry> cache, Bot bot, Player? target)
        {
            cache[bot] = new CacheEntry { Time = Time.time, Target = target, Valid = true };
        }
    }
}