using System.Buffers;
namespace Vortex.Modules.Networking.CustomTypes;

internal static class VarInt
{
    public const int MIN_SIZE = 1;
    public const int MAX_SIZE = 5;

    public static int ReadVarIntFromStream(Stream stream)
    {
        var numRead = 0;
        var result = 0;
        byte read;
        do
        {
            var next = stream.ReadByte();
            if (next < 0)
                throw new EndOfStreamException("Stream ended in the middle of a VarInt");

            read = (byte)next;
            var value = read & 0x7f;
            result |= value << 7 * numRead;

            numRead++;
            if (numRead > MAX_SIZE)
                throw new InvalidDataException("VarInt is too big");
        } while ((read & 0x80) != 0);

        return result;
    }

    public static void WriteVarIntToStream(Stream stream, int value)
    {
        // Shifting has to be unsigned, otherwise the sign bit of a negative value
        // is carried along forever and the loop never terminates.
        var remaining = (uint)value;

        do
        {
            var temp = (byte)(remaining & 127);
            remaining >>= 7;
            if (remaining != 0)
                temp |= 128;
            stream.WriteByte(temp);
        } while (remaining != 0);
    }

    /// <summary>
    /// Reads a VarInt from a sequence reader, tolerating a value that is split
    /// across segment boundaries.
    /// </summary>
    /// <returns><c>true</c> if a complete VarInt was present.</returns>
    public static bool TryReadVarInt(ref SequenceReader<byte> reader, out int value)
    {
        value = 0;

        for (var i = 0; i < MAX_SIZE; i++)
        {
            if (!reader.TryRead(out var read))
                return false;

            value |= (read & 0x7f) << (7 * i);

            if ((read & 0x80) == 0)
                return true;
        }

        throw new InvalidDataException("VarInt is too big");
    }

    /// <summary>
    /// Reads a VarInt from a buffer without consuming it, tolerating an incomplete value.
    /// A VarInt can be split across two socket reads, in which case the caller has to
    /// wait for more data instead of reading past the end of what arrived.
    /// </summary>
    /// <returns><c>true</c> if a complete VarInt was present.</returns>
    public static bool TryReadVarInt(ReadOnlySpan<byte> buffer, out int value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;

        while (bytesRead < buffer.Length)
        {
            var read = buffer[bytesRead];
            value |= (read & 0x7f) << (7 * bytesRead);
            bytesRead++;

            if ((read & 0x80) == 0)
                return true;

            if (bytesRead >= MAX_SIZE)
                throw new InvalidDataException("VarInt is too big");
        }

        // Ran out of data before the VarInt terminated.
        value = 0;
        bytesRead = 0;

        return false;
    }

    public static byte[] VarIntToBytes(int value)
    {
        using var stream = new MemoryStream(MAX_SIZE);

        WriteVarIntToStream(stream, value);

        return stream.ToArray();
    }

    public static byte[] ToBytesAsVarInt(this int value)
        => VarIntToBytes(value);

    public static int ReadVarInt(this Stream stream)
        => ReadVarIntFromStream(stream);

    public static void WriteVarInt(this Stream stream, int value)
        => WriteVarIntToStream(stream, value);
}