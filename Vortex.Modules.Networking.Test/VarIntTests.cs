using Vortex.Modules.Networking.CustomTypes;

namespace Vortex.Modules.Networking.Test;

public class VarIntTests
{
    // The sample values from the protocol documentation, which cover the
    // single byte case, every continuation length and both sign extremes.
    public static TheoryData<int, byte[]> KnownValues => new()
    {
        { 0, [0x00] },
        { 1, [0x01] },
        { 2, [0x02] },
        { 127, [0x7f] },
        { 128, [0x80, 0x01] },
        { 255, [0xff, 0x01] },
        { 25565, [0xdd, 0xc7, 0x01] },
        { 2097151, [0xff, 0xff, 0x7f] },
        { 2147483647, [0xff, 0xff, 0xff, 0xff, 0x07] },
        { -1, [0xff, 0xff, 0xff, 0xff, 0x0f] },
        { -2147483648, [0x80, 0x80, 0x80, 0x80, 0x08] },
    };

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void VarIntToBytes_MatchesProtocolEncoding(int value, byte[] expected)
        => Assert.Equal(expected, VarInt.VarIntToBytes(value));

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void TryReadVarInt_DecodesProtocolEncoding(int expected, byte[] encoded)
    {
        var success = VarInt.TryReadVarInt(encoded, out var value, out var bytesRead);

        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Equal(encoded.Length, bytesRead);
    }

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void WriteVarIntToStream_RoundTrips(int value, byte[] _)
    {
        using var stream = new MemoryStream();
        VarInt.WriteVarIntToStream(stream, value);
        stream.Position = 0;

        Assert.Equal(value, VarInt.ReadVarIntFromStream(stream));
    }

    [Fact]
    public void TryReadVarInt_StopsAtTheEndOfTheValue()
    {
        // A VarInt followed by unrelated bytes must not consume the trailing data.
        byte[] buffer = [0xdd, 0xc7, 0x01, 0xde, 0xad, 0xbe, 0xef];

        Assert.True(VarInt.TryReadVarInt(buffer, out var value, out var bytesRead));
        Assert.Equal(25565, value);
        Assert.Equal(3, bytesRead);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x80 })]
    [InlineData(new byte[] { 0xdd, 0xc7 })]
    [InlineData(new byte[] { 0xff, 0xff, 0xff, 0xff })]
    public void TryReadVarInt_ReportsIncompleteValues(byte[] partial)
    {
        // A VarInt can be split across two socket reads. The caller has to be able
        // to tell "not here yet" apart from a decoded value.
        Assert.False(VarInt.TryReadVarInt(partial, out var value, out var bytesRead));
        Assert.Equal(0, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_RejectsOversizedValues()
    {
        byte[] tooLong = [0xff, 0xff, 0xff, 0xff, 0xff, 0x01];

        Assert.Throws<InvalidDataException>(() => VarInt.TryReadVarInt(tooLong, out _, out _));
    }
}
