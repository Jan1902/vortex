using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour.Test;

public class TaskRunnerLoggingTests
{
    [Fact]
    public async Task WritesOutWhatItWantedAndWhatItDid()
    {
        var logger = new RecordingLogger<TaskRunner>();
        var world = new FakeWorld { LogsStanding = 1 };

        await new TaskRunner(logger).RunAsync(new CutDownTreeTask(world), CancellationToken.None);

        var log = string.Join("\n", logger.Lines);

        // The goal, the precondition it settled on, the act and its outcome.
        Assert.Contains("want: fell the tree", log);
        Assert.Contains("need: be at the tree", log);
        Assert.Contains("acted: Succeeded", log);
        Assert.Contains("done: fell the tree", log);
    }

    [Fact]
    public async Task NamesThePreconditionsItWalkedPast()
    {
        var logger = new RecordingLogger<TaskRunner>();
        var world = new FakeWorld { LogsStanding = 1, AtTree = true };

        await new TaskRunner(logger).RunAsync(new CutDownTreeTask(world), CancellationToken.None);

        // Which preconditions were considered and found to hold is as much of
        // what the task is thinking as the one it settled on.
        Assert.Contains(logger.Lines, l => l.Contains("have: be at the tree"));
        Assert.Contains(logger.Lines, l => l.Contains("need: break log 1"));
    }

    [Fact]
    public async Task IndentsNestedTasksSoTheShapeOfTheTreeIsVisible()
    {
        var logger = new RecordingLogger<TaskRunner>();
        var world = new FakeWorld { LogsStanding = 1 };

        await new TaskRunner(logger).RunAsync(new CutDownTreeTask(world), CancellationToken.None);

        var root = logger.Lines.Single(l => l.Contains("want: fell the tree"));
        var child = logger.Lines.First(l => l.Contains("want: be at the tree"));

        Assert.Equal(0, LeadingSpaces(root));
        Assert.True(LeadingSpaces(child) > LeadingSpaces(root), "a dependency should be indented under the task that named it");
    }

    [Fact]
    public async Task SaysSoWhenThereWasNothingToDo()
    {
        var logger = new RecordingLogger<TaskRunner>();
        var world = new FakeWorld { LogsStanding = 0 };

        await new TaskRunner(logger).RunAsync(new CutDownTreeTask(world), CancellationToken.None);

        Assert.Contains(logger.Lines, l => l.Contains("already so, nothing to do"));
    }

    [Fact]
    public async Task ReportsTheReasonItGaveUp()
    {
        var logger = new RecordingLogger<TaskRunner>();

        await new TaskRunner(logger).RunAsync(new ImpossibleTask(), CancellationToken.None);

        Assert.Contains(logger.Lines, l => l.Contains("gave up:") && l.Contains("no chance"));
    }

    [Fact]
    public async Task SaysNothingWhenDebugLoggingIsOff()
    {
        var logger = new RecordingLogger<TaskRunner>(LogLevel.Information);
        var world = new FakeWorld { LogsStanding = 2 };

        var result = await new TaskRunner(logger).RunAsync(new CutDownTreeTask(world), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Empty(logger.Lines);
    }

    [Fact]
    public async Task LoggingDoesNotChangeWhatTheRunnerDoes()
    {
        var withLogging = new FakeWorld { LogsStanding = 3 };
        var withoutLogging = new FakeWorld { LogsStanding = 3 };

        await new TaskRunner(new RecordingLogger<TaskRunner>())
            .RunAsync(new CutDownTreeTask(withLogging), CancellationToken.None);

        await new TaskRunner(NullLogger<TaskRunner>.Instance)
            .RunAsync(new CutDownTreeTask(withoutLogging), CancellationToken.None);

        // Naming the skipped dependencies means enumerating them differently;
        // it must not ask any task anything extra or change the order of work.
        Assert.Equal(withoutLogging.Log, withLogging.Log);
    }

    private static int LeadingSpaces(string line)
        => line.Length - line.TrimStart(' ').Length;

    private class ImpossibleTask : BotTask
    {
        public override string Description => "no chance";

        public override bool IsSatisfied() => false;

        public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
            => Task.FromResult(TaskResult.Failed("it cannot be done"));
    }
}
