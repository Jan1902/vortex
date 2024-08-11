using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.World.ChunkData;
using Vortex.Modules.World.ChunkData.Palettes;

namespace Vortex.Modules.World;

public class WorldModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<WorldPacketHandler>().AsImplementedInterfaces();
        builder.RegisterType<WorldManager>().AsSelf().AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<ChunkDataHandler>();
        builder.RegisterType<PaletteFactory>();
        builder.RegisterType<FileGlobalPaletteProvider>().AsImplementedInterfaces();
    }
}
