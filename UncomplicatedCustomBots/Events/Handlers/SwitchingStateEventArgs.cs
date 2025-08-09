
using System;
using UncomplicatedCustomBots.API.Features;

namespace UncomplicatedCustomBots.Events.Handlers
{
    public class SwitchingStateEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchingStateEventArgs"/> class.
        /// </summary>
        /// <param name="oldState">The current state of the bot before the switch. May be null if this is the initial state.</param>
        /// <param name="newState">The state that the bot is attempting to switch to.</param>
        /// <param name="bot">The bot instance that is attempting to switch states.</param>
        /// <param name="isAllowed">A value indicating whether the state switch is allowed to proceed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="newState"/> or <paramref name="bot"/> is null.</exception>
        public SwitchingStateEventArgs(API.Features.State oldState, API.Features.State newState, Bot bot, bool isAllowed)
        {
            OldState = oldState;
            NewState = newState ?? throw new ArgumentNullException(nameof(newState));
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
            IsAllowed = isAllowed;
        }

        /// <summary>
        /// Gets the current state of the bot before the switch.
        /// </summary>
        /// <value>
        /// The current <see cref="API.Features.State"/> of the bot, or null if this is the initial state assignment.
        /// </value>
        public API.Features.State? OldState { get; }

        /// <summary>
        /// Gets the state that the bot is attempting to switch to.
        /// </summary>
        /// <value>
        /// The target <see cref="API.Features.State"/> that the bot wants to switch to.
        /// </value>
        public API.Features.State NewState { get; }

        /// <summary>
        /// Gets the bot instance that is attempting to switch states.
        /// </summary>
        /// <value>
        /// The <see cref="API.Features.Bot"/> instance that is requesting the state switch.
        /// </value>
        public Bot Bot { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the state switch is allowed to proceed.
        /// </summary>
        /// <value>
        /// <c>true</c> if the state switch should be allowed; <c>false</c> if it should be cancelled.
        /// </value>
        public bool IsAllowed { get; set; }
    }
}