using LabApi.Features.Wrappers;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.Events.Handlers;
using UnityEngine.AI;

namespace UncomplicatedCustomBots.API.Features
{
    public class Bot
    {
        public static readonly List<Player> PlayerList = [];
        public static readonly List<Bot> BotList = [];
        private static readonly Dictionary<int, Bot> BotByPlayerId = [];
        private static readonly System.Random random = new();
        private static readonly object _randomLock = new();
        private static readonly object _botListLock = new();
        public BotRadius? BotDetectionRadius { get; set; }

        private static void RegisterBot(Bot bot)
        {
            lock (_botListLock)
            {
                PlayerList.Add(bot.Player);
                BotList.Add(bot);
                if (bot.Player != null)
                    BotByPlayerId[bot.Player.PlayerId] = bot;
            }
        }

        private static void UnregisterBot(Bot bot)
        {
            lock (_botListLock)
            {
                PlayerList.Remove(bot.Player);
                BotList.Remove(bot);
                if (bot.Player != null)
                    BotByPlayerId.Remove(bot.Player.PlayerId);
            }
        }

        public static bool TryGetByPlayerId(int playerId, out Bot bot)
        {
            lock (_botListLock)
            {
                return BotByPlayerId.TryGetValue(playerId, out bot!);
            }
        }

        public static Bot[] SnapshotBotList()
        {
            lock (_botListLock)
            {
                return BotList.ToArray();
            }
        }

        public static Player[] SnapshotPlayerList()
        {
            lock (_botListLock)
            {
                return PlayerList.ToArray();
            }
        }

        public Bot()
        {
            string randomName;
            lock (_randomLock)
            {
                randomName = Plugin.Instance.Config.Names[random.Next(Plugin.Instance.Config.Names.Count)];
            }

            ReferenceHub hub = DummyUtils.SpawnDummy(randomName);
            Player = Player.Get(hub) ?? throw new System.InvalidOperationException($"Player.Get returned null for dummy '{randomName}'");

            RegisterBot(this);

            Player.GameObject!.AddComponent<BotComponent>().Initialize(this);
            BotDetectionRadius = Player.GameObject!.AddComponent<BotRadius>();
            BotDetectionRadius?.Init(this);
            Player.GameObject!.AddComponent<Scp173StareMonitor>().Initialize(this);
 
            Player.InfoArea |= PlayerInfoArea.CustomInfo;
            Player.CustomInfo = "Bot";
        }

        public Bot(ReferenceHub hub)
        {
            if (hub == null)
                throw new System.ArgumentNullException(nameof(hub));
                
            Player = Player.Get(hub) ?? throw new System.InvalidOperationException("Player.Get returned null for provided hub");

            RegisterBot(this);

            Player.GameObject!.AddComponent<BotComponent>().Initialize(this);
            BotDetectionRadius = Player.GameObject!.AddComponent<BotRadius>();
            BotDetectionRadius?.Init(this);
            Player.GameObject!.AddComponent<Scp173StareMonitor>().Initialize(this);

            Player.InfoArea |= PlayerInfoArea.CustomInfo;
            Player.CustomInfo = "Bot";
        }

        public void Start()
        {
            if (Player.Role == RoleTypeId.Spectator || Player.Role == RoleTypeId.Destroyed)
            {
                LogManager.Warn($"Cannot start a bot if it is a Spectator or Destroyed!");
                return;
            }

            Player.GroupName = string.Empty;
            State = new WalkingState(this);
            Timing.CallDelayed(Timing.WaitForOneFrame, () => State?.Enter());
        }

        public void RemoveGroup(Player player) => player.UserGroup = null;

        public void ChangeState(States.State newState)
        {
            SwitchingStateEventArgs switchingEventArgs = new(State, newState, this, true);
            Events.Handlers.State.OnStateSwitching(switchingEventArgs);
            if (!switchingEventArgs.IsAllowed)
                return;
                 
            States.State oldState = State;
            oldState?.Exit();
            State = newState;
            State?.Enter();
            SwitchedStateEventArgs switchedEventArgs = new(oldState!, newState, this);
            Events.Handlers.State.OnStateSwitched(switchedEventArgs);
        }

        public void Destroy()
        {
            State?.Exit();

            Context.ClearMemory();

            if (Player != null)
                SquadManager.RemoveFromSquad(this);

            if (Objective.ActiveObjectives.Count > 0)
            {
                uint? toRemove = null;
                foreach (KeyValuePair<uint, Objective> kv in Objective.Objectives)
                {
                    if (kv.Value.Bot == this)
                    {
                        toRemove = kv.Key;
                        break;
                    }
                }
                if (toRemove.HasValue)
                    Objective.Objectives.Remove(toRemove.Value);
            }

            UnregisterBot(this);

            BotComponent? botComponent = Player?.GameObject?.GetComponent<BotComponent>();
            if (botComponent != null)
                UnityEngine.Object.Destroy(botComponent);

            Navigation? navigation = Player?.GameObject?.GetComponent<Navigation>();
            if (navigation != null)
                UnityEngine.Object.Destroy(navigation);

            NavMeshAgent? agent = Player?.GameObject?.GetComponent<NavMeshAgent>();
            if (agent != null)
                UnityEngine.Object.Destroy(agent);

            BotRadius? radius = Player?.GameObject?.GetComponent<BotRadius>();
            if (radius != null)
                UnityEngine.Object.Destroy(radius);

            Scp173StareMonitor? stareMonitor = Player?.GameObject?.GetComponent<Scp173StareMonitor>();
            if (stareMonitor != null)
                UnityEngine.Object.Destroy(stareMonitor);

            Targeting.RemoveBot(this);
        }

        public void Update() => State?.Update();

        public Player Player { get; set; }

        public BotContext Context { get; } = new();

        public States.State State { get; private set; } = null!;

        public int SquadId { get; set; } = -1;

        public bool IsInSquad => SquadId >= 0;

        public bool IsMtf => Player.Role == RoleTypeId.NtfCaptain || Player.Role ==  RoleTypeId.NtfSergeant || Player.Role == RoleTypeId.NtfSpecialist || Player.Role == RoleTypeId.NtfPrivate;

        public bool IsChaos => Player.Team == Team.ChaosInsurgency;

        public bool IsGuard => Player.Role == RoleTypeId.FacilityGuard;

        public bool IsSquadBot => IsMtf || IsChaos || IsGuard;
    }
}