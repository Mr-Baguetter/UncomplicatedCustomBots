using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp939;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp939State : State
    {
        private Player _target = null!;
        private float _fireTimer = 0f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.3f;
        private readonly float _optimalDistance = 1f;
        private readonly float _tooCloseDistance = .6f;
        private readonly float _combatSpeed = 10f;
        private bool _hasValidTarget = false;
        private float _stateStabilityTimer = 0f;
        private const float MIN_STATE_TIME = 2f;
        private float _targetLostTimer = 0f;
        private const float TARGET_LOST_GRACE_PERIOD = 1.5f;
        private readonly Scp939Role scp939;
        private readonly Scp939ClawAbility clawModule;

        public Scp939State(Bot bot) : base(bot)
        {
            scp939 = (Bot.Player.RoleBase as Scp939Role)!;
            scp939.SubroutineModule.TryGetSubroutine(out clawModule);
        }

        public override void Enter()
        {
            Navigation? nav = Bot.CachedNavigation;
            if (nav != null)
            {
                if (!nav.IsInsideElevatorChamber && !nav.IsWalkingIntoElevator && !nav.IsWaitingForElevator && !nav.IsWaitingToEnterElevator)
                {
                    nav.StopNavigation();
                    nav.enabled = false;
                }
            }

            _hasValidTarget = false;
            _stateStabilityTimer = 0f;
            _targetLostTimer = 0f;
        }

        public override void Update()
        {
            _stateStabilityTimer += Time.deltaTime;

            Navigation? elevatorNav = Bot.CachedNavigation;
            if (elevatorNav != null && (elevatorNav.IsInsideElevatorChamber || elevatorNav.IsWalkingIntoElevator || elevatorNav.IsWaitingForElevator || elevatorNav.IsWaitingToEnterElevator))
            {
                return;
            }

            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TARGET_CHECK_INTERVAL)
            {
                Player? newTarget = Targeting.GetTarget(Bot, _target);
                bool hadTarget = _target != null;

                if (newTarget != null && newTarget != _target)
                {
                    _target = newTarget;
                    if (_target != null && Bot.IsValidCombatTarget(_target))
                    {
                        _hasValidTarget = true;
                        _targetLostTimer = 0f;
                    }
                    else if (hadTarget)
                    {
                        _targetLostTimer = 0f;
                    }
                }
                _targetCheckTimer = 0f;
            }

            if (_target == null || !Bot.IsValidCombatTarget(_target))
            {
                _targetLostTimer += Time.deltaTime;
                _hasValidTarget = false;

                if (_stateStabilityTimer >= MIN_STATE_TIME && _targetLostTimer >= TARGET_LOST_GRACE_PERIOD)
                {
                    Bot.ChangeState(new WalkingState(Bot));
                    return;
                }
            }
            else
            {
                _targetLostTimer = 0f;
                _hasValidTarget = true;
            }

            if (_hasValidTarget && _target != null)
                HandleCombatBehavior();
        }

        private void HandleCombatBehavior()
        {
            if (_target == null) 
                return;

            Bot.MoveToOptimalDistance(_target, _optimalDistance, _tooCloseDistance, _combatSpeed);
            if ((Bot.Player.Position - _target.Position).sqrMagnitude <= 7.84f && Bot.HasLineOfSight(_target, PlayerRolesUtils.LineOfSightMask) && !Bot.Player.HasEffect<Flashed>())
                HandleCombat();
        }

        private void HandleCombat()
        {
            if (_target == null || !_target.IsAlive)
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                BotExtensions.TryRunRoleAction(clawModule, ActionName.Shoot, true);
                _fireTimer = 1.2f;
            }
        }

        public override void Exit()
        {
            Navigation? nav = Bot.CachedNavigation;
            if (nav != null)
            {
                nav.enabled = true;
            }
        }
    }
}