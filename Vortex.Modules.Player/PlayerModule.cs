using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Player;

public class PlayerModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PlayerManager>().AsImplementedInterfaces().AsSelf().SingleInstance();
        builder.RegisterType<PlayerPhysics>().AsSelf().SingleInstance();
        builder.RegisterType<MovementSimulator>().AsSelf().SingleInstance();
        builder.RegisterType<MovementPlans>().AsSelf().SingleInstance();
        builder.RegisterType<MovementController>().AsSelf().AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<PlayerPacketHandler>().AsImplementedInterfaces();
    }
}
