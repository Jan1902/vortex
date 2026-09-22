using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Entities;

/// <summary>Right-clicks an entity: shears a sheep, opens a villager's trades.</summary>
public class UseEntityTask(int entityId) : BotTask
{
    private const double Reach = 3.0;

    public override string Description
        => $"use entity {entityId}";

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        if (bot.Entities.Get(entityId) is not { } entity)
            return TaskResult.Failed($"entity {entityId} is gone");

        if ((bot.Player.Position + new Vector3d(0, Aim.EyeHeight, 0)).DistanceTo(entity.Center) > Reach)
        {
            var walked = await bot.Run(new GoNearTask(entity.Position.ToBlockPosition()));

            if (walked.IsFailure)
                return walked;
        }

        bot.Player.LookAt(entity.Center);
        await Task.Delay(50, bot.Cancellation);

        await bot.Interaction.InteractWithEntityAsync(entityId);

        return TaskResult.Success();
    }
}
