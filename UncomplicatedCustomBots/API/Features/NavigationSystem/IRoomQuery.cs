using LabApi.Features.Wrappers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public interface IRoomQuery
    {
        Vector3 GetRoomDestination(Room room, Vector3 requesterPosition);
        Room? GetRoomAtPosition(Vector3 position);
    }
}
