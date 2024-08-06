using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

public class NetworkingModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NetworkingController>().SingleInstance();
        builder.RegisterType<NetworkingConnection>().SingleInstance();
        builder.RegisterType<PacketSerializer>().SingleInstance();

        builder.RegisterPacketHandler<NetworkingPacketHandler>();
    }

    public void Setup(IComponentContext context)
    {
        context.Resolve<NetworkingController>().Setup();
    }
}
