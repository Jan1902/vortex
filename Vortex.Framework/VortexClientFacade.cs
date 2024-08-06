using Vortex.Framework.Abstraction;

namespace Vortex.Framework;

internal class VortexClientFacade(VortexClientConfiguration configuration) : IVortexClient
{
    private readonly VortexClientConfiguration _configuration = configuration;

    public async Task StartAsync()
    {
        
    }
}
