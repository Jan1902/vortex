using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Interaction;

internal class InteractionPacketHandler(ActionSequencer sequencer) : IPacketHandler<BlockChangedAck>
{
    public Task HandleAsync(BlockChangedAck packet)
    {
        sequencer.Confirm(packet.Sequence);

        return Task.CompletedTask;
    }
}
