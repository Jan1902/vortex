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
            var eventType = handlerType.GetGenericArguments()[0];

            if (!_handlers.ContainsKey(eventType))
                _handlers[eventType] = [];

            var handlers = (IEnumerable<object>)context.Resolve(typeof(IEnumerable<>).MakeGenericType(handlerType));
            _handlers[eventType].AddRange(handlers);
        }
    }

    public async Task PublishAsync<TEvent>(TEvent @event)
    {
        if (!_handlers.ContainsKey(typeof(TEvent)))
            return;

        foreach (var handler in _handlers[typeof(TEvent)])
            await ((Task?) typeof(IEventHandler<TEvent>).GetMethod(nameof(IEventHandler<TEvent>.HandleAsync))?.Invoke(handler, [@event]) ?? Task.CompletedTask);
    }

    internal void RegisterProxyHandler<TEvent, TEventArgs>(AsyncEventHandler<TEventArgs>? handler, Func<TEvent, TEventArgs> mappingFunction)
    {
        if (!_handlers.ContainsKey(typeof(TEvent)))
            _handlers[typeof(TEvent)] = [];

        _handlers[typeof(TEvent)].Add(new ProxyHandler<TEvent, TEventArgs>((e) => handler?.Invoke(e) ?? Task.CompletedTask, mappingFunction));
    }
}

internal class ProxyHandler<TEvent, TEventArgs>(Func<TEventArgs, Task> invokeFunction, Func<TEvent, TEventArgs> mappingFunction) : IEventHandler<TEvent>
{
    public Task HandleAsync(TEvent e)
    {
        var eventArgs = mappingFunction(e);

        return invokeFunction(eventArgs);
    }
}