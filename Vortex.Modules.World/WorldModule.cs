using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.World;

public class WorldModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<WorldPacketHandler>().AsImplementedInterfaces();
    }
}
