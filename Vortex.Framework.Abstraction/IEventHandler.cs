namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents an event handler that handles events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle.</typeparam>
public interface IEventHandler<TEvent>
{
    /// <summary>
    /// Handles the specified event asynchronously.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TEvent @event);
}
