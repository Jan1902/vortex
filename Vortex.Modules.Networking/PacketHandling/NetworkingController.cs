using Autofac;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.Data;

namespace Vortex.Modules.Networking.PacketHandling;

internal class NetworkingController(
    IComponentContext componentContext,
    PacketSerializer packetSerializer,
    ILogger<NetworkingController> logger,
    IEventBus eventBus) : IInitialize
{
    private ProtocolState _state = ProtocolState.Handshake;

    private readonly IComponentContext _componentContext = componentContext;

    private List<PacketRegistration> _packetRegistrations = [];

    public async Task SetState(ProtocolState state)
    {
        _state = state;

        await eventBus.PublishAsync(new ProtocolStateChanged(state));
    }

    public void Initialize()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var packetType in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(PacketBase))))
            {
                var attribute = packetType.GetCustomAttribute<PacketAttribute>(true);
                if (attribute is null)
                    continue;

                if (attribute.PacketDirection == PacketDirection.ServerBound)
                    continue;

                _packetRegistrations.Add(new(attribute.PacketId, attribute.State, packetType));
            }
        }
    }

    public async Task HandlePacket(int packetId, byte[] data)
    {
        var registration = _packetRegistrations.FirstOrDefault(p => p.PacketId == packetId && p.State == _state);
        if (registration is null)
        {
            logger.LogInformation("Received unknown packet with id 0x{packetId:X2}", packetId);
            return;
        }

        using var stream = new MemoryStream(data);
        using var reader = new MinecraftBinaryReader(stream);

        var packet = packetSerializer.DeserializePacket(registration.PacketType, reader);

        logger.LogInformation("Received packet of type {packetType}", packet.GetType().Name);

        var eventType = typeof(PacketReceivedEvent<>).MakeGenericType(registration.PacketType);
        var packetReceivedEvent = Activator.CreateInstance(eventType, [packet]);

        await ((Task?)typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))?.MakeGenericMethod(eventType).Invoke(eventBus, [packetReceivedEvent]) ?? Task.CompletedTask);
    }
}

internal record PacketRegistration(int PacketId, ProtocolState State, Type PacketType);