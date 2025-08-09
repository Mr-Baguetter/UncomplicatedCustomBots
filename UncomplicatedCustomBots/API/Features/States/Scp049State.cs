using CommandSystem.Commands.RemoteAdmin.Dummies;
using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.Ragdolls;
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
    internal class Scp049State : State
    {
        private Player _target;
        private Transform _ragdollTarget;
        private float _fireTimer = 0f;
        private float _targetCheckTimer = 0f;
        private float _ragdollCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.3f;
        private const float RAGDOLL_CHECK_INTERVAL = 1.0f;
        private float _optimalDistance = 1f;
        private float _tooCloseDistance = .6f;
        private float _combatSpeed = 10f;
        private const float RAGDOLL_DETECTION_RADIUS = 15f;
        private const float RESURRECT_DISTANCE = 2f;
        private bool _isResurrecting = false;
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

        public static readonly CachedLayerMask HitregMask = new("Default", "Hitbox", "Glass", "CCTV", "Door");

        public Scp049State(Bot bot) : base(bot)
        {
            _ragdollLayerMask = LayerMask.GetMask("Ragdoll");
            _lastPosition = bot.Player.Position;
            _strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
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

            if (HandleRagdollBehavior())
                return;

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

        private bool HandleRagdollBehavior()
        {
            if (_isResurrecting)
            {
                if (_ragdollTarget == null)
                {
                    StopResurrection();
                    return false;
                }

                BasicRagdoll basicRagdoll = _ragdollTarget.GetComponent<BasicRagdoll>();
                if (basicRagdoll == null)
                {
                    StopResurrection();
                    return false;
                }

                Ragdoll ragdoll = Ragdoll.Get(basicRagdoll);
                if (ragdoll == null || !ragdoll.IsRevivableBy(Bot.Player))
                {
                    StopResurrection();
                    return false;
                }

                float distance = Vector3.Distance(Bot.Player.Position, _ragdollTarget.position);
                if (distance > RESURRECT_DISTANCE + 1f)
                {
                    StopResurrection();
                    return false;
                }

                LookAtRagdoll();

                return true;
            }

            _ragdollCheckTimer += Time.deltaTime;
            if (_ragdollCheckTimer >= RAGDOLL_CHECK_INTERVAL)
            {
                _ragdollCheckTimer = 0f;
                if (_ragdollTarget == null)
                    FindNearestRagdoll();
                else
                {
                    BasicRagdoll basicRagdoll = _ragdollTarget.GetComponent<BasicRagdoll>();
                    if (basicRagdoll != null)
                    {
                        Ragdoll ragdoll = Ragdoll.Get(basicRagdoll);
                        if (ragdoll == null || !ragdoll.IsRevivableBy(Bot.Player))
                        {
                            _ragdollTarget = null;
                            FindNearestRagdoll();
                        }
                    }
                    else
                    {
                        _ragdollTarget = null;
                        FindNearestRagdoll();
                    }
                }
            }

            if (_ragdollTarget != null)
            {
                float distance = Vector3.Distance(Bot.Player.Position, _ragdollTarget.position);

                LookAtRagdoll();

                if (distance <= RESURRECT_DISTANCE)
                {
                    BasicRagdoll basicRagdoll = _ragdollTarget.GetComponent<BasicRagdoll>();
                    if (basicRagdoll != null)
                    {
                        Ragdoll ragdoll = Ragdoll.Get(basicRagdoll);
                        if (ragdoll != null && ragdoll.IsRevivableBy(Bot.Player))
                        {
                            StartResurrection();
                            return true;
                        }
                        else
                        {
                            _ragdollTarget = null;
                            return false;
                        }
                    }
                    else
                    {
                        _ragdollTarget = null;
                        return false;
                    }
                }
                else
                {
                    MoveTowardsPosition(_ragdollTarget.position);
                    return true;
                }
            }

            return false;
        }

        private void LookAtRagdoll()
        {
            if (_ragdollTarget == null || !(Bot.Player.RoleBase is IFpcRole fpcRole))
                return;

            Vector3 botPosition = Bot.Player.Camera.position;
            Vector3 ragdollPosition = _ragdollTarget.position;

            Vector3 lookDirection = (ragdollPosition - botPosition).normalized;
            fpcRole.FpcModule.MouseLook.LookAtDirection(lookDirection);
        }

        private void FindNearestRagdoll()
        {
            Collider[] colliders = Physics.OverlapSphere(Bot.Player.Position, RAGDOLL_DETECTION_RADIUS, _ragdollLayerMask);

            Transform closestRagdoll = null;
            float closestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                if (collider.TryGetComponent<BasicRagdoll>(out var basicRagdoll) && basicRagdoll.Info.RoleType.GetTeam() != Team.SCPs && basicRagdoll.Info.RoleType.GetTeam() != Team.Flamingos)
                {
                    Ragdoll ragdoll = Ragdoll.Get(basicRagdoll);
                    if (ragdoll != null && ragdoll.IsRevivableBy(Bot.Player))
                    {
                        float distance = Vector3.Distance(Bot.Player.Position, collider.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestRagdoll = collider.transform;
                        }
                    }
                }
            }

            _ragdollTarget = closestRagdoll;
        }

        private void MoveTowardsPosition(Vector3 targetPosition)
        {
            if (!(Bot.Player.RoleBase is IFpcRole fpcRole))
                return;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 direction = (targetPosition - botPosition);

            direction.y = 0;
            direction = direction.normalized;

            if (direction.sqrMagnitude < 0.01f)
                return;

            Vector3 newPosition = botPosition + direction * _combatSpeed * Time.deltaTime;

            if (newPosition.y > botPosition.y - 2f && newPosition.y < botPosition.y + 2f)
                fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
        }

        private void StartResurrection()
        {
            _isResurrecting = true;

            SilentCommandSender silentSender = new();
            Server.RunCommand($"/dummy action {Bot.Player.PlayerId} Scp049ResurrectAbility Interact->Hold", silentSender);
        }

        private void StopResurrection()
        {
            if (!_isResurrecting)
                return;

            _isResurrecting = false;
            _ragdollTarget = null;

            SilentCommandSender silentSender = new();
            Server.RunCommand($"/dummy action {Bot.Player.PlayerId} Scp049ResurrectAbility Interact->Release", silentSender);
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
                Server.RunCommand($"/dummy action {Bot.Player.PlayerId} Scp049AttackAbility Shoot->Click", silentSender);
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

            if (Physics.Raycast(botPosition, direction, out RaycastHit hit, distance, HitregMask))
                return hit.transform.root == _target.ReferenceHub.transform.root || Vector3.Distance(hit.point, targetPosition) < 0.5f;

            return true;
        }

        public override void Exit()
        {
            StopResurrection();

            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
                nav.enabled = true;
        }
    }
}