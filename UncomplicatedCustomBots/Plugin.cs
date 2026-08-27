global using Logger = LabApi.Features.Console.Logger;
using harmony = HarmonyLib.Harmony;
using LabApi.Features.Wrappers;
using System;
using System.Reflection;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.Events.Handlers;
using EventTarget = LabApi.Events.Handlers.ServerEvents;
using Dummy = UncomplicatedCustomBots.Events.Handlers.Dummy;
using MEC;
using UncomplicatedCustomBots.Events.Internal;
using UncomplicatedCustomBots.API.Features;


#if EXILED
using Exiled.API.Enums;
using Exiled.API.Features;
#else
using LabApi.Loader;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using LabApi.Events.Handlers;
#endif
//& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" UncomplicatedCustomBots.csproj /p:Configuration=LabApi

namespace UncomplicatedCustomBots
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "UncomplicatedCustomBots";
#if EXILED
        public override string Prefix => "UncomplicatedCustomBots";
#else
        public override string Description => "Spawns bots at the start of the round.";
#endif
#if EXILED
        public override Version RequiredExiledVersion { get; } = new(9, 6, 1);
#else
        public override Version RequiredApiVersion => LabApi.Features.LabApiProperties.CurrentVersion;
#endif
#if EXILED
        public override PluginPriority Priority => PluginPriority.First;
#else
        public override LoadPriority Priority => LoadPriority.Medium;        
#endif
        public override Version Version { get; } = new(1, 0, 0);

        public override string Author => "Mr. Baguetter & SpGerg";

        public static Plugin Instance = null!;

        #if EXILED
        public new Assembly Assembly => Assembly.GetExecutingAssembly();
#else
        public Assembly Assembly => Assembly.GetExecutingAssembly();
#endif

        internal bool Prerelease { get; set; } = true;

        private const string HarmonyId = "com.ucs.ucb";
        internal harmony _harmony = null!;
#if EXILED
        public override void OnEnabled()
#else
        public override void Enable()
#endif
        {
            Instance = this;

            _harmony = new(HarmonyId);
            _harmony.PatchAll();

            PlayerHandler.Register();
            ServerHandler.Register();
            SensoryEvents.Register();
            EventTarget.WaitingForPlayers += OnWaitingForPlayers;

            LogManager.Info("===========================================");
            LogManager.Info("Thanks for using UncomplicatedCustomBots");
            LogManager.Info($"    by {Author}");
            LogManager.Info("===========================================");
            LogManager.Info(">> Join our discord: https://discord.gg/5StRGu8EJV <<");

            LogManager.StartFlushCoroutine();

            Bot.EnsureGlobalCollisionsIgnored();

            if (Config.Debug)
            {
                Dummy.DummySpawning += OnDummySpawning;
                Dummy.DummySpawned += OnDummySpawned;
            }

#if EXILED
            base.OnEnabled();
#endif
        }

#if EXILED
        public override void OnDisabled()
#else
        public override void Disable()
#endif
        {
            LogManager.StopFlushCoroutine();

            _harmony.UnpatchAll();
            _harmony = null!;
            Instance = null!;

            PlayerHandler.Unregister();
            ServerHandler.Unregister();
            SensoryEvents.Unregister();
            EventTarget.WaitingForPlayers -= OnWaitingForPlayers;
            if (Config.Debug)
            {
                Dummy.DummySpawning -= OnDummySpawning;
                Dummy.DummySpawned -= OnDummySpawned;
            }

#if EXILED
            base.OnDisabled();
#endif
        }
        
        private void OnWaitingForPlayers()
        {
            Bot.EnsureGlobalCollisionsIgnored();
            Timing.RunCoroutine(Updater.CheckForUpdatesCoroutine());
        }

        private void OnDummySpawning(DummySpawningEventArgs ev)
        {
            LogManager.Debug($"Dummy {ev.Player.DisplayName} is Spawning!");
        }
        private void OnDummySpawned(DummySpawnedEventArgs ev)
        {
            LogManager.Debug($"Dummy {ev.Player.DisplayName} Spawned!");
        }
    }
}
