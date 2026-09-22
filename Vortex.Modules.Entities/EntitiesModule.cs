using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Entities;

public class EntitiesModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<EntityPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<EntityManager>().AsSelf().AsImplementedInterfaces().SingleInstance();
    }
}
