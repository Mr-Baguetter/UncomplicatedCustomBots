using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class FleeState : State
    {
        private readonly Player? _scpTarget;
        private readonly Vector3 _fleeFromPosition;
        private readonly bool _fleeFromPositionOnly;
        private readonly float _fleeDistance = 30f;

        public FleeState(Bot bot, Player scp) : base(bot)
        {
            _scpTarget = scp;
            _fleeFromPosition = scp != null ? scp.Position : Vector3.zero;
        }

        public FleeState(Bot bot, Vector3 position) : base(bot)
        {
            _scpTarget = null;
            _fleeFromPosition = position;
            _fleeFromPositionOnly = true;
        }

        public override void Enter()
        {
            if (_scpTarget == null && !_fleeFromPositionOnly)
            {
                Bot.ChangeState(new WalkingState(Bot));
                return;
            }

            FindFleeDestination();
        }

        private void FindFleeDestination()
        {
            Navigation nav = Bot.Player.GameObject!.GetComponent<Navigation>();
            if (nav == null)
            {
                nav = Bot.Player.GameObject!.AddComponent<Navigation>();
            }
            else
                nav.enabled = true;
                
            if (Bot.Player.RoleBase is IFpcRole fpc)
            {
                float fleeSpeed = fpc.FpcModule.WalkSpeed * 1.7f;

                if (fpc.FpcModule.SprintSpeed > fleeSpeed)
                    fleeSpeed = fpc.FpcModule.SprintSpeed;

                fleeSpeed = Mathf.Clamp(fleeSpeed, 6f, 10f);
                nav.Init(speed: fleeSpeed, enablePatrol: false);
            }
            else
                nav.Init(speed: 18f, enablePatrol: false);

            Room? bestFleeRoom = null;
            float bestDistance = float.MinValue;
            Room? currentRoom = Bot.Player.Room;
            string? currentGOName = currentRoom?.GameObject?.name;
            string? scpRoomGOName = _scpTarget?.Room?.GameObject?.name;

            foreach (Room r in Room.List)
            {
                if (r == null)
                    continue;

                if (currentGOName != null && r.GameObject.name == currentGOName)
                    continue;

                if (scpRoomGOName != null && r.GameObject.name == scpRoomGOName)
                    continue;

                bool sameZone = currentRoom != null && r.Zone == currentRoom.Zone;
                if (bestFleeRoom != null && !sameZone && bestFleeRoom.Zone == currentRoom?.Zone)
                    continue;

                float dist = Vector3.Distance(r.Position, _fleeFromPosition);
                if (dist > bestDistance || (sameZone && bestFleeRoom?.Zone != currentRoom?.Zone))
                {
                    bestDistance = dist;
                    bestFleeRoom = r;
                }
            }

            if (bestFleeRoom != null)
            {
                LogManager.Debug($"{Bot.Player.Nickname} fleeing to {bestFleeRoom.Name} ({bestFleeRoom.Zone})");
                nav.SetDestination(bestFleeRoom);
            }
            else
                Bot.ChangeState(new WalkingState(Bot));
        }

        public override void Update()
        {
            bool fleeCondition;
            if (_scpTarget != null)
            {
                fleeCondition = _scpTarget.Role == RoleTypeId.Spectator || Vector3.Distance(Bot.Player.Position, _fleeFromPosition) > _fleeDistance;
            }
            else
                fleeCondition = Vector3.Distance(Bot.Player.Position, _fleeFromPosition) > _fleeDistance;

            if (fleeCondition)
            {
                Bot.ChangeState(new WalkingState(Bot));
                return;
            }

            if (Bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav) && !nav.IsNavigating && !nav.IsRepathBlocked)
                FindFleeDestination();
        }

        public override void Exit() { }
    }
}