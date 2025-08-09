using UnityEngine;

namespace UncomplicatedCustomBots.API.Interfaces
{
    /// <summary>
    /// Represents an object with a <see cref="Vector3"/> position.
    /// </summary>
    public interface IPosition
    {
        /// <summary>
        /// Gets the position of this object.
        /// </summary>
        public Vector3 Position { get; }
    }
}
