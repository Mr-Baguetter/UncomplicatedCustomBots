using System.Collections.Generic;
using LabApi.Features.Wrappers;
using UnityEngine;
using UnityEngine.AI;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public sealed class RoomQuery : IRoomQuery
    {
        private static readonly float[] DestinationSampleRadii = [1.5f, 4f];
        private const float RoomDestinationHeightTolerance = 5f;

        private readonly Dictionary<Room, Vector3> _centroidCache = [];
        private readonly Dictionary<Room, int> _centroidFrame = [];

        public Room? GetRoomAtPosition(Vector3 position) => Room.GetRoomAtPosition(position);

        public Vector3 GetRoomDestination(Room room, Vector3 requesterPosition)
        {
            if (room == null || room.IsDestroyed)
                return Vector3.zero;

            if (!NavMeshManager.IsBaked)
                return room.Position;

            int frame = Time.frameCount;
            if (_centroidCache.TryGetValue(room, out Vector3 cached) && _centroidFrame.TryGetValue(room, out int f) && frame - f < 600)
            {
                if (NavMesh.SamplePosition(cached, out var chit, 1f, NavMeshManager.WalkableAreaMask))
                    return cached;
            }

            Vector3 roomCenter = room.Position + Vector3.up * 0.5f;
            float floorYEst = roomCenter.y - 0.5f;
            floorYEst = room.Base.WorldspaceBounds.min.y;
            float maxAcceptableY = floorYEst + RoomDestinationHeightTolerance;

            Vector3? direct = SampleWalkableDestination(room, roomCenter, maxAcceptableY);
            if (direct != null)
            {
                CacheCentroid(room, direct.Value, frame);
                return direct.Value;
            }

            Vector3[] radialProbes =
            [
                roomCenter + new Vector3(2f, 0, 0),
                roomCenter + new Vector3(-2f, 0, 0),
                roomCenter + new Vector3(0, 0, 2f),
                roomCenter + new Vector3(0, 0, -2f),
                roomCenter + new Vector3(3f, 0, 3f),
                roomCenter + new Vector3(-3f, 0, 3f),
                roomCenter + new Vector3(3f, 0, -3f),
                roomCenter + new Vector3(-3f, 0, -3f),
            ];
            
            Vector3? bestRadial = null;
            float bestDist = float.MaxValue;
            foreach (Vector3 p in radialProbes)
            {
                Vector3? cand = SampleWalkableDestination(room, p, maxAcceptableY);
                if (cand == null)
                    continue;

                float d = Vector3.Distance(requesterPosition, cand.Value);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestRadial = cand;
                }
            }
            if (bestRadial != null)
            {
                CacheCentroid(room, bestRadial.Value, frame);
                return bestRadial.Value;
            }

            Vector3 fallback = room.Position;
            if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 4f, NavMeshManager.WalkableAreaMask))
            {
                fallback = hit.position;
            }
            else if (NavMesh.SamplePosition(fallback, out hit, 8f, NavMeshManager.WalkableAreaMask))
                fallback = hit.position;

            CacheCentroid(room, fallback, frame);
            return fallback;
        }

        private void CacheCentroid(Room room, Vector3 pos, int frame)
        {
            _centroidCache[room] = pos;
            _centroidFrame[room] = frame;
        }

        private static Vector3? SampleWalkableDestination(Room room, Vector3 probe, float maxAcceptableY)
        {
            foreach (float radius in DestinationSampleRadii)
            {
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, radius, NavMeshManager.WalkableAreaMask))
                    continue;

                if (hit.position.y > maxAcceptableY)
                    continue;

                return hit.position;
            }

            return null;
        }
    }
}
