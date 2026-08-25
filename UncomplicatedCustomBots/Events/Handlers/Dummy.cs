using LabApi.Events;

namespace UncomplicatedCustomBots.Events.Handlers;

public static class Dummy
{
	public static event LabEventHandler<DummySpawningEventArgs> DummySpawning = null!;

	public static event LabEventHandler<DummySpawnedEventArgs> DummySpawned = null!;

	internal static void OnDummySpawning(DummySpawningEventArgs ev) => DummySpawning.InvokeEvent(ev);

	internal static void OnDummySpawned(DummySpawnedEventArgs ev) => DummySpawned.InvokeEvent(ev);
}
