using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class MinecraftBinaryReaderFactory : IMinecraftBinaryReaderFactory
{
    public IMinecraftBinaryReader GetReader(Stream stream)
        => new MinecraftBinaryReader(stream);
}
