using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.PacketHandling;
using Vortex.Modules.Networking.Packets;

namespace Vortex.Modules.Networking;

internal class NetworkingPacketHandler(
    ILogger<NetworkingPacketHandler> logger,
    NetworkingConnection connection,
    NetworkingController controller,
    VortexClientConfiguration configuration)
    : IPacketHandler<LoginSuccessPacket>,
    IPacketHandler<ClientBoundKnownPacks>,
    IPacketHandler<RegistryData>,
    IPacketHandler<FinishConfiguration>,
    IPacketHandler<ClientBoundKeepAlive>
{
    public async Task HandleAsync(LoginSuccessPacket packet)
    {
        logger.LogInformation("Received LoginSuccess packet for {Username} with UUID {Uuid}", packet.Username, packet.Uuid);
        await connection.SendPacket(new LoginAcknowledgedPacket());

        await controller.SetState(ProtocolState.Configuration);

        // The server needs the client's settings before it knows how much world
        // to send, so this is the first thing a client says in configuration.
        await connection.SendPacket(new ClientInformation(
            Locale: configuration.Locale,
            ViewDistance: configuration.ViewDistance,
            ChatMode: 0,
            ChatColors: true,
            DisplayedSkinParts: 0x7f,
            MainHand: 1,
            EnableTextFiltering: false,
            AllowServerListings: true));
    }

    public async Task HandleAsync(ClientBoundKnownPacks packet)
    {
        logger.LogInformation("Received KnownPacks packet with {packCount} packs", packet.KnownPacks.Length);
        await connection.SendPacket(new ServerBoundKnownPacks([new("minecraft", "core", "1.21")]));
    }

    public Task HandleAsync(RegistryData packet)
    {
        logger.LogInformation("Received RegistryData packet with {entryCount} entries", packet.Entries.Length);

        return Task.CompletedTask;
    }

    public async Task HandleAsync(FinishConfiguration packet)
    {
        logger.LogInformation("Received FinishConfiguration packet");

        await connection.SendPacket(new AcknowledgeFinishConfiguration());

        await controller.SetState(ProtocolState.Play);
    }

    public async Task HandleAsync(ClientBoundKeepAlive packet)
    {
        logger.LogInformation("Received KeepAlive packet with KeepAliveId {id}", packet.KeepAliveId);

        await connection.SendPacket(new ServerBoundKeepAlive(packet.KeepAliveId));
    }
}
