using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using LightContainmentZoneDecontamination;
using MapGeneration;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UncomplicatedCustomBots.Events.Handlers;
using UnityEngine;
using Utf8Json.Internal.DoubleConversion;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class Navigation : MonoBehaviour
    {
        #region Constants
        public const float DefaultSpeed = 15f;
        public const float DefaultStoppingDistance = 1f;
        public const float DoorInteractionDistance = 2.3f;
        public const float WaypointReachedDistance = 1f;
        public const float DoorWaitTime = 2f;
        public const float PathRecalculateTime = 15f;
        public const float ElevatorInteractionDistance = 10f;
        public const float ElevatorWaitTime = 10f;
        private const float StuckThreshold = 0.1f;
        private const float StuckTimeLimit = 3f;
        #endregion

        #region Core Fields
        private ReferenceHub _hub;
        private IFpcRole _fpcRole;
        internal float _speed = DefaultSpeed;
        private float _stoppingDistance = DefaultStoppingDistance;
        private Room _currentTargetRoom;
        private readonly List<Vector3> _waypoints = new();
        private int _currentWaypointIndex = 0;
        private bool _isNavigating = false;
        private bool _waitingForDoor = false;
        private DoorVariant _currentDoor;
        private float _doorWaitTimer = 0f;
        private Door _initialClassDDoor = null;
        internal bool _enablePatrolMode = false;
        private readonly List<RoomName> _patrolRooms = new();
        private int _currentPatrolIndex = 0;
        private float _waitTimeAtRoom = 3f;
        private float _roomWaitTimer = 0f;
        private float _pathRecalculateTimer = 0f;
        private Vector3 _lastPosition;
        private float _stuckTimer = 0f;
        private bool _isAttemptingUnstuck = false;
        private bool _needsElevator = false;
        private bool _waitingForElevator = false;
        private bool _usingElevator = false;
        private ElevatorChamber _currentElevator;
        private float _elevatorWaitTimer = 0f;
        private readonly List<Room> _roomPath = new();
        private bool _enablePathVisualization = true;
        private readonly List<ClientSidePrimitive> _currentPathPrimitives = new();
        private Color _waypointColor = Color.green;
        private Color _currentWaypointColor = Color.red;
        private Color _completedWaypointColor = Color.gray;
        private float _primitiveScale = 0.5f;
        private float _waypointVariationRadius = 2.5f;
        private float _minDistanceFromWalls = 1.2f;
        private bool _enableWaypointVariation = true;
        private bool _waitingAtTesla = false;
        private bool _isLczDecontaminated = false;
        private bool _isFacilityNuked = false;
        #endregion

        #region Room Specific Waypoints
        private static readonly Dictionary<string, List<Vector3>> RoomSpecificWaypoints = new()
        {
            ["HCZ_Straight_PipeRoom(Clone)"] =
            [
                new Vector3(-1.89f, 1f, -5.54f),
                new Vector3(2.96f, 1f, -6.19f)
            ],
            ["LCZ_ChkpB(Clone)"] =
            [
                new Vector3(5.34f, 1f, 0.12f),
                new Vector3(14.84f, 1f, 0.12f)
            ],
            ["LCZ_ChkpA(Clone)"] =
            [
                new Vector3(5.34f, 1f, 0.12f),
                new Vector3(14.84f, 1f, 0.12f)
            ],
            ["HCZ_Testroom(Clone)"] =
            [
                new Vector3(6.53f, 1f, 5.48f),
                new Vector3(0f, 1f, 5.93f),
                new Vector3(-6.53f, 1f, 5.48f)
            ],
            ["HCZ_127(Clone)"] =
            [
                new Vector3(-5f, 1f, 0f)
            ],
            ["HCZ_Crossroom_Water(Clone)"] =
            [
                new Vector3(2.26f, 1f, 2.48f)
            ],
            ["HCZ_Nuke(Clone)"] =
            [
                new Vector3(-3.14f, 1f, -0.12f)
            ],
            ["HCZ_TArmory(Clone)"] =
            [
                new Vector3(-2.58f, 1f, 0f)
            ],
            ["HCZ_939(Clone)"] =
            [
                new Vector3(2.04f, 1f, -0.45f)
            ],
            ["LCZ_330(Clone)"] =
            [
                new Vector3(-4.50f, 1f, 0f)
            ],
            ["LCZ_173(Clone)"] =
            [
                new Vector3(-4.28f, 1f, 0f)
            ],
        };
        #endregion
        #region Initialization
        public void Init(float speed = DefaultSpeed, bool enablePatrol = false, bool enableVisualization = true, bool enableVariation = true, float variationRadius = 2.5f)
        {
            _hub = GetComponent<ReferenceHub>();
            _fpcRole = _hub.roleManager.CurrentRole as IFpcRole;
            _speed = speed;
            _enablePatrolMode = enablePatrol;
            _enablePathVisualization = enableVisualization;
            _enableWaypointVariation = enableVariation;
            _waypointVariationRadius = variationRadius;
            _lastPosition = transform.position;

            if (_enablePatrolMode && _patrolRooms.Count == 0)
                SetupDefaultPatrolRoute();
        }
        #endregion

        #region Public Navigation Methods
        public void SetDestination(Room targetRoom)
        {
            if (targetRoom == null)
                return;

            _currentTargetRoom = targetRoom;
            _isNavigating = true;
            ResetNavigationState();
            CalculatePath();
        }

        public void StopNavigation()
        {
            _isNavigating = false;
            ResetNavigationState();
            _waypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;
            _isAttemptingUnstuck = false;
        }

        private void ResetNavigationState()
        {
            _waitingForDoor = false;
            _waitingForElevator = false;
            _usingElevator = false;
            _needsElevator = false;
            _currentWaypointIndex = 0;
        }
        #endregion

        #region Pathfinding

        private bool IsRoomInDangerousZone(Room room)
        {
            FacilityZone roomZone = room.Zone;

            if (_isLczDecontaminated && roomZone == FacilityZone.LightContainment)
            {
                LogManager.Debug($"Blocking access to LCZ room {room.Name} - Zone is decontaminated");
                return true;
            }

            if (_isFacilityNuked && roomZone != FacilityZone.Surface)
            {
                LogManager.Debug($"Blocking access to {room.Name} - Facility is nuked");
                return true;
            }

            return false;
        }

        private void UpdateZoneSafetyStatus()
        {
            DecontaminationController decontaminationController = DecontaminationController.Singleton;
            if (decontaminationController != null)
            {
                bool wasDecontaminated = _isLczDecontaminated;
                _isLczDecontaminated = decontaminationController.IsDecontaminating;

                if (!wasDecontaminated && _isLczDecontaminated)
                    HandleZoneCompromised(FacilityZone.LightContainment);
            }

            AlphaWarheadController warheadController = AlphaWarheadController.Singleton;
            if (warheadController != null)
            {
                bool wasNuked = _isFacilityNuked;
                _isFacilityNuked = warheadController.AlreadyDetonated;

                if (!wasNuked && _isFacilityNuked)
                    HandleZoneCompromised(FacilityZone.Surface);
            }
        }

        private void HandleZoneCompromised(FacilityZone compromisedZone)
        {
            if (_currentTargetRoom != null && _currentTargetRoom.Zone == compromisedZone)
            {
                LogManager.Warn($"Current target room {_currentTargetRoom.Name} is in compromised zone {compromisedZone}. Stopping navigation.");
                StopNavigation();

                if (_enablePatrolMode)
                {
                    FindSafePatrolDestination();
                }
            }

            Room currentRoom = Player.Get(_hub).CachedRoom;
            if (currentRoom != null && currentRoom.Zone == compromisedZone)
            {
                LogManager.Warn($"Bot is in compromised zone {compromisedZone}. Attempting evacuation.");
                EvacuateFromZone(compromisedZone);
            }
        }

        private void EvacuateFromZone(FacilityZone dangerousZone)
        {
            Room currentRoom = Player.Get(_hub).CachedRoom;
            if (currentRoom == null)
                return;

            Room safeRoom = Room.List.Where(r => r.Zone != dangerousZone && !IsRoomInDangerousZone(r)).OrderBy(r => Vector3.Distance(currentRoom.Position, r.Position)).FirstOrDefault();

            if (safeRoom != null)
            {
                LogManager.Info($"Evacuating to safe room: {safeRoom.Name} in {safeRoom.Zone}");
                SetDestination(safeRoom);
            }
            else
            {
                LogManager.Error("No safe evacuation room found!");
                StopNavigation();
            }
        }


        private void FindSafePatrolDestination()
        {
            var safePatrolRooms = _patrolRooms.Select(roomName => Room.Get(roomName).FirstOrDefault()).Where(room => room != null && !IsRoomInDangerousZone(room)).ToList();

            if (safePatrolRooms.Any())
            {
                Room safeRoom = safePatrolRooms.OrderBy(r => UnityEngine.Random.value).FirstOrDefault();
                LogManager.Info($"Continuing patrol to safe room: {safeRoom.Name}");
                SetDestination(safeRoom);
            }
            else
            {
                LogManager.Warn("No safe patrol rooms available. Stopping patrol.");
                StopPatrol();

                Room anySafeRoom = Room.List.Where(r => !IsRoomInDangerousZone(r)).OrderBy(r => UnityEngine.Random.value).FirstOrDefault();

                if (anySafeRoom != null)
                {
                    SetDestination(anySafeRoom);
                }
            }
        }

        private List<Vector3> GetRoomSpecificWaypoints(Room room, bool useRandomWaypoint = false)
        {
            List<Vector3> waypoints = [];
            
            if (RoomSpecificWaypoints.TryGetValue(room.GameObject.name, out List<Vector3> localWaypoints))
            {
                if (useRandomWaypoint && localWaypoints.Count > 0)
                {
                    Vector3 randomLocal = localWaypoints[Random.Range(0, localWaypoints.Count)];
                    waypoints.Add(room.WorldPosition(randomLocal));
                }
                else
                {
                    foreach (Vector3 localPos in localWaypoints)
                        waypoints.Add(room.WorldPosition(localPos));
                }
            }
            
            return waypoints;
        }

        private bool HasRoomSpecificWaypoints(Room room)
        {
            if (RoomSpecificWaypoints.ContainsKey(room.GameObject.name))
                return true;

            return false;
        }

        private Vector3 GetOptimalRoomWaypoint(Room room, Vector3 currentPosition, Room nextRoom = null)
        {
            List<Vector3> specificWaypoints = GetRoomSpecificWaypoints(room);

            if (specificWaypoints.Count == 0)
                return GetVariedRoomWaypoint(room.Position + Vector3.up, room);

            if (specificWaypoints.Count == 1)
                return specificWaypoints[0];

            Vector3 bestWaypoint = specificWaypoints[0];
            float bestScore = float.MaxValue;

            foreach (Vector3 waypoint in specificWaypoints)
            {
                float score = Vector3.Distance(currentPosition, waypoint);

                if (nextRoom != null)
                {
                    float distanceToNext = Vector3.Distance(waypoint, nextRoom.Position);
                    score += distanceToNext * 0.5f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestWaypoint = waypoint;
                }
            }

            return bestWaypoint;
        }

        private void CalculatePath()
        {
            if (_currentTargetRoom == null)
                return;

            Room currentRoom = Player.Get(_hub).CachedRoom;
            if (currentRoom == null || Player.Get(_hub).Team == Team.Dead)
            {
                LogManager.Warn("Cannot calculate path: Current room is null, Sending bot to spawn location");
                return;
            }

            ClearPathVisualization();

            _roomPath.Clear();
            List<Room> foundPath = Room.FindPath(currentRoom, _currentTargetRoom, CalculateRoomWeight);
            if (foundPath != null)
                _roomPath.AddRange(foundPath);

            BuildWaypointPath(currentRoom);

            if (_enablePathVisualization)
                CreatePathVisualization();
        }

        private void BuildWaypointPath(Room currentRoom)
        {
            _waypoints.Clear();
            _currentWaypointIndex = 0;

            if (HandleClassDInitialDoor(currentRoom))
                return;

            if (_roomPath.Count == 0)
            {
                Room randomRoom = RoomExtensions.GetRandomRoom();
                SetDestination(randomRoom);
                LogManager.Debug($"No path found from {currentRoom.Name} to {_currentTargetRoom.Name}, Selecting random room {randomRoom.Name} - {randomRoom.GameObject.name}");
                return;
            }

            for (int i = 0; i < _roomPath.Count; i++)
            {
                Room room = _roomPath[i];
                ProcessRoomWaypoints(room, i);
            }
        }

        private bool HandleClassDInitialDoor(Room currentRoom)
        {
            if (_hub.roleManager.CurrentRole.RoleTypeId != RoleTypeId.ClassD || _initialClassDDoor != null)
                return false;

            Door exitDoor = currentRoom.Doors.OrderBy(d => Vector3.Distance(_hub.transform.position, d.Position)).FirstOrDefault();
            if (exitDoor != null)
            {
                _initialClassDDoor = exitDoor;
                Vector3 dirOut = (exitDoor.Position - currentRoom.Position).normalized;
                Vector3 doorFront = exitDoor.Position + dirOut * 1.5f;
                _waypoints.Add(doorFront);
                LogManager.Debug($"Class-D initial waypoint: door at {doorFront}");
                return true;
            }
            return false;
        }

        private void ProcessRoomWaypoints(Room room, int roomIndex)
        {
            if (HasRoomSpecificWaypoints(room))
            {
                Room nextRoom = roomIndex + 1 < _roomPath.Count ? _roomPath[roomIndex + 1] : null;
                Vector3 optimalWaypoint = GetOptimalRoomWaypoint(room, transform.position, nextRoom);
                
                if (nextRoom != null)
                {
                    Door connectingDoor = FindConnectingDoor(room, nextRoom);
                    if (connectingDoor != null)
                    {
                        _waypoints.Add(optimalWaypoint);
                        
                        Vector3 doorPosition = connectingDoor.Position;
                        Vector3 directionToNextRoom = (nextRoom.Position - doorPosition).normalized;
                        Vector3 offset = directionToNextRoom * 1.5f;
                        
                        _waypoints.Add(doorPosition - offset + Vector3.up * 1f);
                        _waypoints.Add(doorPosition + offset + Vector3.up * 1f);
                        return;
                    }
                }
                
                _waypoints.Add(optimalWaypoint);
            }
            else
            {
                Vector3 roomCenter = GetVariedRoomWaypoint(room.Position + Vector3.up, room);
                
                if (roomIndex + 1 < _roomPath.Count)
                {
                    Room nextRoom = _roomPath[roomIndex + 1];
                    Door connectingDoor = FindConnectingDoor(room, nextRoom);
                    
                    if (connectingDoor != null)
                    {
                        AddRoomAndDoorWaypoints(room, roomCenter, connectingDoor, nextRoom);
                        return;
                    }
                }
                
                _waypoints.Add(roomCenter);
            }
        }

        private Door FindConnectingDoor(Room room, Room nextRoom) => room.Doors.Where(d => nextRoom.Doors.Contains(d)).FirstOrDefault(d => _hub.roleManager.CurrentRole.RoleTypeId != RoleTypeId.ClassD || d != _initialClassDDoor);

        private void AddRoomAndDoorWaypoints(Room room, Vector3 roomCenter, Door connectingDoor, Room nextRoom)
        {
            Vector3 doorPosition = connectingDoor.Position;
            
            if (!RoomSpecificWaypoints.ContainsKey(room.GameObject.name))
                _waypoints.Add(roomCenter);

            Vector3 directionToNextRoom = (nextRoom.Position - doorPosition).normalized;
            Vector3 offset = directionToNextRoom * 1.5f;

            _waypoints.Add(doorPosition - offset);
            _waypoints.Add(doorPosition + offset);
        }

        private Vector3 GetVariedRoomWaypoint(Vector3 roomCenter, Room room)
        {
            if (!_enableWaypointVariation)
                return roomCenter;

            float randomAngle = Random.Range(0f, 2f * Mathf.PI);
            float randomDistance = Random.Range(0f, _waypointVariationRadius);

            Vector3 randomOffset = new(Mathf.Cos(randomAngle) * randomDistance, 0f, Mathf.Sin(randomAngle) * randomDistance);
            
            return ClampPositionToRoomBounds(roomCenter + randomOffset, room, _minDistanceFromWalls);
        }

        private Vector3 ClampPositionToRoomBounds(Vector3 position, Room room, float margin)
        {
            Bounds roomBounds = room.GameObject.GetComponent<MeshCollider>()?.bounds ?? new Bounds(room.Position, Vector3.one * 10f);

            float maxMargin = Mathf.Min(roomBounds.size.x, roomBounds.size.z) * 0.5f;
            margin = Mathf.Min(margin, maxMargin);

            Vector3 minBounds = roomBounds.min + Vector3.one * margin;
            Vector3 maxBounds = roomBounds.max - Vector3.one * margin;

            return new Vector3(Mathf.Clamp(position.x, minBounds.x, maxBounds.x), position.y, Mathf.Clamp(position.z, minBounds.z, maxBounds.z));
        }

        private int CalculateRoomWeight(Room room)
        {
            if (room.GameObject != null && Plugin.Instance.Config.BlacklistedRooms.Contains(room.GameObject.name))
                return int.MaxValue;

            if (IsRoomInDangerousZone(room))
                return int.MaxValue;

            int weight = 1;

            foreach (Door door in room.Doors)
            {
                if (!door.IsOpened && door.Permissions != DoorPermissionFlags.None)
                    weight += 5;
            }

            return weight;
        }
        #endregion

        #region Movement and Navigation
        private void NavigateToWaypoint()
        {
            if (_currentWaypointIndex >= _waypoints.Count)
            {
                _isNavigating = false;
                if (_enablePatrolMode)
                    _roomWaitTimer = _waitTimeAtRoom;
                return;
            }

            Vector3 currentWaypoint = _waypoints[_currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);

            if (distanceToWaypoint <= WaypointReachedDistance)
            {
                _currentWaypointIndex++;
                UpdateWaypointVisualization();
                return;
            }

            DoorVariant blockingDoor = FindBlockingDoor(currentWaypoint);
            if (blockingDoor != null && !blockingDoor.IsConsideredOpen())
            {
                InteractWithDoor(blockingDoor);
                return;
            }

            MoveTowardsTarget(currentWaypoint);
        }

        private void MoveTowardsTarget(Vector3 target)
        {
            if (_fpcRole?.FpcModule?.Motor == null)
                return;

            Vector3 currentPosition = transform.position;
            Vector3 direction = (target - currentPosition).normalized;

            float distanceToTarget = Vector3.Distance(currentPosition, target);
            if (distanceToTarget <= _stoppingDistance)
                return;

            Vector3 movement = Time.deltaTime * _speed * direction;
            if (movement.magnitude > distanceToTarget)
                movement = direction * distanceToTarget;

            _fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(currentPosition + movement);
            _fpcRole.FpcModule.MouseLook.LookAtDirection(direction);
        }

        private DoorVariant FindBlockingDoor(Vector3 waypoint)
        {
            Room currentRoom = Player.Get(_hub).CachedRoom;
            if (currentRoom == null)
                return null;

            DoorVariant closestDoor = null;
            float closestDistance = float.MaxValue;
            Vector3 directionToWaypoint = (waypoint - transform.position).normalized;

            foreach (Door door in currentRoom.Doors)
            {
                if (door.IsOpened) continue;

                float distanceToDoor = Vector3.Distance(transform.position, door.Position);
                Vector3 directionToDoor = (door.Position - transform.position).normalized;

                if (Vector3.Dot(directionToWaypoint, directionToDoor) > 0.7f && 
                    distanceToDoor < DoorInteractionDistance &&
                    distanceToDoor < closestDistance)
                {
                    closestDistance = distanceToDoor;
                    closestDoor = door.Base;
                }
            }
            return closestDoor;
        }
        #endregion

        #region Door Interaction
        private void InteractWithDoor(DoorVariant door)
        {
            if (door == null || door.IsConsideredOpen())
                return;

            float distanceToDoor = Vector3.Distance(transform.position, door.transform.position);

            if (distanceToDoor <= DoorInteractionDistance)
            {
                Player player = Player.Get(_hub);
                if (player != null && Door.Get(door).CanInteract)
                {
                    Item item = player.Items.Where(item => item is KeycardItem keycard && item.Base is IDoorPermissionProvider keycardProvider && door is IDoorPermissionRequester permissions && permissions.PermissionsPolicy.CheckPermissions(keycardProvider.GetPermissions(permissions))).FirstOrDefault();
                    player.CurrentItem = item;
                    if (door.AllowInteracting(_hub, 0))
                    {
                        door.ServerInteract(_hub, 0);
                        _currentDoor = door;
                        _waitingForDoor = true;
                        _doorWaitTimer = DoorWaitTime;
                    }
                }
                else
                {
                    CalculatePath();
                }
            }
            else
            {
                MoveTowardsTarget(door.transform.position);
            }
        }

        private void HandleDoorWaiting()
        {
            _doorWaitTimer -= Time.deltaTime;

            if (_doorWaitTimer <= 0f || (_currentDoor != null && _currentDoor.IsConsideredOpen()))
            {
                _waitingForDoor = false;
                _currentDoor = null;
            }
        }
        #endregion

        #region Stuck Detection and Recovery
        private void CheckIfStuck()
        {
            if (_isAttemptingUnstuck)
                return;

            float distanceMoved = Vector3.Distance(transform.position, _lastPosition);

            if (_isNavigating && distanceMoved < StuckThreshold)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer > StuckTimeLimit)
                {
                    _isAttemptingUnstuck = true;
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }

            _lastPosition = transform.position;
        }

        private void HandleUnstuck()
        {
            if (_currentWaypointIndex < _waypoints.Count)
            {
                Player player = Player.Get(_hub);
                player.Position = _waypoints[_currentWaypointIndex] + Vector3.up * 0.5f;

                _currentWaypointIndex++;
                UpdateWaypointVisualization();
            }

            _isAttemptingUnstuck = false;
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
                Room firstRoom = Room.Get(_patrolRooms[0]).FirstOrDefault();
                if (firstRoom != null)
                    SetDestination(firstRoom);
            }
        }

        private void SetupDefaultPatrolRoute()
        {
            var defaultRooms = new List<RoomName>
            {
                RoomName.LczClassDSpawn,
                RoomName.Lcz914,
                RoomName.LczCheckpointA,
                RoomName.Hcz049,
                RoomName.HczCheckpointA
            };
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
            Room nextRoom = Room.Get(_patrolRooms[_currentPatrolIndex]).FirstOrDefault();

            if (nextRoom != null)
                SetDestination(nextRoom);
        }

        public void StartPatrol()
        {
            _enablePatrolMode = true;
            if (_patrolRooms.Count > 0)
            {
                Room firstRoom = Room.Get(_patrolRooms[_currentPatrolIndex]).FirstOrDefault();
                if (firstRoom != null)
                    SetDestination(firstRoom);
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
        private void RequestElevator()
        {
            Collider[] colliders = Physics.OverlapSphere(_hub.GetPosition(), ElevatorInteractionDistance);
            foreach (Collider collider in colliders)
            {
                if (collider.TryGetComponent<ElevatorChamber>(out var chamber))
                {
                    _waitingForElevator = true;
                    chamber.ServerSetDestination(chamber.NextLevel, false);
                    return;
                }
            }
        }
        private void HandleElevatorWaiting()
        {
            _elevatorWaitTimer -= Time.deltaTime;

            if (_currentElevator != null)
            {
                FacilityZone currentZone = Player.Get(_hub).Zone;
                if (currentZone == Player.Get(_hub).Zone)
                {
                    _waitingForElevator = false;
                    _usingElevator = true;
                    _needsElevator = false;
                    CalculatePath();
                    return;
                }
            }

            if (_elevatorWaitTimer <= 0f)
            {
                _waitingForElevator = false;
                _needsElevator = false;
                _currentElevator = null;
                CalculatePath();
            }
        }
        #endregion

        #region Visualization
        private void CreatePathVisualization()
        {
            if (!Plugin.Instance.Config.Debug)
                return;
                
            for (int i = 0; i < _waypoints.Count; i++)
            {
                ClientSidePrimitive primitive = CreateWaypointPrimitive(_waypoints[i], i);
                if (primitive != null)
                    _currentPathPrimitives.Add(primitive);
            }
        }

        private ClientSidePrimitive CreateWaypointPrimitive(Vector3 position, int waypointIndex)
        {
            PrimitiveObjectToy primitive = PrimitiveObjectToy.Create();
            primitive.Base.name = $"Waypoint_{waypointIndex}";
            primitive.Position = position;
            primitive.Scale = Vector3.one * _primitiveScale;
            
            Color color = waypointIndex == _currentWaypointIndex ? _currentWaypointColor :  waypointIndex < _currentWaypointIndex ? _completedWaypointColor : _waypointColor;
            
            primitive.Base.gameObject.AddComponent<WaypointMarker>().Initialize(30f, waypointIndex);
            primitive.Color = color;
            primitive.Flags = AdminToys.PrimitiveFlags.Visible;

            ClientSidePrimitive clientPrimitive = new(primitive);
            clientPrimitive.SpawnForEveryone();

            return clientPrimitive;
        }
        
        private void UpdateWaypointVisualization()
        {
            if (!_enablePathVisualization || _currentPathPrimitives.Count == 0)
                return;

            for (int i = 0; i < _currentPathPrimitives.Count; i++)
            {
                ClientSidePrimitive primitive = _currentPathPrimitives[i];
                primitive.DestroyForEveryone();
            }
        }

        private void ClearPathVisualization()
        {
            foreach (ClientSidePrimitive primitive in _currentPathPrimitives)
                primitive.DestroyForEveryone();

            _currentPathPrimitives.Clear();
        }

        public void TogglePathVisualization(bool enable)
        {
            _enablePathVisualization = enable;
            if (!enable)
                ClearPathVisualization();
            else if (_waypoints.Count > 0)
                CreatePathVisualization();
        }

        public void UpdateVisualizationSettings(Color waypointColor, Color currentColor, Color completedColor, float scale)
        {
            _waypointColor = waypointColor;
            _currentWaypointColor = currentColor;
            _completedWaypointColor = completedColor;
            _primitiveScale = scale;
            
            if (_enablePathVisualization && _waypoints.Count > 0)
            {
                ClearPathVisualization();
                CreatePathVisualization();
            }
        }
        #endregion

        #region Main Update Loop
        private void Update()
        {
            if (!NetworkServer.active || _hub == null)
            {
                Destroy(this);
                return;
            }

            if (_fpcRole == null)
                _fpcRole = _hub.roleManager.CurrentRole as IFpcRole;

            if (_fpcRole == null)
            {
                Destroy(this);
                return;
            }

            UpdateZoneSafetyStatus();

            if (_isAttemptingUnstuck)
            {
                HandleUnstuck();
                return;
            }

            CheckIfStuck();
            RequestElevator();

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

            if (_isNavigating && _waypoints.Count > 0)
            {
                NavigateToWaypoint();
            }
            else if (_enablePatrolMode)
            {
                HandlePatrolMode();
            }

            _pathRecalculateTimer += Time.deltaTime;
            if (_pathRecalculateTimer >= PathRecalculateTime)
            {
                _pathRecalculateTimer = 0f;
                if (_isNavigating && _currentTargetRoom != null && _currentTargetRoom != Player.Get(_hub).CachedRoom)
                    CalculatePath();
            }
        }
        #endregion

        #region Utility Methods
        public void ToggleWaypointVariation(bool enable, float radius = 2.5f, float wallMargin = 1.2f)
        {
            _enableWaypointVariation = enable;
            _waypointVariationRadius = radius;
            _minDistanceFromWalls = wallMargin;
            
            if (_isNavigating && _currentTargetRoom != null)
                CalculatePath();
        }

        private void OnDestroy()
        {
            ClearPathVisualization();
        }
        #endregion

        #region Properties
        public bool IsNavigating => _isNavigating;
        public bool IsWaitingForDoor => _waitingForDoor;
        public bool IsWaitingForElevator => _waitingForElevator;
        public bool IsWaitingAtTesla => _waitingAtTesla;
        public bool IsUsingElevator => _usingElevator;
        public bool IsLczDecontaminated => _isLczDecontaminated;
        public bool IsFacilityNuked => _isFacilityNuked;
        public Room CurrentTarget => _currentTargetRoom;
        public List<Vector3> CurrentPath => _waypoints;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        #endregion
    }
}