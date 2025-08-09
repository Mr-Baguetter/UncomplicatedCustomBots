using UnityEngine;

namespace UncomplicatedCustomBots.API.Interfaces
{
    /// <summary>
    /// Represents an object with a <see cref="Quaternion"/> rotation.
    /// </summary>
    public interface IRotation
    {
        /// <summary>
        /// Gets the rotation of this object.
        /// </summary>
        public Quaternion Rotation { get; }
    }
}
