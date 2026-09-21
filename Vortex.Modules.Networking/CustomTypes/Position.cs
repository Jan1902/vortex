using Vortex.Modules.Networking.Data;
using Vortex.Shared;

namespace Vortex.Modules.Networking.CustomTypes;

internal static class Position
{
    public static Vector3i ReadPositionFromStream(Stream stream)
    {
        var reader = new MinecraftBinaryReader(stream);
        var value = reader.ReadLong();

        var x = value >> 38;
        var y = value << 52 >> 52;
        var z = value << 26 >> 38;

        return new Vector3i((int)x, (int)y, (int)z);
    }

    public static void WritePositionToStream(Stream stream, Vector3i value)
    {
        var writer = new MinecraftBinaryWriter(stream);

        // Every part has to be widened to long before it is shifted. As ints
        // these shifts are taken modulo 32, so the X and Z fields would land on
        // top of each other and the position would go out as nonsense.
        var result = ((long)(value.X & 0x3FFFFFF) << 38)
            | ((long)(value.Z & 0x3FFFFFF) << 12)
            | (uint)(value.Y & 0xFFF);

        writer.WriteLong(result);
    }

    public static Vector3i ReadPosition(this Stream stream)
        => ReadPositionFromStream(stream);

    public static void WritePosition(this Stream stream, Vector3i value)
        => WritePositionToStream(stream, value);
}