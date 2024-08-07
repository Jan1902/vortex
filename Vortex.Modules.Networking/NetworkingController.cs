using Autofac;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingController(
    IComponentContext componentContext,
    PacketSerializer packetSerializer,
    ILogger<NetworkingController> logger,
    IEventBus eventBus) : IInitialize
{
    private ProtocolState _state;

    private readonly IComponentContext _componentContext = componentContext;

    private Dictionary<int, Type> _packetRegistrations = [];

    public void SetState(ProtocolState state) => _state = state;

    public void Initialize()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var packetType in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(PacketBase))))
            {
                var attribute = packetType.GetCustomAttribute<PacketAttribute>(true);
                if (attribute is null)
                    continue;

                _packetRegistrations[attribute.PacketId] = packetType;
            }
        }
    }

    public async Task HandlePacket(int packetId, byte[] data)
    {
        logger.LogInformation("Received packet with packet id {packetId}", packetId);

        var registration = _packetRegistrations.GetValueOrDefault(packetId);
        if (registration is null)
            return;

        using var stream = new MemoryStream(data);
        using var reader = new MinecraftBinaryReader(stream);

        var packet = packetSerializer.DeserializePacket(packetId, reader);

        var eventType = typeof(PacketReceivedEvent<>).MakeGenericType(registration);
        var packetReceivedEvent = Activator.CreateInstance(eventType, [packet]);

        await ((Task?) typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))?.MakeGenericMethod(eventType).Invoke(eventBus, [packetReceivedEvent]) ?? Task.CompletedTask);
    }
}

internal record PacketRegistration(int PacketId, ProtocolState State, Type PacketType, object[] Handlers);