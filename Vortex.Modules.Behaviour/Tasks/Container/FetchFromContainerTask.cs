using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Container;

/// <summary>
/// Takes items out of the container at a position until the bot carries enough
/// of them, or the container has none left.
/// </summary>
/// <remarks>
/// One stack is moved per round, so that the count is looked at again between
/// stacks rather than emptying the chest of something only a few were wanted of.
/// </remarks>
public class FetchFromContainerTask(
    Vector3i container,
    ItemRequest request,
    IInventoryManager inventory,
    Func<Vector3i, OpenContainerTask> open) : BotTask
{
    public override string Description
        => $"fetch {request} from the container at {container.X} {container.Y} {container.Z}";

    public override bool IsSatisfied()
        => request.CountIn(inventory.Count) >= request.Count;

    public override IEnumerable<BotTask> Dependencies()
    {
        yield return open(container);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (inventory.OpenContainer is not { } window)
            return TaskResult.Failed($"the container at {container.X} {container.Y} {container.Z} is not open");

        var slot = Enumerable.Range(0, window.ContainerSize)
            .FirstOrDefault(slot => window.Slots[slot] is { } stack && request.Items.Contains(stack.Item), -1);

        if (slot < 0)
            return TaskResult.Failed($"the container at {container.X} {container.Y} {container.Z} has no {request} left");

        var before = request.CountIn(inventory.Count);

        await inventory.QuickMoveAsync(slot);

        await inventory.CloseContainerAsync();

        return request.CountIn(inventory.Count) > before
            ? TaskResult.Success()
            : TaskResult.Failed($"no room for {window.Slots[slot]!.Item}");
    }
}
