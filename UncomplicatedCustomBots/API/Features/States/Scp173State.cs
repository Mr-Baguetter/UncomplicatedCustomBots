using CommandSystem.Commands.RemoteAdmin.Dummies;
using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Scp173;
using PlayerRoles.PlayableScps.Scp939;
using PlayerRoles.Ragdolls;
using PlayerRoles.Subroutines;
using RelativePositioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp173State : State
    {
        private Player _target;
        private float _fireTimer = 0f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.3f;
        private float _optimalDistance = 1.5f;
        private float _tooCloseDistance = 0.8f;
        private float _combatSpeed = 10f;
        private LayerMask _ragdollLayerMask;
        private float _strafeTimer = 0f;
        private bool _isStrafing = false;
        private float _strafeDirection = 1f;
        private Vector3 _lastPosition;
        private bool _hasValidTarget = false;
        private float _stateStabilityTimer = 0f;
        private const float MIN_STATE_TIME = 2f;
        private float _targetLostTimer = 0f;
        private const float TARGET_LOST_GRACE_PERIOD = 1.5f;
        private Scp173Role scp173;
        private Scp173MovementModule movementModule;
        private Scp173TeleportAbility teleportAbility;
        private Scp173ObserversTracker observersTracker;
        private Scp173SnapAbility snapAbility;
        private float _teleportCooldown = 0f;
        private const float TELEPORT_COOLDOWN_TIME = 2f;
        private float _observedCheckTimer = 0f;
        private const float OBSERVED_CHECK_INTERVAL = 0.1f;
        private const float SNAP_RANGE = 3f;

        public Scp173State(Bot bot) : base(bot)
        {
            _ragdollLayerMask = LayerMask.GetMask("Ragdoll");
            _lastPosition = bot.Player.Position;
            _strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            scp173 = bot.Player.RoleBase as Scp173Role;
            movementModule = scp173.FpcModule as Scp173MovementModule;
            scp173.SubroutineModule.TryGetSubroutine(out teleportAbility);
            scp173.SubroutineModule.TryGetSubroutine(out observersTracker);
            scp173.SubroutineModule.TryGetSubroutine(out snapAbility);
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
            _teleportCooldown = 0f;
        }

        public override void Update()
        {
            _stateStabilityTimer += Time.deltaTime;
            _teleportCooldown -= Time.deltaTime;

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

            _observedCheckTimer += Time.deltaTime;
            if (_observedCheckTimer >= OBSERVED_CHECK_INTERVAL)
            {
                HandleObservedTeleport();
                _observedCheckTimer = 0f;
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

        private void HandleObservedTeleport()
        {
            if (_target != null && observersTracker != null && observersTracker.IsObserved &&
                _teleportCooldown <= 0f && teleportAbility != null)
            {
                Vector3 teleportPosition = CalculateOptimalTeleportPosition();

                Vector3 lookDirection = (teleportPosition - Bot.Player.Position).normalized;
                scp173.FpcModule.MouseLook.LookAtDirection(lookDirection);

                SilentCommandSender silentSender = new();
                Server.RunCommand($"/dummy action {Bot.Player.PlayerId} Scp173TeleportAbility Zoom->Click", silentSender);
                _teleportCooldown = TELEPORT_COOLDOWN_TIME;
            }
        }

        private Vector3 CalculateOptimalTeleportPosition()
        {
            if (_target == null)
                return Bot.Player.Position;

            Vector3 targetPosition = _target.Position;
            Vector3 botPosition = Bot.Player.Position;
            float distanceToTarget = Vector3.Distance(botPosition, targetPosition);

            if (distanceToTarget <= 10f)
            {
                Vector3 targetForward = _target.ReferenceHub.transform.forward;
                Vector3 behindTarget = targetPosition - targetForward * 1.5f;

                if (IsValidTeleportPosition(behindTarget))
                    return behindTarget;

                Vector3 targetRight = _target.ReferenceHub.transform.right;
                Vector3 rightSide = targetPosition + targetRight * 2f;
                Vector3 leftSide = targetPosition - targetRight * 2f;

                if (IsValidTeleportPosition(rightSide))
                    return rightSide;
                if (IsValidTeleportPosition(leftSide))
                    return leftSide;

                if (IsValidTeleportPosition(targetPosition))
                    return targetPosition;
            }

            Vector3 directionToTarget = (targetPosition - botPosition).normalized;
            float teleportDistance = Mathf.Min(distanceToTarget - 2f, 15f);
            Vector3 closerPosition = botPosition + directionToTarget * teleportDistance;

            if (IsValidTeleportPosition(closerPosition))
                return closerPosition;

            return targetPosition;
        }

        private bool IsValidTeleportPosition(Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 5f))
                return false;

            Collider[] overlapping = Physics.OverlapSphere(position, 0.5f);
            foreach (var collider in overlapping)
            {
                if (collider.gameObject.layer == LayerMask.NameToLayer("Players") || collider.gameObject.layer == LayerMask.NameToLayer("Ragdoll"))
                    continue;
                if (collider.isTrigger == false)
                    return false;
            }

            return true;
        }

        private void HandleCombatBehavior()
        {
            if (_target == null)
                return;

            float distanceToTarget = Vector3.Distance(Bot.Player.Position, _target.Position);

            Vector3 lookDirection = (_target.Position - Bot.Player.Position).normalized;
            scp173.FpcModule.MouseLook.LookAtDirection(lookDirection);

            if (observersTracker == null || !observersTracker.IsObserved)
                HandleCombatMovement();

            if (distanceToTarget <= SNAP_RANGE && CanSnap())
                HandleCombat();
        }

        private bool CanSnap()
        {
            if (_target == null || !_target.IsAlive)
                return false;

            if (observersTracker != null && observersTracker.IsObserved)
                return false;

            return HasLineOfSight();
        }

        private void MoveTowardsPosition(Vector3 targetPosition)
        {
            if (!(Bot.Player.RoleBase is IFpcRole fpcRole))
                return;

            if (observersTracker != null && observersTracker.IsObserved)
                return;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 direction = (targetPosition - botPosition);

            direction.y = 0;
            direction = direction.normalized;

            if (direction.sqrMagnitude < 0.01f)
                return;

            Vector3 lookDirection = (targetPosition - botPosition).normalized;
            fpcRole.FpcModule.MouseLook.LookAtDirection(lookDirection);

            Vector3 newPosition = botPosition + direction * _combatSpeed * Time.deltaTime;

            if (newPosition.y > botPosition.y - 2f && newPosition.y < botPosition.y + 2f)
                fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
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

            if (observersTracker != null && observersTracker.IsObserved)
                return;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 targetPosition = _target.Position;

            Vector3 direction = targetPosition - botPosition;
            float distance = direction.magnitude;

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
                Server.RunCommand($"/dummy action {Bot.Player.PlayerId} Scp173SnapAbility Shoot->Click", silentSender);
                _fireTimer = 0.5f;
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