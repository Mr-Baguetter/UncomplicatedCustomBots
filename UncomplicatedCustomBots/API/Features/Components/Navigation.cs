using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using DrawableLine;
using LabApi.Features.Wrappers;
using LightContainmentZoneDecontamination;
using MapGeneration;
using MEC;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using System;
using System.Collections.Generic;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using UnityEngine.AI;
using static LightContainmentZoneDecontamination.DecontaminationController;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class Navigation : MonoBehaviour
    {
        #region Constants
        public const float DefaultSpeed = 15f;
        public const float DoorInteractionDistance = 2.3f;
        public const float DoorCenterPassDistance = 1.5f;
        public const float WaypointReachedDistance = 1.2f;
        public const float DoorWaitTime = 2f;
        public const float PathRecalculateTime = 15f;
        private const float PathFailRetryDelay = 2.5f;
        private const float StuckThreshold = 0.04f;
        private const float StuckTimeLimit = 4f;
        private const float LookTurnSpeed = 10f;
        public const float ElevatorDetectionRadius = 25f;
        public const float ElevatorEnterDistance = 3f;
        public const float ElevatorWaitTimeout = 30f;
        private const float PathVisualizationDuration = 30f;
        private const float PathVisualizationRedrawInterval = 5f;
        private const float AvoidanceDetectDistance = 2.5f;
        private const float AvoidanceSlideDistance = 2f;
        private const float AvoidanceHeight = 1f;
        private const float NavmeshSteerSampleDistance = 3f;
        private const float NavmeshSteerStrength = 1.5f;
        private const float NavmeshSteerMinDistance = 1.5f;
        private const float BodyRadius = 0.30f;
        private const float MaxHeightSnapDistance = 2f;
        private const float WaypointSnapDistance = 1.5f;
        private const float DoorWaypointClearanceDistance = 1.0f;
        private static readonly float[] DestinationSampleRadii = [1.5f, 4f];
        private static readonly float ClimbableSurfaceNormalY = Mathf.Cos(NavMeshManager.AgentSlope * Mathf.Deg2Rad);
        private static readonly LayerMask ObstacleMask = LayerMask.GetMask("Default", "InvisibleCollider", "Door", "Fence");
        private static readonly int AnySolidMask = ~0;
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[16];
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private static readonly IComparer<RaycastHit> _hitDistanceComparer = new HitDistanceComparer();

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        private static bool IsPlayerCollider(Collider collider)
        {
            if (collider == null)
                return false;

            if (collider.GetComponentInParent<ReferenceHub>() != null)
                return true;

            if (collider.GetComponentInParent<HitboxIdentity>() != null)
                return true;

            return false;
        }
        #endregion

        #region Core Fields
        private ReferenceHub _hub = null!;
        private Player player = null!;
        private Bot _bot = null!;
        private IFpcRole? _fpcRole = null;
        private NavMeshAgent _agent = null!;
        private GameObject _agentHelper = null!;
        private readonly NavigationSystem.RoomQuery _roomQuery = new();
        private bool _useAgentSteering = true;
        internal float _speed = DefaultSpeed;
        private Room _currentTargetRoom = null!;
        private readonly List<Vector3> _waypoints = [];
        private int _currentWaypointIndex = 0;
        private bool _isNavigating = false;
        private bool _waitingForDoor = false;
        private DoorVariant? _currentDoor = null;
        private float _doorWaitTimer = 0f;
        private Door _initialClassDDoor = null!;
        internal bool _enablePatrolMode = false;
        private readonly List<RoomName> _patrolRooms = [];
        private int _currentPatrolIndex = 0;
        private readonly float _waitTimeAtRoom = 3f;
        private float _roomWaitTimer = 0f;
        private float _pathRecalculateTimer = 0f;
        private float _pathFailCooldown = 0f;
        private Vector3 _lastPosition;
        private float _stuckTimer = 0f;
        private bool _isAttemptingUnstuck = false;
        private bool _waitingForElevator = false;
        private bool _usingElevator = false;
        private ElevatorChamber _currentElevator = null!;
        private float _elevatorWaitTimer = 0f;
        private bool _enablePathVisualization = true;
        private float _pathVisualizationTimer = 0f;
        private Color _waypointColor = Color.green;
        private Color _currentWaypointColor = Color.red;
        private Color _completedWaypointColor = Color.gray;
        private bool _isLczDecontaminated = false;
        private bool _isLczDecontaminationImminent = false;
        private bool _isFacilityNuked = false;
        private ElevatorPanel _currentElevatorPanel = null!;
        private bool _waitingToEnterElevator = false;
        private bool _insideElevator = false;
        private Vector3 _elevatorEntryPosition = default;
        private int _targetElevatorLevel = -1;
        private Vector3 _elevatorRideOffset = default;
        private float _elevatorCheckTimer = 0f;
        private bool _fallbackRoomAttempted = false;
        private bool _approachingElevatorPanel = false;
        private bool _walkingIntoElevator = false;
        private ElevatorChamber _walkIntoChamber = null!;
        private float _walkIntoTimer = 0f;
        private const float WalkIntoElevatorTimeout = 5f;
        private readonly Dictionary<DoorVariant, int> _doorFailCounts = [];
        private const int MaxDoorRetryAttempts = 2;
        private DoorVariant _lastFailedDoor = null!;
        private float _squadShareTimer = 0f;
        private const float SquadShareInterval = 0.6f;
        private const float SquadShareMaxAdoptDistance = 8f;
        private const float SquadShareMaxLeaderDistance = 25f;
        private const float SquadShareMaxAdoptDistanceSq = SquadShareMaxAdoptDistance * SquadShareMaxAdoptDistance;
        private const float SquadShareMaxLeaderDistanceSq = SquadShareMaxLeaderDistance * SquadShareMaxLeaderDistance;
        private const float DoorCenterPassDistanceSq = DoorCenterPassDistance * DoorCenterPassDistance;
        private const float WaypointReachedDistanceSq = WaypointReachedDistance * WaypointReachedDistance;
        private const float DoorInteractionDistanceSq = DoorInteractionDistance * DoorInteractionDistance;
        private readonly List<Vector3> _subdivideScratch = [];
        private readonly HashSet<DoorVariant> _insertDoorProcessedScratch = [];
        private readonly List<(int index, Vector3 doorPos, Vector3 pastPos, bool insertPast)> _insertDoorInsertionsScratch = [];
        private DoorVariant? _cachedDoorOnPath = null!;
        private int _cachedDoorWaypointIndex = -1;
        private Vector3 _cachedDoorWaypointPos = Vector3.zero;
        #endregion

        private static readonly List<FacilityZone> DeconZones =
        [
            FacilityZone.LightContainment
        ];

        private static readonly List<FacilityZone> WarheadZones =
        [
            FacilityZone.HeavyContainment,
            FacilityZone.LightContainment,
            FacilityZone.Entrance,
            FacilityZone.Other
        ];

        private int _navMeshRetryCount = 0;
        private const int MaxNavMeshRetries = 60;
        private int _calcDepth = 0;
        private const int MaxCalcDepth = 4;

        #region Initialization
        public void Init(float speed = DefaultSpeed, bool enablePatrol = false, bool enableVisualization = true, bool enableVariation = true, float variationRadius = 2.5f)
        {
            _hub = GetComponent<ReferenceHub>();
            if (_hub == null)
            {
                LogManager.Warn("Navigation.Init: ReferenceHub is null");
                return;
            }

            _fpcRole = _hub.roleManager.CurrentRole as IFpcRole;
            if (_fpcRole == null && Plugin.Instance.Config.Debug)
            {
                LogManager.Debug($"Navigation.Init: bot {(_hub.nicknameSync != null ? _hub.nicknameSync.Network_myNickSync : "unknown")} is not FPC role ({_hub.roleManager.CurrentRole?.RoleTypeId}), movement will be disabled");
            }

            _speed = speed;
            _enablePatrolMode = enablePatrol;
            _enablePathVisualization = enableVisualization;
            _lastPosition = transform.position;

            if (_enablePatrolMode && _patrolRooms.Count == 0)
                SetupDefaultPatrolRoute();

            player = Player.Get(_hub);
            if (player != null)
            {
                _bot = player.GetBot();
                _bot?.CachedNavigation = this;

                _squadShareTimer = player.PlayerId % 5 * 0.12f;
                _elevatorCheckTimer = player.PlayerId % 5 * 0.1f;
                _pathRecalculateTimer = player.PlayerId % 7 * 0.5f;
            }

            EnsureAgent();
        }

        private void EnsureAgent()
        {
            if (_agent != null)
                return;

            if (!NavMeshManager.IsBaked)
                return;

            if (transform.position.y > 3000f)
                return;

            if (transform.position.y < -500f)
                return;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshManager.AgentInitSampleDistance, GetNavMeshAreaMask()))
                return;

            if (_agentHelper == null)
            {
                _agentHelper = new GameObject($"NavAgentHelper_{player?.PlayerId ?? 0}");
                _agentHelper.transform.SetParent(transform, false);
                _agentHelper.transform.position = hit.position;
                _agent = _agentHelper.AddComponent<NavMeshAgent>();
                _agent.enabled = false;
            }
            else
            {
                _agent = _agentHelper.GetComponent<NavMeshAgent>();
                if (_agent == null)
                {
                    _agent = _agentHelper.AddComponent<NavMeshAgent>();
                    _agent.enabled = false;
                }
                else if (_agentHelper.transform.position != hit.position)
                {
                    _agentHelper.transform.position = hit.position;
                }
            }

            _agent.agentTypeID = NavMeshManager.AgentTypeId;
            _agent.radius = NavMeshManager.AgentRadius;
            _agent.height = NavMeshManager.AgentHeight;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.autoTraverseOffMeshLink = false;
            _agent.autoBraking = false;
            string q = Plugin.Instance.Config.NavMeshAvoidanceQuality ?? "None";
            if (q.Equals("None", StringComparison.OrdinalIgnoreCase) || q.Equals("No", StringComparison.OrdinalIgnoreCase) || q.Equals("Disabled", StringComparison.OrdinalIgnoreCase) || q.Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }
            else if (q.Equals("High", StringComparison.OrdinalIgnoreCase))
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            }
            else if (q.Equals("Low", StringComparison.OrdinalIgnoreCase))
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            else if (q.Equals("Medium", StringComparison.OrdinalIgnoreCase))
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            }
            else
            {
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }

            _agent.avoidancePriority = 50 + (player != null ? player.PlayerId % 50 : 0);
            bool wantEnabled = _fpcRole != null;
            if (wantEnabled)
            {
                if (!TryWarpAgentToNavMesh())
                {
                    _agent.enabled = false;
                    _useAgentSteering = false;
                    return;
                }
            }

            _agent.enabled = wantEnabled;
            _useAgentSteering = _agent.enabled && _agent.isOnNavMesh;
        }

        private bool TryWarpAgentToNavMesh()
        {
            if (_agent == null || _agentHelper == null)
                return false;

            if (!NavMeshManager.IsBaked)
                return false;

            float sampleDist = transform.position.y < -500f ? 100f : NavMeshManager.AgentInitSampleDistance;
            Vector3 pos = transform.position;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, sampleDist, GetNavMeshAreaMask()))
            {
                _agentHelper.transform.position = hit.position;
                bool wasEnabled = _agent.enabled;
                if (!wasEnabled)
                    _agent.enabled = true;

                if (_agent.isOnNavMesh)
                    return true;

                try
                {
                    _agent.Warp(hit.position);

                    if (_agent.isOnNavMesh)
                        return true;

                    if (!wasEnabled)
                        _agent.enabled = false;

                    return false;
                }
                catch
                {
                    if (!wasEnabled) 
                        _agent.enabled = false;

                    return false;
                }
            }
            return false;
        }

        private void SyncAgentToTransform()
        {
            if (_agent == null || _agentHelper == null || !_agent.isOnNavMesh)
                return;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, GetNavMeshAreaMask()))
                _agentHelper.transform.position = hit.position;

            if ((_agent.nextPosition - transform.position).sqrMagnitude > 6.25f)
                _agent.Warp(_agentHelper.transform.position);
        }

        private void TryEnableAgentIfNeeded()
        {
            if (_agent == null)
            {
                EnsureAgent();
                if (_agent == null)
                    return;
            }

            if (_agent.enabled && _agent.isOnNavMesh)
                return;

            if (_fpcRole == null)
                return;

            if (!NavMeshManager.IsBaked)
                return;

            if (TryWarpAgentToNavMesh())
                _useAgentSteering = true;
        }
        #endregion

        #region Public Navigation Methods
        public void SetDestination(Room targetRoom)
        {
            if (targetRoom == null)
                return;

            if (_pathFailCooldown > 0f)
                return;

            _currentTargetRoom = targetRoom;
            _isNavigating = true;
            _fallbackRoomAttempted = false;
            _doorFailCounts.Clear();
            _lastFailedDoor = null!;
            ResetNavigationState();
            CalculatePath();
        }

        public void StopNavigation()
        {
            _isNavigating = false;
            _fallbackRoomAttempted = false;
            _approachingElevatorPanel = false;
            ResetNavigationState();
            if (_agent != null && _agent.isOnNavMesh)
                _agent.ResetPath();

            _waypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;
            _isAttemptingUnstuck = false;
        }

        public bool NavigateToWorldPosition(Vector3 targetPosition)
        {
            if (_pathFailCooldown > 0f)
                return false;

            if (!BuildNavMeshPath(targetPosition))
                return false;

            _currentTargetRoom = null!;
            _fallbackRoomAttempted = false;
            _isNavigating = true;

            if (_enablePathVisualization)
                CreatePathVisualization();

            return true;
        }

        public Vector3 ProjectToNavMesh(Vector3 position, float maxDistance = 3f)
        {
            if (NavMeshManager.IsBaked && NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, GetNavMeshAreaMask()))
                return hit.position;

            return position;
        }

        public bool CalculatePathTo(Vector3 targetPosition, List<Vector3> result)
        {
            result.Clear();

            if (!NavMeshManager.IsBaked)
                return false;

            int mask = GetNavMeshAreaMask();
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, NavMeshManager.SampleMaxDistance, mask))
                return false;

            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, NavMeshManager.SampleMaxDistance, mask))
                return false;

            NavMeshPath path = new();
            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, mask, path))
                return false;

            if (path.status == NavMeshPathStatus.PathInvalid || path.corners.Length == 0)
                return false;

            result.AddRange(path.corners);
            SubdivideLongSegments(result);
            SanitizeWaypoints(result);
            if (result.Count == 0)
                return false;

            Room? startRoom = Room.GetRoomAtPosition(startHit.position) ?? player?.CachedRoom ?? Room.GetRoomAtPosition(transform.position);
            if (!ValidateWaypointAdjacency(result, startRoom))
                return false;

            return result.Count > 0;
        }

        private void SubdivideLongSegments(List<Vector3> waypoints)
        {
            float rawMax = Plugin.Instance.Config.MaxWaypointDistance;
            float maxSegmentLength = rawMax <= 0f ? 2.5f : Mathf.Min(rawMax, 2.5f);
            if (maxSegmentLength <= 0f || waypoints.Count < 2)
                return;

            float maxSq = maxSegmentLength * maxSegmentLength;
            _subdivideScratch.Clear();
            if (_subdivideScratch.Capacity < waypoints.Count * 2)
            {
                _subdivideScratch.Capacity = waypoints.Count * 2;
            }

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 from = waypoints[i];
                Vector3 to = waypoints[i + 1];
                _subdivideScratch.Add(from);
                float sq = (to - from).sqrMagnitude;
                if (sq > maxSq)
                {
                    float segmentLength = Mathf.Sqrt(sq);
                    int splits = Mathf.CeilToInt(segmentLength / maxSegmentLength);
                    for (int s = 1; s < splits; s++)
                        _subdivideScratch.Add(Vector3.Lerp(from, to, (float)s / splits));
                }
            }

            _subdivideScratch.Add(waypoints[waypoints.Count - 1]);
            waypoints.Clear();
            waypoints.AddRange(_subdivideScratch);
        }

        private static bool TrySnapToNavMesh(Vector3 position, out Vector3 snapped)
        {
            snapped = position;

            if (!NavMeshManager.IsBaked)
                return false;

            int mask = NavMeshManager.WalkableAreaMask;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, WaypointSnapDistance, mask))
                return false;

            snapped = hit.position;
            return true;
        }

        private static void SanitizeWaypoints(List<Vector3> waypoints)
        {
            if (!NavMeshManager.IsBaked)
                return;

            for (int i = waypoints.Count - 1; i >= 0; i--)
            {
                if (TrySnapToNavMesh(waypoints[i], out Vector3 snapped))
                {
                    waypoints[i] = snapped;
                    continue;
                }

                LogManager.Debug($"Dropped waypoint at {waypoints[i]} no walkable navmesh within {WaypointSnapDistance}m.");
                waypoints.RemoveAt(i);
            }
        }

        private void ResetNavigationState()
        {
            _waitingForDoor = false;
            _waitingForElevator = false;
            _usingElevator = false;
            _waitingToEnterElevator = false;
            _insideElevator = false;
            _approachingElevatorPanel = false;
            _walkingIntoElevator = false;
            _walkIntoChamber = null!;
            _walkIntoTimer = 0f;
            _currentElevatorPanel = null!;
            _currentElevator = null!;
            _targetElevatorLevel = -1;
            _elevatorRideOffset = Vector3.zero;
            _currentWaypointIndex = 0;
            _cachedDoorOnPath = null;
            _cachedDoorWaypointIndex = -1;
        }

        #region Squad Waypoint Sharing
        private bool IsSquadLeader
        {
            get
            {
                if (_bot == null || !_bot.IsInSquad)
                    return false;

                Bot? leader = SquadManager.GetSquadLeader(_bot);
                return leader != null && leader == _bot;
            }
        }

        private bool IsSquadFollower
        {
            get
            {
                if (_bot == null || !_bot.IsInSquad)
                    return false;

                return !IsSquadLeader;
            }
        }

        private Vector3 GetFormationOffset(int memberIndex, List<Vector3> waypoints, int nearestIdx)
        {
            if (memberIndex <= 0 || waypoints.Count == 0)
                return Vector3.zero;

            if (_bot == null)
                return Vector3.zero;

            IReadOnlyList<Bot> squad = SquadManager.GetSquad(_bot);
            int squadSize = squad.Count;
            if (squadSize <= 1)
                return Vector3.zero;

            Vector3 dir;
            if (nearestIdx < waypoints.Count - 1)
            {
                dir = (waypoints[nearestIdx + 1] - waypoints[nearestIdx]).normalized;
            }
            else if (nearestIdx > 0)
            {
                dir = (waypoints[nearestIdx] - waypoints[nearestIdx - 1]).normalized;
            }
            else
                dir = transform.forward;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;

            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            float lateral = 0f;
            float forward = 0f;

            if (squadSize == 2)
            {
                lateral = (memberIndex == 1) ? 0.7f : -0.7f;
                forward = -0.9f;
            }
            else if (squadSize == 3)
            {
                if (memberIndex == 1)
                {
                    lateral = 0.7f;
                    forward = -0.9f;
                }
                else if (memberIndex == 2)
                {
                    lateral = -0.7f;
                    forward = -0.9f;
                }
            }
            else if (squadSize >= 4)
            {
                if (memberIndex == 1)
                {
                    lateral = 0.7f;
                    forward = -0.9f;
                }
                else if (memberIndex == 2)
                {
                    lateral = -0.7f;
                    forward = -0.9f;
                }
                else if (memberIndex == 3)
                {
                    lateral = 0f;
                    forward = -1.8f;
                }
                else
                {
                    lateral = memberIndex % 2 == 0 ? -0.7f : 0.7f;
                    forward = -0.9f * ((memberIndex / 2) + 1);
                }
            }

            return right * lateral + dir * forward;
        }

        public bool TryAdoptSquadWaypoints()
        {
            if (_bot == null || !_bot.IsInSquad)
                return false;

            if (IsSquadLeader)
                return false;

            Bot? leader = SquadManager.GetSquadLeader(_bot);
            if (leader == null || leader == _bot)
                return false;

            Navigation? leaderNav = leader.CachedNavigation;
            if (leaderNav == null || leaderNav == this)
                return false;

            if (!leaderNav._isNavigating || leaderNav._waypoints.Count == 0 || leaderNav._currentTargetRoom == null)
                return false;

            if (leaderNav._insideElevator || leaderNav._walkingIntoElevator || leaderNav._waitingForElevator || leaderNav._waitingToEnterElevator)
                return false;

            if (_insideElevator || _walkingIntoElevator || _waitingForElevator || _waitingToEnterElevator)
                return false;

            if ((transform.position - leader.Player.Position).sqrMagnitude > SquadShareMaxLeaderDistanceSq)
                return false;

            if (_currentTargetRoom != null && _currentTargetRoom != leaderNav._currentTargetRoom && _isNavigating)
                return false;

            if (_currentTargetRoom == null)
            {
                _currentTargetRoom = leaderNav._currentTargetRoom;
            }
            else if (_currentTargetRoom != leaderNav._currentTargetRoom)
                return false;

            List<Vector3> source = leaderNav._waypoints;
            int leaderIdx = leaderNav._currentWaypointIndex;
            if (leaderIdx < 0)
                leaderIdx = 0;

            if (leaderIdx >= source.Count)
                leaderIdx = source.Count - 1;

            int nearestIdx = leaderIdx;
            float bestDistSq = float.MaxValue;
            for (int i = leaderIdx; i < source.Count; i++)
            {
                float d = (transform.position - source[i]).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    nearestIdx = i;
                }
            }

            if (bestDistSq > SquadShareMaxAdoptDistanceSq)
                return false;

            if (_isNavigating && _waypoints.Count > 0 && _currentTargetRoom == leaderNav._currentTargetRoom)
            {
                if (_waypoints.Count >= source.Count - nearestIdx)
                {
                    Vector3 myTail = _waypoints[_waypoints.Count - 1];
                    Vector3 leaderTail = source[source.Count - 1];
                    if ((myTail - leaderTail).sqrMagnitude < 1.44f && Mathf.Abs(_waypoints.Count - (source.Count - nearestIdx)) <= 2)
                        return false;
                }
            }

            int memberIndex = SquadManager.GetSquadMemberIndex(_bot);
            Vector3 offset = GetFormationOffset(memberIndex, source, nearestIdx);

            List<Vector3> newWaypoints = new(source.Count - nearestIdx);
            for (int i = nearestIdx; i < source.Count; i++)
            {
                Vector3 wp = source[i] + offset;
                if (TrySnapToNavMesh(wp, out Vector3 snapped))
                {
                    wp = snapped;
                }
                else if (TrySnapToNavMesh(source[i], out Vector3 snappedOrig))
                {
                    wp = snappedOrig;
                }
                else
                    wp = source[i];

                if ((wp - source[i]).sqrMagnitude > 2.25f && offset.sqrMagnitude > 0.01f)
                {
                    Vector3 reduced = source[i] + offset * 0.5f;
                    if (TrySnapToNavMesh(reduced, out Vector3 snappedReduced))
                        wp = snappedReduced;
                }

                newWaypoints.Add(wp);
            }

            if (newWaypoints.Count == 0)
                return false;

            _waypoints.Clear();
            _waypoints.AddRange(newWaypoints);
            _currentWaypointIndex = 0;
            _isNavigating = true;
            _fallbackRoomAttempted = false;
            _doorFailCounts.Clear();
            _lastFailedDoor = null!;
            _pathFailCooldown = 0f;
            _pathRecalculateTimer = 0f;
            _stuckTimer = 0f;
            _isAttemptingUnstuck = false;

            _waitingForDoor = false;
            _currentDoor = null!;
            if (!_insideElevator && !_walkingIntoElevator)
            {
                _waitingForElevator = false;
                _waitingToEnterElevator = false;
                _approachingElevatorPanel = false;
            }

            if (_enablePathVisualization)
                CreatePathVisualization();

            if (Plugin.Instance.Config.Debug)
            {
                float bestDist = Mathf.Sqrt(bestDistSq);
                LogManager.Debug($"[SquadShare] {player.DisplayName} adopted {newWaypoints.Count} waypoints from leader {leader.Player.DisplayName} (target {leaderNav._currentTargetRoom.Name}, nearest {nearestIdx}/{source.Count}, offset {offset}, bestDist {bestDist:F1}m)");
            }
            return true;
        }

        private void TryPropagateSquadWaypoints()
        {
            if (!IsSquadLeader || _waypoints.Count == 0 || _currentTargetRoom == null)
                return;

            if (_insideElevator || _walkingIntoElevator || _waitingForElevator || _waitingToEnterElevator)
                return;

            List<Bot> mates = SquadManager.GetSquadmates(_bot);
            foreach (Bot mate in mates)
            {
                if (mate == null || !mate.Player.IsAlive || mate.Player.Role == RoleTypeId.Spectator)
                    continue;

                Navigation? mateNav = mate.CachedNavigation;
                if (mateNav == null || mateNav == this)
                    continue;

                if (mateNav._insideElevator || mateNav._walkingIntoElevator || mateNav._waitingForElevator || mateNav._waitingToEnterElevator)
                    continue;

                if (mateNav._isNavigating && mateNav._currentTargetRoom != null && mateNav._currentTargetRoom != _currentTargetRoom)
                    continue;

                if (mateNav._isNavigating && mateNav._waypoints.Count > 0 && mateNav._currentTargetRoom == _currentTargetRoom)
                {
                    if (mateNav._waypoints.Count >= _waypoints.Count)
                    {
                        Vector3 mateTail = mateNav._waypoints[mateNav._waypoints.Count - 1];
                        Vector3 myTail = _waypoints[_waypoints.Count - 1];
                        if ((mateTail - myTail).sqrMagnitude < 1f)
                            continue;
                    }
                }

                if ((mate.Player.Position - transform.position).sqrMagnitude > SquadShareMaxLeaderDistanceSq)
                    continue;

                mateNav.TryAdoptSquadWaypoints();
            }
        }

        public bool TryShareOrAdoptSquadPath()
        {
            if (_bot == null || !_bot.IsInSquad)
                return false;

            if (IsSquadFollower)
                return TryAdoptSquadWaypoints();

            TryPropagateSquadWaypoints();
            return false;
        }
        #endregion
        #endregion

        #region Pathfinding

        public void StartObjective()
        {
            if (_pathFailCooldown > 0f)
                return;

            if (!ObjectivesHandler.TryGetObjective(_bot, out Objective obj))
                return;

            if (IsObjectiveCompleted(obj))
            {
                obj.Completed = true;
                obj.EndObjective(obj.Id);
                return;
            }

            if (obj.RoomToFind != RoomName.Unnamed)
            {
                foreach (Room r in Room.List)
                {
                    if (r.Name == obj.RoomToFind)
                    {
                        SetDestination(r);
                        return;
                    }
                }
            }

            if (obj.EitherItem.Count > 0 || obj.ItemToGet != default)
            {
                Pickup? bestPickup = null;
                float bestDist = float.MaxValue;
                foreach (Pickup p in Pickup.List)
                {
                    if (obj.EitherItem.Count > 0 ? !obj.EitherItem.Contains(p.Type) : p.Type != obj.ItemToGet)
                        continue;

                    float dist = Vector3.Distance(player.Position, p.Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestPickup = p;
                    }
                }

                if (bestPickup != null)
                {
                    Room? pickupRoom = Room.GetRoomAtPosition(bestPickup.Position);
                    if (pickupRoom != null)
                    {
                        SetDestination(pickupRoom);
                        return;
                    }
                }
            }

            if (obj.RoomsOfInterest.Count > 0)
            {
                Room? bestInterest = null;
                float bestDist = float.MaxValue;
                foreach (Room r in Room.List)
                {
                    if (!obj.RoomsOfInterest.Contains(r.Name))
                        continue;

                    float dist = Vector3.Distance(player.Position, r.Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestInterest = r;
                    }
                }

                if (bestInterest != null)
                {
                    SetDestination(bestInterest);
                    return;
                }
            }

            obj.EndObjective(obj.Id);
        }

        private bool IsObjectiveCompleted(Objective obj)
        {
            if (obj.ItemToGet != default)
            {
                foreach (Item i in player.Items)
                {
                    if (i.Type == obj.ItemToGet)
                        return true;
                }
            }

            if (obj.EitherItem.Count > 0)
            {
                foreach (Item i in player.Items)
                {
                    if (obj.EitherItem.Contains(i.Type))
                        return true;
                }
            }

            if (obj.RoomToFind != RoomName.Unnamed && player.Room != null && player.Room.Name == obj.RoomToFind)
                return true;

            if (obj.RoomsOfInterest.Count > 0 && player.Room != null && obj.RoomsOfInterest.Contains(player.Room.Name))
                return true;

            return false;
        }

        private bool IsRoomInDangerousZone(Room room)
        {
            FacilityZone roomZone = room.Zone;

            if ((_isLczDecontaminated || _isLczDecontaminationImminent) && roomZone == FacilityZone.LightContainment)
                return true;

            if (_isFacilityNuked && roomZone != FacilityZone.Surface)
                return true;

            return false;
        }

        private float _zoneSafetyTimer = 0f;
        private const float ZoneSafetyInterval = 1f;

        private void UpdateZoneSafetyStatus()
        {
            _zoneSafetyTimer += Time.deltaTime;
            if (_zoneSafetyTimer < ZoneSafetyInterval)
                return;

            _zoneSafetyTimer = 0f;

            DecontaminationController decontaminationController = Singleton;
            if (decontaminationController != null)
            {
                bool wasDecontaminated = _isLczDecontaminated;
                bool wasDecontaminationImminent = _isLczDecontaminationImminent;
                
                _isLczDecontaminated = decontaminationController.IsDecontaminating;
                _isLczDecontaminationImminent = false;
                
                if (!_isLczDecontaminated && decontaminationController.DecontaminationOverride == DecontaminationStatus.None && decontaminationController.RoundStartTime > 0)
                {
                    double currentServerTime = GetServerTime;
                    
                    if (decontaminationController.DecontaminationPhases != null && decontaminationController.DecontaminationPhases.Length > 0)
                    {
                        float finalPhaseTime = -1f;
                        for (int i = 0; i < decontaminationController.DecontaminationPhases.Length; i++)
                        {
                            DecontaminationPhase phase = decontaminationController.DecontaminationPhases[i];
                            if (phase.Function == DecontaminationPhase.PhaseFunction.Final)
                            {
                                finalPhaseTime = phase.TimeTrigger;
                                break;
                            }
                        }
                        
                        if (finalPhaseTime > 0)
                        {
                            double timeUntilDecontamination = finalPhaseTime - currentServerTime;
                            _isLczDecontaminationImminent = timeUntilDecontamination <= 60.0;
                        }
                    }
                }
                
                if ((!wasDecontaminated && _isLczDecontaminated) || (!wasDecontaminationImminent && _isLczDecontaminationImminent))
                {
                    if (_isLczDecontaminationImminent && !_isLczDecontaminated)
                        LogManager.Info("LCZ decontamination is imminent (less than 60 seconds). Evacuating bots from LCZ.");

                    HandleZoneCompromised(DeconZones);
                }
            }

            AlphaWarheadController warheadController = AlphaWarheadController.Singleton;
            if (warheadController != null)
            {
                List<FacilityZone> zones = WarheadZones;

                bool wasNuked = _isFacilityNuked;
                _isFacilityNuked = warheadController.AlreadyDetonated;

                if (!wasNuked && _isFacilityNuked)
                    HandleZoneCompromised(zones);
            }
        }

        private void HandleZoneCompromised(List<FacilityZone> compromisedZoneList)
        {
            foreach (FacilityZone compromisedZone in compromisedZoneList)
            {
                if (_currentTargetRoom != null && _currentTargetRoom.Zone == compromisedZone)
                {
                    StopNavigation();

                    if (_enablePatrolMode)
                        FindSafePatrolDestination();
                }

                Room currentRoom = player.CachedRoom!;
                if (currentRoom != null && currentRoom.Zone == compromisedZone)
                {
                    LogManager.Warn($"Bot is in compromised zone {compromisedZone}. Attempting evacuation.");
                    EvacuateFromZone(compromisedZone);
                }
            }
        }

        private void EvacuateFromZone(FacilityZone dangerousZone)
        {
            Room currentRoom = player.CachedRoom!;
            if (currentRoom == null)
                return;

            Room? safeRoom = null;
            float safeDist = float.MaxValue;
            foreach (Room r in Room.List)
            {
                if (r.Zone == dangerousZone || IsRoomInDangerousZone(r))
                    continue;
                    
                float dist = Vector3.Distance(currentRoom.Position, r.Position);
                if (dist < safeDist)
                {
                    safeDist = dist;
                    safeRoom = r;
                }
            }

            if (safeRoom != null)
            {
                SetDestination(safeRoom);                
            }
            else
                StopNavigation();
        }


        private void FindSafePatrolDestination()
        {
            List<Room> safePatrolRooms = [];
            foreach (RoomName roomName in _patrolRooms)
            {
                foreach (Room room in Room.Get(roomName))
                {
                    if (!IsRoomInDangerousZone(room))
                        safePatrolRooms.Add(room);
                        
                    break;
                }
            }

            if (safePatrolRooms.Count > 0)
            {
                Room safeRoom = safePatrolRooms[UnityEngine.Random.Range(0, safePatrolRooms.Count)];
                LogManager.Info($"Continuing patrol to safe room: {safeRoom.Name}");
                SetDestination(safeRoom);
            }
            else
            {
                LogManager.Warn("No safe patrol rooms available. Stopping patrol.");
                StopPatrol();

                List<Room>? candidates = null;
                foreach (Room r in Room.List)
                {
                    if (!IsRoomInDangerousZone(r))
                    {
                        candidates ??= [];
                        candidates.Add(r);
                    }
                }

                if (candidates != null && candidates.Count > 0)
                    SetDestination(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            }
        }

        private void CalculatePath()
        {
            if (_calcDepth >= MaxCalcDepth)
            {
                LogManager.Warn($"CalculatePath: max recursion depth reached for {player?.DisplayName}, stopping.");
                _pathFailCooldown = PathFailRetryDelay;
                StopNavigation();
                _calcDepth = 0;
                return;
            }

            _calcDepth++;
            try
            {
                if (_currentTargetRoom == null)
                    return;

                if (transform.position.y > 3000f)
                {
                    LogManager.Debug($"CalculatePath deferred for {player.DisplayName}: still in spawn limbo at {transform.position}");
                    Timing.CallDelayed(0.5f, () =>
                    {
                        if (this != null && _isNavigating)
                            CalculatePath();
                    });

                    return;
                }

                Room? currentRoom = (player.CachedRoom ?? player.Room) ?? Room.GetRoomAtPosition(transform.position);
                if (currentRoom == null)
                {
                    float bestDist = float.MaxValue;
                    Room? bestRoom = null;
                    foreach (Room room in Room.List)
                    {
                        if (room == null || room.GameObject == null)
                            continue;

                        float dist = (room.Position - transform.position).sqrMagnitude;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestRoom = room;
                        }
                    }

                    if (bestRoom != null && bestDist < 2500f)
                        currentRoom = bestRoom;
                }

                if (currentRoom == null || player.Team == Team.Dead)
                {
                    if (transform.position.y > 1000f)
                    {
                        LogManager.Debug($"CalculatePath deferred: Current room is null for {player.DisplayName} at {transform.position}, retrying");
                    }
                    else
                        LogManager.Warn($"Cannot calculate path: Current room is null for {player.DisplayName} at {transform.position} zone UNKNOWN, aborting navigation");

                    _pathFailCooldown = PathFailRetryDelay;
                    StopNavigation();
                    return;
                }

                if (IsZonePairBlocked(currentRoom.Zone, _currentTargetRoom.Zone))
                {
                    if (_fallbackRoomAttempted)
                    {
                        LogManager.Warn($"No allowed destination reachable from {currentRoom.Name} ({currentRoom.Zone}), stopping navigation.");
                        _pathFailCooldown = PathFailRetryDelay;
                        StopNavigation();
                        return;
                    }

                    _fallbackRoomAttempted = true;

                    Room? alternative = PickAlternativeDestination(currentRoom.Zone);
                    if (alternative == null)
                    {
                        LogManager.Warn($"No alternative destination found from {currentRoom.Zone}, stopping navigation.");
                        _pathFailCooldown = PathFailRetryDelay;
                        StopNavigation();
                        return;
                    }

                    LogManager.Debug($"{player.DisplayName} cannot path from {currentRoom.Zone} to {_currentTargetRoom.Zone}, rerouting to {alternative.Name} ({alternative.Zone})");
                    _currentTargetRoom = alternative;
                }

                if (!NavMeshManager.IsBaked)
                {
                    if (_navMeshRetryCount++ >= MaxNavMeshRetries)
                    {
                        LogManager.Warn("NavMesh not baked after max retries, aborting path calculation.");
                        _pathFailCooldown = PathFailRetryDelay;
                        StopNavigation();
                        return;
                    }

                    LogManager.Debug($"NavMesh is not ready yet, retrying path calculation shortly. ({_navMeshRetryCount}/{MaxNavMeshRetries})");
                    Timing.CallDelayed(0.5f, () =>
                    {
                        if (this != null && _isNavigating)
                            CalculatePath();
                    });
                    return;
                }
                _navMeshRetryCount = 0;

                if (IsSquadFollower)
                {
                    if (TryAdoptSquadWaypoints())
                        return;
                }

                Vector3 targetPosition = GetPathTargetPosition(currentRoom);

                if (BuildNavMeshPath(targetPosition))
                {
                    if (_enablePathVisualization)
                        CreatePathVisualization();

                    TryPropagateSquadWaypoints();
                        
                    return;
                }

                if (_fallbackRoomAttempted)
                {
                    string cur = _bot.Player.Room?.Name.ToString() ?? "null";
                    string tgt = _currentTargetRoom?.Name.ToString() ?? "null";
                    LogManager.Debug($"{_bot.Player.DisplayName} - {cur} -> {tgt} No reachable path found stopping navigation (targetPos {targetPosition}).");
                    _pathFailCooldown = PathFailRetryDelay;
                    StopNavigation();
                    return;
                }

                _fallbackRoomAttempted = true;

                Room? preferredRandom = PickAlternativeDestination(currentRoom.Zone);
                Room randomRoom = preferredRandom ?? RoomExtensions.GetRandomRoomByBlacklist();
                if (randomRoom != null && randomRoom != _currentTargetRoom)
                {
                    LogManager.Debug($"No path found from {currentRoom.Name} to {_currentTargetRoom.Name}, Selecting random room {randomRoom.Name} - {randomRoom.GameObject.name}");
                    _currentTargetRoom = randomRoom;
                    CalculatePath();
                }
                else
                {
                    _pathFailCooldown = PathFailRetryDelay;
                    StopNavigation();
                }
            }
            finally
            {
                _calcDepth--;
            }
        }

        private Vector3 GetPathTargetPosition(Room currentRoom)
        {
            Vector3? classDDoorFront = GetClassDInitialDoorTarget(currentRoom);
            if (classDDoorFront != null)
            {
                LogManager.Debug($"ClassD initial waypoint: door at {classDDoorFront.Value}");
                return classDDoorFront.Value;
            }

            if (NeedsElevatorTravel(currentRoom, _currentTargetRoom))
            {
                LogManager.Debug($"Cross zone travel detected: {currentRoom.Zone} -> {_currentTargetRoom.Zone}");

                Room? checkpointRoom = FindCheckpointForZoneTravel(currentRoom, _currentTargetRoom);
                if (checkpointRoom != null)
                {
                    LogManager.Debug($"Navigating to checkpoint {checkpointRoom.Name} before elevator travel");
                    return GetRoomDestination(checkpointRoom);
                }

                LogManager.Warn($"{_bot.Player.DisplayName} Could not find appropriate checkpoint for zone travel");
            }

            return GetRoomDestination(_currentTargetRoom);
        }

        private Vector3 GetRoomDestination(Room room) => _roomQuery.GetRoomDestination(room, transform.position);

        private int GetNavMeshAreaMask() => NavMeshManager.WalkableAreaMask;

        private bool BuildNavMeshPath(Vector3 targetPosition)
        {
            _waypoints.Clear();
            _currentWaypointIndex = 0;
            if (transform.position.y > 3000f)
                return false;

            int areaMask = GetNavMeshAreaMask();
            float startSnap = NavMeshManager.StartSnapDistance;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, startSnap, areaMask))
            {
                LogManager.Debug($"No navmesh found near {player.DisplayName} at {transform.position} (deferred)");
                return false;
            }

            if ((transform.position - startHit.position).sqrMagnitude > 16f)
            {
                if (Plugin.Instance.Config.Debug)
                {
                    float d = Vector3.Distance(transform.position, startHit.position);
                    LogManager.Debug($"Bot is {d:F2}m from nearest navmesh.");
                }

                return false;
            }

            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, NavMeshManager.SampleMaxDistance, areaMask))
            {
                if (!NavMesh.SamplePosition(targetPosition, out targetHit, 4f, WalkableWithDoorsMaskFallback()))
                {
                    LogManager.Debug($"No navmesh found near target {targetPosition} (start {startHit.position}, targetZone {Room.GetRoomAtPosition(targetPosition)?.Zone})");
                    return false;
                }
            }

            NavMeshPath path = new();
            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, areaMask, path))
            {
                LogManager.Debug($"NavMesh.CalculatePath failed from {startHit.position} to {targetHit.position}");
                return false;
            }

            if (path.status == NavMeshPathStatus.PathInvalid || path.corners == null || path.corners.Length == 0)
            {
                LogManager.Debug($"NavMesh path is unusable (status: {path.status})");
                return false;
            }

            if (path.status == NavMeshPathStatus.PathPartial)
                LogManager.Debug("NavMesh returned a partial path; navigating as far as possible.");

            _waypoints.AddRange(path.corners);

            if ((_waypoints[_waypoints.Count - 1] - targetHit.position).sqrMagnitude > 0.25f)
                _waypoints.Add(targetHit.position);

            SubdivideLongSegments(_waypoints);
            ClampWaypointsToZoneHeights();
            SanitizeWaypoints(_waypoints);

            if (_waypoints.Count == 0)
            {
                LogManager.Debug("All waypoints were rejected during navmesh validation, treating path as failed.");
                return false;
            }

            InsertDoorWaypoints();
            SubdivideLongSegments(_waypoints);
            SanitizeWaypoints(_waypoints);

            if (_waypoints.Count == 0)
            {
                LogManager.Debug("All waypoints were rejected after door waypoint processing, treating path as failed.");
                return false;
            }

            Room? startRoom = Room.GetRoomAtPosition(startHit.position) ?? player.CachedRoom ?? Room.GetRoomAtPosition(transform.position);
            if (!ValidateWaypointAdjacency(_waypoints, startRoom))
            {
                LogManager.Debug($"NavMesh path adjacency warning: waypoints traverse nonadjacent rooms from {startRoom?.Name} to {Room.GetRoomAtPosition(targetHit.position)?.Name} - but physics check passed, allowing.");
            }

            if (!ValidatePathPhysics(_waypoints, transform.position))
            {
                LogManager.Debug($"NavMesh path physics validation failed from {startRoom?.Name} to {Room.GetRoomAtPosition(targetHit.position)?.Name}, treating as failed.");
                return false;
            }

            LogManager.Debug($"NavMesh path built with {_waypoints.Count} waypoints (status: {path.status})");
            return true;
        }

        private static int WalkableWithDoorsMaskFallback() => NavMeshManager.WalkableWithDoorsMask;

        private bool ValidatePathPhysics(List<Vector3> waypoints, Vector3 startPos)
        {
            if (waypoints == null || waypoints.Count < 2)
                return true;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 from = waypoints[i];
                Vector3 to = waypoints[i + 1];
                if ((from - to).sqrMagnitude < 0.09f)
                    continue;
                    
                bool fromOnMesh = NavMesh.SamplePosition(from, out NavMeshHit fh, 1f, GetNavMeshAreaMask());
                bool toOnMesh = NavMesh.SamplePosition(to, out NavMeshHit th, 1f, GetNavMeshAreaMask());
                if (fromOnMesh && toOnMesh)
                {
                    Vector3 fromSnap = fh.position;
                    Vector3 toSnap = th.position;
                    if (NavMesh.Raycast(fromSnap, toSnap, out NavMeshHit hit, GetNavMeshAreaMask()))
                    {
                        float distToHit = Vector3.Distance(fromSnap, hit.position);
                        float segLen = Vector3.Distance(fromSnap, toSnap);
                        if (distToHit < segLen - 0.6f)
                        {
                            Vector3 dir = (toSnap - fromSnap).normalized;
                            float dist = segLen;
                            Vector3 origin = fromSnap + Vector3.up * 0.5f;
                            if (Physics.Raycast(origin, dir, out RaycastHit phyHit, dist, AnySolidMask, QueryTriggerInteraction.Ignore))
                            {
                                if (phyHit.collider != null && !IsPlayerCollider(phyHit.collider) && phyHit.collider.GetComponentInParent<DoorVariant>() == null)
                                    return false;

                                if (Physics.CapsuleCast(fromSnap + Vector3.up * 0.2f, fromSnap + Vector3.up * 1f, BodyRadius, dir, out RaycastHit capHit, dist, AnySolidMask, QueryTriggerInteraction.Ignore))
                                {
                                    if (capHit.collider != null && !IsPlayerCollider(capHit.collider) && capHit.collider.GetComponentInParent<DoorVariant>() == null)
                                        return false;
                                }
                            }
                        }
                    }
                }

                Vector3 mid = (from + to) * 0.5f;
                int overlaps = Physics.OverlapSphereNonAlloc(mid + Vector3.up * 0.5f, BodyRadius, _overlapBuffer, AnySolidMask, QueryTriggerInteraction.Ignore);
                for (int o = 0; o < overlaps; o++)
                {
                    Collider c = _overlapBuffer[o];
                    if (c == null || c.isTrigger || c.transform.root == transform.root)
                        continue;

                    if (IsPlayerCollider(c))
                        continue;

                    if (c.GetComponentInParent<DoorVariant>() != null)
                        continue;

                    if (c is MeshCollider mc2 && !mc2.convex)
                        continue;

                    if (c is not BoxCollider && c is not SphereCollider && c is not CapsuleCollider && c is not MeshCollider)
                        continue;

                    float hitsDistSq = (c.ClosestPoint(mid) - mid).sqrMagnitude;
                    if (hitsDistSq < 0.0225f)
                    {
                        Vector3 dir2 = (to - from).normalized;
                        if (Physics.Raycast(from + Vector3.up * 0.5f, dir2, out RaycastHit rh, (to - from).magnitude, AnySolidMask, QueryTriggerInteraction.Ignore))
                        {
                            if (rh.collider == c)
                                return false;
                        }
                    }
                }
            }
            return true;
        }

        private static bool ValidateWaypointAdjacency(List<Vector3> waypoints, Room? startRoom)
        {
            if (waypoints == null || waypoints.Count == 0 || startRoom == null)
                return true;

            Room prevRoom = startRoom;
            foreach (Vector3 wp in waypoints)
            {
                Room? cur = Room.GetRoomAtPosition(wp);
                if (cur == null)
                    continue;

                if (cur == prevRoom)
                    continue;

                bool adjacent = false;
                foreach (Room r in prevRoom.AdjacentRooms)
                {
                    if (r == cur)
                    {
                        adjacent = true;
                        break;
                    }
                }

                if (!adjacent)
                {
                    foreach (Room r in cur.AdjacentRooms)
                    {
                        if (r == prevRoom)
                        {
                            adjacent = true;
                            break;
                        }
                    }
                }

                if (!adjacent)
                    return false;

                prevRoom = cur;
            }

            return true;
        }

        private void ClampWaypointsToZoneHeights()
        {
            Dictionary<FacilityZone, Vector2> limits = Plugin.Instance.Config.WaypointHeightLimits;
            if (limits == null || limits.Count == 0)
                return;

            Room? botRoom = player?.CachedRoom ?? player?.Room ?? Room.GetRoomAtPosition(transform.position);
            bool botInHcz049 = botRoom != null && botRoom.Name == RoomName.Hcz049;

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Vector3 waypoint = _waypoints[i];
                Room? waypointRoom = Room.GetRoomAtPosition(waypoint);
                if (waypointRoom != null && waypointRoom.Name == RoomName.Hcz049)
                    continue;

                if (botInHcz049)
                    continue;

                if (!limits.TryGetValue(GetWaypointZone(waypoint), out Vector2 band))
                    continue;

                float clampedY = Mathf.Clamp(waypoint.y, band.x, band.y);
                if (Mathf.Approximately(clampedY, waypoint.y))
                    continue;

                Vector3 clamped = new(waypoint.x, clampedY, waypoint.z);
                if (TrySnapToNavMesh(clamped, out Vector3 snapped))
                {
                    _waypoints[i] = snapped;
                }
                else
                    LogManager.Debug($"Skipped height clamp at {waypoint} - clamped position {clamped} has no walkable navmesh nearby.");
            }
        }

        private void InsertDoorWaypoints()
        {
            if (_waypoints.Count < 2)
                return;

            _insertDoorProcessedScratch.Clear();
            _insertDoorInsertionsScratch.Clear();
            HashSet<DoorVariant> processedDoors = _insertDoorProcessedScratch;
            List<(int index, Vector3 doorPos, Vector3 pastPos, bool insertPast)> insertions = _insertDoorInsertionsScratch;

            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                Vector3 from = _waypoints[i];
                Vector3 to = _waypoints[i + 1];
                Vector3 delta = to - from;
                float segmentLength = delta.magnitude;
                Vector3 segmentDir = delta / (segmentLength > 0.001f ? segmentLength : 1f);

                if (segmentLength < 1f)
                    continue;

                int hitCount = Physics.RaycastNonAlloc(from + Vector3.up * 0.5f, segmentDir, _raycastBuffer, segmentLength, ObstacleMask, QueryTriggerInteraction.Collide);
                if (hitCount == 0)
                    continue;

                Array.Sort(_raycastBuffer, 0, hitCount, _hitDistanceComparer);

                for (int j = 0; j < hitCount; j++)
                {
                    if (_raycastBuffer[j].collider == null)
                        continue;

                    DoorVariant door = _raycastBuffer[j].collider.GetComponentInParent<DoorVariant>();
                    if (door == null || !processedDoors.Add(door))
                        continue;

                    Vector3 doorPos = _raycastBuffer[j].point;
                    if (doorPos == Vector3.zero)
                        doorPos = door.transform.position;

                    if ((doorPos - from).sqrMagnitude < 0.25f || (doorPos - to).sqrMagnitude < 0.25f)
                        continue;

                    Vector3 doorForward = door.transform.forward;
                    doorForward.y = 0f;
                    if (doorForward.sqrMagnitude < 0.001f)
                    {
                        doorForward = segmentDir;
                        doorForward.y = 0f;
                    }

                    doorForward.Normalize();
                    if (Vector3.Dot(doorForward, segmentDir) < 0f)
                    {
                        doorForward = -doorForward;
                    }

                    Vector3 pastPos = doorPos + doorForward * DoorWaypointClearanceDistance;
                    bool insertPast = (pastPos - to).sqrMagnitude >= 0.25f;

                    insertions.Add((i + 1, doorPos, pastPos, insertPast));
                }
            }

            for (int i = insertions.Count - 1; i >= 0; i--)
            {
                (int index, Vector3 doorPos, Vector3 pastPos, bool insertPast) = insertions[i];

                if (TrySnapToNavMesh(doorPos, out Vector3 snappedDoorPos))
                    doorPos = snappedDoorPos;

                if (insertPast)
                {
                    if (TrySnapToNavMesh(pastPos, out Vector3 snappedPastPos))
                        pastPos = snappedPastPos;

                    _waypoints.Insert(index, pastPos);
                }

                _waypoints.Insert(index, doorPos);
            }
        }

        private static FacilityZone GetWaypointZone(Vector3 position)
        {
            Room? room = Room.GetRoomAtPosition(position);
            return room?.Zone ?? FacilityZone.Other;
        }

        private Vector3? GetClassDInitialDoorTarget(Room currentRoom)
        {
            if (_hub.roleManager.CurrentRole.RoleTypeId != RoleTypeId.ClassD || _initialClassDDoor != null || Round.Duration.TotalSeconds >= 30)
                return null;

            Door? closest = null;
            float closestDist = float.MaxValue;
            foreach (Door d in currentRoom.Doors)
            {
                float dist = Vector3.Distance(_hub.transform.position, d.Position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = d;
                }
            }

            if (closest != null)
            {
                _initialClassDDoor = closest;
                Vector3 dirOut = (closest.Position - currentRoom.Position).normalized;
                return closest.Position + dirOut * 1.5f;
            }

            return null;
        }

        private static readonly ElevatorGroup[] KeycardCheckpointGroups =
        [
            ElevatorGroup.LczA01,
            ElevatorGroup.LczA02,
            ElevatorGroup.LczB01,
            ElevatorGroup.LczB02
        ];

        private static readonly ElevatorGroup[] ExitGateGroups =
        [
            ElevatorGroup.GateA01,
            ElevatorGroup.GateA02,
            ElevatorGroup.GateB
        ];

        private static bool IsKeycardElevatorChamber(ElevatorChamber chamber) => chamber != null && (KeycardCheckpointGroups.Contains(chamber.AssignedGroup) || ExitGateGroups.Contains(chamber.AssignedGroup));

        private static DoorPermissionFlags GetRequiredElevatorPermission(ElevatorChamber chamber)
        {
            if (chamber == null)
                return DoorPermissionFlags.None;

            if (KeycardCheckpointGroups.Contains(chamber.AssignedGroup))
                return DoorPermissionFlags.Checkpoints;

            if (ExitGateGroups.Contains(chamber.AssignedGroup))
                return DoorPermissionFlags.ExitGates;

            return DoorPermissionFlags.None;
        }

        private bool HasElevatorKeycardAccess(ElevatorChamber chamber)
        {
            if (player == null || chamber == null)
                return true;

            DoorPermissionFlags required = GetRequiredElevatorPermission(chamber);
            if (required == DoorPermissionFlags.None)
                return true;

            foreach (Item item in player.Items)
            {
                if (item is KeycardItem keycard && (keycard.Permissions & required) == required)
                    return true;
            }

            return false;
        }

        private bool IsCheckpointElevatorRestricted(Room room)
        {
            if (player.Team == Team.SCPs)
                return false;

            if (room?.Name is not (RoomName.LczCheckpointA or RoomName.LczCheckpointB or RoomName.HczCheckpointA or RoomName.HczCheckpointB or RoomName.EzGateA or RoomName.EzGateB))
                return false;

            foreach (Door door in room.Doors)
            {
                if (door.Base is not Interactables.Interobjects.ElevatorDoor elevatorDoor)
                    continue;

                if (elevatorDoor.Chamber == null || !IsKeycardElevatorChamber(elevatorDoor.Chamber))
                    continue;

                return !HasElevatorKeycardAccess(elevatorDoor.Chamber);
            }

            return false;
        }
        #endregion

        #region Movement and Navigation
        private void NavigateToWaypoint()
        {
            if (_currentWaypointIndex >= _waypoints.Count)
            {
                _isNavigating = false;
                if (Plugin.Instance.Config.Debug)
                {
                    LogManager.Debug($"Navigation completed. Current room: {player.CachedRoom?.Name}, Target room: {_currentTargetRoom?.Name}");
                }

                Room currentRoom = player.CachedRoom!;
                if (currentRoom != null && TryHandleElevatorIfNeeded(currentRoom))
                    return;

                if (_enablePatrolMode)
                    _roomWaitTimer = _waitTimeAtRoom;

                return;
            }

            while (_currentWaypointIndex < _waypoints.Count && (transform.position - _waypoints[_currentWaypointIndex]).sqrMagnitude <= WaypointReachedDistanceSq)
            {
                _currentWaypointIndex++;
                UpdateWaypointVisualization();
                if (_currentWaypointIndex >= _waypoints.Count)
                {
                    _isNavigating = false;
                    if (Plugin.Instance.Config.Debug)
                    {
                        LogManager.Debug($"Navigation completed. Current room: {player.CachedRoom?.Name}, Target room: {_currentTargetRoom?.Name}");
                    }

                    Room currentRoom = player.CachedRoom!;
                    if (currentRoom != null && TryHandleElevatorIfNeeded(currentRoom))
                        return;

                    if (_enablePatrolMode)
                        _roomWaitTimer = _waitTimeAtRoom;

                    return;
                }
            }

            if (_currentWaypointIndex >= _waypoints.Count)
                return;

            Vector3 currentWaypoint = _waypoints[_currentWaypointIndex];
            DoorVariant pathDoor = FindDoorOnPath(currentWaypoint);
            if (pathDoor != null && !pathDoor.TargetState)
            {
                if (pathDoor is Interactables.Interobjects.ElevatorDoor)
                    return;

                InteractWithDoor(pathDoor);
                return;
            }

            bool isFinalWaypoint = _currentWaypointIndex == _waypoints.Count - 1;

            if (pathDoor != null && (transform.position - pathDoor.transform.position).sqrMagnitude > DoorCenterPassDistanceSq)
            {
                MoveTowards(pathDoor.transform.position, _speed, slowDown: isFinalWaypoint);
                return;
            }

            Vector3 target = currentWaypoint;
            if (!isFinalWaypoint && _currentWaypointIndex + 1 < _waypoints.Count)
            {
                if ((transform.position - currentWaypoint).sqrMagnitude < 4f)
                {
                    Vector3 next = _waypoints[_currentWaypointIndex + 1];
                    target = Vector3.Lerp(currentWaypoint, next, 0.35f);
                }
            }

            MoveTowards(target, _speed, slowDown: isFinalWaypoint);
        }

        private Room? FindCheckpointForZoneTravel(Room currentRoom, Room targetRoom)
        {
            FacilityZone currentZone = currentRoom.Zone;
            FacilityZone targetZone = targetRoom.Zone;
            int currentCluster = GetWalkCluster(currentZone);
            int targetCluster = GetWalkCluster(targetZone);

            LogManager.Debug($"Finding checkpoint for travel: {currentZone} -> {targetZone}");

            if (currentCluster < 0 || targetCluster < 0 || currentCluster == targetCluster)
                return null!;

            if (currentCluster == 2)
                return GetSurfaceGateRoom();

            List<RoomName> possibleCheckpoints = [];

            if (currentCluster == 0)
            {
                possibleCheckpoints.Add(RoomName.LczCheckpointA);
                possibleCheckpoints.Add(RoomName.LczCheckpointB);
            }
            else if (targetCluster == 0)
            {
                possibleCheckpoints.Add(RoomName.HczCheckpointA);
                possibleCheckpoints.Add(RoomName.HczCheckpointB);
            }
            else if (targetCluster == 2)
            {
                possibleCheckpoints.Add(RoomName.EzGateA);
                possibleCheckpoints.Add(RoomName.EzGateB);
            }

            Room closestCheckpoint = null!;
            float closestDistance = float.MaxValue;

            foreach (RoomName checkpointName in possibleCheckpoints)
            {
                foreach (Room checkpoint in Room.Get(checkpointName))
                {
                    if (checkpoint == currentRoom)
                        continue;

                    if (!IsRoomInDangerousZone(checkpoint) && !IsCheckpointElevatorRestricted(checkpoint))
                    {
                        float distance = Vector3.Distance(currentRoom.Position, checkpoint.Position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestCheckpoint = checkpoint;
                        }
                    }
                    break;
                }
            }

            if (closestCheckpoint != null)
                return closestCheckpoint!;

            closestDistance = float.MaxValue;
            Room fallback = null!;
            foreach (RoomName checkpointName in possibleCheckpoints)
            {
                foreach (Room checkpoint in Room.Get(checkpointName))
                {
                    if (checkpoint == currentRoom)
                        continue;

                    if (IsRoomInDangerousZone(checkpoint))
                        continue;

                    float distance = Vector3.Distance(currentRoom.Position, checkpoint.Position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        fallback = checkpoint;
                    }
                    break;
                }
            }

            if (fallback != null)
                return fallback!;

            foreach (RoomName checkpointName in possibleCheckpoints)
            {
                foreach (Room r in Room.Get(checkpointName))
                {
                    if (r == currentRoom)
                        continue;

                    return r!;
                }
            }

            return null!;
        }

        public void MoveTowards(Vector3 target, float speed = -1f, bool slowDown = false, bool lookAtTarget = true)
        {
            if (_fpcRole?.FpcModule?.Motor == null)
                return;

            if (_useAgentSteering && _agent != null && _agent.enabled && _agent.isOnNavMesh && NavMeshManager.IsBaked)
            {
                SyncAgentToTransform();
                _agent.speed = speed > 0f ? speed : _speed;
                bool targetOnMesh = false;
                try
                {
                    if (NavMesh.SamplePosition(target, out NavMeshHit th, 2f, GetNavMeshAreaMask()))
                    {
                        Vector3 agentTarget = th.position;
                        targetOnMesh = true;
                        if (Vector3.Distance(_agent.destination, agentTarget) > 0.5f || !_agent.hasPath)
                            _agent.SetDestination(agentTarget);
                    }
                }
                catch
                {
                    _useAgentSteering = false;
                    targetOnMesh = false;
                }

                if (targetOnMesh)
                {
                    Vector3 vel;
                    try
                    {
                        vel = _agent.desiredVelocity;
                    }
                    catch
                    {
                        vel = Vector3.zero;
                    }

                    if (vel.sqrMagnitude > 0.01f)
                    {
                        float moveSpeed = speed > 0f ? speed : _speed;
                        float distToTarget = Vector3.Distance(transform.position, target);
                        if (slowDown && distToTarget < 3f)
                            moveSpeed *= distToTarget / 3f;

                        Vector3 dir = vel.normalized;
                        if (dir.sqrMagnitude < 0.01f)
                            dir = (target - transform.position).normalized;

                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f)
                            dir.Normalize();

                        float appliedSpeed = Mathf.Min(moveSpeed, vel.magnitude);
                        if (appliedSpeed < 0.01f)
                            appliedSpeed = moveSpeed;

                        Vector3 movement = dir * appliedSpeed * Time.deltaTime;
                        if (slowDown && movement.magnitude > distToTarget)
                            movement = dir * distToTarget;

                        Vector3 newPosition = transform.position + movement;
                        newPosition = ResolveCollision(transform.position, newPosition);

                        if (NavMesh.SamplePosition(newPosition, out NavMeshHit samp, NavmeshSteerSampleDistance, GetNavMeshAreaMask()))
                        {
                            if (Mathf.Abs(samp.position.y - newPosition.y) <= MaxHeightSnapDistance)
                            {
                                newPosition.y = samp.position.y;
                            }
                            else if (Vector3.Distance(samp.position, newPosition) > 1f)
                            {
                                newPosition = samp.position;
                            }
                        }

                        if (NavMesh.SamplePosition(newPosition, out NavMeshHit agentHit, 1f, GetNavMeshAreaMask()))
                        {
                            _agent.nextPosition = agentHit.position;
                        }
                        else
                            _agent.nextPosition = newPosition;

                        _fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
                        if (lookAtTarget)
                        {
                            Vector3 lookDir = dir;
                            lookDir.y = Mathf.Clamp((target - transform.position).normalized.y, -0.2f, 0.2f);
                            lookDir.Normalize();
                            _fpcRole.FpcModule.MouseLook.LookAtDirection(lookDir, Time.deltaTime * LookTurnSpeed);
                        }

                        return;
                    }
                }
            }

            Vector3 currentPosition = transform.position;
            Vector3 toTarget = target - currentPosition;
            Vector3 direction = toTarget;
            direction.y = 0f;
            direction.Normalize();

            if (direction.sqrMagnitude < 0.001f)
                return;

            Vector3 avoidance = ComputeAvoidance(currentPosition, direction);
            Vector3 navSteer = ComputeNavMeshSteer(currentPosition, target);
            Vector3 finalDir = (direction + avoidance + navSteer).normalized;

            if (finalDir.sqrMagnitude < 0.001f)
                return;

            float moveSpeed2 = speed > 0f ? speed : _speed;
            float distanceToTarget2 = toTarget.magnitude;

            if (slowDown && distanceToTarget2 < 3f)
                moveSpeed2 *= distanceToTarget2 / 3f;

            Vector3 movement2 = Time.deltaTime * moveSpeed2 * finalDir;

            if (slowDown && movement2.magnitude > distanceToTarget2)
                movement2 = finalDir * distanceToTarget2;

            Vector3 newPosition2 = currentPosition + movement2;
            if (NavMeshManager.IsBaked && NavMesh.SamplePosition(newPosition2, out NavMeshHit sample2, NavmeshSteerSampleDistance, GetNavMeshAreaMask()))
            {
                if (Mathf.Abs(sample2.position.y - newPosition2.y) <= MaxHeightSnapDistance)
                    newPosition2.y = sample2.position.y;
            }

            newPosition2 = ResolveCollision(currentPosition, newPosition2);

            _fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition2);

            if (!lookAtTarget)
                return;

            Vector3 lookDirection = finalDir;
            lookDirection.y = Mathf.Clamp(toTarget.normalized.y, -0.2f, 0.2f);
            lookDirection.Normalize();
            _fpcRole.FpcModule.MouseLook.LookAtDirection(lookDirection, Time.deltaTime * LookTurnSpeed);
        }

        private Vector3 ComputeNavMeshSteer(Vector3 position, Vector3 target)
        {
            if (!NavMeshManager.IsBaked)
                return Vector3.zero;

            int mask = GetNavMeshAreaMask();
            if (!NavMesh.SamplePosition(position, out NavMeshHit startHit, NavmeshSteerSampleDistance, mask))
                return Vector3.zero;

            if (!NavMesh.SamplePosition(target, out NavMeshHit targetHit, NavmeshSteerSampleDistance, mask))
                return Vector3.zero;

            if (!NavMesh.Raycast(startHit.position, targetHit.position, out NavMeshHit edgeHit, mask))
                return Vector3.zero;

            float distanceToEdge = Vector3.Distance(startHit.position, edgeHit.position);
            if (distanceToEdge < NavmeshSteerMinDistance)
                return Vector3.zero;

            Vector3 toTarget = targetHit.position - startHit.position;
            Vector3 edge = Vector3.Cross(Vector3.up, edgeHit.normal).normalized;
            if (edge.sqrMagnitude < 0.01f)
                return Vector3.zero;

            if (Vector3.Dot(edge, toTarget) < 0f)
                edge = -edge;

            float strength = Mathf.Clamp01(NavmeshSteerStrength / distanceToEdge);
            return edge * strength;
        }


        private Vector3 ResolveCollision(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f)
                return to;

            Vector3 direction = delta / distance;
            Vector3 bottom = from + Vector3.up * 0.05f;
            Vector3 top = from + Vector3.up * 0.55f;

            if (Physics.CapsuleCast(bottom, top, BodyRadius, direction, out RaycastHit hit, distance, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (IsPlayerCollider(hit.collider))
                    return to;

                if (hit.normal.y >= ClimbableSurfaceNormalY)
                    return to;

                if (hit.collider != null)
                {
                    DoorVariant door = hit.collider.GetComponentInParent<DoorVariant>();
                    if (door != null)
                    {
                        if (door.TargetState)
                            return to;

                        if (IsDoorFailed(door))
                            return to;
                    }
                }

                float hitHeightAboveFeet = hit.point.y - from.y;
                if (hitHeightAboveFeet <= NavMeshManager.AgentClimb + 0.05f && hit.normal.y < 0.4f)
                {
                    if (IsWalkableNavMeshPoint(to, 0.6f))
                        return to;
                }

                float safeDistance = Mathf.Max(0f, hit.distance - 0.03f);
                Vector3 blocked = from + direction * safeDistance;

                Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude > 0.01f)
                {
                    slide.Normalize();
                    blocked += slide * Mathf.Min(0.25f, distance - safeDistance);
                }

                return blocked;
            }

            return to;
        }

        private void MoveTowardsTarget(Vector3 target) => MoveTowards(target, _speed, slowDown: true);

        private static bool IsWalkableNavMeshPoint(Vector3 point, float sampleRadius = 0.5f)
        {
            if (!NavMeshManager.IsBaked)
                return false;

            return NavMesh.SamplePosition(point, out NavMeshHit hit, sampleRadius, NavMeshManager.WalkableAreaMask) && Vector3.Distance(hit.position, point) <= sampleRadius;
        }

        private Vector3 ComputeAvoidance(Vector3 position, Vector3 direction)
        {
            CharacterController? charController = _fpcRole?.FpcModule.Motor.MainModule.CharController;
            bool blocked = charController != null && (charController.collisionFlags & CollisionFlags.Sides) != 0;

            Vector3 origin = position + Vector3.up * AvoidanceHeight;
            if (Physics.Raycast(origin, direction, out RaycastHit frontHit, AvoidanceDetectDistance, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsPlayerCollider(frontHit.collider) && !IsWalkableNavMeshPoint(frontHit.point) && frontHit.normal.y < 0.4f && frontHit.distance < AvoidanceSlideDistance)
                {
                    Vector3 slide = Vector3.ProjectOnPlane(direction, frontHit.normal);
                    slide.y = 0f;

                    if (slide.sqrMagnitude < 0.001f)
                    {
                        Vector3 lateral = Vector3.Cross(Vector3.up, frontHit.normal).normalized;
                        bool rightClear = HasSideClearance(position, lateral);
                        bool leftClear = HasSideClearance(position, -lateral);
                        if (rightClear && !leftClear)
                        {
                            slide = lateral;
                        }
                        else if (leftClear && !rightClear)
                        {
                            slide = -lateral;
                        }
                        else
                            return Vector3.zero;
                    }

                    slide.Normalize();
                    float strength = blocked ? 1f : Mathf.Clamp01(1f - (frontHit.distance - 0.5f) / (AvoidanceSlideDistance - 0.5f));
                    return Vector3.Slerp(direction, slide, strength).normalized - direction;
                }
            }

            if (blocked)
            {
                Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                bool leftClear = HasSideClearance(position, -right);
                bool rightClear = HasSideClearance(position, right);

                if (leftClear && !rightClear)
                    return -right;

                if (rightClear && !leftClear)
                    return right;

                if (leftClear && rightClear)
                    return right;
            }

            return Vector3.zero;
        }

        private bool HasSideClearance(Vector3 position, Vector3 sideDirection, float checkDistance = 1.5f)
        {
            int hitCount = Physics.RaycastNonAlloc(position + Vector3.up * 0.5f, sideDirection, _raycastBuffer, checkDistance, ObstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                if (!IsPlayerCollider(_raycastBuffer[i].collider))
                    return false;
            }

            hitCount = Physics.RaycastNonAlloc(position + Vector3.up * AvoidanceHeight, sideDirection, _raycastBuffer, checkDistance, ObstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                if (!IsPlayerCollider(_raycastBuffer[i].collider))
                    return false;
            }

            return true;
        }

        private DoorVariant FindDoorOnPath(Vector3 waypoint)
        {
            if (_cachedDoorWaypointIndex == _currentWaypointIndex && _cachedDoorWaypointPos == waypoint && _cachedDoorOnPath != null)
            {
                if (_cachedDoorOnPath != null && !IsDoorFailed(_cachedDoorOnPath) && !_cachedDoorOnPath.TargetState)
                    return _cachedDoorOnPath!;

                if (_cachedDoorOnPath == null)
                    return null!;
            }

            Vector3 toWaypoint = waypoint - transform.position;
            float distanceToWaypoint = toWaypoint.magnitude;
            Vector3 directionToWaypoint = distanceToWaypoint > 0.001f ? toWaypoint / distanceToWaypoint : Vector3.forward;

            DoorVariant? raycastDoor = FindDoorByRaycast(transform.position, directionToWaypoint, distanceToWaypoint);
            if (raycastDoor != null && !IsDoorFailed(raycastDoor))
            {
                _cachedDoorWaypointIndex = _currentWaypointIndex;
                _cachedDoorWaypointPos = waypoint;
                _cachedDoorOnPath = raycastDoor;
                return raycastDoor;
            }

            if (_bot?.BotDetectionRadius == null)
            {
                _cachedDoorWaypointIndex = _currentWaypointIndex;
                _cachedDoorWaypointPos = waypoint;
                _cachedDoorOnPath = null;
                return null!;
            }

            DoorVariant closestDoor = null!;
            float closestDistanceSq = float.MaxValue;
            float waypointSq = distanceToWaypoint * distanceToWaypoint;

            foreach (DoorVariant door in _bot.BotDetectionRadius.DoorsInRange)
            {
                if (door == null || IsDoorFailed(door))
                    continue;

                Vector3 toDoor = door.transform.position - transform.position;
                float distanceToDoorSq = toDoor.sqrMagnitude;
                if (distanceToDoorSq >= waypointSq)
                    continue;

                Vector3 directionToDoor = toDoor.sqrMagnitude > 0.001f ? toDoor.normalized : Vector3.forward;

                if (Vector3.Dot(directionToWaypoint, directionToDoor) > 0.5f && distanceToDoorSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceToDoorSq;
                    closestDoor = door;
                }
            }

            _cachedDoorWaypointIndex = _currentWaypointIndex;
            _cachedDoorWaypointPos = waypoint;
            _cachedDoorOnPath = closestDoor;
            return closestDoor!;
        }

        private DoorVariant? FindDoorByRaycast(Vector3 origin, Vector3 direction, float maxDistance)
        {
            maxDistance = Mathf.Min(maxDistance, 8f);
            if (maxDistance <= 0.2f)
                return null;

            int hitCount = Physics.RaycastNonAlloc(origin + Vector3.up * 0.5f, direction, _raycastBuffer, maxDistance, ObstacleMask, QueryTriggerInteraction.Collide);
            if (hitCount == 0)
                return null;

            Array.Sort(_raycastBuffer, 0, hitCount, _hitDistanceComparer);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastBuffer[i];
                if (hit.collider == null)
                    continue;

                if (hit.collider.GetComponentInParent<DoorVariant>() is { } door && !door.TargetState)
                    return door;
            }

            return null;
        }
        #endregion

        #region Door Interaction
        private void InteractWithDoor(DoorVariant door)
        {
            if (door == null || door.TargetState || _waitingForDoor)
                return;

            if (door.gameObject == null)
            {
                _waitingForDoor = false;
                _currentDoor = null!;
                return;
            }

            if (IsDoorFailed(door))
            {
                _doorFailCounts.TryGetValue(door, out int failCount);
                LogManager.Debug($"Door at {door.transform.position} has failed {failCount} times, rerouting around it.");
                CalculatePath();
                return;
            }

            if ((transform.position - door.transform.position).sqrMagnitude > DoorInteractionDistanceSq)
            {
                MoveTowardsTarget(door.transform.position);
                return;
            }

            if (player == null || !Door.Get(door).CanInteract)
            {
                RecordDoorFailure(door);
                CalculatePath();
                return;
            }

            if (door.PermissionsPolicy.RequiredPermissions != DoorPermissionFlags.None && player.Team != Team.SCPs)
            {
                Item? foundItem = null;
                foreach (Item item in player.Items)
                {
                    if (item is KeycardItem kc && item.Base is IDoorPermissionProvider keycardProvider && door is IDoorPermissionRequester permissions && permissions.PermissionsPolicy.CheckPermissions(keycardProvider.GetPermissions(permissions)))
                    {
                        foundItem = item;
                        break;
                    }
                }
                if (foundItem == null)
                {
                    LogManager.Debug($"{player.DisplayName} has no keycard for door, rerouting.");
                    RecordDoorFailure(door);
                    CalculatePath();
                    return;
                }

                KeycardItem keycard = (foundItem as KeycardItem)!;
                LogManager.Debug($"{player.DisplayName} - {player.PlayerId} - Found {foundItem.Type}, Card Permissions: {keycard.Permissions}, Door Permissions:  {Door.Get(door).Permissions}");
                player.CurrentItem = foundItem;
            }

            _waitingForDoor = true;
            _doorWaitTimer = DoorWaitTime;
            _lastFailedDoor = null!;

            Timing.CallDelayed(Timing.WaitForOneFrame, () =>
            {
                if (door == null || door.TargetState || !_waitingForDoor)
                    return;

                if (door.AllowInteracting(_hub, 0))
                {
                    door.ServerInteract(_hub, 0);
                    _currentDoor = door;
                }
            });
        }

        private void HandleDoorWaiting()
        {
            if (_currentDoor == null)
                return;

            _doorWaitTimer -= Time.deltaTime;

            bool doorDestroyed = _currentDoor == null || _currentDoor.gameObject == null;
            bool doorOpened = !doorDestroyed && _currentDoor!.TargetState;

            if (doorDestroyed || doorOpened || _doorWaitTimer <= 0f)
            {
                if (!doorOpened && _currentDoor != null && !doorDestroyed)
                {
                    LogManager.Debug($"Door at {_currentDoor.transform.position} did not open in time, marking as failed and rerouting.");
                    RecordDoorFailure(_currentDoor);
                    _waitingForDoor = false;
                    _currentDoor = null!;
                    CalculatePath();
                    return;
                }

                _waitingForDoor = false;
                _currentDoor = null!;
            }
        }

        private void RecordDoorFailure(DoorVariant door)
        {
            if (door == null)
                return;

            _doorFailCounts.TryGetValue(door, out int count);
            _doorFailCounts[door] = count + 1;
            _lastFailedDoor = door;
        }

        private bool IsDoorFailed(DoorVariant door)
        {
            if (door == null)
                return false;

            return _doorFailCounts.TryGetValue(door, out int count) && count >= MaxDoorRetryAttempts;
        }
        #endregion

        #region Stuck Detection and Recovery
        private void CheckIfStuck()
        {
            if (_isAttemptingUnstuck)
                return;

            if (!NavMeshManager.IsBaked)
                return;

            if (_waitingForDoor || _waitingForElevator || _waitingToEnterElevator || _insideElevator || _walkingIntoElevator || _approachingElevatorPanel)
                return;

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.isOnOffMeshLink)
                return;

            if (_isNavigating && _waypoints.Count > 0 && _currentWaypointIndex < _waypoints.Count)
            {
                DoorVariant doorAhead = FindDoorOnPath(_waypoints[_currentWaypointIndex]);
                if (doorAhead != null && !doorAhead.TargetState)
                    return;
            }
            else if (_waypoints.Count == 0)
            {
                if (_isNavigating)
                {
                    _stuckTimer += Time.deltaTime;
                    if (_stuckTimer > StuckTimeLimit)
                    {
                        _isAttemptingUnstuck = true;
                        _stuckTimer = 0f;
                    }

                    _lastPosition = transform.position;
                }

                return;
            }

            Vector3 displacement = transform.position - _lastPosition;
            float progress = displacement.magnitude;

            if (_isNavigating && _currentWaypointIndex < _waypoints.Count)
            {
                Vector3 toWaypoint = _waypoints[_currentWaypointIndex] - transform.position;
                toWaypoint.y = 0f;
                if (toWaypoint.sqrMagnitude > 0.01f)
                {
                    toWaypoint.Normalize();
                    progress = Vector3.Dot(displacement, toWaypoint);
                }
            }

            bool closeToWaypoint = _isNavigating && _currentWaypointIndex < _waypoints.Count && Vector3.Distance(transform.position, _waypoints[_currentWaypointIndex]) <= WaypointReachedDistance;
            if (_isNavigating && !closeToWaypoint && progress < StuckThreshold)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer > StuckTimeLimit)
                {
                    _isAttemptingUnstuck = true;
                    _stuckTimer = 0f;
                }
            }
            else
                _stuckTimer = 0f;

            _lastPosition = transform.position;
        }

        private int _stuckRecoveryAttempts = 0;
        private const int AttemptsBeforeCarving = 2;
        private GameObject? _tempStuckObstacle;
        private const float StuckObstacleRadius = 0.6f;
        private const float StuckObstacleDuration = 10f;

        private void HandleUnstuck()
        {
            _isAttemptingUnstuck = false;
            _stuckTimer = 0f;
            if (!NavMeshManager.IsBaked)
            {
                LogManager.Debug($"{player.DisplayName} unstuck requested but NavMesh not baked yet, waiting.");
                _stuckRecoveryAttempts = 0;
                return;
            }
            _stuckRecoveryAttempts++;

            if (_stuckRecoveryAttempts <= 2)
            {
                LogManager.Warn($"{player.DisplayName} stuck (attempt {_stuckRecoveryAttempts}) at {transform.position} - {player.Room?.GameObject.name}, recovering.");
            }
            else
                LogManager.Debug($"{player.DisplayName} stuck (attempt {_stuckRecoveryAttempts}) at {transform.position} - {player.Room?.GameObject.name}, recovering.");

            Vector3 basePos = transform.position;
            float radius = _stuckRecoveryAttempts > 2 ? 6f : 3f;
            Vector3 side = Vector3.Cross(Vector3.up, transform.forward);
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.right;

            side.y = 0f;
            side.Normalize();

            Vector3 back = -transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f)
                back = Vector3.back;

            back.Normalize();

            List<Vector3> candidates =
            [
                basePos + back * 1.5f + Vector3.up * 0.5f,
                basePos + side * 1.5f + Vector3.up * 0.5f,
                basePos - side * 1.5f + Vector3.up * 0.5f
            ];

            if (_stuckRecoveryAttempts > 1)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 rnd = UnityEngine.Random.insideUnitCircle * radius;
                    Vector3 rPos = basePos + new Vector3(rnd.x, 0.5f, rnd.y);
                    candidates.Add(rPos);
                }
            }

            bool teleported = false;
            for (int ci = 0; ci < candidates.Count; ci++)
            {
                Vector3 candidate = candidates[ci];
                Vector3 navCandidate = candidate;
                if (NavMeshManager.IsBaked && NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshManager.AgentInitSampleDistance, GetNavMeshAreaMask()))
                {
                    navCandidate = hit.position;
                    if (Mathf.Abs(navCandidate.y - transform.position.y) > MaxHeightSnapDistance + 2f)
                        continue;
                }

                bool isRandom = ci >= 3;
                if (!isRandom && !IsSafePosition(navCandidate))
                    continue;

                if (isRandom && _stuckRecoveryAttempts <= 2 && !IsSafePosition(navCandidate))
                    continue;

                Vector3 safe = ProjectToNavMesh(navCandidate, 5f);
                if (safe != navCandidate && Vector3.Distance(safe, navCandidate) > 1f)
                    safe = navCandidate;
                    
                player.Position = new Vector3(safe.x, safe.y + 0.1f, safe.z);
                if (_agent != null && _agent.enabled)
                {
                    _agent.Warp(player.Position);
                } 
                else
                    _agentHelper?.transform.position = player.Position;

                teleported = true;
                break;
            }
            if (!teleported && _stuckRecoveryAttempts > 3)
            {
                Room rr = RoomExtensions.GetRandomRoom();
                if (rr != null)
                {
                    Vector3 dest = _roomQuery.GetRoomDestination(rr, transform.position);
                    if (dest != Vector3.zero)
                    {
                        player.Position = dest + Vector3.up * 0.5f;
                        if (_agent != null && _agent.enabled)
                        {
                            _agent.Warp(player.Position);
                        }
                    }
                }
            }

            if (_stuckRecoveryAttempts >= AttemptsBeforeCarving)
            {
                PlaceTemporaryNavObstacle(basePos);
                _stuckRecoveryAttempts = 0;
            }

            if (_currentTargetRoom != null)
                CalculatePath();
        }

        private void PlaceTemporaryNavObstacle(Vector3 position)
        {
            if (_tempStuckObstacle != null)
                Destroy(_tempStuckObstacle);

            _tempStuckObstacle = new GameObject("TempStuckObstacle");
            _tempStuckObstacle.transform.position = position;

            NavMeshObstacle obstacle = _tempStuckObstacle.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = StuckObstacleRadius;
            obstacle.height = 2f;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            LogManager.Debug($"Carved temporary navmesh obstacle at {position} for {StuckObstacleDuration}s.");

            GameObject captured = _tempStuckObstacle;
            Timing.CallDelayed(StuckObstacleDuration, () =>
            {
                if (captured != null)
                {
                    Destroy(captured);
                    if (_tempStuckObstacle == captured)
                        _tempStuckObstacle = null;
                }
            });
        }

        private void OnDestroy()
        {
            if (_tempStuckObstacle != null)
            {
                Destroy(_tempStuckObstacle);
                _tempStuckObstacle = null;
            }

            if (_agentHelper != null)
            {
                Destroy(_agentHelper);
                _agentHelper = null!;
                _agent = null!;
            }
        }

        private bool IsSafePosition(Vector3 position)
        {
            bool grounded = false;
            int groundHits = Physics.RaycastNonAlloc(position + Vector3.up * 2f, Vector3.down, _raycastBuffer, 6f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < groundHits; i++)
            {
                RaycastHit hit = _raycastBuffer[i];
                if (hit.collider.isTrigger || hit.collider.transform.root == transform.root)
                    continue;

                if (IsPlayerCollider(hit.collider))
                    continue;

                grounded = true;
                break;
            }

            if (!grounded)
                return false;

            int overlapCount = Physics.OverlapSphereNonAlloc(position + Vector3.up * 1.1f, 0.35f, _overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider collider = _overlapBuffer[i];
                if (collider.isTrigger || collider.transform.root == transform.root)
                    continue;

                if (IsPlayerCollider(collider))
                    continue;

                return false;
            }

            return true;
        }
        #endregion

        #region Patrol System
        public void SetPatrolRoute(List<RoomName> rooms)
        {
            _patrolRooms.Clear();
            _patrolRooms.AddRange(rooms);
            _enablePatrolMode = true;
            _currentPatrolIndex = 0;

            if (_patrolRooms.Count > 0)
            {
                foreach (Room firstRoom in Room.Get(_patrolRooms[0]))
                {
                    SetDestination(firstRoom);
                    break;
                }
            }
        }

        private void SetupDefaultPatrolRoute()
        {
            List<RoomName> defaultRooms =
            [
                RoomName.LczClassDSpawn,
                RoomName.Lcz914,
                RoomName.LczCheckpointA,
                RoomName.Hcz049,
                RoomName.HczCheckpointA
            ];

            SetPatrolRoute(defaultRooms);
        }

        private void HandlePatrolMode()
        {
            if (_patrolRooms.Count == 0)
                return;

            if (_roomWaitTimer > 0f)
            {
                _roomWaitTimer -= Time.deltaTime;
                return;
            }

            _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolRooms.Count;
            foreach (Room nextRoom in Room.Get(_patrolRooms[_currentPatrolIndex]))
            {
                SetDestination(nextRoom);
                break;
            }
        }

        public void StartPatrol()
        {
            _enablePatrolMode = true;
            if (_patrolRooms.Count > 0)
            {
                foreach (Room firstRoom in Room.Get(_patrolRooms[_currentPatrolIndex]))
                {
                    SetDestination(firstRoom);
                    break;
                }
            }
        }

        public void StopPatrol()
        {
            _enablePatrolMode = false;
            _isNavigating = false;
        }

        public void AddPatrolRoom(RoomName roomName)
        {
            if (!_patrolRooms.Contains(roomName))
                _patrolRooms.Add(roomName);
        }
        #endregion

        #region Elevator System
        private bool NeedsElevatorTravel(Room currentRoom, Room targetRoom)
        {
            if (currentRoom == null || targetRoom == null)
                return false;

            int currentCluster = GetWalkCluster(currentRoom.Zone);
            int targetCluster = GetWalkCluster(targetRoom.Zone);

            if (currentCluster < 0 || targetCluster < 0)
                return false;

            return currentCluster != targetCluster;
        }

        private bool TryHandleElevatorIfNeeded(Room currentRoom)
        {
            if (currentRoom == null || _currentTargetRoom == null)
                return false;

            if (currentRoom.Name == RoomName.Hcz049 && _currentTargetRoom.Name != RoomName.Hcz049)
            {
                if (TryHandleScp049ElevatorExit(currentRoom))
                    return true;
            }

            if (!NeedsElevatorTravel(currentRoom, _currentTargetRoom))
                return false;

            ElevatorPanel panel = FindNearbyElevatorPanel(currentRoom.Zone, _currentTargetRoom.Zone, currentRoom);
            if (panel == null)
                return false;

            LogManager.Debug($"Elevator travel needed from {currentRoom.Zone} to {_currentTargetRoom.Zone}");
            HandleCheckpointElevatorLogic(currentRoom);
            return true;
        }

        private bool TryHandleScp049ElevatorExit(Room currentRoom)
        {
            if (currentRoom == null || currentRoom.Name != RoomName.Hcz049)
                return false;

            if (!ElevatorChamber.TryGetChamber(ElevatorGroup.Scp049, out ElevatorChamber chamber) || chamber == null)
            {
                LogManager.Warn("Could not find the SCP049 elevator chamber while trying to leave the 049 room");
                return false;
            }

            if (_insideElevator || _waitingForElevator || _waitingToEnterElevator || _walkingIntoElevator)
                return true;

            Vector3 elevatorPad = currentRoom.WorldPosition(new Vector3(-4.64f, 1f, 20f));

            if (Vector3.Distance(transform.position, elevatorPad) > ElevatorEnterDistance)
            {
                if (BuildNavMeshPath(elevatorPad))
                {
                    _approachingElevatorPanel = true;
                    LogManager.Debug($"Bot in the SCP049 room has a navmesh path to the elevator pad");
                }
                else
                    LogManager.Warn($"Failed to build navmesh path to SCP-049 elevator pad at {elevatorPad}");

                return true;
            }

            _approachingElevatorPanel = false;
            _currentElevator = chamber;

            int currentLevel = GetNearestElevatorLevel(chamber, transform.position);
            if (currentLevel < 0)
                currentLevel = GetElevatorLevelForZone(chamber, currentRoom.Zone);

            int targetLevel;
            if (currentLevel >= 0 && chamber.FloorDoors != null && chamber.FloorDoors.Count > 1)
            {
                targetLevel = (currentLevel + 1) % chamber.FloorDoors.Count;
            }
            else
                targetLevel = GetElevatorTargetLevel(chamber, currentRoom.Zone);

            if (currentLevel < 0 || targetLevel < 0 || currentLevel == targetLevel)
            {
                LogManager.Warn($"Could not determine levels for the SCP049 elevator (current={currentLevel}, target={targetLevel})");
                CalculatePath();
                return true;
            }

            _targetElevatorLevel = targetLevel;

            if (chamber.DestinationLevel != currentLevel || !chamber.IsReady)
            {
                LogManager.Debug($"Calling the SCP049 elevator to level {currentLevel}");
                _waitingForElevator = true;
                _elevatorWaitTimer = ElevatorWaitTimeout;
                chamber.ServerSetDestination(currentLevel, false);
            }
            else
            {
                LogManager.Debug("SCP049 elevator already at the 049 room level, entering");
                EnterElevator(chamber);
            }

            return true;
        }

        private void HandleCheckpointElevatorLogic(Room currentRoom)
        {
            if (currentRoom == null || _currentTargetRoom == null)
                return;

            LogManager.Debug($"HandleCheckpointElevatorLogic called for room: {currentRoom.Name}");

            ElevatorPanel elevatorPanel = FindNearbyElevatorPanel(currentRoom.Zone, _currentTargetRoom.Zone, currentRoom);
            if (elevatorPanel == null)
            {
                LogManager.Warn($"No elevator panel found near checkpoint {currentRoom.Name}");
                CalculatePath();
                return;
            }

            LogManager.Debug($"Found elevator panel near {currentRoom.Name}");

            ElevatorChamber chamber = elevatorPanel.AssignedChamber;
            if (chamber == null)
            {
                LogManager.Warn("Elevator panel has no assigned chamber");
                CalculatePath();
                return;
            }

            if (IsKeycardElevatorChamber(chamber) && player.Team != Team.SCPs && !HasElevatorKeycardAccess(chamber))
            {
                LogManager.Debug($"Bot lacks the keycard permission ({GetRequiredElevatorPermission(chamber)}) required for elevator {chamber.AssignedGroup}, skipping elevator");
                _approachingElevatorPanel = false;
                return;
            }

            float distanceToPanel = Vector3.Distance(transform.position, elevatorPanel.transform.position);
            if (distanceToPanel > ElevatorEnterDistance)
            {
                Vector3 panelTarget = elevatorPanel.transform.position + Vector3.up * 0.5f;
                if (TrySnapToNavMesh(panelTarget, out Vector3 snappedPanelTarget))
                    panelTarget = snappedPanelTarget;

                _waypoints.Clear();
                _currentWaypointIndex = 0;
                _waypoints.Add(panelTarget);
                _approachingElevatorPanel = true;
                return;
            }

            _approachingElevatorPanel = false;
            _currentElevatorPanel = elevatorPanel;

            FacilityZone currentZone = currentRoom.Zone;
            int currentLevel = GetElevatorLevelForZone(chamber, currentZone);
            int targetLevel = GetElevatorTargetLevel(chamber, currentZone);

            if (currentLevel < 0 || targetLevel < 0 || currentLevel == targetLevel)
            {
                LogManager.Warn($"Could not determine elevator levels for zone {currentZone} (current={currentLevel}, target={targetLevel})");
                CalculatePath();
                return;
            }

            _targetElevatorLevel = targetLevel;
            _currentElevator = chamber;

            LogManager.Debug($"Bot at checkpoint in {currentZone}, target zone: {_currentTargetRoom.Zone}, current level: {currentLevel}, target level: {targetLevel}");
            LogManager.Debug($"Elevator current destination: {chamber.DestinationLevel}, is ready: {chamber.IsReady}");

            if (chamber.DestinationLevel != currentLevel || !chamber.IsReady)
            {
                LogManager.Debug($"Calling elevator to current level {currentLevel}");
                _waitingForElevator = true;
                _elevatorWaitTimer = ElevatorWaitTimeout;
                chamber.ServerSetDestination(currentLevel, false);
            }
            else
            {
                LogManager.Debug($"Elevator already at current level {currentLevel}, entering elevator");
                EnterElevator(chamber);
            }
        }

        private ElevatorPanel FindNearbyElevatorPanel(FacilityZone currentZone, FacilityZone targetZone, Room currentRoom)
        {
            ElevatorPanel closestPanel = null!;
            float closestDistance = float.MaxValue;

            foreach (ElevatorPanel panel in ElevatorPanel.AllPanels)
            {
                if (panel == null || panel.AssignedChamber == null)
                    continue;

                if (!IsElevatorUseful(panel.AssignedChamber, currentZone, targetZone, currentRoom))
                    continue;

                if (IsKeycardElevatorChamber(panel.AssignedChamber) && player.Team != Team.SCPs && !HasElevatorKeycardAccess(panel.AssignedChamber))
                    continue;

                float distance = Vector3.Distance(transform.position, panel.transform.position);
                if (distance <= ElevatorDetectionRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPanel = panel;
                }
            }

            return closestPanel!;
        }

        private static int GetZoneRank(FacilityZone zone) => zone switch
        {
            FacilityZone.LightContainment => 0,
            FacilityZone.HeavyContainment => 1,
            FacilityZone.Entrance => 2,
            FacilityZone.Surface => 3,
            _ => -1
        };

        private static int GetWalkCluster(FacilityZone zone) => zone switch
        {
            FacilityZone.LightContainment => 0,
            FacilityZone.HeavyContainment => 1,
            FacilityZone.Entrance => 1,
            FacilityZone.Surface => 2,
            _ => -1
        };

        private static bool IsZonePairBlocked(FacilityZone a, FacilityZone b)
        {
            if (a == b)
                return false;

            if (a == FacilityZone.Other || b == FacilityZone.Other)
                return false;

            int clusterA = GetWalkCluster(a);
            int clusterB = GetWalkCluster(b);

            if (clusterA < 0 || clusterB < 0)
                return false;

            return false;
        }

        private Room? PickAlternativeDestination(FacilityZone currentZone)
        {
            List<string> blacklist = Plugin.Instance.Config.BlacklistedRooms;
            List<Room> candidates = [];
            List<Room> fallback = [];

            foreach (Room room in Room.List)
            {
                if (room == null || blacklist.Contains(room.GameObject.name))
                    continue;

                if (room == _currentTargetRoom || IsZonePairBlocked(currentZone, room.Zone))
                    continue;

                bool isUnnamedOrOther = room.Name == RoomName.Unnamed || room.Zone == FacilityZone.Other;
                if (isUnnamedOrOther)
                {
                    fallback.Add(room);
                }
                else
                    candidates.Add(room);
            }

            List<Room> pool = candidates.Count > 0 ? candidates : fallback;
            if (pool.Count == 0)
                return null;

            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        private static FacilityZone GetElevatorDoorZone(Interactables.Interobjects.ElevatorDoor door)
        {
            if (door == null || door.Rooms == null || door.Rooms.Length == 0)
                return FacilityZone.Other;

            return door.Rooms[0]?.Zone ?? FacilityZone.Other;
        }

        private int GetElevatorLevelForZone(ElevatorChamber chamber, FacilityZone zone)
        {
            if (chamber == null)
                return -1;

            int i = 0;
            foreach (Interactables.Interobjects.ElevatorDoor door in chamber.FloorDoors)
            {
                if (GetElevatorDoorZone(door) == zone)
                    return i;
                i++;
            }

            return -1;
        }

        private int GetElevatorTargetLevel(ElevatorChamber chamber, FacilityZone currentZone)
        {
            int currentLevel = GetElevatorLevelForZone(chamber, currentZone);
            if (currentLevel < 0)
                return -1;

            int i = 0;
            foreach (Interactables.Interobjects.ElevatorDoor _ in chamber.FloorDoors)
            {
                if (i != currentLevel)
                    return i;

                i++;
            }

            return -1;
        }

        private int GetNearestElevatorLevel(ElevatorChamber chamber, Vector3 position)
        {
            if (chamber == null || chamber.FloorDoors == null || chamber.FloorDoors.Count == 0)
                return -1;

            int bestIndex = -1;
            float bestDist = float.MaxValue;
            int i = 0;
            foreach (Interactables.Interobjects.ElevatorDoor door in chamber.FloorDoors)
            {
                if (door == null)
                {
                    i++;
                    continue;
                }

                Vector3 doorPos;
                try
                {
                    doorPos = door.TargetPosition;
                }
                catch
                {
                    doorPos = door.transform.position;
                }

                float dist = Vector3.Distance(position, doorPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                   
                    bestIndex = i;
                }
                i++;
            }

            return bestIndex;
        }

        private bool IsElevatorUseful(ElevatorChamber chamber, FacilityZone currentZone, FacilityZone targetZone, Room currentRoom)
        {
            if (currentRoom.Name == RoomName.Hcz049)
                return true;

            if (chamber == null)
                return false;

            int currentRank = GetZoneRank(currentZone);
            int targetRank = GetZoneRank(targetZone);
            if (currentRank < 0 || targetRank < 0)
                return false;

            int lowerRank = int.MaxValue;
            int upperRank = int.MinValue;
            foreach (Interactables.Interobjects.ElevatorDoor door in chamber.FloorDoors)
            {
                int rank = GetZoneRank(GetElevatorDoorZone(door));
                if (rank < 0)
                    continue;

                lowerRank = Mathf.Min(lowerRank, rank);
                upperRank = Mathf.Max(upperRank, rank);
            }

            if (lowerRank == int.MaxValue || lowerRank == upperRank)
                return false;

            if (currentRank == lowerRank)
                return targetRank > currentRank;

            if (currentRank == upperRank)
                return targetRank < currentRank;

            return false;
        }

        private Room GetSurfaceGateRoom()
        {
            Room closestRoom = null!;
            float closestDistance = float.MaxValue;

            foreach (ElevatorChamber chamber in ElevatorChamber.AllChambers)
            {
                if (chamber == null)
                    continue;

                switch (chamber.AssignedGroup)
                {
                    case ElevatorGroup.GateA01 or ElevatorGroup.GateA02 or ElevatorGroup.GateB:
                        break;

                    default:
                        continue;
                }

                foreach (Interactables.Interobjects.ElevatorDoor door in chamber.FloorDoors)
                {
                    if (door?.Rooms == null)
                        continue;

                    foreach (RoomIdentifier roomIdentifier in door.Rooms)
                    {
                        if (roomIdentifier == null || roomIdentifier.Zone != FacilityZone.Surface)
                            continue;

                        Room room = Room.Get(roomIdentifier);
                        if (room == null)
                            continue;

                        float distance = Vector3.Distance(transform.position, room.Position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestRoom = room;
                        }
                    }
                }
            }

            return closestRoom!;
        }

        private void EnterElevator(ElevatorChamber chamber)
        {
            if (chamber == null)
                return;

            Vector3 elevatorCenter = chamber.transform.position + Vector3.up;
            float distanceToCenter = Vector3.Distance(transform.position, elevatorCenter);

            if (distanceToCenter <= ElevatorEnterDistance)
            {
                _elevatorRideOffset = elevatorCenter - chamber.transform.position;
                _insideElevator = true;
                _waitingToEnterElevator = false;
                _waitingForElevator = true;
                _elevatorWaitTimer = ElevatorWaitTimeout;

                LogManager.Debug($"Bot is already inside elevator chamber, sending to target level {_targetElevatorLevel}");
                Timing.CallDelayed(0.1f, () =>
                {
                    chamber.ServerSetDestination(_targetElevatorLevel, false);
                });
                return;
            }

            LogManager.Debug($"Bot is {distanceToCenter:F1}m from elevator center, starting walkin phase");
            _walkingIntoElevator = true;
            _walkIntoChamber = chamber;
            _walkIntoTimer = WalkIntoElevatorTimeout;
        }

        private void HandleElevatorWaiting()
        {
            _elevatorWaitTimer -= Time.deltaTime;

            if (_currentElevator == null)
            {
                if (_elevatorWaitTimer <= 0f)
                {
                    LogManager.Warn("Elevator wait timeout, recalculating path");
                    ResetElevatorStates();
                    CalculatePath();
                }

                return;
            }

            ElevatorChamber chamber = _currentElevator;
            FacilityZone currentZone = player.Zone;
            int currentLevel = GetElevatorLevelForZone(chamber, currentZone);

            if (currentLevel >= 0 && chamber.DestinationLevel == currentLevel && chamber.IsReady)
            {
                LogManager.Debug($"Elevator arrived at current level {currentLevel}, entering elevator");
                _waitingForElevator = false;
                EnterElevator(chamber);
                return;
            }

            if (_elevatorWaitTimer <= 0f)
            {
                LogManager.Warn("Elevator wait timeout, recalculating path");
                ResetElevatorStates();
                CalculatePath();
            }
        }

        private void HandleElevatorEntry()
        {
            if (!_waitingToEnterElevator || _currentElevatorPanel?.AssignedChamber == null)
                return;

            ElevatorChamber chamber = _currentElevatorPanel.AssignedChamber;
            float distanceToElevator = Vector3.Distance(transform.position, _elevatorEntryPosition);

            if (distanceToElevator <= ElevatorEnterDistance)
            {
                _waitingToEnterElevator = false;
                _insideElevator = true;

                LogManager.Debug($"Bot entered elevator, calling to target level {_targetElevatorLevel}");
                chamber.ServerSetDestination(_targetElevatorLevel, false);

                _waitingForElevator = true;
                _elevatorWaitTimer = ElevatorWaitTimeout;
            }
            else if (_waypoints.Count > 0 && _isNavigating)
            {
                NavigateToWaypoint();
            }
        }

        private void HandleWalkingIntoElevator()
        {
            if (!_walkingIntoElevator || _walkIntoChamber == null)
            {
                _walkingIntoElevator = false;
                return;
            }

            ElevatorChamber chamber = _walkIntoChamber;
            Vector3 elevatorCenter = chamber.transform.position + Vector3.up;
            float distanceToCenter = Vector3.Distance(transform.position, elevatorCenter);

            if (distanceToCenter <= ElevatorEnterDistance)
            {
                LogManager.Debug($"Bot reached elevator center, starting ride to level {_targetElevatorLevel}");
                _walkingIntoElevator = false;
                _walkIntoChamber = null!;

                _elevatorRideOffset = elevatorCenter - chamber.transform.position;
                _insideElevator = true;
                _waitingForElevator = true;
                _elevatorWaitTimer = ElevatorWaitTimeout;

                Timing.CallDelayed(0.1f, () =>
                {
                    chamber.ServerSetDestination(_targetElevatorLevel, false);
                });
                return;
            }

            _walkIntoTimer -= Time.deltaTime;
            if (_walkIntoTimer <= 0f)
            {
                LogManager.Warn($"Walk into elevator timed out, forcing entry");
                _walkingIntoElevator = false;
                _walkIntoChamber = null!;

                _elevatorRideOffset = elevatorCenter - chamber.transform.position;
                _insideElevator = true;
                _waitingForElevator = true;
                _elevatorWaitTimer = ElevatorWaitTimeout;

                Timing.CallDelayed(0.1f, () =>
                {
                    chamber.ServerSetDestination(_targetElevatorLevel, false);
                });
                return;
            }

            MoveTowards(elevatorCenter, _speed, lookAtTarget: false);
        }

        private void HandleElevatorTravel()
        {
            if (!_insideElevator || _currentElevator == null)
                return;

            _elevatorWaitTimer -= Time.deltaTime;

            ElevatorChamber chamber = _currentElevator;
            Vector3 ridePosition = chamber.transform.position + _elevatorRideOffset;
            _fpcRole?.FpcModule.Motor.ReceivedPosition = new RelativePosition(ridePosition);

            _elevatorCheckTimer += Time.deltaTime;
            if (_elevatorCheckTimer >= 1f)
            {
                _elevatorCheckTimer = 0f;
                if (Vector3.Distance(transform.position, ridePosition) > 3f)
                {
                    LogManager.Debug("Bot drifted out of the elevator chamber, correcting position");
                    player.Position = ridePosition;
                }
            }

            if (chamber.DestinationLevel == _targetElevatorLevel && chamber.IsReady)
            {
                LogManager.Debug($"Elevator reached target level {_targetElevatorLevel}, exiting elevator");

                ResetElevatorStates();
                _waypoints.Clear();
                _currentWaypointIndex = 0;

                if (_currentTargetRoom != null)
                {
                    LogManager.Debug($"Continuing navigation to {_currentTargetRoom.Name} after elevator travel");
                    Timing.CallDelayed(0.5f, () => CalculatePath());
                }
                else if (_enablePatrolMode)
                {
                    _roomWaitTimer = _waitTimeAtRoom;
                }

                return;
            }

            if (_elevatorWaitTimer <= 0f)
            {
                LogManager.Warn("Elevator travel timeout, recalculating path");
                ResetElevatorStates();
                CalculatePath();
            }
        }

        private void ResetElevatorStates()
        {
            _waitingForElevator = false;
            _waitingToEnterElevator = false;
            _insideElevator = false;
            _approachingElevatorPanel = false;
            _walkingIntoElevator = false;
            _walkIntoChamber = null!;
            _walkIntoTimer = 0f;
            _currentElevator = null!;
            _currentElevatorPanel = null!;
            _targetElevatorLevel = -1;
            _elevatorRideOffset = Vector3.zero;
            _elevatorWaitTimer = 0f;
            _elevatorCheckTimer = 0f;
            _elevatorEntryPosition = default;
        }
        #endregion

        #region Visualization
        private void CreatePathVisualization()
        {
            if (!_enablePathVisualization || _waypoints.Count == 0)
                return;

            DrawableLines.IsDebugModeEnabled = Plugin.Instance.Config.Debug;

            int completedCount = _currentWaypointIndex;
            int remainingCount = _waypoints.Count - completedCount;

            if (completedCount >= 2)
            {
                Vector3[] completed = new Vector3[completedCount];
                for (int i = 0; i < completedCount; i++)
                    completed[i] = _waypoints[i];

                DrawableLines.ServerGenerateLine(PathVisualizationDuration, _completedWaypointColor, completed);
            }

            if (remainingCount >= 2)
            {
                Vector3[] remaining = new Vector3[remainingCount];
                for (int i = 0; i < remainingCount; i++)
                    remaining[i] = _waypoints[completedCount + i];

                DrawableLines.ServerGenerateLine(PathVisualizationDuration, _waypointColor, remaining);
            }

            if (remainingCount > 0)
            {
                Vector3 waypoint = _waypoints[completedCount];
                Vector3 forward = remainingCount > 1 ? (_waypoints[completedCount + 1] - waypoint).normalized : Vector3.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
                DrawableLines.ServerGenerateLine(PathVisualizationDuration, _currentWaypointColor, waypoint - side * 0.4f, waypoint + side * 0.4f);
                DrawableLines.ServerGenerateLine(PathVisualizationDuration, _currentWaypointColor, waypoint - forward * 0.4f, waypoint + forward * 0.4f);
            }
        }

        private void UpdateWaypointVisualization()
        {
            if (!_enablePathVisualization)
                return;

            _pathVisualizationTimer = Mathf.Max(_pathVisualizationTimer, PathVisualizationRedrawInterval - 0.1f);
        }

        public void TogglePathVisualization(bool enable)
        {
            _enablePathVisualization = enable;
            if (enable)
                CreatePathVisualization();
        }

        public void UpdateVisualizationSettings(Color waypointColor, Color currentColor, Color completedColor, float scale)
        {
            _waypointColor = waypointColor;
            _currentWaypointColor = currentColor;
            _completedWaypointColor = completedColor;

            if (_enablePathVisualization && _waypoints.Count > 0)
                CreatePathVisualization();
        }
        #endregion

        #region Main Update Loop
        private void Update()
        {
            if (_hub == null)
            {
                Destroy(this);
                return;
            }

            if (_hub.roleManager.CurrentRole is IFpcRole curRole)
            {
                _fpcRole = curRole;
            }
            else
                _fpcRole = null;

            if (_fpcRole == null)
            {
                if (_agent != null && _agent.enabled)
                {
                    _agent.enabled = false;
                    _useAgentSteering = false;
                }

                return;
            }

            UpdateZoneSafetyStatus();

            if (transform.position.y > 3000f)
                return;

            if (transform.position.y < -500f)
            {
                LogManager.Warn($"{player.DisplayName} falling at {transform.position}, emergency rescue.");
                Room rescueRoom = RoomExtensions.GetRandomRoom();
                if (rescueRoom == null)
                {
                    foreach (Room r in Room.List)
                    {
                        rescueRoom = r;
                        break;
                    }
                }

                if (rescueRoom != null)
                {
                    Vector3 dest = _roomQuery.GetRoomDestination(rescueRoom, transform.position);
                    if (dest == Vector3.zero || (dest - transform.position).sqrMagnitude < 25f)
                    {
                        if (!NavMesh.SamplePosition(rescueRoom.Position + Vector3.up, out NavMeshHit hr, 20f, GetNavMeshAreaMask()))
                        {
                            dest = rescueRoom.Position + Vector3.up;
                        }
                        else
                            dest = hr.position;
                    }
                    player.Position = dest + Vector3.up * 0.5f;
                    _stuckRecoveryAttempts = 0;
                }

                _stuckTimer = 0f;
                _isAttemptingUnstuck = false;
                if (_agent != null)
                {
                    _agent.Warp(player.Position);
                    _useAgentSteering = _agent.isOnNavMesh;
                }

                _agentHelper?.transform.position = player.Position;
                return;
            }

            if (_isAttemptingUnstuck)
            {
                HandleUnstuck();
                return;
            }

            CheckIfStuck();
            TryEnableAgentIfNeeded();

            if (NavMeshManager.IsBaked && _navMeshRetryCount >= MaxNavMeshRetries / 2 && !_isNavigating && _currentTargetRoom != null && _pathFailCooldown <= 0f)
            {
                _navMeshRetryCount = 0;
                _isNavigating = true;
                LogManager.Debug($"{player.DisplayName} retrying path after NavMesh baked.");
                CalculatePath();
                return;
            }
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.isOnOffMeshLink)
            {
                _agent.isStopped = true;

                if (!_insideElevator && !_waitingForElevator && !_walkingIntoElevator)
                {
                    Room cr = player.CachedRoom!;
                    if (cr != null)
                        TryHandleElevatorIfNeeded(cr);
                }
            }
            else if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }

            if (_waitingToEnterElevator)
            {
                HandleElevatorEntry();
                return;
            }

            if (_walkingIntoElevator)
            {
                HandleWalkingIntoElevator();
                return;
            }

            if (_insideElevator)
            {
                HandleElevatorTravel();
                return;
            }

            if (_waitingForElevator)
            {
                HandleElevatorWaiting();
                return;
            }

            if (_waitingForDoor)
            {
                HandleDoorWaiting();
                return;
            }

            _elevatorCheckTimer -= Time.deltaTime;
            Room currentRoom = player.CachedRoom!;
            if (_isNavigating && _elevatorCheckTimer <= 0f && currentRoom != null)
            {
                _elevatorCheckTimer = 0.5f;
                if (TryHandleElevatorIfNeeded(currentRoom))
                    return;
            }

            if (_isNavigating && _waypoints.Count > 0)
            {
                NavigateToWaypoint();
            }
            else if (_enablePatrolMode)
            {
                HandlePatrolMode();
            }

            _squadShareTimer += Time.deltaTime;
            if (_squadShareTimer >= SquadShareInterval)
            {
                _squadShareTimer = 0f;
                if (_bot != null && _bot.IsInSquad && _bot.Player.IsAlive)
                {
                    if (IsSquadFollower)
                    {
                        if (!_waitingForDoor && !_insideElevator && !_walkingIntoElevator && !_waitingForElevator && !_waitingToEnterElevator && !_approachingElevatorPanel)
                        {
                            if (_isNavigating || SquadManager.TryGetSquadDestination(_bot, out _))
                                TryAdoptSquadWaypoints();
                        }
                    }
                    else if (IsSquadLeader && _isNavigating && _waypoints.Count > 0)
                    {
                        TryPropagateSquadWaypoints();
                    }
                }
            }

            _pathRecalculateTimer += Time.deltaTime;
            float jitteredInterval = PathRecalculateTime + (player.PlayerId % 5 - 2);
            if (_pathRecalculateTimer >= jitteredInterval)
            {
                _pathRecalculateTimer = 0f;
                if (_isNavigating && !_approachingElevatorPanel && _currentTargetRoom != null && _currentTargetRoom != player.CachedRoom)
                    CalculatePath();
            }

            if (_pathFailCooldown > 0f)
                _pathFailCooldown -= Time.deltaTime;

            _pathVisualizationTimer += Time.deltaTime;
            if (_enablePathVisualization && _pathVisualizationTimer >= PathVisualizationRedrawInterval)
            {
                _pathVisualizationTimer = 0f;
                if (_waypoints.Count > 0)
                    CreatePathVisualization();
            }
        }

        #endregion

        #region Properties
        public bool IsNavigating => _isNavigating;
        public bool IsRepathBlocked => _pathFailCooldown > 0f;
        public bool IsWaitingForDoor => _waitingForDoor;
        public bool IsWaitingForElevator => _waitingForElevator;
        public bool IsUsingElevator => _usingElevator;
        public bool IsLczDecontaminated => _isLczDecontaminated;
        public bool IsFacilityNuked => _isFacilityNuked;
        public Room CurrentTarget => _currentTargetRoom;
        public List<Vector3> CurrentPath => _waypoints;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public bool IsWaitingToEnterElevator => _waitingToEnterElevator;
        public bool IsWalkingIntoElevator => _walkingIntoElevator;
        public bool IsInsideElevatorChamber => _insideElevator;
        public ElevatorPanel CurrentElevatorPanel => _currentElevatorPanel;
        #endregion
    }
}