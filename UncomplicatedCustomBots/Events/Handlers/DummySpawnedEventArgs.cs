using System;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.Events.Handlers
{
    public class DummySpawnedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DummySpawnedEventArgs"/> class.
        /// </summary>
        /// <param name="hub">The dummy refrencehub that spawned.</param>
        public DummySpawnedEventArgs(ReferenceHub hub)
        {
            Player = Player.Get(hub) ?? throw new ArgumentNullException(nameof(hub));
        }

        /// <summary>
        /// Gets the bot collecting the item.
        /// </summary>
        public Player Player { get; }
    }
}