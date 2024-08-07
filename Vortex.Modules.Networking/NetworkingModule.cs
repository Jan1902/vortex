using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

public class NetworkingModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NetworkingController>().AsSelf().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<NetworkingConnection>().As<INetworkingConnection>().SingleInstance();
        builder.RegisterType<PacketSerializer>().SingleInstance();

        builder.RegisterType<NetworkingPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<NetworkingEventHandler>().AsImplementedInterfaces();
    }
}
