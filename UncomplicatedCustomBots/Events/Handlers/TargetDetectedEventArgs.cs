using System;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.Events.Handlers
{ 
    public class TargetDetectedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TargetDetectedEventArgs"/> class.
        /// </summary>
        /// <param name="bot">The bot that detected the target.</param>
        /// <param name="target">The detected target player.</param>
        /// <param name="distance">The distance to the target.</param>
        /// <param name="hasLineOfSight">Whether the bot has line of sight to the target.</param>
        public TargetDetectedEventArgs(Bot bot, Player target, float distance, bool hasLineOfSight)
        {
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Distance = distance;
            HasLineOfSight = hasLineOfSight;
            ShouldChangeState = true;
        }

        /// <summary>
        /// Gets the bot that detected the target.
        /// </summary>
        public Bot Bot { get; }

        /// <summary>
        /// Gets the detected target player.
        /// </summary>
        public Player Target { get; }

        /// <summary>
        /// Gets the distance to the target.
        /// </summary>
        public float Distance { get; }

        /// <summary>
        /// Gets a value indicating whether the bot has line of sight to the target.
        /// </summary>
        public bool HasLineOfSight { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the bot should change state in response to this detection.
        /// </summary>
        /// <remarks>
        /// Event handlers can set this to false to prevent automatic state transitions.
        /// </remarks>
        public bool ShouldChangeState { get; set; }
    }   
}