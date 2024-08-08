using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingManager(NetworkingConnection connection) : INetworkingManager, IEventHandler<ProtocolStateChanged>
{
    private readonly TaskCompletionSource _playEnteredCompletionSource = new();

    public Task Connect()
        => connection.Connect();

    public async Task ConnectAndWaitForPlay()
    {
        await connection.Connect();
        await _playEnteredCompletionSource.Task;
    }

    public Task HandleAsync(ProtocolStateChanged @event)
    {
        if (@event.State == ProtocolState.Play)
            Task.Run(() => _playEnteredCompletionSource.SetResult());

        return Task.CompletedTask;
    }

    public Task SendPacket(PacketBase packet)
        => connection.SendPacket(packet);
}
