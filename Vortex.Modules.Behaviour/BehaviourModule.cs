using Autofac;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour.Knowledge;
using Vortex.Modules.Behaviour.Sources;
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
        builder.RegisterType<ObtainItemsTask>().AsSelf();

        // What the bot knows about the world beyond what it can see right now.
        builder.RegisterType<FailureMemory>().AsSelf().SingleInstance();

        // Where items can come from. Which of them a task tree may use, and in
        // what order, is the running policy's call, not the registration order.
        builder.RegisterType<CraftSource>().As<IItemSource>();
    }
}
