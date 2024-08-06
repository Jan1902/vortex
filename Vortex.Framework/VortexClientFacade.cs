using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Framework;

internal class VortexClientFacade(VortexClientConfiguration configuration, INetworkingConnection connection) : IVortexClient
{
    private readonly VortexClientConfiguration _configuration = configuration;

    public async Task StartAsync()
    {
        await connection.Connect();
    }
}
