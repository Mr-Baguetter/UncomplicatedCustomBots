using DrawableLine;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using MapGeneration.RoomConnectors;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UncomplicatedCustomBots.API.Struct;
using UncomplicatedCustomBots.API.YamlObjects;
using UnityEngine.Rendering;
using LabApi.Events.Handlers;

namespace UncomplicatedCustomBots.API.Managers
{
    public static class NavMeshManager
    {
        private readonly record struct PendingMeshRequest(MeshCollider Collider, string MeshName, int ExpectedVertexCount, Matrix4x4 Transform, int Area);

        public const int AgentTypeId = 0;
        public const float AgentRadius = 0.25f;
        public const float AgentHeight = 0.83f;
        public const float AgentClimb = 0.25f;
        public const float AgentSlope = 35f;
        public const float VoxelSize = 0.05f;
        public const int TileSize = 32;
        public const int DefaultArea = 0;
        public const int NotWalkableArea = 1;
        public const int DoorBlockedArea = 2;
        public const int DoorBlockedAreaMask = 1 << DoorBlockedArea;
        public const int WalkableAreaMask = 1 << DefaultArea;
        public const int WalkableWithDoorsMask = (1 << DefaultArea) | (1 << DoorBlockedArea);
        public const float SampleMaxDistance = 2f;
        public const float AgentInitSampleDistance = 7f;
        public const float StartSnapDistance = 5f;

        private static readonly string[] ClutterSourceNames = ["Broken Electrical Box Open Connector"];
        private static readonly string[] BakedLayers = ["Default", "InvisibleCollider", "Fence"];

        private const float DoorwayGapHalfWidth = 1.0f;
        private const float DoorframeBlockoutWidth = 1.6f;
        private const float DoorframeBlockoutHeight = 4f;
        private const float DoorframeBlockoutThickness = 0.4f;

        private static NavMeshDataInstance _navMeshInstance;
        private static NavMeshData? _navMeshData;
        private static bool _buildInProgress;

        public static bool IsBaked => _navMeshInstance.valid;

        private static readonly List<NavMeshData> _elevatorData = [];
        private static readonly List<NavMeshDataInstance> _elevatorInstances = [];
        private static readonly List<Transform> _elevatorTransforms = [];
        private static readonly List<Vector3> _elevatorLastPos = [];
        private static readonly List<Quaternion> _elevatorLastRot = [];
        private static bool _elevatorTrackingRegistered = false;
        private static bool _elevatorEventsRegistered = false;
        private static float _nextElevatorRebuildTime = 0f;

        public static List<CustomNavBlocker> CustomNavBlocker = [];
        public static List<CustomNavMesh> CustomNavMesh = [];

        private static readonly List<PrimitiveObjectToy> _customPrimitiveToys = [];

        public static void Init()
        {
            CustomNavBlocker.Clear();
            CustomNavMesh.Clear();

            YamlLoader.ParseYamlFiles<CustomNavBlocker>("navblockers", (yaml) =>
            {
                if (!string.IsNullOrWhiteSpace(yaml.ObjectName))
                    CustomNavBlocker.Add(yaml);
            });

            YamlLoader.ParseYamlFiles<CustomNavMesh>("navmesh", (yaml) =>
            {
                if (!string.IsNullOrWhiteSpace(yaml.ObjectName))
                    CustomNavMesh.Add(yaml);
            });

            BuildWithDelay(0.2f);
        }

        public static void BuildWithDelay(float delay = 1f) => Timing.CallDelayed(delay, () => Timing.RunCoroutine(BuildAsync()));

