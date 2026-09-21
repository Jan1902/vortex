using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour.Test;

/// <summary>
/// Stands in for the world the tasks read and write. In a real task this is a
/// manager injected through the constructor; here it is the only state there is,
/// which is the point being tested.
/// </summary>
internal class FakeWorld
{
    public readonly List<string> Log = [];

    public bool AtTree { get; set; }
    public int LogsStanding { get; set; }
}

/// <summary>Satisfied once the player is at the tree. Walks there when it is not.</summary>
internal class GoToTreeTask(FakeWorld world) : BotTask
{
    public override string Description => "be at the tree";

    public override bool IsSatisfied() => world.AtTree;

    public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        world.Log.Add("walk");
        world.AtTree = true;

        return Task.FromResult(TaskResult.Success());
    }
}

/// <summary>Breaks one log, and knocks the player out of position doing it.</summary>
internal class BreakOneLogTask(FakeWorld world) : BotTask
{
    private readonly int _standingWhenCreated = world.LogsStanding;

    public override string Description => $"break log {_standingWhenCreated}";

    public override bool IsSatisfied() => world.LogsStanding < _standingWhenCreated;

    public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        world.Log.Add("break");
        world.LogsStanding--;

        // Felling a log leaves the player out of reach of the next one, which is
        // what makes walking and mining have to interleave.
        world.AtTree = false;

        return Task.FromResult(TaskResult.Success());
    }
}

/// <summary>Pure goal: names what must hold, never acts.</summary>
internal class CutDownTreeTask(FakeWorld world) : BotTask
{
    public override string Description => "fell the tree";

    public override bool IsSatisfied() => world.LogsStanding == 0;

    public override IEnumerable<BotTask> Dependencies()
    {
        // Named unconditionally and in a fixed order. Whether going to the tree
        // is any work is not this task's business.
        yield return new GoToTreeTask(world);
        yield return new BreakOneLogTask(world);
    }
}
