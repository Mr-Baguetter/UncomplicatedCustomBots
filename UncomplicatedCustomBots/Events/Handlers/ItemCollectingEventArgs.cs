using System;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.Events.Handlers
{
    public class ItemCollectingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemCollectingEventArgs"/> class.
        /// </summary>
        /// <param name="bot">The bot collecting the item.</param>
        /// <param name="item">The item being collected.</param>
        /// <param name="distance">The distance to the item.</param>
        public ItemCollectingEventArgs(Bot bot, Pickup item, float distance, bool isAllowed)
        {
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Distance = distance;
            IsAllowed = isAllowed;
        }

        /// <summary>
        /// Gets the bot collecting the item.
        /// </summary>
        public Bot Bot { get; }

        /// <summary>
        /// Gets the item being collected.
        /// </summary>
        public Pickup Item { get; }

        /// <summary>
        /// Gets the distance to the item.
        /// </summary>
        public float Distance { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the item should be collected.
        /// </summary>
        public bool IsAllowed { get; set; }
    }
}