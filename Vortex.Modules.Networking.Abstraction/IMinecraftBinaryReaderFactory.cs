namespace Vortex.Modules.Networking.Abstraction;

public interface IMinecraftBinaryReaderFactory
{
    IMinecraftBinaryReader GetReader(Stream stream);
}
