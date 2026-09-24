using Microsoft.Extensions.Logging;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;

namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// Everything a task works with, and the one way to run a task from another.
/// </summary>
public sealed class Bot(
    IWorldManager world,
    IPlayerManager player,
    IMovementController movement,
    IPathfinder pathfinder,
    IInventoryManager inventory,
    IInteractionManager interaction,
    ICraftingManager crafting,
    IEntityManager entities,
    ILogger<Bot> logger)
{
    /// <summary>How deep tasks may run inside each other before it is taken for a loop.</summary>
    private const int MaxDepth = 32;

    private readonly List<BotTask> _stack = [];
    private readonly object _stackLock = new();

    public IWorldManager World => world;
    public IPlayerManager Player => player;
    public IMovementController Movement => movement;
    public IPathfinder Pathfinder => pathfinder;
    public IInventoryManager Inventory => inventory;
    public IInteractionManager Interaction => interaction;
    public ICraftingManager Crafting => crafting;
    public IEntityManager Entities => entities;
    public ILogger Logger => logger;

    /// <summary>What the bot remembers of the containers it has looked into.</summary>
    public ChestMemory Chests { get; } = new();

    /// <summary>Cancels whatever is running; tasks pass it to anything that waits.</summary>
    public CancellationToken Cancellation { get; set; }

    /// <summary>
    /// Whether routes may change the world on the way: break blocks to get
    /// through, and place the bot's throwaway blocks to get up or across.
    /// </summary>
    public bool MayDig { get; set; }

    /// <summary>The tasks running right now, outermost first.</summary>
    public IReadOnlyList<string> Stack
    {
        get
        {
            lock (_stackLock)
                return _stack.Select(task => task.Description).ToList();
        }
    }

    /// <summary>
    /// The tasks running right now. For a task that has to know what is being
    /// done further up, such as which items are already being fetched.
    /// </summary>
    public IReadOnlyList<BotTask> RunningTasks
    {
        get
        {
            lock (_stackLock)
                return _stack.ToList();
        }
    }

    /// <summary>
    /// Runs a task, unless it is done already, and says how it went. Never
    /// throws: a crash inside the task comes back as a failure.
    /// </summary>
    public async Task<TaskResult> Run(BotTask task)
    {
        if (Cancellation.IsCancellationRequested)
            return TaskResult.Cancelled();

        if (task.IsDone(this))
            return TaskResult.Success();

        int depth;

        lock (_stackLock)
        {
            depth = _stack.Count;

            if (depth >= MaxDepth)
                return TaskResult.Failed($"tasks nested {MaxDepth} deep at '{task.Description}', most likely going round in circles");

            _stack.Add(task);
        }

        var indent = new string(' ', depth * 2);

        logger.LogDebug("{Indent}{Task}", indent, task.Description);

        try
        {
            var result = await task.RunAsync(this);

            if (result.IsFailure)
                logger.LogDebug("{Indent}{Task}: {Result}", indent, task.Description, result);

            return result;
        }
        catch (OperationCanceledException)
        {
            return TaskResult.Cancelled();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Task} crashed", task.Description);

            return TaskResult.Failed($"{task.Description} crashed: {exception.Message}");
        }
        finally
        {
            lock (_stackLock)
                _stack.Remove(task);
        }
    }
}
