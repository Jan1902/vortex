using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Knowledge;
using Vortex.Modules.Behaviour.Tasks;

namespace Vortex.Modules.Behaviour.Test;

/// <summary>
/// Getting items from wherever they can be had, and falling back to the next
/// way when one fails.
/// </summary>
public class ObtainItemsTaskTests
{
    private readonly FakeInventory _inventory = new();
    private readonly FakeBrain _brain = new();
    private readonly FailureMemory _failures = new();
    private readonly List<string> _log = [];

    [Fact]
    public async Task TakesTheFirstSourceInThePolicysOrder()
    {
        var chest = Source(ItemSourceKind.Container, "chest", succeeds: true);
        var craft = Source(ItemSourceKind.Craft, "craft", succeeds: true);

        var result = await Run(Obtain(Item.Stick, 1, craft, chest));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(["chest"], _log);
    }

    [Fact]
    public async Task FollowsAnotherOrderWhenThePolicySaysSo()
    {
        _brain.Policy = new BehaviourPolicy([ItemSourceKind.Craft, ItemSourceKind.Container]);

        var chest = Source(ItemSourceKind.Container, "chest", succeeds: true);
        var craft = Source(ItemSourceKind.Craft, "craft", succeeds: true);

        await Run(Obtain(Item.Stick, 1, chest, craft));

        Assert.Equal(["craft"], _log);
    }

    [Fact]
    public async Task FallsBackToTheNextSourceWhenOneFails()
    {
        var chest = Source(ItemSourceKind.Container, "chest", succeeds: false);
        var craft = Source(ItemSourceKind.Craft, "craft", succeeds: true);

        var result = await Run(Obtain(Item.Stick, 1, chest, craft));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(["chest", "craft"], _log);
    }

    [Fact]
    public async Task RemembersWhatFailedForTheNextTime()
    {
        var chest = Source(ItemSourceKind.Container, "chest", succeeds: false);
        var craft = Source(ItemSourceKind.Craft, "craft", succeeds: true);

        await Run(Obtain(Item.Stick, 1, chest, craft));
        await Run(Obtain(Item.Stick, 2, chest, craft));

        // The chest was empty a moment ago; there is no point walking back.
        Assert.Equal(["chest", "craft", "craft"], _log);
    }

    [Fact]
    public async Task LeavesOutSourcesThePolicyDoesNotAllow()
    {
        _brain.Policy = new BehaviourPolicy([ItemSourceKind.Container]);

        var craft = Source(ItemSourceKind.Craft, "craft", succeeds: true);

        var result = await Run(Obtain(Item.Stick, 1, craft));

        Assert.False(result.IsSuccess);
        Assert.Contains("no way to get 1 Stick", result.Reason);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task FailsOnceEveryWayHasFailed()
    {
        var chest = Source(ItemSourceKind.Container, "chest", succeeds: false);
        var craft = Source(ItemSourceKind.Craft, "craft", succeeds: false);

        var result = await Run(Obtain(Item.Stick, 1, chest, craft));

        Assert.False(result.IsSuccess);
        Assert.Equal(["chest", "craft"], _log);
    }

    [Fact]
    public async Task CountsAnyOfTheKindsAsked()
    {
        _inventory.Put(9, Item.BirchPlanks, 3);
        _inventory.Put(10, Item.OakPlanks, 1);

        var request = new ItemRequest(new HashSet<Item> { Item.OakPlanks, Item.BirchPlanks }, 4);

        var result = await Run(new ObtainItemsTask(request, ObtainChain.Empty, _inventory, [], _brain, _failures, NullLogger<ObtainItemsTask>.Instance));

        Assert.True(result.IsSuccess, result.ToString());
    }

    private ObtainItemsTask Obtain(Item item, int count, params IItemSource[] sources)
        => new(ItemRequest.Of(item, count), ObtainChain.Empty, _inventory, sources, _brain, _failures, NullLogger<ObtainItemsTask>.Instance);

    /// <summary>A source whose one offer either hands over the items or fails.</summary>
    private FakeSource Source(ItemSourceKind kind, string name, bool succeeds)
        => new(kind, name, request => new ActionTask(name, () =>
        {
            _log.Add(name);

            if (!succeeds)
                return TaskResult.Failed($"{name} had none");

            _inventory.Put(20 + _log.Count, request.Items.First(), request.Count);

            return TaskResult.Success();
        }));

    private static Task<TaskResult> Run(BotTask task)
        => new TaskRunner(NullLogger<TaskRunner>.Instance).RunAsync(task, CancellationToken.None);

    private sealed class FakeSource(ItemSourceKind kind, string name, Func<ItemRequest, BotTask> offer) : IItemSource
    {
        public ItemSourceKind Kind => kind;

        public IEnumerable<ItemSourceOption> Options(ItemRequest request, ObtainChain chain)
        {
            yield return new ItemSourceOption(name, offer(request));
        }
    }

    /// <summary>Does one thing once, and is satisfied after it worked.</summary>
    private sealed class ActionTask(string name, Func<TaskResult> act) : BotTask
    {
        private bool _done;

        public override string Description => name;

        public override bool IsSatisfied() => _done;

        public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            var result = act();
            _done = result.IsSuccess;

            return Task.FromResult(result);
        }
    }

    private sealed class FakeBrain : IBotBrain
    {
        public BehaviourPolicy Policy { get; set; } = BehaviourPolicy.Default;

        public bool IsBusy => false;
        public IReadOnlyList<string> CurrentStack => [];

        public Task<TaskResult> RunAsync(BotTask task, BehaviourPolicy policy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Cancel() { }

        public TTask CreateTask<TTask>(params object[] arguments) where TTask : BotTask
            => throw new NotSupportedException();
    }
}
