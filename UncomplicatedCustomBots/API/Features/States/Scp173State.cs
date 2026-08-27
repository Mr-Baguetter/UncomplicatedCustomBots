using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp173;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp173State : State
    {
        private Player _target = null!;
        private float _fireTimer = 0f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.3f;
        private readonly float _optimalDistance = 1.5f;
        private readonly float _combatSpeed = 10f;
        private bool _hasValidTarget = false;
        private float _stateStabilityTimer = 0f;
        private const float MIN_STATE_TIME = 2f;
        private float _targetLostTimer = 0f;
        private const float TARGET_LOST_GRACE_PERIOD = 1.5f;
        private readonly Scp173Role scp173;
        private readonly Scp173TeleportAbility teleportAbility;
        private readonly Scp173SnapAbility snapAbility;
        private readonly Scp173ObserversTracker observersTracker;
        private float _teleportCooldown = 0f;
        private const float TELEPORT_COOLDOWN_TIME = 2f;
        private float _observedCheckTimer = 0f;
        private const float OBSERVED_CHECK_INTERVAL = 0.1f;
        private static readonly Collider[] _overlapBuffer = new Collider[16];
        private static readonly int _playersLayer = LayerMask.NameToLayer("Players");
        private static readonly int _ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        private Navigation _navigator = null!;
        private float _combatPathTimer = 0f;
        private const float CombatPathRecalcInterval = 0.5f;

        public Scp173State(Bot bot) : base(bot)
        {
            scp173 = (bot.Player.RoleBase as Scp173Role)!;
            scp173.SubroutineModule.TryGetSubroutine(out teleportAbility);
            scp173.SubroutineModule.TryGetSubroutine(out observersTracker);
            scp173.SubroutineModule.TryGetSubroutine(out snapAbility);
        }

        public override void Enter()
        {
            Navigation? cached = Bot.CachedNavigation;
            if (cached == null)
            {
                cached = Bot.Player.GameObject!.AddComponent<Navigation>();
                Bot.CachedNavigation = cached;
            }

            _navigator = cached;

            if (_navigator.IsInsideElevatorChamber || _navigator.IsWalkingIntoElevator || _navigator.IsWaitingForElevator || _navigator.IsWaitingToEnterElevator)
            {
                _hasValidTarget = false;
                _stateStabilityTimer = 0f;
                _targetLostTimer = 0f;
                _teleportCooldown = 0f;
                _combatPathTimer = 0f;
                return;
            }

            _navigator.enabled = true;

            if (Bot.Player.RoleBase is IFpcRole fpc)
            {
                float chaseSpeed = _combatSpeed;

                if (fpc.FpcModule.SprintSpeed > chaseSpeed)
                    chaseSpeed = fpc.FpcModule.SprintSpeed;

                chaseSpeed = Mathf.Clamp(chaseSpeed, 6f, 10f);

                if (chaseSpeed < fpc.FpcModule.WalkSpeed * 1.6f)
                    chaseSpeed = fpc.FpcModule.WalkSpeed * 1.6f;

                _navigator.Init(speed: chaseSpeed, enablePatrol: false);
            }
            else
            {
                _navigator.Init(speed: _combatSpeed, enablePatrol: false);
            }

            _navigator.StopNavigation();

            _hasValidTarget = false;
            _stateStabilityTimer = 0f;
            _targetLostTimer = 0f;
            _teleportCooldown = 0f;
            _combatPathTimer = 0f;
        }

        public override void Update()
        {
            _stateStabilityTimer += Time.deltaTime;
            _teleportCooldown -= Time.deltaTime;

            Navigation? elevatorNav = Bot.CachedNavigation;
            if (elevatorNav != null && (elevatorNav.IsInsideElevatorChamber || elevatorNav.IsWalkingIntoElevator || elevatorNav.IsWaitingForElevator || elevatorNav.IsWaitingToEnterElevator))
                return;

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

            _observedCheckTimer += Time.deltaTime;
            if (_observedCheckTimer >= OBSERVED_CHECK_INTERVAL)
            {
                HandleObservedTeleport();
                _observedCheckTimer = 0f;
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

        private void HandleObservedTeleport()
        {
            if (_target != null && observersTracker != null && observersTracker.IsObserved && _teleportCooldown <= 0f && teleportAbility != null)
            {
                if (_navigator != null && _navigator.IsNavigating)
                    _navigator.StopNavigation();

                _combatPathTimer = 0f;

                Vector3 teleportPosition = CalculateOptimalTeleportPosition();
                Vector3 lookDirection = (teleportPosition - Bot.Player.Position).normalized;
                scp173.FpcModule.MouseLook.LookAtDirection(lookDirection);
                BotExtensions.TryRunRoleAction(teleportAbility, ActionName.Zoom, true);
                _teleportCooldown = TELEPORT_COOLDOWN_TIME;
            }
        }

        private Vector3 CalculateOptimalTeleportPosition()
        {
            if (_target == null)
                return Bot.Player.Position;

            Vector3 targetPosition = _target.Position;
            Vector3 botPosition = Bot.Player.Position;
            float sq = (botPosition - targetPosition).sqrMagnitude;

            if (sq <= 100f)
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

            float distanceToTarget = Mathf.Sqrt(sq);
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

            int count = Physics.OverlapSphereNonAlloc(position, 0.5f, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlapBuffer[i];
                if (collider.gameObject.layer == _playersLayer || collider.gameObject.layer == _ragdollLayer)
                    continue;

                if (!collider.isTrigger)
                    return false;
            }

            return true;
        }

        private void HandleCombatBehavior()
        {
            if (_target == null)
                return;

            Vector3 lookDirection = (_target.Position - Bot.Player.Position).normalized;
            scp173.FpcModule.MouseLook.LookAtDirection(lookDirection);

            if (observersTracker == null || !observersTracker.IsObserved)
            {
                HandleCombatMovement();
            }
            else
            {
                if (_navigator != null && _navigator.IsNavigating)
                    _navigator.StopNavigation();

                _combatPathTimer = 0f;
            }

            if ((Bot.Player.Position - _target.Position).sqrMagnitude <= 9f && CanSnap())
                HandleCombat();
        }

        private bool CanSnap()
        {
            if (_target == null || !_target.IsAlive)
                return false;

            if (observersTracker != null && observersTracker.IsObserved)
                return false;

            return Bot.HasLineOfSight(_target, PlayerRolesUtils.LineOfSightMask);
        }

        private void HandleCombatMovement()
        {
            if (observersTracker != null && observersTracker.IsObserved)
            {
                _combatPathTimer = 0f;

                if (_navigator != null && _navigator.IsNavigating)
                    _navigator.StopNavigation();

                return;
            }

            if (_target == null || _navigator == null)
                return;

            if (_navigator.IsInsideElevatorChamber || _navigator.IsWalkingIntoElevator || _navigator.IsWaitingForElevator || _navigator.IsWaitingToEnterElevator)
                return;

            Vector3 targetPosition = _target.Position;
            Vector3 botPosition = Bot.Player.Position;
            float sq = (botPosition - targetPosition).sqrMagnitude;

            if (sq <= _optimalDistance * _optimalDistance)
            {
                StopChaseMovement();
                return;
            }

            HandleChaseMovement(targetPosition);
        }

        private void HandleChaseMovement(Vector3 targetPosition)
        {
            if (_navigator == null)
                return;

            _combatPathTimer -= Time.deltaTime;
            if (_combatPathTimer <= 0f)
            {
                Vector3 projected = _navigator.ProjectToNavMesh(targetPosition);

                if (!_navigator.NavigateToWorldPosition(projected))
                {
                    if (Plugin.Instance.Config.Debug)
                    {
                        LogManager.Debug($"{Bot.Player.Nickname} failed to build navmesh chase path to {targetPosition}, moving directly.");
                    }
                }

                _combatPathTimer = CombatPathRecalcInterval;
            }

            if (_navigator.IsNavigating)
                return;

            _navigator.MoveTowards(_navigator.ProjectToNavMesh(targetPosition), _combatSpeed, lookAtTarget: false);
        }

        private void StopChaseMovement()
        {
            _combatPathTimer = 0f;

            if (_navigator != null && _navigator.IsNavigating)
                _navigator.StopNavigation();
        }

        private void HandleCombat()
        {
            if (_target == null || !_target.IsAlive)
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                BotExtensions.TryRunRoleAction(snapAbility, ActionName.Shoot, true);
                _fireTimer = 0.5f;
            }
        }

        public override void Exit()
        {
            _combatPathTimer = 0f;

            Navigation? nav = _navigator ?? Bot.CachedNavigation;
            nav?.enabled = true;
        }
    }
}
