using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.World;

public class WorldModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<WorldPacketHandler>().AsImplementedInterfaces();

        builder.RegisterType<ChunkDataHandler>();
        builder.RegisterType<PaletteFactory>();
        builder.RegisterType<FileGlobalPaletteProvider>().AsImplementedInterfaces();
    }
}
