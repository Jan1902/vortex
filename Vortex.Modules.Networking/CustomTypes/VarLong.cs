namespace Vortex.Modules.Networking.CustomTypes;

/// <summary>
/// The 64 bit form of a VarInt, used where a packet packs several fields into
/// one number -- the entries of a section blocks update, for instance.
/// </summary>
internal static class VarLong
{
    public const int MAX_SIZE = 10;

    public static long ReadVarLongFromStream(Stream stream)
    {
        var numRead = 0;
        var result = 0L;
        byte read;

        do
        {
            var next = stream.ReadByte();
            if (next < 0)
                throw new EndOfStreamException("Stream ended in the middle of a VarLong");

            read = (byte)next;
            var value = read & 0x7fL;
            result |= value << 7 * numRead;

            numRead++;
            if (numRead > MAX_SIZE)
                throw new InvalidDataException("VarLong is too big");
        } while ((read & 0x80) != 0);

        return result;
    }

    public static void WriteVarLongToStream(Stream stream, long value)
    {
        // Unsigned, so the sign bit of a negative value does not get carried
        // along forever and stop the loop from ever terminating.
        var remaining = (ulong)value;

        do
        {
            var temp = (byte)(remaining & 127);
            remaining >>= 7;

            if (remaining != 0)
                temp |= 128;

            stream.WriteByte(temp);
        } while (remaining != 0);
    }

    public static long ReadVarLong(this Stream stream)
        => ReadVarLongFromStream(stream);

    public static void WriteVarLong(this Stream stream, long value)
        => WriteVarLongToStream(stream, value);
}
