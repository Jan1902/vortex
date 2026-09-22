using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Interaction;

internal class InteractionManager(INetworkingManager networking, ActionSequencer sequencer) : IInteractionManager
{
    /// <summary>
    /// How long to wait for the server to confirm an act. It confirms within a
    /// tick when all is well, so this only covers lag.
    /// </summary>
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>How often to swing while breaking, in ticks.</summary>
    private const int SwingInterval = 5;

    public async Task<bool> UseItemOnBlockAsync(Vector3i block, BlockFace face, Hand hand = Hand.Main, Vector3f? cursor = null, CancellationToken cancellationToken = default)
    {
        var (sequence, confirmed) = sequencer.Next();
        var point = cursor ?? new Vector3f(0.5f, 0.5f, 0.5f);

        await networking.SendPacket(new UseItemOn(hand, block, face, point.X, point.Y, point.Z, InsideBlock: false, sequence));
        await networking.SendPacket(new Swing(hand));

        return await WaitFor(confirmed, cancellationToken);
    }

    public async Task<bool> UseItemAsync(float yaw, float pitch, Hand hand = Hand.Main, CancellationToken cancellationToken = default)
    {
        var (sequence, confirmed) = sequencer.Next();

        await networking.SendPacket(new UseItem(hand, sequence, yaw, pitch));

        return await WaitFor(confirmed, cancellationToken);
    }

    public async Task<bool> DigAsync(Vector3i block, BlockFace face, int ticks, CancellationToken cancellationToken = default)
    {
        var (start, started) = sequencer.Next();

        await networking.SendPacket(new PlayerAction(DigAction.Start, block, face, start));

        // A block that breaks at once is broken by starting; there is nothing to finish.
        if (ticks <= 0)
            return await WaitFor(started, cancellationToken);

        try
        {
            // Swinging all along is what a player does; the server does not
            // need it, but others see the bot working.
            for (var tick = 0; tick <= ticks; tick++)
            {
                if (tick % SwingInterval == 0)
                    await networking.SendPacket(new Swing(Hand.Main));

                await Task.Delay(Tick, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            await networking.SendPacket(new PlayerAction(DigAction.Cancel, block, face, sequencer.Next().Sequence));

            throw;
        }

        var (finish, finished) = sequencer.Next();

        await networking.SendPacket(new PlayerAction(DigAction.Finish, block, face, finish));

        return await WaitFor(finished, cancellationToken);
    }

    public Task SwingAsync(Hand hand = Hand.Main)
        => networking.SendPacket(new Swing(hand));

    private static async Task<bool> WaitFor(Task<bool> confirmed, CancellationToken cancellationToken)
    {
        var timeout = Task.Delay(ConfirmationTimeout, cancellationToken);

        return await Task.WhenAny(confirmed, timeout) == confirmed && confirmed.Result;
    }
}
