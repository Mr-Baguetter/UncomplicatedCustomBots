using LabApi.Events;

namespace UncomplicatedCustomBots.Events.Handlers;

public static class State
{
	public static event LabEventHandler<SwitchedStateEventArgs> StateSwitched;

	public static event LabEventHandler<SwitchingStateEventArgs> StateSwitching;

	public static event LabEventHandler<TargetDetectedEventArgs> TargetDetected;
	
	public static event LabEventHandler<ItemCollectedEventArgs> ItemCollected;

	public static event LabEventHandler<ItemCollectingEventArgs> ItemCollecting;

	internal static void OnStateSwitched(SwitchedStateEventArgs ev) => StateSwitched.InvokeEvent(ev);

	internal static void OnStateSwitching(SwitchingStateEventArgs ev) => StateSwitching.InvokeEvent(ev);

	internal static void OnTargetDetected(TargetDetectedEventArgs ev) => TargetDetected.InvokeEvent(ev);

	internal static void OnItemCollected(ItemCollectedEventArgs ev) => ItemCollected.InvokeEvent(ev);

	internal static void OnItemCollecting(ItemCollectingEventArgs ev) => ItemCollecting.InvokeEvent(ev);
}
