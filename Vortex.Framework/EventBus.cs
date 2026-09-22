using System.Threading.Channels;
using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;

namespace Vortex.Framework;

internal class EventBus(IComponentContext context, ILogger<EventBus> logger) : IEventBus, IInitialize, IDisposable
{
    private readonly Dictionary<Type, List<object>> _handlers = [];

    /// <summary>
    /// Work handed to the bot author, waiting to run away from the packet loop.
    /// </summary>
    /// <remarks>
    /// Events reach the client through the same call that is reading packets off
    /// the socket, so anything the bot author does in a handler holds that loop
    /// up. A handler that starts a task and waits for it -- the ordinary way to
    /// react to a chat command -- stops the client from receiving anything at
    /// all until the task is finished: no block updates, no position
    /// corrections. The bot goes deaf exactly while it acts.
    /// <para>
    /// Queueing here keeps the packet loop free. One reader drains the queue, so
    /// handlers still run one at a time and in the order the events arrived.
    /// </para>
    /// </remarks>
    private readonly Channel<Func<Task>> _clientCallbacks =
        Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });

    private readonly CancellationTokenSource _shutdown = new();

    private Task? _callbackPump;

    public void Initialize()
    {
        var handlerTypes = context.ComponentRegistry.Registrations
            .SelectMany(r => r.Services)
            .Where(s => s is IServiceWithType)
            .Select(s => ((IServiceWithType)s).ServiceType)
            // The handler interfaces themselves, not every service that happens
            // to implement one: a handler also registered as its own class
            // would otherwise count as a handler type with no event to it.
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            // Distinct, because the same handler interface shows up once per
            // registration that offers it. Without this, every handler for an
            // event with two handlers registered would be invoked twice.
            .Distinct()
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            var eventType = handlerType.GetGenericArguments()[0];

            if (!_handlers.ContainsKey(eventType))
                _handlers[eventType] = [];

            var handlers = (IEnumerable<object>)context.Resolve(typeof(IEnumerable<>).MakeGenericType(handlerType));
            _handlers[eventType].AddRange(handlers);
        }

        _callbackPump ??= Task.Run(() => RunClientCallbacks(_shutdown.Token));
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

        _handlers[typeof(TEvent)].Add(new ProxyHandler<TEvent, TEventArgs>(Queue, mappingFunction));

        // Hands the event to the queue and returns at once, so however long the
        // bot author's handler takes, the packet loop is not waiting on it.
        Task Queue(TEventArgs args)
        {
            _clientCallbacks.Writer.TryWrite(() => handler?.Invoke(args) ?? Task.CompletedTask);

            return Task.CompletedTask;
        }
    }

    private async Task RunClientCallbacks(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var callback in _clientCallbacks.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await callback();
                }
                catch (Exception e)
                {
                    // One handler throwing must not stop every later event from
                    // being delivered.
                    logger.LogError(e, "An event handler threw");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    public void Dispose()
    {
        _clientCallbacks.Writer.TryComplete();
        _shutdown.Cancel();
        _shutdown.Dispose();
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
