using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.Ragdolls;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp049State : State
    {
        private Player _target = null!;
        private Transform _ragdollTarget = null!;
        private float _fireTimer = 0f;
        private float _targetCheckTimer = 0f;
        private float _ragdollCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.3f;
        private const float RAGDOLL_CHECK_INTERVAL = 1.0f;
        private readonly float _optimalDistance = 1f;
        private readonly float _tooCloseDistance = .6f;
        private readonly float _combatSpeed = 10f;
        private const float RAGDOLL_DETECTION_RADIUS = 15f;
        private const float RESURRECT_DISTANCE = 2f;
        private bool _isResurrecting = false;
        private LayerMask _ragdollLayerMask;
        private bool _hasValidTarget = false;
        private float _stateStabilityTimer = 0f;
        private const float MIN_STATE_TIME = 2f;
        private float _targetLostTimer = 0f;
        private const float TARGET_LOST_GRACE_PERIOD = 1.5f;
        private readonly Scp049Role scp049;
        private readonly Scp049ResurrectAbility resurrect;
        private readonly Scp049AttackAbility attack;

        public static readonly CachedLayerMask HitregMask = new("Default", "Hitbox", "Glass", "CCTV", "Door");
        private static readonly Collider[] _ragdollOverlapBuffer = new Collider[16];

        public Scp049State(Bot bot) : base(bot)
        {
            _ragdollLayerMask = LayerMask.GetMask("Ragdoll");
            scp049 = bot.Player.RoleBase as Scp049Role ?? throw new System.InvalidOperationException($"Scp049State created for non-Scp049 role {bot.Player.Role}");
            if (!scp049.SubroutineModule.TryGetSubroutine(out resurrect))
                LogManager.Warn($"Scp049State: resurrect subroutine not found for {bot.Player.DisplayName}");

            if (!scp049.SubroutineModule.TryGetSubroutine(out attack))
                LogManager.Warn($"Scp049State: attack subroutine not found for {bot.Player.DisplayName}");
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
                return;

            if (HandleRagdollBehavior())
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
            if ((Bot.Player.Position - _target.Position).sqrMagnitude <= 7.84f && Bot.HasLineOfSight(_target, HitregMask) && !Bot.Player.HasEffect<Flashed>())
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

                if ((Bot.Player.Position - _ragdollTarget.position).sqrMagnitude > 9f)
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
                {
                    FindNearestRagdoll();
                }
                else
                {
                    BasicRagdoll basicRagdoll = _ragdollTarget.GetComponent<BasicRagdoll>();
                    if (basicRagdoll != null)
                    {
                        Ragdoll ragdoll = Ragdoll.Get(basicRagdoll);
                        if (ragdoll == null || !ragdoll.IsRevivableBy(Bot.Player))
                        {
                            _ragdollTarget = null!;
                            FindNearestRagdoll();
                        }
                    }
                    else
                    {
                        _ragdollTarget = null!;
                        FindNearestRagdoll();
                    }
                }
            }

            if (_ragdollTarget != null)
            {
                LookAtRagdoll();

                if ((Bot.Player.Position - _ragdollTarget.position).sqrMagnitude <= 4f)
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
                            _ragdollTarget = null!;
                            return false;
                        }
                    }
                    else
                    {
                        _ragdollTarget = null!;
                        return false;
                    }
                }
                else
                {
                    Bot.MoveTowards(_ragdollTarget.position, _combatSpeed);
                    return true;
                }
            }

            return false;
        }

        private void LookAtRagdoll()
        {
            if (_ragdollTarget == null)
                return;

            Bot.LookAt(_ragdollTarget.position);
        }

        private void FindNearestRagdoll()
        {
            int count = Physics.OverlapSphereNonAlloc(Bot.Player.Position, RAGDOLL_DETECTION_RADIUS, _ragdollOverlapBuffer, _ragdollLayerMask);

            Transform? closestRagdoll = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _ragdollOverlapBuffer[i];
                if (collider.TryGetComponent<BasicRagdoll>(out var basicRagdoll) && basicRagdoll.Info.RoleType.GetTeam() != Team.SCPs && basicRagdoll.Info.RoleType.GetTeam() != Team.Flamingos)
                {
                    Ragdoll ragdoll = Ragdoll.Get(basicRagdoll);
                    if (ragdoll != null && ragdoll.IsRevivableBy(Bot.Player))
                    {
                        float sq = (Bot.Player.Position - collider.transform.position).sqrMagnitude;
                        if (sq < closestDistance)
                        {
                            closestDistance = sq;
                            closestRagdoll = collider.transform;
                        }
                    }
                }
            }

            _ragdollTarget = closestRagdoll!;
        }
        
        private void StartResurrection()
        {
            if (resurrect == null)
                return;
                
            _isResurrecting = true;
            BotExtensions.TryRunRoleAction(resurrect, ActionName.Interact, false);
        }

        private void StopResurrection()
        {
            if (!_isResurrecting)
                return;

            _isResurrecting = false;
            _ragdollTarget = null!;

            if (resurrect != null)
                BotExtensions.TryStopRoleAction(resurrect, ActionName.Interact);
        }

        private void HandleCombat()
        {
            if (_target == null || !_target.IsAlive || attack == null)
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                BotExtensions.TryRunRoleAction(attack, ActionName.Shoot, true);
                _fireTimer = 1.2f;
            }
        }

        public override void Exit()
        {
            StopResurrection();

            Navigation? nav = Bot.CachedNavigation;
            nav?.enabled = true;
        }
    }
}