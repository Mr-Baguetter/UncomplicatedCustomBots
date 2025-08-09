using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class WaypointMarker : MonoBehaviour
    {
        public int WaypointIndex { get; private set; }
        private float _lifetime;
        
        public void Initialize(float lifetime, int waypointIndex)
        {
            _lifetime = lifetime;
            WaypointIndex = waypointIndex;
            
            Destroy(gameObject, _lifetime);
        }
    }
}