using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Inventory;

public class InventoryModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<InventoryPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<InventoryManager>().AsSelf().AsImplementedInterfaces().SingleInstance();
    }
}
