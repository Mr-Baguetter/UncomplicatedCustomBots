using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Wrappers;
using MEC;
using System.Collections.Generic;
using System.Linq;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using EventTarget = LabApi.Events.Handlers.ServerEvents;


namespace UncomplicatedCustomBots.Events.Internal
{
    internal static class ServerHandler
    {
        public static void Register()
        {
            EventTarget.MapGenerated += OnMapGenerated;
            EventTarget.RoundStarting += OnRoundStarted;
        }

        public static void Unregister()
        {
            EventTarget.MapGenerated -= OnMapGenerated;
            EventTarget.RoundStarting -= OnRoundStarted;
        }

        public static void OnMapGenerated(MapGeneratedEventArgs ev)
        {
            NavMeshManager.Init();
        }

        public static void OnRoundStarted(RoundStartingEventArgs ev)
        {
            SquadManager.Cleanup();
            if (Player.ReadyList.Count() >= Plugin.Instance.Config.MaxPlayers)
                return;

            if (!NavMeshManager.IsBaked)
            {
                LogManager.Info($"NavMesh not yet baked at round start, delaying bot spawn until bake completes.");
                Timing.RunCoroutine(SpawnBotsWhenReady());
                return;
            }

            for (int i = 0; i < Plugin.Instance.Config.MaxBots; i++)
            {
                new Bot();
            }
        }

        private static IEnumerator<float> SpawnBotsWhenReady()
        {
            float start = Time.realtimeSinceStartup;
            const float timeout = 25f;
            while (!NavMeshManager.IsBaked && Time.realtimeSinceStartup - start < timeout)
                yield return Timing.WaitForOneFrame;

            if (!NavMeshManager.IsBaked)
            {
                LogManager.Warn($"NavMesh still not baked after {timeout}s, spawning bots anyway (they will retry paths).");
            }
            else
            {
                LogManager.Info($"NavMesh baked (valid={NavMeshManager.IsBaked}), spawning {Plugin.Instance.Config.MaxBots} bots.");
            }

            if (Player.ReadyList.Count() >= Plugin.Instance.Config.MaxPlayers)
                yield break;

            for (int i = 0; i < Plugin.Instance.Config.MaxBots; i++)
            {
                new Bot();
                // Stagger spawns to avoid spike
                if (i % 3 == 2)
                    yield return Timing.WaitForOneFrame;
            }
        }
    }
}