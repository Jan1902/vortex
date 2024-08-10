using System.Collections;

namespace Vortex.Modules.Networking;

internal static class BitSet
{
    public static void WriteBitSetToStream(Stream stream, bool[] bitSet)
    {
        var bitArray = new BitArray(bitSet);
        var bytes = new byte[(int) Math.Ceiling(bitSet.Length / 8d)];
        bitArray.CopyTo(bytes, 0);

        stream.Write(bytes, 0, bytes.Length);
    }

    public static bool[] ReadBitSetFromStream(Stream stream, int length)
    {
        var bytes = new byte[(int) Math.Ceiling(length / 8d)];
        stream.Read(bytes, 0, bytes.Length);

        var bitArray = new BitArray(bytes);
        var resultArray = new bool[length];
        bitArray.CopyTo(resultArray, 0);

        return resultArray;
    }

    public static void WriteBitSet(this Stream stream, bool[] bitSet)
        => WriteBitSetToStream(stream, bitSet);

    public static bool[] ReadBitSet(this Stream stream, int length)
        => ReadBitSetFromStream(stream, length);
}