        public static IEnumerator<float> BuildAsync()
        {
            if (_buildInProgress)
            {
                LogManager.Warn("NavMesh: a bake is already in progress, ignoring request.");
                yield break;
            }

            _buildInProgress = true;
            Clear();

            List<NavMeshBuildSource> sources = [];
            NavMeshBuildSettings settings = default;
            Bounds bounds = default;
            NavMeshData? pendingData = null;
            AsyncOperation? asyncOp = null;

            try
            {
                int layerMask = LayerMask.GetMask(BakedLayers);
                NavMeshBuilder.CollectSources(null, layerMask, NavMeshCollectGeometry.PhysicsColliders, DefaultArea, [], sources);
                yield return Timing.WaitForOneFrame;

                List<PendingMeshRequest> pending = GatherUnreadableMeshRequests(layerMask);
                Task<List<(PendingMeshRequest request, DecodedMeshData data)>> decodeTask = Task.Run(() => DecodeRequests(pending));
                while (!decodeTask.IsCompleted)
                    yield return Timing.WaitForOneFrame;

                if (decodeTask.IsFaulted)
                {
                    LogManager.Error($"NavMesh: background mesh decode failed: {decodeTask.Exception}");
                }
                else
                {
                    int recovered = 0;
                    foreach ((PendingMeshRequest request, DecodedMeshData data) in decodeTask.Result)
                    {
                        Mesh runtimeMesh = new();
                        if (data.Use32BitIndices)
                            runtimeMesh.indexFormat = IndexFormat.UInt32;

                        runtimeMesh.SetVertices(data.Positions);
                        runtimeMesh.SetTriangles(data.Indices, 0);
                        runtimeMesh.RecalculateBounds();
                        runtimeMesh.name = request.MeshName;
                        runtimeMesh.UploadMeshData(false);

                        sources.Add(new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.Mesh,
                            sourceObject = runtimeMesh,
                            area = request.Area,
                            component = request.Collider,
                            transform = request.Transform
                        });

                        recovered++;
                        if (recovered % 25 == 0)
                            yield return Timing.WaitForOneFrame;
                    }

                    LogManager.Info($"NavMesh: unreadable mesh recovery done, recovered={recovered}, missing={pending.Count - recovered}.");
                    yield return Timing.WaitForOneFrame;
                }

                CarveDoorways(sources);
                yield return Timing.WaitForOneFrame;

                LogUnknownSources(sources);
                yield return Timing.WaitForOneFrame;

                ApplyCustomNavOverrides(sources);
                yield return Timing.WaitForOneFrame;

                for (int i = sources.Count - 1; i >= 0; i--)
                {
                    Component component = sources[i].component;
                    if (component == null)
                        continue;

                    if (component.GetComponentInParent<ElevatorChamber>() != null)
                    {
                        sources.RemoveAt(i);
                        continue;
                    }

                    DoorVariant doorComponent = component.GetComponentInParent<DoorVariant>();
                    if (doorComponent != null)
                    {
                        if (doorComponent is not Interactables.Interobjects.ElevatorDoor)
                        {
                            sources.RemoveAt(i);
                            continue;
                        }
                    }

                    if (CheckObject(component))
                    {
                        NavMeshBuildSource source = sources[i];
                        source.area = NotWalkableArea;
                        sources[i] = source;
                    }
                }
                yield return Timing.WaitForOneFrame;

                if (sources.Count == 0)
                {
                    LogManager.Warn("NavMesh: no sources were collected, bake aborted.");
                    yield break;
                }

                bounds = CalculateTotalBounds(sources);
                settings = GetBuildSettings();
                yield return Timing.WaitForOneFrame;

                float buildStart = Time.realtimeSinceStartup;
                pendingData = new NavMeshData(settings.agentTypeID)
                {
                    position = Vector3.zero,
                    rotation = Quaternion.identity
                };

                bool usedAsync = true;
                try
                {
                    asyncOp = NavMeshBuilder.UpdateNavMeshDataAsync(pendingData, settings, sources, bounds);
                }
                catch (Exception ex)
                {
                    LogManager.Error($"NavMesh: UpdateNavMeshDataAsync failed to start: {ex}");
                    asyncOp = null;
                    usedAsync = false;
                }

                if (asyncOp == null)
                {
                    LogManager.Warn("NavMesh: UpdateNavMeshDataAsync unavailable, falling back to synchronous bake (may hitch briefly).");
                    usedAsync = false;
                    NavMeshData? syncData = null;
                    try
                    {
                        syncData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"NavMesh: synchronous BuildNavMeshData failed: {ex}");
                    }
                    if (syncData == null)
                    {
                        LogManager.Warn("NavMesh: synchronous bake returned null, bake aborted.");
                        yield break;
                    }
                    pendingData = syncData;
                    _navMeshData = pendingData;
                    float syncElapsed = Time.realtimeSinceStartup - buildStart;
                    _navMeshInstance = NavMesh.AddNavMeshData(_navMeshData);
                    LogManager.Info($"NavMesh baked successfully (sync fallback): {sources.Count} sources, bounds {bounds.size}, valid={_navMeshInstance.valid}, elapsed={syncElapsed:F2}s");
                }
                else
                {
                    float lastLog = buildStart;
                    const float asyncTimeout = 30f;
                    while (!asyncOp.isDone)
                    {
                        if (Time.realtimeSinceStartup - buildStart > asyncTimeout)
                        {
                            LogManager.Warn($"NavMesh async bake timed out after {asyncTimeout}s (progress {asyncOp.progress:P0}), falling back to synchronous.");
                            NavMeshBuilder.Cancel(pendingData);
                            usedAsync = false;
                            NavMeshData? syncData = null;
                            try
                            {
                                syncData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                            }
                            catch (Exception ex)
                            {
                                LogManager.Error($"NavMesh: synchronous fallback failed: {ex}");
                            }
                            if (syncData == null)
                            {
                                LogManager.Warn("NavMesh: synchronous fallback returned null, bake aborted.");
                                yield break;
                            }
                            pendingData = syncData;
                            _navMeshData = pendingData;
                            float syncElapsed = Time.realtimeSinceStartup - buildStart;
                            _navMeshInstance = NavMesh.AddNavMeshData(_navMeshData);
                            LogManager.Info($"NavMesh baked successfully (sync fallback after timeout): {sources.Count} sources, bounds {bounds.size}, valid={_navMeshInstance.valid}, elapsed={syncElapsed:F2}s");
                            break;
                        }
                        if (Time.realtimeSinceStartup - lastLog > 2f)
                        {
                            LogManager.Debug($"NavMesh async baking... {asyncOp.progress * 100f:F0}%");
                            lastLog = Time.realtimeSinceStartup;
                        }
                        yield return Timing.WaitForOneFrame;
                    }

                    if (usedAsync)
                    {
                        _navMeshData = pendingData;
                        if (_navMeshData == null)
                        {
                            LogManager.Warn("NavMesh: async bake returned null data, bake aborted.");
                            yield break;
                        }

                        float elapsed = Time.realtimeSinceStartup - buildStart;
                        _navMeshInstance = NavMesh.AddNavMeshData(_navMeshData);
                        LogManager.Info($"NavMesh baked successfully (async): {sources.Count} sources, bounds {bounds.size}, valid={_navMeshInstance.valid}, elapsed={elapsed:F2}s");
                        if (!_navMeshInstance.valid)
                        {
                            LogManager.Warn("NavMesh: async bake produced invalid instance, data may be empty.");
                        }
                    }
                }

