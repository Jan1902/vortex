namespace Vortex.Modules.World.ChunkData;

internal class CompactedDataArray(ulong[] dataArray, int bitsPerEntry)
{
    private int EntriesPerLong => 64 / bitsPerEntry;
    private ulong IndividualValueMask => (ulong)((1 << bitsPerEntry) - 1);

    public ulong this[int index] => Get(index);

    public ulong Get(int index)
    {
        var longIndex = index / EntriesPerLong;
        var individualOffset = index % EntriesPerLong * bitsPerEntry;

        var value = dataArray[longIndex] >> individualOffset;
        value &= IndividualValueMask;

        return value;
    }
}