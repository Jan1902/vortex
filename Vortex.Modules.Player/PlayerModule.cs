using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Player;

public class PlayerModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PlayerManager>().AsImplementedInterfaces().AsSelf().SingleInstance();
        builder.RegisterType<PlayerPhysics>().AsSelf().SingleInstance();

        builder.RegisterType<PlayerPacketHandler>().AsImplementedInterfaces();
    }
}
