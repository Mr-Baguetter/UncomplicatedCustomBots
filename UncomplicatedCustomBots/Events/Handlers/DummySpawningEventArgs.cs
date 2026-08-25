using System;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.Events.Handlers
{
    public class DummySpawningEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DummySpawnedEventArgs"/> class.
        /// </summary>
        /// <param name="hub">The dummy player thats spawning.</param>
        public DummySpawningEventArgs(ReferenceHub hub, bool isAllowed)
        {
            Player = Player.Get(hub) ?? throw new ArgumentNullException(nameof(hub));
            IsAllowed = isAllowed;
        }

        /// <summary>
        /// Gets the dummy player.
        /// </summary>
        public Player Player { get; }

        /// <summary>
        /// Can this dummy spawn.
        /// </summary>
        public bool IsAllowed { get; }
    }
}