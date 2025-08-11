using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Interfaces;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.Events.Handlers;
using Unity.Mathematics;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace UncomplicatedCustomBots.API.Features
{
    public class Bot
    {
        public static readonly List<Player> PlayerList = [];
        public static readonly List<Bot> BotList = [];
        private static readonly System.Random random = new();

        public Bot()
        {
            string randomName = Plugin.Instance.Config.Names[random.Next(Plugin.Instance.Config.Names.Count)];
            ReferenceHub hub = DummyUtils.SpawnDummy(randomName);
            Player player = Player.Get(hub);

            Player = player;

            PlayerList.Add(player);
            BotList.Add(this);

            Scenario = Scenario.Create(player.Role);

            ChangeRole(player.Role);

            Player.GameObject.AddComponent<BotComponent>().Initialize(this);
        }

        public Bot(ReferenceHub hub)
        {
            Player player = Player.Get(hub);

            Player = player;

            PlayerList.Add(player);
            BotList.Add(this);

            Scenario = Scenario.Create(player.Role);

            ChangeRole(player.Role);

            Player.GameObject.AddComponent<BotComponent>().Initialize(this);
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

        public void ChangeRole(RoleTypeId roleTypeId) => Scenario = Scenario.Create(roleTypeId);

        public void RemoveGroup(Player player) => player.UserGroup = null;

        public void ChangeState(State newState)
        {
            SwitchingStateEventArgs switchingEventArgs = new(State, newState, this, true);
            Events.Handlers.State.OnStateSwitching(switchingEventArgs);
            if (!switchingEventArgs.IsAllowed)
                return;
                
            State?.Exit();
            State = newState;
            State?.Enter();
            SwitchedStateEventArgs switchedEventArgs = new(State, newState, this);
            Events.Handlers.State.OnStateSwitched(switchedEventArgs);
        }

        public void Destroy()
        {
            State?.Exit();

            if (Player != null && PlayerList.Contains(Player))
                PlayerList.Remove(Player);

            if (BotList.Contains(this))
                BotList.Remove(this);

            BotComponent botComponent = Player?.GameObject?.GetComponent<BotComponent>();
            if (botComponent != null)
                UnityEngine.Object.Destroy(botComponent);

            Navigation navigation = Player?.GameObject?.GetComponent<Navigation>();
            if (navigation != null)
                UnityEngine.Object.Destroy(navigation);
        }

        public void Update() => State?.Update();

        public Player Player { get; private set; }

        public State State { get; private set; }

        public Scenario Scenario { get; private set; }
    }
}