using Exiled.API.Features;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Interfaces;

namespace UncomplicatedCustomBots.API.Features
{
    public class Bot
    {
        public Bot(Player player)
        {
            Player = player;

            ChangeRole(player.Role.Type);

            Player.GameObject.AddComponent<BotComponent>().Initialize(this);
        }

        public void ChangeRole(RoleTypeId roleTypeId)
        {
            Scenario = Scenario.Create(roleTypeId);
        }

        public void Move(DirectionType directionType)
        {
            if (State is not IWalkState walkState)
            {
                return;
            }

            walkState.MoveDirections = directionType;
        }

        public Player Player { get; private set; }

        public State State { get; private set; }

        public Scenario Scenario { get; private set; }
    }
}
