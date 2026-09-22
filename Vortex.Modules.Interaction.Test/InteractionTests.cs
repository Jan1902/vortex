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

    [Fact]
    public async Task DigsForTheTimeItIsGiven()
    {
        _networking.Server = ConfirmDigging;

        var started = DateTime.UtcNow;
        Assert.True(await _interaction.DigAsync(new Vector3i(3, 64, 3), BlockFace.West, ticks: 4));

        var actions = _networking.Sent.OfType<PlayerAction>().ToList();
        Assert.Equal([DigAction.Start, DigAction.Finish], actions.Select(a => a.Action));
        Assert.All(actions, a => Assert.Equal(BlockFace.West, a.Face));
        Assert.True(actions[1].Sequence > actions[0].Sequence);
        Assert.Contains(_networking.Sent, packet => packet is Swing);
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task OnlyStartsABlockThatBreaksAtOnce()
    {
        _networking.Server = ConfirmDigging;

        Assert.True(await _interaction.DigAsync(Vector3i.Zero, BlockFace.Up, ticks: 0));

        Assert.Equal(DigAction.Start, _networking.Sent.OfType<PlayerAction>().Single().Action);
    }

    [Fact]
    public void WritesOnlyWhatAnAttackNeeds()
    {
        var bytes = Write(new Interact(300, InteractAction.Attack, 0, 0, 0, Hand.Main, Sneaking: false));

        // Entity 300 as a VarInt, the action, no hand, not sneaking.
        Assert.Equal(new byte[] { 0xAC, 0x02, 1, 0 }, bytes);
    }

    [Fact]
    public void WritesThePointAndHandForInteractingAtAPoint()
    {
        var bytes = Write(new Interact(5, InteractAction.InteractAt, 0.5f, 1f, 0f, Hand.Off, Sneaking: true));

        Assert.Equal(1 + 1 + 3 * 4 + 1 + 1, bytes.Length);
        Assert.Equal((byte)Hand.Off, bytes[^2]);
        Assert.Equal(1, bytes[^1]);
    }

    [Fact]
    public async Task AttacksAndSwings()
    {
        await _interaction.AttackAsync(42);

        Assert.Equal(InteractAction.Attack, Assert.IsType<Interact>(_networking.Sent[0]).Action);
        Assert.IsType<Swing>(_networking.Sent[1]);
    }

    private static byte[] Write(Interact packet)
    {
        using var stream = new MemoryStream();
        new InteractSerializer().SerializePacket(packet, new Vortex.Modules.Networking.Data.MinecraftBinaryWriter(stream));

        return stream.ToArray();
    }

    private void ConfirmDigging(Vortex.Modules.Networking.Abstraction.PacketBase packet)
    {
        if (packet is PlayerAction action)
            _sequencer.Confirm(action.Sequence);
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
