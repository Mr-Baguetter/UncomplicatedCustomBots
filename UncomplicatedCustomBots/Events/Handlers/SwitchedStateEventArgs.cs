using System;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.Events.Handlers
{
    public class SwitchedStateEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchedStateEventArgs"/> class.
        /// </summary>
        /// <param name="oldState">The previous state of the bot. May be null if this is the initial state.</param>
        /// <param name="newState">The current state of the bot after the switch.</param>
        /// <param name="bot">The bot instance that switched states.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="newState"/> or <paramref name="bot"/> is null.</exception>
        public SwitchedStateEventArgs(API.Features.State oldState, API.Features.State newState, Bot bot)
        {
            OldState = oldState;
            NewState = newState ?? throw new ArgumentNullException(nameof(newState));
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        /// <summary>
        /// Gets the previous state of the bot before the switch occurred.
        /// </summary>
        /// <value>
        /// The previous <see cref="API.Features.State"/> of the bot, or null if this was the initial state assignment.
        /// </value>
        public API.Features.State? OldState { get; }

        /// <summary>
        /// Gets the current state of the bot after the switch.
        /// </summary>
        /// <value>
        /// The new <see cref="API.Features.State"/> that the bot has switched to.
        /// </value>
        public API.Features.State NewState { get; }

        /// <summary>
        /// Gets the bot instance that switched states.
        /// </summary>
        /// <value>
        /// The <see cref="API.Features.Bot"/> instance that performed the state switch.
        /// </value>
        public Bot Bot { get; }
    }
}