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
        builder.RegisterType<CollectItemsTask>().AsSelf();
        builder.RegisterType<UseBlockTask>().AsSelf();
        builder.RegisterType<MineBlockTask>().AsSelf();
        builder.RegisterType<HarvestBlockTask>().AsSelf();
        builder.RegisterType<OpenContainerTask>().AsSelf();
        builder.RegisterType<TakeItemsTask>().AsSelf();
        builder.RegisterType<StoreItemsTask>().AsSelf();
        builder.RegisterType<PlaceBlockTask>().AsSelf();
        builder.RegisterType<CraftTask>().AsSelf();
        builder.RegisterType<AttackTask>().AsSelf();
        builder.RegisterType<UseEntityTask>().AsSelf();
    }
}
