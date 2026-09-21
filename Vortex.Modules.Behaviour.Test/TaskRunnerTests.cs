using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour.Test;

public class TaskRunnerTests
{
    [Fact]
    public async Task InterleavesDependenciesWithoutAnyTaskTrackingProgress()
    {
        var world = new FakeWorld { LogsStanding = 3 };

        var result = await Run(new CutDownTreeTask(world));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(0, world.LogsStanding);

        // Walking and mining alternate although nothing says so: breaking a log
        // puts the player out of position, so the next round finds the first
        // dependency unsatisfied again.
        Assert.Equal(["walk", "break", "walk", "break", "walk", "break"], world.Log);
    }

    [Fact]
    public async Task SkipsDependenciesThatAreAlreadySatisfied()
    {
        var world = new FakeWorld { LogsStanding = 1, AtTree = true };

        var result = await Run(new CutDownTreeTask(world));

        Assert.True(result.IsSuccess, result.ToString());

        // The goal named "be at the tree" all the same; the runner found it
        // already true and went straight to mining.
        Assert.Equal(["break"], world.Log);
    }

    [Fact]
    public async Task DoesNothingWhenTheGoalIsAlreadyReached()
    {
        var world = new FakeWorld { LogsStanding = 0 };

        var result = await Run(new CutDownTreeTask(world));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Empty(world.Log);
    }

    [Fact]
    public async Task RunningTheSameTaskTwiceIsHarmless()
    {
        var world = new FakeWorld { LogsStanding = 2 };
        var task = new CutDownTreeTask(world);

        Assert.True((await Run(task)).IsSuccess);
        var afterFirstRun = world.Log.Count;

        Assert.True((await Run(task)).IsSuccess);

        Assert.Equal(afterFirstRun, world.Log.Count);
    }

    [Fact]
    public async Task FailsWhenADependencyFails()
    {
        var result = await Run(new NeedsFailingDependencyTask());

        Assert.Equal(TaskOutcome.Failed, result.Outcome);
        Assert.Equal("nope", result.Reason);
    }

    [Fact]
    public async Task GivesUpWhenADependencyKeepsComingBackUnsatisfied()
    {
        // The dependency reports success and is unsatisfied again on the next
        // round. Without a guard this is an endless loop.
        var result = await Run(new OscillatingTask());

        Assert.Equal(TaskOutcome.Failed, result.Outcome);
        Assert.Contains("right after it reported success", result.Reason);
    }

    [Fact]
    public async Task GivesUpOnATaskThatDependsOnItself()
    {
        var result = await Run(new SelfDependentTask());

        Assert.Equal(TaskOutcome.Failed, result.Outcome);
        Assert.Contains("depends on itself", result.Reason);
    }

    [Fact]
    public async Task GivesUpOnAGoalItsDependenciesNeverReach()
    {
        // Everything it asks for is satisfied and it does nothing itself, yet it
        // never considers itself done.
        var result = await Run(new UnreachableGoalTask());

        Assert.Equal(TaskOutcome.Failed, result.Outcome);
        Assert.Contains("made no progress", result.Reason);
    }

    [Fact]
    public async Task StopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var world = new FakeWorld { LogsStanding = 3 };

        var result = await Run(new CutDownTreeTask(world), cancellation.Token);

        Assert.Equal(TaskOutcome.Cancelled, result.Outcome);
        Assert.Empty(world.Log);
    }

    [Fact]
    public async Task ReportsTheWholeStackWhileWorking()
    {
        var runner = new TaskRunner(NullLogger<TaskRunner>.Instance);
        var observer = new StackObservingTask(runner);

        await runner.RunAsync(observer, CancellationToken.None);

        Assert.Equal(["watch the stack", "report the stack"], observer.Seen);
        Assert.Empty(runner.Stack);
    }

    private static Task<TaskResult> Run(BotTask task, CancellationToken cancellationToken = default)
        => new TaskRunner(NullLogger<TaskRunner>.Instance).RunAsync(task, cancellationToken);

    private class NeedsFailingDependencyTask : BotTask
    {
        public override string Description => "needs something impossible";
        public override bool IsSatisfied() => false;
        public override IEnumerable<BotTask> Dependencies() => [new AlwaysFailsTask()];
    }

    private class AlwaysFailsTask : BotTask
    {
        public override string Description => "the impossible thing";
        public override bool IsSatisfied() => false;
        public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
            => Task.FromResult(TaskResult.Failed("nope"));
    }

    private class OscillatingTask : BotTask
    {
        // Shared, because Dependencies hands out a fresh instance every round --
        // a task cannot carry state across rounds even if it wanted to.
        private readonly bool[] _satisfied = [true];

        public override string Description => "chase a moving goal";
        public override bool IsSatisfied() => false;
        public override IEnumerable<BotTask> Dependencies() => [new SatisfiedOnlyWhileRunningTask(_satisfied)];
    }

    private class SatisfiedOnlyWhileRunningTask(bool[] satisfied) : BotTask
    {
        public override string Description => "briefly true";

        public override bool IsSatisfied()
        {
            // False when the runner picks it, true once it is being run, so it
            // reports success and is pending again on the very next round.
            satisfied[0] = !satisfied[0];

            return satisfied[0];
        }
    }

    private class SelfDependentTask : BotTask
    {
        public override string Description => "go in circles";
        public override bool IsSatisfied() => false;
        public override IEnumerable<BotTask> Dependencies() => [new SelfDependentTask()];
    }

    private class UnreachableGoalTask : BotTask
    {
        public override string Description => "wish for it";
        public override bool IsSatisfied() => false;
    }

    private class StackObservingTask(TaskRunner runner) : BotTask
    {
        private bool _done;

        public IReadOnlyList<string> Seen { get; private set; } = [];

        public override string Description => "watch the stack";

        public override bool IsSatisfied() => _done;

        public override IEnumerable<BotTask> Dependencies()
            => _done ? [] : [new ReportStackTask(runner, stack => { Seen = stack; _done = true; })];
    }

    private class ReportStackTask(TaskRunner runner, Action<IReadOnlyList<string>> report) : BotTask
    {
        public override string Description => "report the stack";

        public override bool IsSatisfied() => false;

        public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            report(runner.Stack);

            return Task.FromResult(TaskResult.Success());
        }
    }
}
