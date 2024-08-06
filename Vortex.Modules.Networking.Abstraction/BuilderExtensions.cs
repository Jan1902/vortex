using Autofac;

namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Extension methods for registering packet handlers.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Registers a packet handler of type <typeparamref name="THandler"/>.
    /// </summary>
    /// <typeparam name="THandler">The type of the packet handler.</typeparam>
    /// <param name="builder">The Autofac container builder.</param>
    public static void RegisterPacketHandler<THandler>(this ContainerBuilder builder)
        where THandler : class
    {
        builder.RegisterType<THandler>().AsImplementedInterfaces();
    }
}
