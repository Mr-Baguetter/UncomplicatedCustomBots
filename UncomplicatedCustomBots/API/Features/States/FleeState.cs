using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using System.Linq;
using UncomplicatedCustomBots.API.Features.Components;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class FleeState : State
    {
        private Player _scpTarget;
        private float _fleeDistance = 30f;
        private float _repathTimer = 0f;
        private const float RepathInterval = 2f;

        public FleeState(Bot bot, Player scp) : base(bot)
        {
            _scpTarget = scp;
        }

        public override void Enter()
        {
            _repathTimer = 0f;
            FindFleeDestination();
        }

        private void FindFleeDestination()
        {
            if (_scpTarget == null)
            {
                Bot.ChangeState(new WalkingState(Bot));
                return;
            }

            if (!Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
            {
                nav = Bot.Player.GameObject.AddComponent<Navigation>();
                nav.Init(speed: 20f, enablePatrol: false);
            }

            if (Bot.Player.Room != null)
            {
                Room bestFleeRoom = Room.List.Where(r => r.Zone == Bot.Player.Room.Zone && r != Bot.Player.Room).OrderByDescending(r => Vector3.Distance(r.Position, _scpTarget.Position)).FirstOrDefault();
                if (bestFleeRoom != null)
                {
                    nav.SetDestination(bestFleeRoom);
                    return;
                }
            }

            Room fallbackRoom = Room.List.OrderByDescending(r => Vector3.Distance(r.Position, _scpTarget.Position)).FirstOrDefault();
            if (fallbackRoom != null)
                nav.SetDestination(fallbackRoom);
            else
                Bot.ChangeState(new WalkingState(Bot));
        }

        public override void Update()
        {
            if (_scpTarget == null || _scpTarget.Role == RoleTypeId.Spectator || Vector3.Distance(Bot.Player.Position, _scpTarget.Position) > _fleeDistance)
            {
                Bot.ChangeState(new WalkingState(Bot));
                return;
            }

            _repathTimer += Time.deltaTime;
            if (_repathTimer >= RepathInterval)
            {
                _repathTimer = 0f;
                FindFleeDestination();
            }

            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav) && !nav.IsNavigating)
                FindFleeDestination();
        }

        public override void Exit() { }
    }
}