using Autofac;
using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Framework;

internal class VortexClientFacade(
    IComponentContext context,
    INetworkingManager connection,
    IChatManager chat,
    IWorldManager world,
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

    private void SetupEventPassThroughs()
    {
        eventBus.RegisterProxyHandler<ChatMessageReceivedEvent, ChatMessageReceivedEventArgs>(ChatMessageReceived, (e) => new(e.Message));
    }

    public Task SendChatMessage(string message)
        => chat.SendMessage(message);

    public BlockState? GetBlock(Vector3i position)
        => world.GetBlock(position);

    public Chunk? GetChunk(Vector2i position)
        => world.GetChunk(position);
}
