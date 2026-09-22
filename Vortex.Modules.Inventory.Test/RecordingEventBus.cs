using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Inventory.Test;

/// <summary>Keeps every event published, for tests to look at.</summary>
internal class RecordingEventBus : IEventBus
{
    public List<object> Events { get; } = [];

    public Task PublishAsync<TEvent>(TEvent @event)
    {
        Events.Add(@event!);

        return Task.CompletedTask;
    }
}
