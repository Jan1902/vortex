using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Player;

public class PlayerModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PlayerPacketHandler>().AsImplementedInterfaces();
    }
}
