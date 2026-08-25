using UnityEngine;
using UnityEngine.AI;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public interface INavMeshEngine
    {
        bool IsBaked { get; }
        bool SamplePosition(Vector3 position, out NavMeshHit hit, float maxDistance, int areaMask);
        bool CalculatePath(Vector3 from, Vector3 to, int areaMask, NavMeshPath path);
        bool Raycast(Vector3 from, Vector3 to, out NavMeshHit hit, int areaMask);
        Vector3 ProjectToNavMesh(Vector3 position, float maxDistance = 3f);
        bool TrySnapToNavMesh(Vector3 position, float snapDistance, out Vector3 snapped);
    }
}
