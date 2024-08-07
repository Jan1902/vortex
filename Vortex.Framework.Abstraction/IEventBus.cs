namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents an event bus that is responsible for publishing events.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to be published.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<TEvent>(TEvent @event);
}
