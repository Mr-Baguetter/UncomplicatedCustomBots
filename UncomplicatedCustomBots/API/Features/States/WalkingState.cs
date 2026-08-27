using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using System;
using UncomplicatedCustomBots.API.Struct;
using UnityEngine;
using UncomplicatedCustomBots.API.Managers;
using PlayerRoles.PlayableScps.Scp173;
using PlayerRoles.PlayableScps.Scp3114;
using UncomplicatedCustomBots.Events.Handlers;
using LabApi.Features.Extensions;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Features.Components;
using PlayerRoles.FirstPersonControl;

namespace UncomplicatedCustomBots.API.Features.States
{
    internal class WalkingState : State
    {
        private static readonly Pickup[] _pickupSnapshotBuffer = new Pickup[32];

        internal readonly Navigation _navigator;
        private float _idleTimer = 0.0f;
        private bool _isWaiting = false;
        private float _detectionRange = 25f;
        private float _detectionAngle = 120f;
        private float _detectionCheckInterval = 0.2f;
        private float _detectionTimer = 0f;
        private Player _lastDetectedTarget = null!;
        private float _lastDetectionTime = 0f;
        private float _itemCheckTimer = 0f;
        private float _squadRegroupTimer = 0f;
        private const float ITEM_CHECK_INTERVAL = 0.5f;
        private const float SENSED_EVENT_MEMORY = 5f;
        private const float GrenadeFleeRange = 10f;
        private const float SQUAD_REGROUP_CHECK_INTERVAL = 2f;
        private float _scp173ObservedDuration = 0f;
        private Scp173ObserversTracker _scp173Tracker = null!;
        private Scp173Role _scp173Role = null!;
        private const float SCP173_STARE_TRIGGER = 1f;

        public WalkingState(Bot bot) : base(bot)
        {
            Navigation? cached = bot.CachedNavigation;
            if (cached == null)
            {
                cached = bot.Player.GameObject!.AddComponent<Navigation>();
                bot.SetCachedNavigation(cached);
            }
            else
            {
                cached.enabled = true;
            }

            _navigator = cached;
        }

        public override void Enter()
        {
            if (Bot.Player.RoleBase is IFpcRole fpc)
            {
                float patrolSpeed = fpc.FpcModule.WalkSpeed * 1.7f;

                if (fpc.FpcModule.SprintSpeed > patrolSpeed)
                    patrolSpeed = fpc.FpcModule.SprintSpeed;

                patrolSpeed = Mathf.Clamp(patrolSpeed, 6f, 10f);
                _navigator.Init(speed: patrolSpeed, enablePatrol: false);
            }
            else
                _navigator.Init(speed: 18f, enablePatrol: false);

            _scp173ObservedDuration = 0f;
        }

