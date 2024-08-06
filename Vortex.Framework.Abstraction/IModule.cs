using Autofac;

namespace Vortex.Framework.Abstraction;

public interface IModule
{
    void Load(ContainerBuilder builder);
    void Setup(IComponentContext context);
}
