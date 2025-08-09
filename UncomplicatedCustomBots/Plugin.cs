using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.Events.Internal;
using UnityEngine;
using EventTarget = LabApi.Events.Handlers.ServerEvents;
using PlayerEvents = UncomplicatedCustomBots.Events.Internal.PlayerEvents;
using Server = UncomplicatedCustomBots.Events.Internal.Server;


#if LABAPI
using LabApi.Features.Wrappers;
using LabApi.Loader;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using LabApi.Loader;
using LabApi.Events.Handlers;
#elif EXILED
using Exiled.API.Enums;
using Exiled.API.Features;
#endif

namespace UncomplicatedCustomBots
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "UncomplicatedCustomBots";
#if EXILED
        public override string Prefix => "UncomplicatedCustomBots";
#elif LABAPI
        public override string Description => "Spawns bots at the start of the round.";
#endif

#if EXILED
        public override Version RequiredExiledVersion { get; } = new(9, 6, 1);
#elif LABAPI
        public override Version RequiredApiVersion => LabApi.Features.LabApiProperties.CurrentVersion;
#endif

#if EXILED
        public override PluginPriority Priority => PluginPriority.First;
#elif LABAPI
        public override LoadPriority Priority => LoadPriority.Medium;        
#endif

        public override Version Version { get; } = new(1, 0, 0);

        public override string Author => "Mr. Baguetter";

        public static Plugin Instance;

        public Assembly Assembly => Assembly.GetExecutingAssembly();

        internal static HttpManager HttpManager;

        internal bool Prerelease { get; set; } = true;

        internal Harmony _harmony;
#if LABAPI
        public override void Enable()
        {
            Instance = this;

            HttpManager = new("usf");
            HttpManager.RegisterEvents();

            _harmony = new($"com.ucs.ucb_labapi-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            _harmony.PatchAll();

            PlayerEvents.Register();
            Server.Register();
            EventTarget.WaitingForPlayers += OnWaitingForPlayers;

            LogManager.Info("===========================================");
            LogManager.Info("Thanks for using UncomplicatedCustomBots");
            LogManager.Info($"    by {Author}");
            LogManager.Info("===========================================");
            LogManager.Info(">> Join our discord: https://discord.gg/5StRGu8EJV <<");
        }

        public override void Disable()
        {
            _harmony.UnpatchAll();
            HttpManager.UnregisterEvents();
            HttpManager = null;
            _harmony = null;
            Instance = null;

            Events.Internal.PlayerEvents.Unregister();
            Events.Internal.Server.Unregister();
            EventTarget.WaitingForPlayers -= OnWaitingForPlayers;
        }
#elif EXILED
        public override void OnEnabled()
        {
            Instance = this;

            HttpManager = new("usf");
            HttpManager.RegisterEvents();

            _harmony = new($"com.ucs.ucb_exiled-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            _harmony.PatchAll();

            PlayerEvents.Register();
            Server.Register();
            EventTarget.WaitingForPlayers += OnWaitingForPlayers;

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            _harmony.UnpatchAll();
            HttpManager.UnregisterEvents();
            HttpManager = null;
            _harmony = null;
            Instance = null;

            PlayerEvents.Unregister();
            Server.Unregister();
            EventTarget.WaitingForPlayers -= OnWaitingForPlayers;

            base.OnDisabled();
        }
#endif

        private void OnWaitingForPlayers() => _ = Updater.CheckForUpdatesAsync();
    }
}
