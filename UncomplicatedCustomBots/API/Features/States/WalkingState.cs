using Exiled.API.Features;
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
        public WalkingState(Player player) : base(player)
        {
        }

        public DirectionType MoveDirections { get; set; }

        public override void Enter()
        {
            
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
