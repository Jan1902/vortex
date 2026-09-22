using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Interaction.Test;

/// <summary>Keeps every packet sent, and can answer them as the server would.</summary>
internal class RecordingNetworking : INetworkingManager
{
    public List<PacketBase> Sent { get; } = [];

    /// <summary>Called for each packet sent, to play the server.</summary>
    public Action<PacketBase>? Server { get; set; }

    public Task Connect() => Task.CompletedTask;

    public Task ConnectAndWaitForPlay() => Task.CompletedTask;

    public Task SendPacket(PacketBase packet)
    {
        Sent.Add(packet);
        Server?.Invoke(packet);

        return Task.CompletedTask;
    }
}
