using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Interaction;

public class InteractionModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<InteractionPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<InteractionManager>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<ActionSequencer>().AsSelf().SingleInstance();
    }
}
