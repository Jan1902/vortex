using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Player;

namespace Vortex.Modules.Player.Test;

public class DeathHandlingTests
{
    [Fact]
    public async Task RespawnsAfterDying()
    {
        var (handler, sent, player) = Create();

        await handler.HandleAsync(new SetHealth(0, 0, 0));

        // A dead player sits on the death screen and stops receiving chunks, so
        // respawning is what gets the bot its world back.
        var command = Assert.IsType<ClientCommand>(Assert.Single(sent));
        Assert.Equal(ClientCommandAction.PerformRespawn, command.ActionId);
        Assert.False(player.IsAlive);
    }

    [Fact]
    public async Task DoesNotRespawnWhileAlive()
    {
        var (handler, sent, player) = Create();

        await handler.HandleAsync(new SetHealth(7.5f, 12, 0));

        Assert.Empty(sent);
        Assert.True(player.IsAlive);
        Assert.Equal(7.5f, player.Health);
    }

    [Fact]
    public async Task DoesNotRespawnWhenTurnedOff()
    {
        var (handler, sent, _) = Create(autoRespawn: false);

        await handler.HandleAsync(new SetHealth(0, 0, 0));

        Assert.Empty(sent);
    }

    [Fact]
    public void StartsOutAlive()
        => Assert.True(Create().Player.IsAlive);

    private static (PlayerPacketHandler Handler, List<PacketBase> Sent, PlayerManager Player) Create(bool autoRespawn = true)
    {
        var networking = new FakeNetworking();
        var configuration = new VortexClientConfiguration { AutoRespawn = autoRespawn };

        var physics = new PlayerPhysics(new EmptyWorld());

        var player = new PlayerManager(
            NullLogger<PlayerManager>.Instance,
            networking,
            physics,
            new MovementController(
                new MovementPlans(new MovementSimulator(physics)),
                NullLogger<MovementController>.Instance));

        var handler = new PlayerPacketHandler(
            NullLogger<PlayerPacketHandler>.Instance,
            networking,
            configuration,
            player);

        return (handler, networking.Sent, player);
    }

    private sealed class FakeNetworking : INetworkingManager
    {
        public List<PacketBase> Sent { get; } = [];

        public Task Connect() => Task.CompletedTask;

        public Task ConnectAndWaitForPlay() => Task.CompletedTask;

        public Task SendPacket(PacketBase packet)
        {
            Sent.Add(packet);

            return Task.CompletedTask;
        }
    }

    private sealed class EmptyWorld : World.Abstraction.IWorldManager
    {
        public Shared.BlockState? GetBlock(Shared.Vector3i position) => null;

        public Shared.Chunk? GetChunk(Shared.Vector2i position) => null;
    }
}
