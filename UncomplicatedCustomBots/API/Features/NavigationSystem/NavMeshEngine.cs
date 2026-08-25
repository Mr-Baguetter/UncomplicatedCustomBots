using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using UnityEngine.AI;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public sealed class NavMeshEngine : INavMeshEngine
    {
        public bool IsBaked => NavMeshManager.IsBaked;

        public bool SamplePosition(Vector3 position, out NavMeshHit hit, float maxDistance, int areaMask) => NavMesh.SamplePosition(position, out hit, maxDistance, areaMask);

        public bool CalculatePath(Vector3 from, Vector3 to, int areaMask, NavMeshPath path) => NavMesh.CalculatePath(from, to, areaMask, path);

        public bool Raycast(Vector3 from, Vector3 to, out NavMeshHit hit, int areaMask) => NavMesh.Raycast(from, to, out hit, areaMask);

        public Vector3 ProjectToNavMesh(Vector3 position, float maxDistance = 3f)
        {
            if (IsBaked && NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMeshManager.WalkableAreaMask))
                return hit.position;

            return position;
        }

        public bool TrySnapToNavMesh(Vector3 position, float snapDistance, out Vector3 snapped)
        {
            snapped = position;
            if (!IsBaked)
                return false;

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, snapDistance, NavMeshManager.WalkableAreaMask))
                return false;
                
            snapped = hit.position;
            return true;
        }
    }
}
