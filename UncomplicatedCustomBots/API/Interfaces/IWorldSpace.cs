using UnityEngine;

namespace UncomplicatedCustomBots.API.Interfaces
{
    /// <summary>
    /// Represents an object with a <see cref="Vector3"/> position and a <see cref="Quaternion"/> rotation.
    /// </summary>
    public interface IWorldSpace : IPosition, IRotation
    {
    }
}
