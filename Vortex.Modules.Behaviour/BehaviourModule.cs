using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour;

public class BehaviourModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        // Tasks are not registered: they are made with new, and get what they
        // work with from the Bot they are run with.
        builder.RegisterType<Bot>().AsSelf().SingleInstance();
        builder.RegisterType<BotBrain>().AsImplementedInterfaces().AsSelf().SingleInstance();
    }
}
