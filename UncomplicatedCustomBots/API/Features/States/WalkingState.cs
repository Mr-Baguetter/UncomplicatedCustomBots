using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Interfaces;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using InventorySystem.Items.Pickups;
using InventorySystem.Items;
using UncomplicatedCustomBots.API.Managers;
using PlayerRoles.PlayableScps.Scp3114;
using UncomplicatedCustomBots.Events.Handlers;
using MEC;
using LabApi.Features.Extensions;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class WalkingState : State
    {
        internal readonly Navigation _navigator;
        private float _idleTimer = 0.0f;
        private bool _isWaiting = false;
        private float _detectionRange = 25f;
        private float _detectionAngle = 120f;
        private float _detectionCheckInterval = 0.2f;
        private float _detectionTimer = 0f;
        private Player _lastDetectedTarget = null;
        LayerMask HitregMask = ~LayerMask.GetMask("Door", "Glass", "Fence", "CCTV");
        private float _lastDetectionTime = 0f;

        public WalkingState(Bot bot) : base(bot)
        {
            if (!bot.Player.GameObject.TryGetComponent<Navigation>(out _navigator))
                _navigator = bot.Player.GameObject.AddComponent<Navigation>();
            else
                _navigator.enabled = true;
        }

        public override void Enter()
        {
            _navigator.Init(speed: 18f, enablePatrol: false);
        }

        public override void Update()
        {
            _detectionTimer += Time.deltaTime;

            if (Bot.Player.Role == RoleTypeId.Spectator || Bot.Player.Role == RoleTypeId.Destroyed)
                return;

            if (Bot.Player.Role == RoleTypeId.Scp096 || Bot.Player.Role == RoleTypeId.Scp079)
                Bot.Player.SetRole(RoleTypeId.ClassD, RoleChangeReason.RoundStart);

            if (Bot.Player.Role == RoleTypeId.Scp0492)
                Bot.ChangeState(new Scp0492State(Bot, Player.ReadyList.Where(p => p.Role == RoleTypeId.Scp049).FirstOrDefault()));

            Player scpTarget = DetectScpTarget();
            if (scpTarget != null && Bot.Player.Health < 30)
            {
                Bot.ChangeState(new FleeState(Bot, scpTarget));
                return;
            }

            if (_detectionTimer >= _detectionCheckInterval)
            {
                Player combatTarget = DetectCombatTarget();
                if (combatTarget != null && Bot.Player.Team != Team.SCPs)
                {
                    Bot.ChangeState(new CombatState(Bot));
                    return;
                }

                if (combatTarget != null)
                {
                    float distance = Vector3.Distance(Bot.Player.Position, combatTarget.Position);
                    bool hasLineOfSight = HasLineOfSight(combatTarget);
                    switch (Bot.Player.Role)
                    {
                        case RoleTypeId.Scp049:
                            if (distance < 15f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp049State(Bot));
                                return;
                            }
                            break;
                        case RoleTypeId.Scp106:
                            if (distance < 15f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp106State(Bot));
                                return;
                            }
                            break;
                        case RoleTypeId.Scp939:
                            if (distance < 15f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp939State(Bot));
                                return;
                            }
                            break;
                        case RoleTypeId.Scp173:
                            if (distance < 15f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp173State(Bot));
                                return;
                            }
                            break;
                        case RoleTypeId.Scp3114:
                            Scp3114Role scp3114 = Bot.Player.RoleBase as Scp3114Role;
                            if (distance < 15f && hasLineOfSight && !scp3114.Disguised)
                            {
                                Bot.ChangeState(new Scp3114State(Bot));
                                return;
                            }
                            break;
                        case RoleTypeId.Scp096: // 096 wont be supported. Really annoying to setup.
                            Timing.CallDelayed(Timing.WaitForOneFrame, () => Bot.Player.SetRole(RoleTypeId.ClassD, RoleChangeReason.RoundStart));
                            break;
                        case RoleTypeId.Scp079: // 079 wont be supported. Cant communicate to other SCPs without being overpowered or annoying to other SCPs.
                            Timing.CallDelayed(Timing.WaitForOneFrame, () => Bot.Player.SetRole(RoleTypeId.ClassD, RoleChangeReason.RoundStart));
                            break;
                        default:
                            if (Bot.Player.Faction == Faction.SCP)
                                LogManager.Warn($"{Bot.Player.Nickname} - {Bot.Player.PlayerId} - {Bot.Player.Role.GetFullName()} is not a recognized SCP!");
                            break;
                    }
                }
                _detectionTimer = 0f;
            }

            CheckForItems();

            HandleNavigation();
        }

        /// <summary>
        /// Detects SCP targets using both proximity and line of sight
        /// </summary>
        private Player DetectScpTarget()
        {
            Player scpTarget = Targeting.GetScpTarget(Bot.Player);
            if (scpTarget == null) return null;

            float distance = Vector3.Distance(Bot.Player.Position, scpTarget.Position);
            if (distance < 15f && HasLineOfSight(scpTarget))
                return scpTarget;

            return null;
        }
        
        private Player DetectCombatTarget()
        {
            Player potentialTarget = Targeting.GetTarget(Bot.Player);
            if (potentialTarget == null)
                return null;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 targetPosition = potentialTarget.Position;
            float distance = Vector3.Distance(botPosition, targetPosition);

            if (distance > _detectionRange)
                return null;

            if (!IsTargetInFieldOfView(potentialTarget))
                if (distance > 5f)
                    return null;

            if (HasLineOfSight(potentialTarget))
            {
                _lastDetectedTarget = potentialTarget;
                _lastDetectionTime = Time.time;
                return potentialTarget;
            }

            if (_lastDetectedTarget == potentialTarget && Time.time - _lastDetectionTime < 2f && distance < _detectionRange * 0.7f)
            {
                TargetDetectedEventArgs detectedEventArgs = new(Bot, potentialTarget, distance, HasLineOfSight(potentialTarget));
                Events.Handlers.State.OnTargetDetected(detectedEventArgs);
                return potentialTarget;
            }

            return null;
        }

        /// <summary>
        /// Checks if target is within the bot's field of view
        /// </summary>
        private bool IsTargetInFieldOfView(Player target)
        {
            Vector3 botPosition = Bot.Player.Position;
            Vector3 botForward = Bot.Player.Camera.forward;
            Vector3 directionToTarget = (target.Position - botPosition).normalized;

            float angle = Vector3.Angle(botForward, directionToTarget);
            return angle <= _detectionAngle * 0.5f;
        }

        private bool HasLineOfSight(Player target)
        {
            Vector3 botCamera = Bot.Player.Camera.position;
            Vector3 toTarget = (target.Position - botCamera);
            float distance = toTarget.magnitude;

            if (distance > 50f)
                return false;

            Vector3 direction = toTarget.normalized;

            if (Vector3.Angle(Bot.Player.Rotation * Vector3.forward, direction) > 30f)
                return false;

            Vector3 targetCenter = target.Position + Vector3.up * 1.0f;

            Vector3[] targetPoints =
            [
                targetCenter,
                targetCenter + Vector3.left * 0.3f,
                targetCenter + Vector3.right * 0.3f,
                targetCenter + Vector3.up * 0.5f,
                targetCenter + Vector3.down * 0.5f
            ];

            int visiblePoints = 0;

            foreach (Vector3 point in targetPoints)
            {
                Vector3 dir = (point - botCamera).normalized;
                float dist = Vector3.Distance(botCamera, point);

                if (Physics.Raycast(botCamera, dir, out RaycastHit hit, dist, PlayerRolesUtils.LineOfSightMask))
                {
                    if (hit.transform.root == target.ReferenceHub.transform.root)
                        visiblePoints++;
                    else if (Vector3.Distance(hit.point, point) < 0.8f)
                        visiblePoints++;
                }
                else
                {
                    visiblePoints++;
                }
            }

            return visiblePoints >= 2;
        }

        /// <summary>
        /// Handle navigation and idle behavior
        /// </summary>
        private void HandleNavigation()
        {
            if (!_navigator.IsNavigating && !_navigator._enablePatrolMode)
            {
                if (!_isWaiting)
                {
                    _isWaiting = true;
                    _idleTimer = 5.0f;
                }
                else
                {
                    _idleTimer -= Time.deltaTime;
                    if (_idleTimer <= 0)
                    {
                        Room randomRoom = Room.List.Where(r => r != null && !Plugin.Instance.Config.BlacklistedRooms.Contains(r.GameObject.name)).OrderBy(x => UnityEngine.Random.value).FirstOrDefault();
                        if (randomRoom != null)
                            _navigator.SetDestination(randomRoom);
                        _isWaiting = false;
                    }
                }
            }
        }

        /// <summary>
        /// Check for nearby items to collect
        /// </summary>
        private void CheckForItems()
        {
            if (Bot.Player.IsInventoryFull)
                return;

            if (Bot.Player.Team == Team.SCPs || Bot.Player.Team == Team.Flamingos || Bot.Player.Team == Team.Dead)
                return;

            foreach (Pickup item in Pickup.List)
            {
                float distance = Vector3.Distance(Bot.Player.Position, item.Position);

                if (distance < 2f)
                {
                    if (Plugin.Instance.Config.AllowedPickupItems.Contains(item.Type))
                    {
                        ItemCollectingEventArgs collectingargs = new(Bot, item, distance, true);
                        Events.Handlers.State.OnItemCollecting(collectingargs);
                        if (!collectingargs.IsAllowed)
                            continue;

                        try
                        {
                            Bot.Player.AddItem(item.Base.Info.ItemId);
                            item.Destroy();
                            ItemCollectedEventArgs collectedargs = new(Bot, item, distance);
                            Events.Handlers.State.OnItemCollected(collectedargs);
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"Error collecting item: {ex.Message}");
                        }
                    }
                }
            }
        }

        public void SetDetectionRange(float range)
        {
            _detectionRange = Mathf.Max(5f, range);
        }

        public void SetDetectionAngle(float angle)
        {
            _detectionAngle = Mathf.Clamp(angle, 30f, 180f);
        }

        public void SetDetectionInterval(float interval)
        {
            _detectionCheckInterval = Mathf.Max(0.1f, interval);
        }

        public override void Exit()
        {
            if (_navigator != null)
                UnityEngine.Object.Destroy(_navigator);
        }

        #region Properties
        public float DetectionRange => _detectionRange;
        public float DetectionAngle => _detectionAngle;
        public Player LastDetectedTarget => _lastDetectedTarget;
        #endregion
    }
}