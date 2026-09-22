using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour;

internal class BotBrain(Bot bot, ILogger<BotBrain> logger) : IBotBrain
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _current;

    public bool IsBusy => _current is not null;

    public IReadOnlyList<string> CurrentStack => bot.Stack;

    public Bot Bot => bot;

    public async Task<TaskResult> RunAsync(BotTask task, bool mayDig = false, CancellationToken cancellationToken = default)
    {
        // A new order replaces the old one rather than queueing behind it.
        Cancel();

        await _gate.WaitAsync(cancellationToken);

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _current = cancellation;

        bot.Cancellation = cancellation.Token;
        bot.MayDig = mayDig;

        try
        {
            logger.LogInformation("Working on: {Task}", task.Description);

            var result = await bot.Run(task);

            logger.LogInformation("{Task}: {Result}", task.Description, result);

            return result;
        }
        finally
        {
            bot.MayDig = false;
            _current = null;
            cancellation.Dispose();
            _gate.Release();
        }
    }

    public void Cancel()
        => _current?.Cancel();
}
