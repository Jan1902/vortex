using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour;

/// <summary>
/// The bot's single point of intent. Holds the one task that is allowed to run
/// and hands out what is needed to build tasks from outside.
/// </summary>
internal class BotBrain(
    TaskRunner runner,
    IComponentContext context,
    ILogger<BotBrain> logger) : IBotBrain
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _current;

    public bool IsBusy
        => _current is not null;

    public IReadOnlyList<string> CurrentStack
        => runner.Stack;

    public BehaviourPolicy Policy { get; private set; } = BehaviourPolicy.Default;

    public Task<TaskResult> RunAsync(BotTask task, CancellationToken cancellationToken = default)
        => RunAsync(task, BehaviourPolicy.Default, cancellationToken);

    public async Task<TaskResult> RunAsync(BotTask task, BehaviourPolicy policy, CancellationToken cancellationToken = default)
    {
        // Whatever is running loses: a new order replaces the old one rather
        // than queueing behind it, the same way a new movement replaces the
        // movement in progress.
        Cancel();

        await _gate.WaitAsync(cancellationToken);

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _current = cancellation;

        // One tree runs at a time, so the policy can simply be the brain's for
        // as long as it runs, rather than something every task hands down.
        Policy = policy;

        try
        {
            logger.LogInformation("Working on: {Task}", task.Description);

            var result = await runner.RunAsync(task, cancellation.Token);

            if (result.IsSuccess)
                logger.LogInformation("Done: {Task}", task.Description);
            else
                logger.LogWarning("Gave up on {Task} -- {Result}", task.Description, result);

            return result;
        }
        finally
        {
            Policy = BehaviourPolicy.Default;
            _current = null;
            cancellation.Dispose();
            _gate.Release();
        }
    }

    public void Cancel()
        => _current?.Cancel();

    public TTask CreateTask<TTask>(params object[] arguments) where TTask : BotTask
        => context.Resolve<TTask>(arguments.Select(a => (Parameter)new TypedParameter(a.GetType(), a)));
}
