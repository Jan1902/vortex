namespace Vortex.Framework.Abstraction;

public interface IEventHandler<TEvent>
{
    Task HandleAsync(TEvent @event);
}
