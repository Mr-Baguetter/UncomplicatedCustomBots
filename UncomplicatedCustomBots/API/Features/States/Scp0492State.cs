using CommandSystem.Commands.RemoteAdmin.Dummies;
using CustomPlayerEffects;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Usables;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class Scp0492State : State
    {
        private Player _target;
        private float _fireTimer = 0f;
        private float _optimalDistance = 1f;
        LayerMask HitregMask = ~LayerMask.GetMask("Default", "Door", "Glass", "Fence", "CCTV");
        private float _tooCloseDistance = .4f;
        private float _combatSpeed = 13.5f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.5f;
        private float _stateChangeTimer = 0f;
        private float _noTargetSightTimer = 0f;
        private readonly Player SCP049;

        public Scp0492State(Bot bot, Player scp049) : base(bot)
        {
            SCP049 = scp049;
        }

        public override void Enter()
        {
            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
            {
                nav.StopNavigation();
                nav.enabled = false;
            }

            if (!Bot.Player.GameObject.TryGetComponent<PlayerFollower>(out var follower))
                follower = Bot.Player.GameObject.AddComponent<PlayerFollower>();

            follower.enabled = true;
            follower.Init(SCP049.ReferenceHub);

            _stateChangeTimer = 0f;
            _noTargetSightTimer = 0f;
        }

        public override void Update()
        {
            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TARGET_CHECK_INTERVAL)
            {
                _target = Targeting.GetTarget(Bot.Player);
                _targetCheckTimer = 0f;
            }

            if (_target != null && !ShouldExitCombat())
            {
                if (Bot.Player.GameObject.TryGetComponent<PlayerFollower>(out var follower) && follower.enabled)
                    follower.enabled = false;
                    
                HandleCombatMovement();
                HandleCombat();
            }
            else
            {
                if (Bot.Player.GameObject.TryGetComponent<PlayerFollower>(out var follower))
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

        private bool ShouldExitCombat()
        {
            if (_target == null || !_target.IsAlive || _target.Role == RoleTypeId.Spectator)
                return true;

            if (_target.Faction == Bot.Player.Faction)
                return true;

            if (_target.Role == RoleTypeId.Tutorial && !Plugin.Instance.Config.AttackTutorials)
                return true;

            float distance = Vector3.Distance(Bot.Player.Position, _target.Position);
            if (distance > 30f)
                return true;

            return false;
        }

        private void HandleCombatMovement()
        {
            if (_target == null || !(Bot.Player.RoleBase is IFpcRole fpcRole))
                return;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 targetPosition = _target.Position;

            Vector3 directionToTarget = targetPosition - botPosition;
            float distance = directionToTarget.magnitude;
            directionToTarget.Normalize();

            fpcRole.FpcModule.MouseLook.LookAtDirection(directionToTarget);
            Vector3 moveDirection = Vector3.zero;

            if (distance > _optimalDistance)
            {
                moveDirection = directionToTarget;
            }
            else if (distance < _tooCloseDistance)
            {
                moveDirection = -directionToTarget;
            }

            if (moveDirection != Vector3.zero)
            {
                moveDirection.y = 0;
                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    moveDirection.Normalize();
                    Vector3 newPosition = botPosition + moveDirection * _combatSpeed * Time.deltaTime;

                    if (IsValidPosition(newPosition))
                    {
                        fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
                    }
                }
            }
        }

        private bool IsValidPosition(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                return hit.distance < 3f;

            return false;
        }

        private void HandleCombat()
        {
            float distanceToTarget = Vector3.Distance(Bot.Player.Position, _target.Position);
            if (_target == null || !HasLineOfSight() || Bot.Player.HasEffect<Flashed>() || distanceToTarget > 2f)
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                SilentCommandSender silentSender = new();
                Server.RunCommand($"/dummy action {Bot.Player.PlayerId} ZombieAttackAbility Shoot->Click", silentSender);
            }
        }

        /// <summary>
        /// Checks if the bot has a clear line of sight to its target.
        /// </summary>
        /// <returns>True if there are no obstructions, otherwise false.</returns>
        private bool HasLineOfSight()
        {
            if (_target == null)
                return false;

            Vector3 botPosition = Bot.Player.Position + Vector3.up * 1.5f;
            Vector3 targetPosition = _target.Position + Vector3.up * 1.5f;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float distance = Vector3.Distance(botPosition, targetPosition);

            if (Physics.Raycast(botPosition, direction, out RaycastHit hit, distance, HitregMask))
            {
                if (hit.transform.root == _target.ReferenceHub.transform.root)
                    return true;

                return false;
            }

            return true;
        }

        public override void Exit()
        {
            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
            {
                nav.enabled = true;
            }
            if (Bot.Player.GameObject.TryGetComponent<PlayerFollower>(out var follower))
            {
                follower.enabled = false;
            }
        }
    }
}