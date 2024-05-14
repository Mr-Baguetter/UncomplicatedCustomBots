using Exiled.API.Features;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Interfaces;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class WalkingState : State, IWalkState
    {
        public WalkingState(Bot bot) : base(bot)
        {
            _way = new();
        }

        public DirectionType MoveDirections { get; set; }

        public ITarget Target { get; set; }

        private Queue<Room> _way;

        public override void Enter()
        {
            switch (Bot.Scenario.Role)
            {
                case RoleTypeId.ClassD:
                    Target = new RoomTarget(Vector3.zero, Exiled.API.Enums.RoomType.Lcz914);
                    break;
            }

            _way = Target.GetWay(Player.CurrentRoom);
        }

        public override void Exit()
        {
            
        }

        public override void Update()
        {
            switch (MoveDirections)
            {
                case DirectionType.Forward:
                    Player.Position += Player.CameraTransform.forward * 0.1f;
                    break;

                case DirectionType.Back:
                    Player.Position -= Player.CameraTransform.forward * 0.1f;
                    break;
            }
        }
    }
}
