using Vortex.Modules.Interaction.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Interaction.Test;

public class InteractionTests
{
    private readonly RecordingNetworking _networking = new();
    private readonly ActionSequencer _sequencer = new();
    private readonly InteractionManager _interaction;

    public InteractionTests()
        => _interaction = new InteractionManager(_networking, _sequencer);

    [Fact]
    public async Task ClicksABlockAndSwings()
    {
        _networking.Server = packet =>
        {
            if (packet is UseItemOn use)
                _sequencer.Confirm(use.Sequence);
        };

        var confirmed = await _interaction.UseItemOnBlockAsync(new Vector3i(1, 64, 2), BlockFace.Up);

        Assert.True(confirmed);

        var use = Assert.IsType<UseItemOn>(_networking.Sent[0]);
        Assert.Equal(new Vector3i(1, 64, 2), use.Location);
        Assert.Equal(BlockFace.Up, use.Face);
        Assert.Equal(0.5f, use.CursorY);
        Assert.IsType<Swing>(_networking.Sent[1]);
    }

    [Fact]
    public async Task NumbersEachActAnew()
    {
        _networking.Server = packet =>
        {
            if (packet is UseItemOn use)
                _sequencer.Confirm(use.Sequence);
            if (packet is UseItem item)
                _sequencer.Confirm(item.Sequence);
        };

        await _interaction.UseItemOnBlockAsync(Vector3i.Zero, BlockFace.North);
        await _interaction.UseItemAsync(90, 0);

        Assert.Equal(1, _networking.Sent.OfType<UseItemOn>().Single().Sequence);
        Assert.Equal(2, _networking.Sent.OfType<UseItem>().Single().Sequence);
    }

    [Fact]
    public async Task GivesUpWhenTheServerDoesNotConfirm()
        => Assert.False(await _interaction.UseItemOnBlockAsync(Vector3i.Zero, BlockFace.North));

    [Fact]
    public void ConfirmingOneActConfirmsTheOnesBefore()
    {
        var (_, first) = _sequencer.Next();
        var (second, secondConfirmed) = _sequencer.Next();
        var (_, third) = _sequencer.Next();

        _sequencer.Confirm(second);

        Assert.True(first.IsCompletedSuccessfully);
        Assert.True(secondConfirmed.IsCompletedSuccessfully);
        Assert.False(third.IsCompleted);
    }

    [Theory]
    [InlineData(0.5, 70, 0.5, BlockFace.Up)]
    [InlineData(0.5, 60, 0.5, BlockFace.Down)]
    [InlineData(5, 65, 0.5, BlockFace.East)]
    [InlineData(-5, 65, 0.5, BlockFace.West)]
    [InlineData(0.5, 65, -5, BlockFace.North)]
    [InlineData(0.5, 65, 5, BlockFace.South)]
    public void FindsTheSideFacingAPoint(double x, double y, double z, BlockFace face)
        => Assert.Equal(face, BlockFaces.Facing(new Vector3i(0, 64, 0), new Vector3d(x, y, z)));
}
