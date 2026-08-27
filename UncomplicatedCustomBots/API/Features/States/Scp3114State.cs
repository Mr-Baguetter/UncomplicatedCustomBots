using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.Ragdolls;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp3114State : State
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
        private readonly Scp3114Role scp3114;
        private readonly Scp3114Disguise disguiseModule;
        private readonly Scp3114Slap slapModule;
        private readonly Scp3114Strangle strangleModule;

        public static readonly CachedLayerMask HitregMask = new("Default", "Hitbox", "Glass", "CCTV", "Door");
        private static readonly Collider[] _ragdollOverlapBuffer = new Collider[16];

        public Scp3114State(Bot bot) : base(bot)
        {
            _ragdollLayerMask = LayerMask.GetMask("Ragdoll");
            scp3114 = (Bot.Player.RoleBase as Scp3114Role)!;
            scp3114.SubroutineModule.TryGetSubroutine(out disguiseModule);
            scp3114.SubroutineModule.TryGetSubroutine(out slapModule);
            scp3114.SubroutineModule.TryGetSubroutine(out strangleModule);
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

            if (HandleRagdollBehavior())
                return;

            if (scp3114.Disguised)
            {
                Bot.ChangeState(new WalkingState(Bot));
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
            if ((Bot.Player.Position - _target.Position).sqrMagnitude <= 7.84f && Bot.HasLineOfSight(_target, HitregMask) && !Bot.Player.HasEffect<Flashed>())
                HandleCombat();
        }


        private bool HandleRagdollBehavior()
        {
            if (scp3114.Disguised)
            {
                _ragdollTarget = null!;
                _isResurrecting = false;
                return false;
            }

            if (_isResurrecting)
            {
                if ((Bot.Player.Position - _ragdollTarget.position).sqrMagnitude > 9f)
                    return false;

                if (Bot.Player.RoleBase is IFpcRole fpcRole)
                {
                    Vector3 directionToRagdoll = (_ragdollTarget.position - Bot.Player.Position).normalized;
                    fpcRole.FpcModule.MouseLook.LookAtDirection(directionToRagdoll);
                }
                
                return true;
            }

            _ragdollCheckTimer += Time.deltaTime;
            if (_ragdollCheckTimer >= RAGDOLL_CHECK_INTERVAL)
            {
                _ragdollCheckTimer = 0f;
                if (_ragdollTarget == null)
                    FindNearestRagdoll();
            }

            if (_ragdollTarget != null)
            {
                if ((Bot.Player.Position - _ragdollTarget.position).sqrMagnitude <= 4f)
                {
                    BasicRagdoll ragdoll = _ragdollTarget.GetComponent<BasicRagdoll>();
                    ForceDisguise(disguiseModule, ragdoll);
                    return true;
                }
                else
                {
                    Bot.MoveTowards(_ragdollTarget.position, _combatSpeed);
                    return true;
                }
            }

            return false;
        }

        private static void ForceDisguise(Scp3114Disguise disguise, BasicRagdoll targetRagdoll)
        {
            if (disguise.Cooldown.IsReady && disguise.AnyValidateBegin(targetRagdoll, out var _))
            {
                disguise.CurRagdoll = targetRagdoll;
                disguise.ClientTryStart();
            }
        }

        private void FindNearestRagdoll()
        {
            int count = Physics.OverlapSphereNonAlloc(Bot.Player.Position, RAGDOLL_DETECTION_RADIUS, _ragdollOverlapBuffer, _ragdollLayerMask);

            Transform? closestRagdoll = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _ragdollOverlapBuffer[i];
                if (collider.TryGetComponent<BasicRagdoll>(out var ragdoll) && ragdoll.Info.RoleType.GetTeam() != Team.SCPs)
                {
                    float sq = (Bot.Player.Position - collider.transform.position).sqrMagnitude;
                    if (sq < closestDistance)
                    {
                        closestDistance = sq;
                        closestRagdoll = collider.transform;
                    }
                }
            }

            _ragdollTarget = closestRagdoll!;
        }

        private void HandleCombat()
        {
            if (_target == null || !_target.IsAlive)
                return;

            float sq = (Bot.Player.Position - _target.Position).sqrMagnitude;
            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                if (sq <= 0.64f)
                {
                    BotExtensions.TryRunRoleAction(strangleModule, ActionName.Shoot, false);
                }
                else
                    BotExtensions.TryRunRoleAction(slapModule, ActionName.Shoot, true);

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