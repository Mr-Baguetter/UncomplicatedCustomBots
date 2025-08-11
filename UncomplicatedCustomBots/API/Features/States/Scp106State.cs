using CommandSystem.Commands.RemoteAdmin.Dummies;
using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.Ragdolls;
using PlayerRoles.Subroutines;
using RelativePositioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp106State : State
    {
        private Player _target;
        private float _fireTimer = 0f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.3f;
        private float _optimalDistance = 1f;
        private float _tooCloseDistance = .6f;
        private float _combatSpeed = 10f;
        private float _strafeTimer = 0f;
        private bool _isStrafing = false;
        private float _strafeDirection = 1f;
        private Vector3 _lastPosition;
        private bool _hasValidTarget = false;
        private float _stateStabilityTimer = 0f;
        private const float MIN_STATE_TIME = 2f;
        private float _targetLostTimer = 0f;
        private const float TARGET_LOST_GRACE_PERIOD = 1.5f;
        private Scp106Role scp106;

        public Scp106State(Bot bot) : base(bot)
        {
            _lastPosition = bot.Player.Position;
            _strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            scp106 = bot.Player.RoleBase as Scp106Role;
        }

        public override void Enter()
        {
            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
            {
                nav.StopNavigation();
                nav.enabled = false;
            }

            _lastPosition = Bot.Player.Position;
            _hasValidTarget = false;
            _stateStabilityTimer = 0f;
            _targetLostTimer = 0f;
            _strafeTimer = 0f;
            _isStrafing = false;
        }

        public override void Update()
        {
            _stateStabilityTimer += Time.deltaTime;

            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TARGET_CHECK_INTERVAL)
            {
                Player newTarget = Targeting.GetTarget(Bot.Player);
                bool hadTarget = _target != null;

                if (newTarget != _target)
                {
                    _target = newTarget;
                    if (_target != null && IsValidCombatTarget(_target))
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

            if (_target == null || !IsValidCombatTarget(_target))
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

            float distanceToTarget = Vector3.Distance(Bot.Player.Position, _target.Position);

            HandleCombatMovement();

            if (distanceToTarget <= 2.8f && HasLineOfSight() && !Bot.Player.HasEffect<Flashed>())
                HandleCombat();
        }

        private bool IsValidCombatTarget(Player target)
        {
            if (target == null || !target.IsAlive || target.Role == RoleTypeId.Spectator)
                return false;

            if (target.Faction == Bot.Player.Faction)
                return false;

            if (target.Role == RoleTypeId.Tutorial && !Plugin.Instance.Config.AttackTutorials)
                return false;

            float distance = Vector3.Distance(Bot.Player.Position, target.Position);
            if (distance > 25f)
                return false;

            return true;
        }

        private void HandleCombatMovement()
        {
            if (_target == null || !_target.IsAlive || !(Bot.Player.RoleBase is IFpcRole fpcRole))
                return;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 targetPosition = _target.Position;

            Vector3 direction = targetPosition - botPosition;
            float distance = direction.magnitude;

            fpcRole.FpcModule.MouseLook.LookAtDirection(direction.normalized);

            Vector3 moveDirection = Vector3.zero;
            float moveSpeed = _combatSpeed;

            if (distance > _optimalDistance)
                moveDirection = direction.normalized;
            else if (distance < _tooCloseDistance)
            {
                moveDirection = -direction.normalized;
                moveSpeed *= 0.7f;
            }

            if (moveDirection != Vector3.zero)
            {
                moveDirection.y = 0;
                Vector3 newPosition = botPosition + moveDirection * moveSpeed * Time.deltaTime;
                fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
            }
        }

        private void HandleCombat()
        {
            if (_target == null || !_target.IsAlive)
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                SilentCommandSender silentSender = new();
                Server.RunCommand($"/dummy action {Bot.Player.PlayerId} Scp106Attack Shoot->Click", silentSender);
                _fireTimer = 1.2f;
            }
        }

        private bool HasLineOfSight()
        {
            if (_target == null)
                return false;

            Vector3 botPosition = Bot.Player.Position + Vector3.up * 1.5f;
            Vector3 targetPosition = _target.Position + Vector3.up * 1.0f;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float distance = Vector3.Distance(botPosition, targetPosition);

            if (Physics.Raycast(botPosition, direction, out RaycastHit hit, distance, PlayerRolesUtils.LineOfSightMask))
                return hit.transform.root == _target.ReferenceHub.transform.root || Vector3.Distance(hit.point, targetPosition) < 0.5f;

            return true;
        }

        public override void Exit()
        {
            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
                nav.enabled = true;
        }
    }
}