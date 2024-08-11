using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Data;
using Vortex.Modules.Networking.PacketHandling;

namespace Vortex.Modules.Networking;

public class NetworkingModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NetworkingController>().AsSelf().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<NetworkingManager>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<NetworkingConnection>().SingleInstance();
        builder.RegisterType<PacketSerializer>().SingleInstance();

        builder.RegisterType<MinecraftBinaryReaderFactory>().AsImplementedInterfaces();

        builder.RegisterType<NetworkingPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<NetworkingEventHandler>().AsImplementedInterfaces();
    }
}
