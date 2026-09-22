using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Crafting;

public class CraftingModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CraftingPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<CraftingManager>().AsSelf().AsImplementedInterfaces().SingleInstance();
    }
}
