using Autofac;
using Serilog;
using Serilog.Extensions.Autofac.DependencyInjection;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Chat;
using Vortex.Modules.Entities;
using Vortex.Modules.Navigation;
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
            typeof(WorldModule),
            typeof(EntitiesModule),
            typeof(NavigationModule),
            typeof(BehaviourModule)
        ];

    private readonly List<Type> _loadedTasks = [];

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
    /// Turns on debug logging, which is where the task runner writes out what it
    /// is trying to achieve, which precondition it settled on and how each step
    /// turned out.
    /// </summary>
    /// <returns>The current instance of <see cref="VortexClientBuilder"/>.</returns>
    public VortexClientBuilder WithVerboseLogging()
    {
        _configuration.VerboseLogging = true;

        return this;
    }

    /// <summary>
    /// Turns on protocol logging on top of <see cref="WithVerboseLogging"/>,
    /// including a line for every packet the client has no definition for.
    /// </summary>
    /// <remarks>
    /// Useful while working on the protocol. In the play state this is several
    /// lines per tick, so nothing else can be followed alongside it.
    /// </remarks>
    /// <returns>The current instance of <see cref="VortexClientBuilder"/>.</returns>
    public VortexClientBuilder WithProtocolLogging()
    {
        _configuration.ProtocolLogging = true;

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
    /// Registers a task so that it can be resolved with its managers filled in,
    /// either through an injected <c>Func&lt;..., TTask&gt;</c> factory or
    /// through <see cref="IBotBrain.CreateTask{TTask}"/>. Tasks that ship with
    /// Vortex are registered already; this is for your own.
    /// </summary>
    /// <typeparam name="TTask">The type of the task to register.</typeparam>
    /// <returns>The current instance of <see cref="VortexClientBuilder"/>.</returns>
    public VortexClientBuilder AddTask<TTask>() where TTask : BotTask
    {
        _loadedTasks.Add(typeof(TTask));

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

        if (_configuration.ProtocolLogging)
            loggerConfiguration.MinimumLevel.Verbose();
        else if (_configuration.VerboseLogging)
            loggerConfiguration.MinimumLevel.Debug();

        containerBuilder.RegisterSerilog(loggerConfiguration);

        containerBuilder.RegisterType<VortexClientFacade>().AsImplementedInterfaces();
        containerBuilder.RegisterInstance(_configuration);
        containerBuilder.RegisterType<EventBus>().SingleInstance().AsSelf().AsImplementedInterfaces();

        foreach (var module in _loadedModules)
        {
            var instance = (IModule)Activator.CreateInstance(module)!;
            instance.Load(containerBuilder);
        }

        foreach (var task in _loadedTasks)
            containerBuilder.RegisterType(task).AsSelf();

        var container = containerBuilder.Build();

        return container.Resolve<IVortexClient>();
    }
}