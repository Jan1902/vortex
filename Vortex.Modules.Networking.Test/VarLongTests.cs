using Vortex.Modules.Networking.CustomTypes;

namespace Vortex.Modules.Networking.Test;

public class VarLongTests
{
    // The sample values from the protocol documentation: the single byte case,
    // every continuation length and both sign extremes.
    public static TheoryData<long, byte[]> KnownValues => new()
    {
        { 0, [0x00] },
        { 1, [0x01] },
        { 2, [0x02] },
        { 127, [0x7f] },
        { 128, [0x80, 0x01] },
        { 255, [0xff, 0x01] },
        { 2147483647, [0xff, 0xff, 0xff, 0xff, 0x07] },
        { 9223372036854775807, [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x7f] },
        { -1, [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01] },
        { -2147483648, [0x80, 0x80, 0x80, 0x80, 0xf8, 0xff, 0xff, 0xff, 0xff, 0x01] },
        { -9223372036854775808, [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01] },
    };

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void WriteVarLong_MatchesProtocolEncoding(long value, byte[] expected)
    {
        using var stream = new MemoryStream();

        VarLong.WriteVarLongToStream(stream, value);

        Assert.Equal(expected, stream.ToArray());
    }

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void ReadVarLong_MatchesProtocolEncoding(long expected, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        Assert.Equal(expected, VarLong.ReadVarLongFromStream(stream));
    }

    [Fact]
    public void ReadVarLong_StopsAtTheEndOfItsOwnValue()
    {
        // Two values back to back: reading the first must leave the second
        // alone, which is what makes an array of them work.
        using var stream = new MemoryStream([0x80, 0x01, 0x7f]);

        Assert.Equal(128, VarLong.ReadVarLongFromStream(stream));
        Assert.Equal(127, VarLong.ReadVarLongFromStream(stream));
    }

    [Fact]
    public void ReadVarLong_RejectsAValueThatNeverTerminates()
    {
        using var stream = new MemoryStream(Enumerable.Repeat((byte)0xff, 16).ToArray());

        Assert.Throws<InvalidDataException>(() => VarLong.ReadVarLongFromStream(stream));
    }

    [Fact]
    public void ReadVarLong_RejectsATruncatedValue()
    {
        using var stream = new MemoryStream([0x80]);

        Assert.Throws<EndOfStreamException>(() => VarLong.ReadVarLongFromStream(stream));
    }
}
