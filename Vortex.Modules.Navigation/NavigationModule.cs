using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Navigation.Abstraction;

namespace Vortex.Modules.Navigation;

public class NavigationModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AStarPathfinder>().AsImplementedInterfaces().SingleInstance();

        // What a task gets when it does not ask for anything in particular.
        // Walking only, because every other ability has a cost or a risk that
        // the caller should have to opt into.
        builder.RegisterInstance(MovementCapabilities.Walking).AsSelf();
    }
}
