using Autofac;
using Serilog;
using Serilog.Extensions.Autofac.DependencyInjection;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Chat;
using Vortex.Modules.Networking;
using Vortex.Modules.Player;
using Vortex.Modules.World;

namespace Vortex.Framework;

/// <summary>
/// Builder class for creating an instance of <see cref="IVortexClient"/>.
/// </summary>
public class VortexClientBuilder
{
    private readonly VortexClientConfiguration _configuration = new();
    private readonly List<Type> _loadedModules =
        [
            typeof(NetworkingModule),
            typeof(ChatModule),
            typeof(PlayerModule),
            typeof(WorldModule)
        ];

    /// <summary>
    /// Sets the hostname and port to connect to.
    /// </summary>
    /// <param name="hostname">The hostname to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <returns>The current instance of <see cref="VortexClientBuilder"/>.</returns>
    public VortexClientBuilder ConnectTo(string hostname, int port)
    {
        _configuration.Hostname = hostname;
        _configuration.Port = port;

        return this;
    }

    /// <summary>
    /// Adds a module of type <typeparamref name="TModule"/> to the client.
    /// </summary>
    /// <typeparam name="TModule">The type of the module to add.</typeparam>
    /// <returns>The current instance of <see cref="VortexClientBuilder"/>.</returns>
    public VortexClientBuilder AddModule<TModule>() where TModule : IModule
    {
        _loadedModules.Add(typeof(TModule));

        return this;
    }

    /// <summary>
    /// Builds an instance of <see cref="IVortexClient"/> using the configured settings.
    /// </summary>
    /// <returns>An instance of <see cref="IVortexClient"/>.</returns>
    public IVortexClient Build()
    {
        var containerBuilder = new ContainerBuilder();

        var loggerConfiguration = new LoggerConfiguration()
            .WriteTo.Console();

        containerBuilder.RegisterSerilog(loggerConfiguration);

        containerBuilder.RegisterType<VortexClientFacade>().AsImplementedInterfaces();
        containerBuilder.RegisterInstance(_configuration);
        containerBuilder.RegisterType<EventBus>().SingleInstance().AsImplementedInterfaces();

        foreach (var module in _loadedModules)
        {
            var instance = (IModule)Activator.CreateInstance(module)!;
            instance.Load(containerBuilder);
        }

        var container = containerBuilder.Build();

        return container.Resolve<IVortexClient>();
    }
}