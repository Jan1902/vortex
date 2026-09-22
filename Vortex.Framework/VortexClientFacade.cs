using Autofac;
using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Framework;

internal class VortexClientFacade(
    IComponentContext context,
    INetworkingManager connection,
    IChatManager chat,
    IWorldManager world,
    IPlayerManager player,
    IMovementController movement,
    IBotBrain brain,
    IEntityManager entities,
    IInventoryManager inventory,
    IInteractionManager interaction,
    ICraftingManager crafting,
    ILogger<VortexClientFacade> logger,
    EventBus eventBus) : IVortexClient
{
    public event AsyncEventHandler<ChatMessageReceivedEventArgs>? ChatMessageReceived;

    public async Task StartAsync()
    {
        logger.LogInformation("Starting Vortex client...");

        logger.LogInformation("Initializing modules...");

        var toInit = context.Resolve<IEnumerable<IInitialize>>();

        foreach (var init in toInit)
            init.Initialize();

        var toInitAsync = context.Resolve<IEnumerable<IInitializeAsync>>();
        await Task.WhenAll(toInitAsync.Select(s => s.InitializeAsync()));

        SetupEventPassThroughs();

        logger.LogInformation("Initialization complete.");
        logger.LogInformation("Connecting to server...");

        await connection.ConnectAndWaitForPlay();
    }

    public async Task StopAsync()
    {
        logger.LogInformation("Stopping Vortex client...");

        brain.Cancel();

        await connection.Disconnect();
    }

    private void SetupEventPassThroughs()
    {
        eventBus.RegisterProxyHandler<ChatMessageReceivedEvent, ChatMessageReceivedEventArgs>(ChatMessageReceived, (e) => new(
            e.Message,
            e.Sender,
            e.Sender is { } sender ? entities.GetPlayer(sender)?.Name : null));
    }

    public Task SendChatMessage(string message)
        => chat.SendMessage(message);

    public BlockState? GetBlock(Vector3i position)
        => world.GetBlock(position);

    public Chunk? GetChunk(Vector2i position)
        => world.GetChunk(position);

    public Vector3d Position
        => player.Position;

    public bool IsOnGround
        => player.IsOnGround;

    public void LookAt(Vector3d target)
        => player.LookAt(target);

    public IMovementController Movement
        => movement;

    public IBotBrain Brain
        => brain;

    public IEntityManager Entities
        => entities;

    public IInventoryManager Inventory
        => inventory;

    public IInteractionManager Interaction
        => interaction;

    public ICraftingManager Crafting
        => crafting;
}
