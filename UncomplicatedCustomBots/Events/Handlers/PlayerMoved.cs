using LabApi.Events;
using PlayerRoles.FirstPersonControl;

namespace UncomplicatedCustomBots.Events.Handlers;

public static class PlayerMoved
{
	public static event LabEventHandler<PlayerMovedEventArgs> Moved = null!;

	internal static void OnPlayerMoved(FirstPersonMovementModule module) => Moved.InvokeEvent(new PlayerMovedEventArgs(module.Hub));
}