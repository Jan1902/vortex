using Autofac;
using Autofac.Core;
using Vortex.Framework.Abstraction;

namespace Vortex.Framework;

internal class EventBus(IComponentContext context) : IEventBus, IInitialize
{
    private readonly Dictionary<Type, List<object>> _handlers = [];

    public void Initialize()
    {
        var handlerTypes = context.ComponentRegistry.Registrations
            .SelectMany(r => r.Services)
            .Where(s => s is IServiceWithType)
            .Select(s => ((IServiceWithType)s).ServiceType)
            .Where(t => t.IsClosedTypeOf(typeof(IEventHandler<>)))
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            if (!_handlers.ContainsKey(handlerType))
                _handlers[handlerType.GetGenericArguments()[0]] = [];

            var handlers = (IEnumerable<object>)context.Resolve(typeof(IEnumerable<>).MakeGenericType(handlerType));
            _handlers[handlerType.GetGenericArguments()[0]].AddRange(handlers);
        }
    }

    public async Task PublishAsync<TEvent>(TEvent @event)
    {
        if (!_handlers.ContainsKey(typeof(TEvent)))
            return;

        foreach (var handler in _handlers[typeof(TEvent)])
            await ((Task?) typeof(IEventHandler<TEvent>).GetMethod(nameof(IEventHandler<TEvent>.HandleAsync))?.Invoke(handler, [@event]) ?? Task.CompletedTask);
    }
}
