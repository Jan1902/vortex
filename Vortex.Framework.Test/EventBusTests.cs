using Autofac;
using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Framework.Abstraction;

namespace Vortex.Framework.Test;

public class EventBusTests
{
    private record TestEvent(string Message);

    private record TestEventArgs(string Message);

    [Fact]
    public async Task PublishingDoesNotWaitForTheBotAuthorsHandler()
    {
        using var bus = Create();

        var handlerStarted = new TaskCompletionSource();
        var letHandlerFinish = new TaskCompletionSource();

        bus.RegisterProxyHandler<TestEvent, TestEventArgs>(
            _ =>
            {
                handlerStarted.TrySetResult();

                return letHandlerFinish.Task;
            },
            e => new TestEventArgs(e.Message));

        // This is the call the packet loop makes. If it waits for the handler,
        // the client stops reading packets for as long as the handler runs --
        // which is how the bot used to go deaf for the whole of a walk.
        await bus.PublishAsync(new TestEvent("first")).WaitAsync(TimeSpan.FromSeconds(5));

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        letHandlerFinish.SetResult();
    }

    [Fact]
    public async Task EventsKeepFlowingWhileAnEarlierHandlerIsStillBusy()
    {
        using var bus = Create();

        var letFirstFinish = new TaskCompletionSource();
        var seen = new List<string>();
        var bothSeen = new TaskCompletionSource();

        bus.RegisterProxyHandler<TestEvent, TestEventArgs>(
            async args =>
            {
                if (args.Message == "first")
                    await letFirstFinish.Task;

                lock (seen)
                {
                    seen.Add(args.Message);

                    if (seen.Count == 2)
                        bothSeen.TrySetResult();
                }
            },
            e => new TestEventArgs(e.Message));

        await bus.PublishAsync(new TestEvent("first")).WaitAsync(TimeSpan.FromSeconds(5));
        await bus.PublishAsync(new TestEvent("second")).WaitAsync(TimeSpan.FromSeconds(5));

        letFirstFinish.SetResult();

        await bothSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Still delivered in the order they were published: one reader drains
        // the queue, so freeing the packet loop does not scramble the events.
        Assert.Equal(["first", "second"], seen);
    }

    [Fact]
    public async Task AHandlerThatThrowsDoesNotStopLaterEvents()
    {
        using var bus = Create();

        var secondSeen = new TaskCompletionSource();

        bus.RegisterProxyHandler<TestEvent, TestEventArgs>(
            args =>
            {
                if (args.Message == "first")
                    throw new InvalidOperationException("the bot author's bug");

                secondSeen.TrySetResult();

                return Task.CompletedTask;
            },
            e => new TestEventArgs(e.Message));

        await bus.PublishAsync(new TestEvent("first"));
        await bus.PublishAsync(new TestEvent("second"));

        await secondSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InternalHandlersStillRunInOrderOnThePacketLoop()
    {
        // Packet handlers inside the client must stay ordered and finish before
        // the next packet is read: chunk data has to be applied before the block
        // updates that follow it.
        var order = new List<string>();
        var builder = new ContainerBuilder();

        builder.RegisterInstance(new RecordingHandler(order, "one")).As<IEventHandler<TestEvent>>();
        builder.RegisterInstance(new RecordingHandler(order, "two")).As<IEventHandler<TestEvent>>();
        builder.Register(c => new EventBus(c.Resolve<IComponentContext>(), NullLogger<EventBus>.Instance))
            .AsSelf()
            .SingleInstance();

        using var container = builder.Build();

        var bus = container.Resolve<EventBus>();
        bus.Initialize();

        await bus.PublishAsync(new TestEvent("x"));

        Assert.Equal(["one", "two"], order);
    }

    private static EventBus Create()
    {
        var builder = new ContainerBuilder();

        builder.Register(c => new EventBus(c.Resolve<IComponentContext>(), NullLogger<EventBus>.Instance))
            .AsSelf()
            .SingleInstance();

        var container = builder.Build();
        var bus = container.Resolve<EventBus>();

        bus.Initialize();

        return bus;
    }

    private class RecordingHandler(List<string> order, string name) : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent e)
        {
            order.Add(name);

            return Task.CompletedTask;
        }
    }
}
