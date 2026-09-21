using Vortex.Modules.Networking.CustomTypes;
using Vortex.Shared;

namespace Vortex.Modules.Networking.Test;

public class PositionTests
{
    public static TheoryData<int, int, int> Positions => new()
    {
        { 0, 0, 0 },
        { 1, 2, 3 },
        { 100, 64, -200 },
        { -1, -1, -1 },
        { -30000000, -64, 30000000 },
        { 33554431, 2047, -33554432 },
        { -33554432, -2048, 33554431 },
    };

    [Theory]
    [MemberData(nameof(Positions))]
    public void WriteThenRead_GivesBackTheSamePosition(int x, int y, int z)
    {
        var position = new Vector3i(x, y, z);

        using var stream = new MemoryStream();
        Position.WritePositionToStream(stream, position);

        stream.Position = 0;

        Assert.Equal(position, Position.ReadPositionFromStream(stream));
    }

    [Fact]
    public void Write_MatchesTheProtocolLayout()
    {
        // The worked example from the protocol documentation: x 18357644,
        // y 831, z -20882616 packs to this long.
        using var stream = new MemoryStream();
        Position.WritePositionToStream(stream, new Vector3i(18357644, 831, -20882616));

        // The wire is big endian; System.BitConverter reads native order.
        var packed = System.BitConverter.ToInt64(stream.ToArray().Reverse().ToArray());

        Assert.Equal(unchecked((long)0b01000110000001110110001100_10110000010101101101001000_001100111111), packed);
    }

    [Fact]
    public void Write_KeepsTheAxesApart()
    {
        // X and Z used to collide, because shifting an int by 38 wraps round to
        // a shift by 6 rather than moving the value where it belongs.
        using var stream = new MemoryStream();
        Position.WritePositionToStream(stream, new Vector3i(1000, 0, 0));

        using var other = new MemoryStream();
        Position.WritePositionToStream(other, new Vector3i(0, 0, 1000));

        Assert.NotEqual(stream.ToArray(), other.ToArray());
    }
}
