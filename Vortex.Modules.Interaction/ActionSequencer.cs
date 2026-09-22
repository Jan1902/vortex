namespace Vortex.Modules.Interaction;

/// <summary>
/// Numbers the acts that change blocks, and lets callers wait for the server
/// to confirm them.
/// </summary>
/// <remarks>
/// The server confirms once per tick, naming the highest number it has dealt
/// with; everything numbered up to it is done too.
/// </remarks>
internal class ActionSequencer
{
    private readonly object _lock = new();
    private readonly SortedDictionary<int, TaskCompletionSource<bool>> _pending = [];

    private int _last;

    /// <summary>
    /// Takes the next number.
    /// </summary>
    /// <returns>The number, and a task that completes once the server has confirmed it.</returns>
    public (int Sequence, Task<bool> Confirmed) Next()
    {
        lock (_lock)
        {
            var sequence = ++_last;
            var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _pending[sequence] = confirmation;

            return (sequence, confirmation.Task);
        }
    }

    public void Confirm(int sequence)
    {
        List<TaskCompletionSource<bool>> confirmed;

        lock (_lock)
        {
            confirmed = _pending.TakeWhile(pending => pending.Key <= sequence).Select(pending => pending.Value).ToList();

            foreach (var key in _pending.Keys.TakeWhile(key => key <= sequence).ToList())
                _pending.Remove(key);
        }

        foreach (var confirmation in confirmed)
            confirmation.TrySetResult(true);
    }
}
