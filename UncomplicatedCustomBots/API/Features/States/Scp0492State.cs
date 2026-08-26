using CommandSystem.Commands.RemoteAdmin.Dummies;
using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp0492State : State
    {
        private Player _target = null!;
        private float _fireTimer = 0f;
        private readonly float _optimalDistance = 1f;
        LayerMask ObstacleMask = LayerMask.GetMask("Default", "Door", "Glass", "Fence", "CCTV");
        private readonly float _tooCloseDistance = .4f;
        private readonly float _combatSpeed = 13.5f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.5f;
        private readonly Player SCP049;
        private readonly ZombieRole zombie;
        private readonly ZombieAttackAbility attackModule;

        public Scp0492State(Bot bot, Player scp049, float speed = 13.5f) : base(bot)
        {
            SCP049 = scp049;
            _combatSpeed = speed;
            zombie = (Bot.Player.RoleBase as ZombieRole)!;
            zombie.SubroutineModule.TryGetSubroutine(out attackModule);
        }

        public override void Enter()
        {
            if (Bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav))
            {
                nav.StopNavigation();
                nav.enabled = false;
            }

            if (!Bot.Player.GameObject!.TryGetComponent<PlayerFollower>(out var follower))
                follower = Bot.Player.GameObject.AddComponent<PlayerFollower>();

            follower.enabled = true;
            if (SCP049 != null)
                follower.Init(SCP049.ReferenceHub);
        }

        public override void Update()
        {
            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TARGET_CHECK_INTERVAL)
            {
                _target = GetValidTarget();
                _targetCheckTimer = 0f;
            }

            if (_target != null && !ShouldExitCombat())
            {
                if (Bot.Player.GameObject!.TryGetComponent<PlayerFollower>(out var follower) && follower.enabled)
                    follower.enabled = false;
                    
                HandleCombatMovement();
                HandleCombat();
            }
            else
            {
                if (Bot.Player.GameObject!.TryGetComponent<PlayerFollower>(out var follower))
                {
                    if (!follower.enabled)
                    {
                        follower.enabled = true;
                        if (SCP049 != null)
                            follower.Init(SCP049.ReferenceHub);
                    }
                }
            }
        }

        private Player GetValidTarget()
        {
            Player? potentialTarget = Targeting.GetTarget(Bot);

            if (potentialTarget != null && Bot.HasLineOfSight(potentialTarget, ObstacleMask, allowTargetHit: false))
                return potentialTarget;
                
            return null!;
        }

        private bool ShouldExitCombat()
        {
            if (!Bot.IsValidCombatTarget(_target))
                return true;

            if (!Bot.HasLineOfSight(_target, ObstacleMask, allowTargetHit: false))
                return true;

            return false;
        }

        private void HandleCombatMovement()
        {
            Bot.MoveToOptimalDistance(_target, _optimalDistance, _tooCloseDistance, _combatSpeed);
        }

        private void HandleCombat()
        {
            float distanceToTarget = Vector3.Distance(Bot.Player.Position, _target.Position);
            if (_target == null || !Bot.HasLineOfSight(_target, ObstacleMask, allowTargetHit: false) || Bot.Player.HasEffect<Flashed>() || distanceToTarget > 2f)
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
                BotExtensions.TryRunRoleAction(attackModule, ActionName.Shoot, true);
        }

        public override void Exit()
        {
            if (Bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav))
            {
                nav.enabled = true;
            }
            
            if (Bot.Player.GameObject!.TryGetComponent<PlayerFollower>(out var follower))
            {
                follower.enabled = false;
            }
        }
    }
}