        public override void Update()
        {
            _detectionTimer += Time.deltaTime;

            if (Bot.Player.Role == RoleTypeId.Spectator || Bot.Player.Role == RoleTypeId.Destroyed)
                return;

            if (Bot.Player.Role == RoleTypeId.Scp096 || Bot.Player.Role == RoleTypeId.Scp079)
                Bot.Player.SetRole(RoleTypeId.ClassD, RoleChangeReason.RoundStart);

            if (Bot.Player.Role == RoleTypeId.Scp0492)
            {
                Player? scp049 = null;
                foreach (Player p in Player.ReadyList)
                {
                    if (p.Role == RoleTypeId.Scp049)
                    {
                        scp049 = p;
                        break;
                    }
                }

                if (scp049 != null)
                {
                    Bot.ChangeState(new Scp0492State(Bot, scp049));
                }
                else
                    LogManager.Debug($"Bot {Bot.Player.DisplayName} is SCP-049-2 but no SCP-049 found, staying in WalkingState");
                    
                return;
            }

            if (_detectionTimer >= _detectionCheckInterval)
            {
                _detectionTimer = 0f;

                bool inElevator = _navigator.IsInsideElevatorChamber || _navigator.IsWalkingIntoElevator || _navigator.IsWaitingForElevator || _navigator.IsWaitingToEnterElevator;

                Player? scpTarget = DetectScpTarget();
                if (scpTarget != null && Bot.Player.Health < 30 && !inElevator)
                {
                    Bot.ChangeState(new FleeState(Bot, scpTarget));
                    return;
                }

                Player? combatTarget = DetectCombatTarget();
                if (combatTarget != null && Targeting.IsValidTarget(Bot, combatTarget) && !inElevator)
                {
                    Bot.ChangeState(new CombatState(Bot));
                    return;
                }

                if (combatTarget != null && !inElevator)
                {
                    float sq = (Bot.Player.Position - combatTarget.Position).sqrMagnitude;
                    bool hasLineOfSight = HasLineOfSight(combatTarget);
                    if (sq >= 625f)
                    {
                        hasLineOfSight = false;
                    }

                    switch (Bot.Player.Role)
                    {
                        case RoleTypeId.Scp049:
                            if (sq < 625f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp049State(Bot));
                                return;
                            }
                            break;

                        case RoleTypeId.Scp106:
                            if (sq < 625f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp106State(Bot));
                                return;
                            }
                            break;

                        case RoleTypeId.Scp939:
                            if (sq < 625f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp939State(Bot));
                                return;
                            }
                            break;

                        case RoleTypeId.Scp173:
                            if (sq < 625f && hasLineOfSight)
                            {
                                Bot.ChangeState(new Scp173State(Bot));
                                return;
                            }
                            break;

                        case RoleTypeId.Scp3114:
                            Scp3114Role scp3114 = (Bot.Player.RoleBase as Scp3114Role)!;
                            if (sq < 625f && hasLineOfSight && !scp3114.Disguised)
                            {
                                Bot.ChangeState(new Scp3114State(Bot));
                                return;
                            }
                            break;

                        default:
                            if (Bot.Player.Faction == Faction.SCP)
                                LogManager.Warn($"{Bot.Player.Nickname} - {Bot.Player.PlayerId} - {Bot.Player.Role.GetFullName()} is not a recognized SCP!");
                            break;
                    }
                }
            }

            if (Bot.Player.Role == RoleTypeId.Scp173 && !_navigator.IsInsideElevatorChamber && !_navigator.IsWalkingIntoElevator && !_navigator.IsWaitingForElevator && !_navigator.IsWaitingToEnterElevator)
            {
                Scp173Role? scpRole = Bot.Player.RoleBase as Scp173Role;
                if (scpRole != _scp173Role || _scp173Tracker == null)
                {
                    _scp173Role = scpRole!;
                    if (_scp173Role != null)
                    {
                        _scp173Role.SubroutineModule.TryGetSubroutine(out _scp173Tracker);
                    }
                    else
                        _scp173Tracker = null!;
                }

                if (_scp173Tracker != null)
                {
                    if (_scp173Tracker.IsObserved)
                    {
                        _scp173ObservedDuration += Time.deltaTime;
                    }
                    else
                        _scp173ObservedDuration = 0f;

                    if (_scp173ObservedDuration >= SCP173_STARE_TRIGGER)
                    {
                        LogManager.Debug($"WalkingState: {Bot.Player.DisplayName} stared at for {_scp173ObservedDuration:F2}s -> Scp173State");
                        Bot.ChangeState(new Scp173State(Bot));
                        return;
                    }
                }
            }
            else
                _scp173ObservedDuration = 0f;

            CheckForItems();
            InvestigateSensedEvents();

            if (HandleSquadGrouping())
                return;

            HandleNavigation();
        }

        private Player? DetectScpTarget()
        {
            Player? scpTarget = Targeting.GetScpTarget(Bot);
            if (scpTarget == null)
                return null;

            if ((Bot.Player.Position - scpTarget.Position).sqrMagnitude < 225f && HasLineOfSight(scpTarget))
                return scpTarget;

            return null;
        }
        
        private Player? DetectCombatTarget()
        {
            Player? potentialTarget = Targeting.GetTarget(Bot);
            if (potentialTarget == null)
                return null;

            Vector3 botPosition = Bot.Player.Position;
            Vector3 targetPosition = potentialTarget.Position;
            float sq = (botPosition - targetPosition).sqrMagnitude;

            if (sq > _detectionRange * _detectionRange)
                return null;

            if (!IsTargetInFieldOfView(potentialTarget))
            {
                if (sq > 25f)
                    return null;
            }

            if (HasLineOfSight(potentialTarget))
            {
                _lastDetectedTarget = potentialTarget;
                _lastDetectionTime = Time.time;
                return potentialTarget;
            }

            float range07Sq = _detectionRange * 0.7f * _detectionRange * 0.7f;
            if (_lastDetectedTarget == potentialTarget && Time.time - _lastDetectionTime < 2f && sq < range07Sq)
            {
                float dist = Mathf.Sqrt(sq);
                TargetDetectedEventArgs detectedEventArgs = new(Bot, potentialTarget, dist, HasLineOfSight(potentialTarget));
                Events.Handlers.State.OnTargetDetected(detectedEventArgs);
                return potentialTarget;
            }

            return null;
        }

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
            Vector3 toTarget = target.Position - botCamera;
            float distance = toTarget.magnitude;

            if (distance > 50f)
                return false;

            Vector3 direction = toTarget.normalized;

            if (Vector3.Angle(Bot.Player.Rotation * Vector3.forward, direction) > 30f)
                return false;

            Vector3 targetCenter = target.Position + Vector3.up * 1.0f;
            Transform targetRoot = target.ReferenceHub.transform.root;
            int visiblePoints = 0;

            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = i switch
                {
                    0 => Vector3.zero,
                    1 => Vector3.left * 0.3f,
                    2 => Vector3.right * 0.3f,
                    3 => Vector3.up * 0.5f,
                    _ => Vector3.down * 0.5f,
                };

                Vector3 point = targetCenter + offset;
                Vector3 dir = (point - botCamera).normalized;
                float dist = Vector3.Distance(botCamera, point);

                if (Physics.Raycast(botCamera, dir, out RaycastHit hit, dist, PlayerRolesUtils.LineOfSightMask))
                {
                    if (hit.transform.root == targetRoot)
                    {
                        visiblePoints++;
                    }
                    else if ((hit.point - point).sqrMagnitude < 0.64f)
                    {
                        visiblePoints++;
                    }
                }
                else
                {
                    visiblePoints++;
                }
            }

            return visiblePoints >= 2;
        }

        private bool TryShareSquadWaypoints()
        {
            if (!Bot.IsSquadBot || !Bot.IsInSquad)
                return false;

            if (SquadManager.IsSquadLeader(Bot))
                return false;

            if (_navigator.IsInsideElevatorChamber || _navigator.IsWalkingIntoElevator || _navigator.IsWaitingForElevator || _navigator.IsWaitingToEnterElevator)
                return false;

            Bot? leader = SquadManager.GetSquadLeader(Bot);
            if (leader == null || leader == Bot)
                return false;

            Navigation? leaderNav = leader.CachedNavigation;
            if (leaderNav == null || !leaderNav.IsNavigating || leaderNav.CurrentTarget == null)
                return false;

            if ((Bot.Player.Position - leader.Player.Position).sqrMagnitude > 900f)
                return false;

            if (_navigator.CurrentTarget == leaderNav.CurrentTarget && _navigator.CurrentPath.Count > 0 && _navigator.IsNavigating)
                return false;

            if (_navigator.TryAdoptSquadWaypoints())
            {
                _isWaiting = false;
                _idleTimer = 0f;
                return true;
            }

            if (_navigator.CurrentTarget != leaderNav.CurrentTarget && !_navigator.IsWaitingForDoor)
            {
                int idx = SquadManager.GetSquadMemberIndex(Bot);
                if (idx > 0)
                    _squadRegroupTimer = idx * 0.1f;

                _navigator.SetDestination(leaderNav.CurrentTarget);
                _isWaiting = false;
                _navigator.TryAdoptSquadWaypoints();
                return true;
            }

            return false;
        }

        private bool HandleSquadGrouping()
        {
            if (!Bot.IsSquadBot || !Bot.IsInSquad)
                return false;

            if (TryShareSquadWaypoints())
                return true;

            _squadRegroupTimer += Time.deltaTime;
            if (_squadRegroupTimer < SQUAD_REGROUP_CHECK_INTERVAL)
                return false;

            _squadRegroupTimer = 0f;

            List<Bot> squadmates = SquadManager.GetSquadmates(Bot);
            if (squadmates.Count == 0)
                return false;

            Vector3 averagePos = SquadManager.GetSquadAveragePosition(Bot, squadmates);
            float spread = SquadManager.GetSquadSpread(Bot, squadmates, averagePos);
            float regroupDistance = Plugin.Instance.Config.SquadRegroupDistance;

            if (spread < regroupDistance)
                return false;

            Room? squadRoom = Room.GetRoomAtPosition(averagePos);

            if (squadRoom != null)
            {
                if (Plugin.Instance.Config.Debug)
                {
                    LogManager.Debug($"{Bot.Player.Nickname} regrouping with squad towards {squadRoom.Name}");
                }

                _navigator.SetDestination(squadRoom);
                _isWaiting = false;
                return true;
            }

            return false;
        }

        private void HandleNavigation()
        {
            if (!_navigator.IsNavigating && !_navigator._enablePatrolMode)
            {
                if (Bot.IsInSquad && !SquadManager.IsSquadLeader(Bot))
                {
                    Bot? leader = SquadManager.GetSquadLeader(Bot);
                    if (leader != null)
                    {
                        Navigation? leaderNav = leader.CachedNavigation;
                        if (leaderNav != null && leaderNav.IsNavigating && leaderNav.CurrentTarget != null)
                        {
                            if ((Bot.Player.Position - leader.Player.Position).sqrMagnitude <= 900f)
                            {
                                if (_navigator.TryAdoptSquadWaypoints())
                                {
                                    _isWaiting = false;
                                    return;
                                }

                                _navigator.SetDestination(leaderNav.CurrentTarget);
                                _isWaiting = false;
                                _navigator.TryAdoptSquadWaypoints();
                                return;
                            }
                        }
                    }
                }

                if (ObjectivesHandler.TryAssignObjective(Bot))
                {
                    _navigator.StartObjective();
                    return;
                }

                if (!_isWaiting)
                {
                    _isWaiting = true;
                    float stagger = 0f;
                    if (Bot.IsInSquad)
                    {
                        int idx = SquadManager.GetSquadMemberIndex(Bot);
                        if (idx > 0)
                            stagger = idx * 0.35f;
                    }
                    _idleTimer = 5.0f + stagger;
                }
                else
                {
                    _idleTimer -= Time.deltaTime;
                    if (_idleTimer <= 0)
                    {
                        if (Bot.IsInSquad && !SquadManager.IsSquadLeader(Bot))
                        {
                            Bot? leader = SquadManager.GetSquadLeader(Bot);
                            Navigation? leaderNav = leader?.CachedNavigation;
                            if (leaderNav != null && leaderNav.IsNavigating && leaderNav.CurrentTarget != null)
                            {
                                if (_navigator.TryAdoptSquadWaypoints())
                                {
                                    _isWaiting = false;
                                    return;
                                }

                                _navigator.SetDestination(leaderNav.CurrentTarget);
                                _isWaiting = false;
                                return;
                            }
                        }

                        Room? randomRoom = GetRandomUnblacklistedRoom();
                        if (randomRoom != null)
                            _navigator.SetDestination(randomRoom);
                            
                        _isWaiting = false;
                    }
                }
            }
            else if (_navigator.IsNavigating && Bot.IsInSquad && !SquadManager.IsSquadLeader(Bot))
            {
                if (_squadRegroupTimer <= 0f)
                    TryShareSquadWaypoints();
            }
        }

        private void InvestigateSensedEvents()
        {
            bool isScp939 = Bot.Player.Role == RoleTypeId.Scp939;
            if ((Bot.Player.Team == Team.SCPs && !isScp939) || Bot.Player.Team == Team.Dead)
                return;

            if (Bot.Context.SensedEvents.Count == 0)
                return;

            float now = Time.time;

            SensedEvent bestGrenade = default;
            bool foundGrenade = false;
            for (int i = 0; i < Bot.Context.SensedEvents.Count; i++)
            {
                SensedEvent e = Bot.Context.SensedEvents[i];
                if (e.Type == SensedEventType.Grenade && now - e.Time < SENSED_EVENT_MEMORY)
                {
                    if (!foundGrenade || e.Time > bestGrenade.Time)
                    {
                        bestGrenade = e;
                        foundGrenade = true;
                    }
                }
            }

            if (foundGrenade && bestGrenade.Time > 0f && (Bot.Player.Position - bestGrenade.Position).sqrMagnitude < GrenadeFleeRange * GrenadeFleeRange)
            {
                Bot.ChangeState(new FleeState(Bot, bestGrenade.Position));
                return;
            }

            if (_navigator.IsNavigating || _isWaiting)
                return;

            SensedEvent bestInteresting = default;
            bool foundInteresting = false;
            for (int i = 0; i < Bot.Context.SensedEvents.Count; i++)
            {
                SensedEvent e = Bot.Context.SensedEvents[i];
                bool isInteresting;
                if (isScp939)
                {
                    isInteresting = e.Type == SensedEventType.Speaking || e.Type == SensedEventType.Gunshot || e.Type == SensedEventType.Tesla;
                }
                else
                    isInteresting = e.Type == SensedEventType.Gunshot || e.Type == SensedEventType.DoorOpen || e.Type == SensedEventType.Tesla || e.Type == SensedEventType.Speaking;

                if (!isInteresting)
                    continue;

                if (now - e.Time > SENSED_EVENT_MEMORY)
                    continue;

                if (!foundInteresting || e.Priority > bestInteresting.Priority || (e.Priority == bestInteresting.Priority && e.Time > bestInteresting.Time))
                {
                    bestInteresting = e;
                    foundInteresting = true;
                }
            }

            if (foundInteresting && bestInteresting.Time > 0f && (Bot.Player.Position - bestInteresting.Position).sqrMagnitude > 36f)
            {
                Room? eventRoom = Room.GetRoomAtPosition(bestInteresting.Position);
                if (eventRoom != null)
                {
                    _isWaiting = false;
                    _navigator.SetDestination(eventRoom);
                }
            }
        }

        private void CheckForItems()
        {
            _itemCheckTimer += Time.deltaTime;
            if (_itemCheckTimer < ITEM_CHECK_INTERVAL)
                return;

            _itemCheckTimer = 0f;

            if (Bot.Player.IsInventoryFull)
                return;

            if (Bot.Player.Team == Team.SCPs || Bot.Player.Team == Team.Flamingos || Bot.Player.Team == Team.Dead)
                return;

            if (Bot.BotDetectionRadius == null)
                return;

            Vector3 botPosition = Bot.Player.Position;

            List<Pickup> pickupsInRange = Bot.BotDetectionRadius.PickupsInRange;
            int count = pickupsInRange.Count;
            if (count > _pickupSnapshotBuffer.Length)
            {
                if (Plugin.Instance.Config.Debug)
                {
                    LogManager.Debug($"CheckForItems: {count} pickups in range, only processing {_pickupSnapshotBuffer.Length}");
                }

                count = _pickupSnapshotBuffer.Length;
            }
                
            for (int i = 0; i < count; i++)
                _pickupSnapshotBuffer[i] = pickupsInRange[i];

            for (int i = 0; i < count; i++)
            {
                Pickup item = _pickupSnapshotBuffer[i];
                if (item == null)
                    continue;

                if ((botPosition - item.Position).sqrMagnitude < 4f)
                {
                    if (Plugin.Instance.Config.AllowedPickupItems.Contains(item.Type))
                    {
                        float distance = Vector3.Distance(botPosition, item.Position);
                        ItemCollectingEventArgs collectingargs = new(Bot, item, distance, true);
                        Events.Handlers.State.OnItemCollecting(collectingargs);
                        if (!collectingargs.IsAllowed)
                            continue;

                        try
                        {
                            Bot.Player.AddItem(item.Base.Info.ItemId);
                            Bot.BotDetectionRadius.PickupsInRange.Remove(item);
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

        public void SetDetectionRange(float range) => _detectionRange = Mathf.Max(5f, range);

        public void SetDetectionAngle(float angle) => _detectionAngle = Mathf.Clamp(angle, 30f, 180f);

        public void SetDetectionInterval(float interval) => _detectionCheckInterval = Mathf.Max(0.1f, interval);

        public override void Exit() { }

        private static HashSet<string> _blacklistSet = [];
        private static int _blacklistGen = -1;
        private static readonly List<Room> _candidatesScratch = [];
        private static readonly List<Room> _fallbackScratch = [];
        private static Room? GetRandomUnblacklistedRoom()
        {
            List<string> blacklist = Plugin.Instance.Config.BlacklistedRooms;
            if (_blacklistGen != blacklist.Count || _blacklistSet.Count != blacklist.Count)
            {
                _blacklistSet = new(blacklist);
                _blacklistGen = blacklist.Count;
            }

            _candidatesScratch.Clear();
            _fallbackScratch.Clear();

            foreach (Room room in Room.List)
            {
                if (room == null || _blacklistSet.Contains(room.GameObject.name))
                    continue;

                if (room.Name == RoomName.Unnamed || room.Zone == FacilityZone.Other)
                {
                    _fallbackScratch.Add(room);
                }
                else
                {
                    _candidatesScratch.Add(room);
                }
            }

            List<Room> pool = _candidatesScratch.Count > 0 ? _candidatesScratch : _fallbackScratch;
            if (pool.Count == 0)
                return null;

            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        #region Properties
        public float DetectionRange => _detectionRange;
        public float DetectionAngle => _detectionAngle;
        public Player LastDetectedTarget => _lastDetectedTarget;
        #endregion
    }
}