using Autofac;

namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents a module that can be loaded into the vortex client
/// </summary>
public interface IModule
{
    /// <summary>
    /// Loads the module into the vortex client.
    /// </summary>
    /// <param name="builder">The container builder.</param>
    void Load(ContainerBuilder builder);
}
