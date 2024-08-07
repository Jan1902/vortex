using Autofac;
using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Framework;

internal class VortexClientFacade(
    IComponentContext context,
    INetworkingConnection connection,
    ILogger<VortexClientFacade> logger,
    IChatManager chat) : IVortexClient
{
    public async Task StartAsync()
    {
        logger.LogInformation("Starting Vortex client...");

        logger.LogInformation("Initializing modules...");

        var toInit = context.Resolve<IEnumerable<IInitialize>>();

        foreach (var init in toInit)
            init.Initialize();

        var toInitAsync = context.Resolve<IEnumerable<IInitializeAsync>>();
        await Task.WhenAll(toInitAsync.Select(s => s.InitializeAsync()));

        logger.LogInformation("Initialization complete.");
        logger.LogInformation("Connecting to server...");

        await connection.Connect();

        // TODO: Wait until we entered play mode
    }

    public Task SendChatMessage(string message)
            => chat.SendMessage(message);
}