                try
                {
                    Features.NavigationSystem.ElevatorLinkRegistry.Build();
                    int linkCount = Features.NavigationSystem.ElevatorLinkRegistry.LinkCount;
                    LogManager.Info($"Elevator NavMeshLinks built: {linkCount} links (via NavMeshLinkData)");
                }
                catch (Exception ex)
                {
                    LogManager.Warn($"Failed to build elevator links: {ex.Message}");
                }

                try
                {
                    BuildElevatorMeshes();
                    RegisterElevatorEvents();
                }
                catch (Exception ex)
                {
                    LogManager.Warn($"Failed to build elevator dynamic navmeshes: {ex.Message}");
                }

                LogManager.Info("DoorObstacleRegistry: disabled (doors no longer carve NavMesh)");
            }
            finally
            {
                if (pendingData != null && _navMeshData != pendingData)
                {
                    if (asyncOp != null && !asyncOp.isDone)
                        NavMeshBuilder.Cancel(pendingData);

                    if (_navMeshData == null)
                        pendingData = null;
                }
                _buildInProgress = false;
            }
        }

        private static List<PendingMeshRequest> GatherUnreadableMeshRequests(int layerMask)
        {
            List<PendingMeshRequest> requests = [];

            foreach (MeshCollider collider in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (collider == null || collider.sharedMesh == null || collider.sharedMesh.isReadable)
                    continue;

                if (((1 << collider.gameObject.layer) & layerMask) == 0)
                    continue;

                if (collider.GetComponentInParent<DoorVariant>() != null)
                    continue;

                requests.Add(new PendingMeshRequest(collider, collider.sharedMesh.name, collider.sharedMesh.vertexCount, collider.transform.localToWorldMatrix, CheckObject(collider) ? NotWalkableArea : DefaultArea));
            }

            return requests;
        }

        private static List<(PendingMeshRequest request, DecodedMeshData data)> DecodeRequests(List<PendingMeshRequest> pending)
        {
            List<(PendingMeshRequest, DecodedMeshData)> results = [];

            foreach (PendingMeshRequest request in pending)
            {
                try
                {
                    if (RuntimeMeshReader.TryDecodeMeshRaw(request.MeshName, request.ExpectedVertexCount, out DecodedMeshData data))
                        results.Add((request, data));
                }
                catch (Exception exception)
                {
                    LogManager.Warn($"NavMesh: decode threw for '{request.MeshName}': {exception.GetType().Name}: {exception.Message}");
                }
            }

            return results;
        }

        public static void Clear()
        {
            if (_navMeshInstance.valid)
            {
                NavMesh.RemoveNavMeshData(_navMeshInstance);
                _navMeshInstance = default;
            }

            _navMeshData = null;
            Features.NavigationSystem.ElevatorLinkRegistry.Clear();
            ClearElevatorMeshes();
            ClearCustomPrimitives();
            UnregisterElevatorEvents();
        }

        public static void ClearCustom()
        {
            CustomNavMesh.Clear();
            CustomNavBlocker.Clear();
        }

        private static void ClearCustomPrimitives()
        {
            for (int i = _customPrimitiveToys.Count - 1; i >= 0; i--)
            {
                PrimitiveObjectToy toy = _customPrimitiveToys[i];
                try
                {
                    toy?.Destroy();
                }
                catch (Exception ex)
                {
                    LogManager.Debug($"NavMesh ClearCustomPrimitives: failed to destroy toy: {ex.Message}");
                }
            }
            _customPrimitiveToys.Clear();
        }

        private static PrimitiveObjectToy? SpawnCustomPrimitive(Vector3 position, Quaternion rotation, Vector3 scale, string sourceName, bool isBlocker, PrimitiveType type = PrimitiveType.Cube, bool visible = false, Color? color = null)
        {
            try
            {
                PrimitiveObjectToy toy = PrimitiveObjectToy.Create();
                toy.Position = position;
                toy.Rotation = rotation;
                toy.Scale = scale;
                toy.Type = type;
                AdminToys.PrimitiveFlags flags = AdminToys.PrimitiveFlags.Collidable;
                if (visible)
                    flags |= AdminToys.PrimitiveFlags.Visible;

                toy.Flags = flags;
                if (color.HasValue)
                    toy.Color = color.Value;

                toy.GameObject.name = $"{(isBlocker ? "CustomNavBlocker" : "CustomNavMesh")}_{sourceName}_{_customPrimitiveToys.Count}";
                toy.GameObject.layer = LayerMask.NameToLayer("Default");
                toy.Spawn();
                _customPrimitiveToys.Add(toy);
                LogManager.Debug($"NavMesh spawned {(isBlocker ? "blocker" : "walkable")} primitive '{toy.GameObject.name}' type {type} at {position} rot {rotation.eulerAngles} scale {scale} visible={visible}");
                return toy;
            }
            catch (Exception ex)
            {
                LogManager.Warn($"NavMesh failed to spawn {(isBlocker ? "blocker" : "walkable")} primitive for '{sourceName}': {ex.Message}");
                return null;
            }
        }

        private static PrimitiveObjectToy? SpawnCustomBlockerPrimitive(Vector3 position, Quaternion rotation, Vector3 scale, string sourceName, PrimitiveType type = PrimitiveType.Cube, bool visible = false, Color? color = null)
            => SpawnCustomPrimitive(position, rotation, scale, sourceName, true, type, visible, color);

        private static PrimitiveObjectToy? SpawnCustomWalkablePrimitive(Vector3 position, Quaternion rotation, Vector3 scale, string sourceName, PrimitiveType type = PrimitiveType.Cube, bool visible = false, Color? color = null)
            => SpawnCustomPrimitive(position, rotation, scale, sourceName, false, type, visible, color);

        private static NavMeshBuildSettings GetBuildSettings()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(AgentTypeId);
            if (settings.agentTypeID == -1)
                settings.agentTypeID = AgentTypeId;

            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = AgentSlope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = VoxelSize;
            settings.overrideTileSize = true;
            settings.tileSize = TileSize;

            return settings;
        }

        public static void DebugDrawNavMesh(Player player, float duration = 30f)
        {
            Room? room = player.Room;
            if (room?.GameObject == null)
                return;

            Bounds bounds = room.Base.WorldspaceBounds;
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            const float SurfaceLift = 0.03f;

            HashSet<(int, int)> drawnEdges = [];

            int triangleCount = tri.indices.Length / 3;
            for (int t = 0; t < triangleCount; t++)
            {
                int i0 = tri.indices[t * 3];
                int i1 = tri.indices[t * 3 + 1];
                int i2 = tri.indices[t * 3 + 2];

                Vector3 a = tri.vertices[i0];
                Vector3 b = tri.vertices[i1];
                Vector3 c = tri.vertices[i2];

                Vector3 center = (a + b + c) / 3f;
                if (!bounds.Contains(center))
                    continue;

                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Vector3 offset = normal * SurfaceLift;
                a += offset;
                b += offset;
                c += offset;

                int area = tri.areas[t];
                Color color = area == NotWalkableArea ? Color.red : Color.cyan;

                DrawEdgeOnce(drawnEdges, i0, i1, a, b, color, duration);
                DrawEdgeOnce(drawnEdges, i1, i2, b, c, color, duration);
                DrawEdgeOnce(drawnEdges, i2, i0, c, a, color, duration);
            }
        }

        private static void DrawEdgeOnce(HashSet<(int, int)> drawn, int indexA, int indexB, Vector3 a, Vector3 b, Color color, float duration)
        {
            (int, int) key = indexA < indexB ? (indexA, indexB) : (indexB, indexA);
            if (!drawn.Add(key))
                return;

            DrawableLines.ServerGenerateLine(duration, color, a, b);
        }

        private static bool CheckObject(Component component)
        {
            if (IsAtlasObject(component) || IsClutterSource(component))
                return true;

            return false;
        }

        private static bool IsAtlasObject(Component component)
        {
            Transform current = component.transform;
            while (current != null)
            {
                if (current.name.Contains("atlas", StringComparison.OrdinalIgnoreCase))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static bool IsClutterSource(Component component)
        {
            Transform current = component.transform;
            while (current != null)
            {
                if (current.GetComponent<SpawnableClutterConnector>() != null)
                    return true;

                foreach (string clutterName in ClutterSourceNames)
                {
                    if (current.name.Contains(clutterName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void CarveDoorways(List<NavMeshBuildSource> sources)
        {
            HashSet<Transform> excluded = [];

            foreach (SpawnableRoomConnector connector in UnityEngine.Object.FindObjectsByType<SpawnableRoomConnector>(FindObjectsSortMode.None))
            {
                if (connector == null)
                    continue;

                foreach (MeshCollider collider in connector.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (collider == null || collider.sharedMesh == null)
                        continue;

                    if (!collider.name.Contains("Doorframe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    excluded.Add(collider.transform);
                    Vector3 right = collider.transform.right;
                    Vector3 blockSize = new(DoorframeBlockoutWidth, DoorframeBlockoutHeight, DoorframeBlockoutThickness);
                    float blockOffset = DoorwayGapHalfWidth + DoorframeBlockoutWidth * 0.5f;

                    sources.Add(CreateBoxSource(collider.transform.position + right * blockOffset + Vector3.up, collider.transform.rotation, blockSize));
                    sources.Add(CreateBoxSource(collider.transform.position - right * blockOffset + Vector3.up, collider.transform.rotation, blockSize));
                    Vector3 gapSize = new(DoorwayGapHalfWidth * 2f + 0.3f, 0.08f, DoorframeBlockoutThickness + 0.2f);
                    sources.Add(CreateWalkableBoxSource(collider.transform.position + Vector3.up * 0.02f, collider.transform.rotation, gapSize));
                }
            }

            if (excluded.Count == 0)
                return;

            for (int i = sources.Count - 1; i >= 0; i--)
            {
                Component component = sources[i].component;
                if (component == null)
                    continue;

                Transform current = component.transform;
                while (current != null)
                {
                    if (excluded.Contains(current))
                    {
                        sources.RemoveAt(i);
                        break;
                    }

                    current = current.parent;
                }
            }
        }

        private static NavMeshBuildSource CreateBoxSource(Vector3 position, Quaternion rotation, Vector3 size)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = size,
                area = NotWalkableArea,
                component = null,
                transform = Matrix4x4.TRS(position, rotation, Vector3.one)
            };
        }

        private static NavMeshBuildSource CreateWalkableBoxSource(Vector3 position, Quaternion rotation, Vector3 size)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = size,
                area = DefaultArea,
                component = null,
                transform = Matrix4x4.TRS(position, rotation, Vector3.one)
            };
        }

        private static void ApplyCustomNavOverrides(List<NavMeshBuildSource> sources)
        {
            if (CustomNavBlocker.Count == 0 && CustomNavMesh.Count == 0)
                return;

            static bool IsMatch(Transform tr, string pattern)
            {
                if (tr == null || string.IsNullOrWhiteSpace(pattern))
                    return false;

                if (tr.name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;

                string path = GetHierarchyPath(tr);
                return path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
            }

            int blockerApplied = 0;
            int meshApplied = 0;

            foreach (CustomNavBlocker blocker in CustomNavBlocker)
            {
                if (string.IsNullOrWhiteSpace(blocker.ObjectName))
                    continue;
                    
                bool matched = false;
                for (int i = 0; i < sources.Count; i++)
                {
                    Component comp = sources[i].component;
                    if (comp == null || comp.transform == null)
                        continue;

                    if (!IsMatch(comp.transform, blocker.ObjectName))
                        continue;

                    NavMeshBuildSource s = sources[i];
                    if (s.area != NotWalkableArea)
                    {
                        s.area = NotWalkableArea;
                        sources[i] = s;
                        blockerApplied++;
                    }
                    matched = true;
                }

                if (!matched)
                {
                    if (blocker.Position.HasValue && blocker.Scale.HasValue)
                    {
                        Vector3 pos = blocker.Position.Value;
                        Vector3 size = blocker.Scale.Value;
                        Quaternion rot = blocker.RotationEuler.HasValue ? Quaternion.Euler(blocker.RotationEuler.Value) : Quaternion.identity;
                        PrimitiveObjectToy? spawned = SpawnCustomBlockerPrimitive(pos, rot, size, blocker.ObjectName, blocker.PrimitiveType, blocker.Visible, blocker.Color);
                        if (spawned != null)
                        {
                            sources.Add(CreateBoxSource(spawned.Position, spawned.Rotation, spawned.Scale));
                        }
                        else
                            sources.Add(CreateBoxSource(pos, rot, size));

                        blockerApplied++;
                        matched = true;
                    }
                    else
                    {
                        bool primitiveMatched = false;
                        foreach (AdminToys.PrimitiveObjectToy prim in UnityEngine.Object.FindObjectsByType<AdminToys.PrimitiveObjectToy>(FindObjectsSortMode.None))
                        {
                            if (prim == null || prim.transform == null)
                                continue;

                            if (!IsMatch(prim.transform, blocker.ObjectName))
                                continue;

                            Vector3 size = prim.Scale;
                            if (size.sqrMagnitude < 0.01f)
                            {
                                Collider c = prim.GetComponent<Collider>();
                                if (c != null)
                                {
                                    size = c.bounds.size;
                                }
                                else
                                    size = prim.transform.lossyScale;
                            }

                            if (size.sqrMagnitude < 0.01f)
                                continue;

                            Vector3 pos = prim.Position;
                            Quaternion rot = prim.Rotation;
                            sources.Add(CreateBoxSource(pos, rot, size));
                            blockerApplied++;
                            matched = true;
                            primitiveMatched = true;
                            LogManager.Debug($"NavMesh CustomNavBlocker '{blocker.ObjectName}' built from existing PrimitiveObjectToy '{prim.name}' at {pos} scale {size}");
                            break;
                        }

                        if (!primitiveMatched)
                        {
                            foreach (Collider col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                            {
                                if (col == null || col.isTrigger)
                                    continue;

                                if (!IsMatch(col.transform, blocker.ObjectName))
                                    continue;

                                Vector3 size = col.bounds.size;
                                if (size.sqrMagnitude < 0.01f)
                                    continue;

                                Vector3 pos = col.bounds.center;
                                Quaternion rot = col.transform.rotation;
                                PrimitiveObjectToy? spawned = SpawnCustomBlockerPrimitive(pos, rot, size, blocker.ObjectName);
                                if (spawned != null)
                                {
                                    sources.Add(CreateBoxSource(spawned.Position, spawned.Rotation, spawned.Scale));
                                    LogManager.Debug($"NavMesh CustomNavBlocker '{blocker.ObjectName}' spawned blocker primitive '{spawned.GameObject.name}' and added box source from primitive.");
                                }
                                else
                                    sources.Add(CreateBoxSource(pos, rot, size));

                                blockerApplied++;
                                matched = true;
                                break;
                            }
                        }
                    }
                }
                if (!matched)
                    LogManager.Debug($"NavMesh CustomNavBlocker '{blocker.ObjectName}' matched no collider/primitive/source.");
            }

            foreach (CustomNavMesh custom in CustomNavMesh)
            {
                if (string.IsNullOrWhiteSpace(custom.ObjectName))
                    continue;
                    
                bool matched = false;
                for (int i = 0; i < sources.Count; i++)
                {
                    Component comp = sources[i].component;
                    if (comp == null || comp.transform == null)
                        continue;
                        
                    if (!IsMatch(comp.transform, custom.ObjectName))
                        continue;

                    NavMeshBuildSource s = sources[i];
                    if (s.area != DefaultArea)
                    {
                        s.area = DefaultArea;
                        sources[i] = s;
                        meshApplied++;
                    }
                    matched = true;
                }
                if (!matched)
                {
                    if (custom.Position.HasValue && custom.Scale.HasValue)
                    {
                        Vector3 pos = custom.Position.Value;
                        Vector3 size = custom.Scale.Value;
                        Quaternion rot = custom.RotationEuler.HasValue ? Quaternion.Euler(custom.RotationEuler.Value) : Quaternion.identity;
                        PrimitiveObjectToy? spawned = SpawnCustomWalkablePrimitive(pos, rot, size, custom.ObjectName, custom.PrimitiveType, custom.Visible, custom.Color);
                        if (spawned != null)
                        {
                            sources.Add(CreateWalkableBoxSource(spawned.Position, spawned.Rotation, spawned.Scale));
                        }
                        else
                            sources.Add(CreateWalkableBoxSource(pos, rot, size));

                        meshApplied++;
                        matched = true;
                    }
                    else
                    {
                        bool primitiveMatched = false;
                        foreach (AdminToys.PrimitiveObjectToy prim in UnityEngine.Object.FindObjectsByType<AdminToys.PrimitiveObjectToy>(FindObjectsSortMode.None))
                        {
                            if (prim == null || prim.transform == null)
                                continue;

                            if (!IsMatch(prim.transform, custom.ObjectName))
                                continue;

                            Vector3 size = prim.Scale;
                            if (size.sqrMagnitude < 0.01f)
                            {
                                Collider c = prim.GetComponent<Collider>();
                                if (c != null)
                                {
                                    size = c.bounds.size;
                                }
                                else
                                    size = prim.transform.lossyScale;
                            }
                            
                            if (size.sqrMagnitude < 0.01f)
                                continue;

                            Vector3 pos = prim.Position;
                            Quaternion rot = prim.Rotation;
                            sources.Add(CreateWalkableBoxSource(pos, rot, size));
                            meshApplied++;
                            matched = true;
                            primitiveMatched = true;
                            LogManager.Debug($"NavMesh CustomNavMesh '{custom.ObjectName}' built from existing PrimitiveObjectToy '{prim.name}' at {pos} scale {size}");
                            break;
                        }

                        if (!primitiveMatched)
                        {
                            foreach (Collider col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                            {
                                if (col == null || col.isTrigger)
                                    continue;

                                if (!IsMatch(col.transform, custom.ObjectName))
                                    continue;

                                Vector3 size = col.bounds.size;
                                if (size.sqrMagnitude < 0.01f)
                                    continue;

                                Vector3 pos = col.bounds.center;
                                Quaternion rot = col.transform.rotation;
                                PrimitiveObjectToy? spawned2 = SpawnCustomWalkablePrimitive(pos, rot, size, custom.ObjectName);
                                if (spawned2 != null)
                                {
                                    sources.Add(CreateWalkableBoxSource(spawned2.Position, spawned2.Rotation, spawned2.Scale));
                                    LogManager.Debug($"NavMesh CustomNavMesh '{custom.ObjectName}' spawned walkable primitive '{spawned2.GameObject.name}' and added walkable box source from primitive.");
                                }
                                else
                                    sources.Add(CreateWalkableBoxSource(pos, rot, size));

                                meshApplied++;
                                matched = true;
                                break;
                            }
                        }
                    }
                }
                if (!matched)
                    LogManager.Debug($"NavMesh CustomNavMesh '{custom.ObjectName}' matched no collider/primitive/source.");
            }

            if (blockerApplied > 0 || meshApplied > 0)
                LogManager.Info($"NavMesh custom overrides applied: walkable={meshApplied}, blocked={blockerApplied} (blockers={CustomNavBlocker.Count}, meshes={CustomNavMesh.Count}) primitives={_customPrimitiveToys.Count}");
        }

        private static void LogUnknownSources(List<NavMeshBuildSource> sources)
        {
            HashSet<string> seen = [];
            foreach (NavMeshBuildSource source in sources)
            {
                if (source.component == null)
                    continue;

                string path = GetHierarchyPath(source.component.transform);
                seen.Add(path);
            }
        }

        private static string GetHierarchyPath(Transform t) => t.parent == null ? t.name : GetHierarchyPath(t.parent) + "/" + t.name;

        private static Bounds CalculateTotalBounds(List<NavMeshBuildSource> sources)
        {
            Bounds bounds = new(Vector3.zero, Vector3.zero);
            bool initialized = false;

            void EncapsulatePoint(Vector3 pt)
            {
                if (!initialized)
                {
                    bounds = new Bounds(pt, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(pt);
            }

            foreach (NavMeshBuildSource source in sources)
            {
                Vector3 center = source.transform.GetPosition();
                EncapsulatePoint(center);

                switch (source.shape)
                {
                    case NavMeshBuildSourceShape.Mesh:
                        Bounds? meshBounds = null;
                        if (source.component is MeshCollider collider && collider.sharedMesh != null)
                        {
                            meshBounds = collider.sharedMesh.bounds;
                        }
                        else if (source.sourceObject is Mesh renderMesh)
                            meshBounds = renderMesh.bounds;

                        if (meshBounds.HasValue)
                        {
                            Bounds b = meshBounds.Value;
                            Vector3 min = b.min;
                            Vector3 max = b.max;
                            Vector3[] corners =
                            [
                                new(min.x, min.y, min.z),
                                new(max.x, min.y, min.z),
                                new(min.x, max.y, min.z),
                                new(min.x, min.y, max.z),
                                new(max.x, max.y, min.z),
                                new(max.x, min.y, max.z),
                                new(min.x, max.y, max.z),
                                new(max.x, max.y, max.z),
                            ];
                            foreach (Vector3 c in corners)
                                EncapsulatePoint(source.transform.MultiplyPoint(c));
                        }
                        break;

                    case NavMeshBuildSourceShape.Box:
                    case NavMeshBuildSourceShape.Sphere:
                    case NavMeshBuildSourceShape.Capsule:
                        Vector3 he = source.size * 0.5f;
                        Vector3[] boxCorners =
                        [
                            new(he.x, he.y, he.z),
                            new(-he.x, he.y, he.z),
                            new(he.x, -he.y, he.z),
                            new(he.x, he.y, -he.z),
                            new(-he.x, -he.y, he.z),
                            new(-he.x, he.y, -he.z),
                            new(he.x, -he.y, -he.z),
                            new(-he.x, -he.y, -he.z),
                        ];

                        foreach (Vector3 c in boxCorners)
                            EncapsulatePoint(source.transform.MultiplyPoint(c));

                        break;
                }
            }

            bounds.Expand(4f);
            return bounds;
        }

        #region Elevator Dynamic NavMesh
        private static void RegisterElevatorEvents()
        {
            if (_elevatorEventsRegistered)
                return;

            _elevatorEventsRegistered = true;
            ElevatorChamber.OnElevatorMoved += OnElevatorMoved;
            ElevatorChamber.OnElevatorSpawned += OnElevatorSpawned;
            ElevatorChamber.OnElevatorRemoved += OnElevatorRemoved;
            ServerEvents.ElevatorSequenceChanged += OnLabApiElevatorSequenceChanged;
        }

        private static void UnregisterElevatorEvents()
        {
            if (!_elevatorEventsRegistered)
                return;

            _elevatorEventsRegistered = false;
            ElevatorChamber.OnElevatorMoved -= OnElevatorMoved;
            ElevatorChamber.OnElevatorSpawned -= OnElevatorSpawned;
            ElevatorChamber.OnElevatorRemoved -= OnElevatorRemoved;
            ServerEvents.ElevatorSequenceChanged -= OnLabApiElevatorSequenceChanged;
        }

        private static void OnElevatorSpawned(ElevatorChamber chamber)
        {
            Timing.CallDelayed(0.5f, () =>
            {
                if (IsBaked)
                    BuildElevatorMeshes();
            });
        }

        private static void OnElevatorRemoved(ElevatorChamber chamber)
        {
            ClearElevatorMeshes();
            if (IsBaked)
                Timing.CallDelayed(0.2f, () => BuildElevatorMeshes());
        }

        private static void OnElevatorMoved(Bounds elevatorBounds, ElevatorChamber chamber, Vector3 deltaPos, Quaternion deltaRot)
        {
            if (Time.realtimeSinceStartup < _nextElevatorRebuildTime)
                return;

            if (chamber.CurSequence != ElevatorChamber.ElevatorSequence.Ready)
            {
                Timing.CallDelayed(0.6f, () =>
                {
                    if (chamber != null && chamber.CurSequence == ElevatorChamber.ElevatorSequence.Ready)
                        TriggerElevatorNavMeshRefresh(chamber);
                });

                _nextElevatorRebuildTime = Time.realtimeSinceStartup + 2f;
            }
        }

        private static void OnLabApiElevatorSequenceChanged(LabApi.Events.Arguments.ServerEvents.ElevatorSequenceChangedEventArgs ev)
        {
            if (ev == null || ev.Elevator == null)
                return;

            if (ev.NewSequence == ElevatorChamber.ElevatorSequence.Ready)
            {
                ElevatorChamber chamber = ev.Elevator.Base;
                if (chamber != null)
                    TriggerElevatorNavMeshRefresh(chamber);
            }
        }

        private static void TriggerElevatorNavMeshRefresh(ElevatorChamber chamber)
        {
            if (Time.realtimeSinceStartup < _nextElevatorRebuildTime)
                return;

            _nextElevatorRebuildTime = Time.realtimeSinceStartup + 3f;
            if (_elevatorInstances.Count == 0)
                BuildElevatorMeshes();

            LogManager.Debug($"Elevator {chamber.AssignedGroup} arrived at level {chamber.DestinationLevel}, dynamic navmesh now at {chamber.transform.position}");
        }

        private static void BuildElevatorMeshes()
        {
            ClearElevatorMeshes();
            if (ElevatorChamber.AllChambers == null || ElevatorChamber.AllChambers.Count == 0)
            {
                LogManager.Debug("BuildElevatorMeshes: no chambers found, deferring.");
                Timing.CallDelayed(1f, () => BuildElevatorMeshes());
                return;
            }

            int layerMask = LayerMask.GetMask(BakedLayers);
            NavMeshBuildSettings settings = GetBuildSettings();

            foreach (ElevatorChamber chamber in ElevatorChamber.AllChambers.ToArray())
            {
                if (chamber == null || chamber.transform == null)
                    continue;

                List<NavMeshBuildSource> elevSources = [];
                try
                {
                    NavMeshBuilder.CollectSources(chamber.transform, layerMask, NavMeshCollectGeometry.PhysicsColliders, DefaultArea, [], elevSources);
                }
                catch (Exception ex)
                {
                    LogManager.Debug($"BuildElevatorMeshes CollectSources failed for {chamber.AssignedGroup}: {ex.Message}");
                    continue;
                }
                if (elevSources.Count == 0)
                {
                    List<NavMeshBuildSource> allSources = [];
                    NavMeshBuilder.CollectSources(null, layerMask, NavMeshCollectGeometry.PhysicsColliders, DefaultArea, [], allSources);
                    Bounds eb = chamber.WorldspaceBounds.Bounds;
                    eb.Expand(1.5f);
                    foreach (NavMeshBuildSource src in allSources)
                    {
                        Vector3 srcPos = src.transform.GetPosition();
                        if (eb.Contains(srcPos))
                            elevSources.Add(src);
                    }

                    if (elevSources.Count == 0)
                    {
                        foreach (Collider col in chamber.GetComponentsInChildren<Collider>(true))
                        {
                            if (col == null)
                                continue;

                            elevSources.Add(CreateBoxSource(col.bounds.center, col.transform.rotation, col.bounds.size));
                        }
                    }
                }

                if (elevSources.Count == 0)
                    continue;

                for (int i = elevSources.Count - 1; i >= 0; i--)
                {
                    Component c = elevSources[i].component;
                    if (c == null)
                        continue;

                    if (CheckObject(c))
                    {
                        NavMeshBuildSource s = elevSources[i];
                        s.area = NotWalkableArea;
                        elevSources[i] = s;
                    }
                }

                Bounds bounds = CalculateTotalBounds(elevSources);
                bounds.Expand(1f);

                NavMeshData? data = null;
                try
                {
                    data = NavMeshBuilder.BuildNavMeshData(settings, elevSources, bounds, chamber.transform.position, chamber.transform.rotation);
                }
                catch (Exception ex)
                {
                    LogManager.Warn($"Elevator navmesh build failed for {chamber.AssignedGroup}: {ex.Message}");
                    continue;
                }

                if (data == null)
                {
                    LogManager.Debug($"BuildNavMeshData returned null for elevator {chamber.AssignedGroup}");
                    continue;
                }

                data.name = $"Elevator_{chamber.AssignedGroup}_{_elevatorData.Count}";

                NavMeshDataInstance inst;
                try
                {
                    inst = NavMesh.AddNavMeshData(data, chamber.transform.position, chamber.transform.rotation);
                }
                catch
                {
                    data.position = chamber.transform.position;
                    data.rotation = chamber.transform.rotation;
                    inst = NavMesh.AddNavMeshData(data);
                }

                if (!inst.valid)
                {
                    LogManager.Warn($"Elevator navmesh instance invalid for {chamber.AssignedGroup}");
                    continue;
                }

                _elevatorData.Add(data);
                _elevatorInstances.Add(inst);
                _elevatorTransforms.Add(chamber.transform);
                _elevatorLastPos.Add(chamber.transform.position);
                _elevatorLastRot.Add(chamber.transform.rotation);
            }

            if (_elevatorInstances.Count > 0 && !_elevatorTrackingRegistered)
            {
                NavMesh.onPreUpdate += UpdateElevatorMeshes;
                _elevatorTrackingRegistered = true;
                LogManager.Info($"Elevator dynamic navmeshes built: {_elevatorInstances.Count} instances (will follow chambers)");
            }
            else if (_elevatorInstances.Count == 0)
            {
                LogManager.Debug("BuildElevatorMeshes: no elevator meshes built, will retry later.");
            }
        }

        private static void UpdateElevatorMeshes()
        {
            for (int i = 0; i < _elevatorInstances.Count; i++)
            {
                Transform tr = _elevatorTransforms[i];
                if (tr == null)
                    continue;

                Vector3 curPos = tr.position;
                Quaternion curRot = tr.rotation;

                if (_elevatorLastPos[i] == curPos && _elevatorLastRot[i] == curRot)
                    continue;

                NavMeshDataInstance oldInst = _elevatorInstances[i];
                if (oldInst.valid)
                    NavMesh.RemoveNavMeshData(oldInst);

                NavMeshData data = _elevatorData[i];
                NavMeshDataInstance newInst;
                try
                {
                    newInst = NavMesh.AddNavMeshData(data, curPos, curRot);
                }
                catch
                {
                    data.position = curPos;
                    data.rotation = curRot;
                    newInst = NavMesh.AddNavMeshData(data);
                }

                _elevatorInstances[i] = newInst;
                _elevatorLastPos[i] = curPos;
                _elevatorLastRot[i] = curRot;
            }
        }

        private static void ClearElevatorMeshes()
        {
            if (_elevatorTrackingRegistered)
            {
                NavMesh.onPreUpdate -= UpdateElevatorMeshes;
                _elevatorTrackingRegistered = false;
            }

            for (int i = 0; i < _elevatorInstances.Count; i++)
            {
                NavMeshDataInstance inst = _elevatorInstances[i];
                if (inst.valid)
                    NavMesh.RemoveNavMeshData(inst);
            }

            _elevatorInstances.Clear();
            _elevatorData.Clear();
            _elevatorTransforms.Clear();
            _elevatorLastPos.Clear();
            _elevatorLastRot.Clear();
        }
        #endregion
    }
}