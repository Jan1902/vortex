using Autofac;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour.Tasks;

namespace Vortex.Modules.Behaviour;

public class BehaviourModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TaskRunner>().AsSelf().SingleInstance();
        builder.RegisterType<BotBrain>().AsImplementedInterfaces().AsSelf().SingleInstance();

        // Tasks are resolved, not constructed, so that they can take managers and
        // factories for their own dependencies through the constructor. Register
        // them as themselves: a task is asked for by its concrete type, either as
        // a delegate factory (Func<Vector3i, WithinReachTask>) or through
        // IBotBrain.CreateTask.
        builder.RegisterType<WithinReachTask>().AsSelf();
        builder.RegisterType<GoToTask>().AsSelf();
    }
}
