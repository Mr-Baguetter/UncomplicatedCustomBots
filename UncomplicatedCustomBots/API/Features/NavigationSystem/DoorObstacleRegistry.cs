using System.Collections.Generic;
using Interactables.Interobjects.DoorUtils;
using UnityEngine;
using UnityEngine.AI;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public sealed class DoorObstacleRegistry : MonoBehaviour
    {
        private static readonly Dictionary<DoorVariant, DoorObstacleRegistry> _registry = [];
        private DoorVariant _door = null!;
        private NavMeshObstacle _obstacle = null!;
        private float _pollTimer = 0f;
        private const float PollInterval = 0.2f;

        public static DoorObstacleRegistry GetOrCreate(DoorVariant door)
        {
            if (_registry.TryGetValue(door, out var existing) && existing != null)
                return existing;

            GameObject go = new($"DoorObstacle_{door.GetInstanceID()}");
            go.transform.SetParent(door.transform, false);
            go.transform.localPosition = Vector3.zero;
            DoorObstacleRegistry reg = go.AddComponent<DoorObstacleRegistry>();
            reg._door = door;
            reg._obstacle = go.AddComponent<NavMeshObstacle>();
            reg._obstacle.shape = NavMeshObstacleShape.Box;
            reg._obstacle.size = new Vector3(2.2f, 3f, 0.4f);
            reg._obstacle.center = Vector3.zero;
            reg._obstacle.carving = false;
            reg._obstacle.carveOnlyStationary = false;
            _registry[door] = reg;
            return reg;
        }

        public static void EnsureForDoors(IEnumerable<DoorVariant> doors)
        {
            foreach (DoorVariant d in doors)
            {
                if (d != null)
                    GetOrCreate(d);
            }
        }

        private void Update()
        {
            if (_door == null)
            {
                Destroy(gameObject);
                return;
            }

            _pollTimer += Time.deltaTime;
            if (_pollTimer < PollInterval)
                return;
                
            _pollTimer = 0f;
            bool shouldCarve = false;
            if (_obstacle.carving != shouldCarve)
                _obstacle.carving = shouldCarve;
        }

        private void OnDestroy()
        {
            if (_door != null) 
                _registry.Remove(_door);
        }
    }
}
