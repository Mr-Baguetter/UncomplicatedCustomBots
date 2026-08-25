using System;
using LabApi.Features.Wrappers;

namespace UncomplicatedCustomBots.Events.Handlers
{
    public class PlayerMovedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerMovedEventArgs"/> class.
        /// </summary>
        /// <param name="hub">The reference hub of the player that moved.</param>
        public PlayerMovedEventArgs(ReferenceHub hub)
        {
            Player = Player.Get(hub) ?? throw new ArgumentNullException(nameof(hub));
        }

        /// <summary>
        /// Gets the player that moved.
        /// </summary>
        public Player Player { get; }
    }
}