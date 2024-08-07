using Autofac;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Reflection;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingController(IComponentContext componentContext, PacketSerializer packetSerializer, ILogger<NetworkingController> logger)
{
    private ProtocolState _state;

    private readonly IComponentContext _componentContext = componentContext;

    private PacketRegistration[]? _packetRegistrations;

    public void SetState(ProtocolState state) => _state = state;

    public void Setup()
    {
        var registrations = new List<PacketRegistration>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var packetType in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(PacketBase))))
            {
                var attribute = packetType.GetCustomAttribute<PacketAttribute>(true);
                if (attribute is null)
                    continue;

                var handlers = (IEnumerable)_componentContext.Resolve(typeof(IEnumerable<>).MakeGenericType(typeof(IPacketHandler<>).MakeGenericType(packetType)));

                registrations.Add(new(attribute.PacketId, attribute.State, packetType, [.. handlers]));
            }
        }

        _packetRegistrations = [.. registrations];
    }

    public async Task HandlePacket(int packetId, byte[] data)
    {
        logger.LogInformation($"Received packet with packet id {packetId}");

        var registration = _packetRegistrations?.FirstOrDefault(r => r.PacketId == packetId);
        if (registration is null)
            return;

        var packetType = registration?.PacketType;
        if (packetType is null)
            return;

        using var stream = new MemoryStream(data);
        using var reader = new MinecraftBinaryReader(stream);

        var packet = packetSerializer.DeserializePacket(packetId, reader);

        foreach (var handler in registration!.Handlers)
        {
            var handleMethod = handler.GetType().GetMethod(nameof(IPacketHandler<HandshakePacket>.HandleAsync), [packetType]);
            var task = handleMethod?.Invoke(handler, [packet]);
            await ((Task?) task ?? Task.CompletedTask);
        }
    }
}

internal record PacketRegistration(int PacketId, ProtocolState State, Type PacketType, object[] Handlers);