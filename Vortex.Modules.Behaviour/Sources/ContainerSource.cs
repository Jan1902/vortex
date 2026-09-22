using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Knowledge;
using Vortex.Modules.Behaviour.Tasks.Container;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Sources;

/// <summary>
/// Gets items out of containers the bot has seen them in, nearest first.
/// </summary>
/// <remarks>
/// Goes by <see cref="ContainerMemory"/>, so only by what was there when the bot
/// last looked. A container that has been emptied since is found out by
/// opening it, which brings the memory up to date and the offer to an end.
/// </remarks>
internal class ContainerSource(
    ContainerMemory containers,
    IWorldManager world,
    IPlayerManager player,
    Func<Vector3i, ItemRequest, FetchFromContainerTask> fetch) : IItemSource
{
    public ItemSourceKind Kind => ItemSourceKind.Container;

    public IEnumerable<ItemSourceOption> Options(ItemRequest request, ObtainChain chain)
    {
        var holding = containers.Known
            .Where(known => known.Contents.Any(stack => request.Items.Contains(stack.Item)))
            .Select(known => known.Position)
            .OrderBy(position => new Vector3d(position.X + 0.5, position.Y, position.Z + 0.5).DistanceTo(player.Position))
            .ToList();

        foreach (var position in holding)
        {
            // Broken or taken away since: nothing to open any more.
            if (world.GetBlock(position)?.Block is Block.Air or Block.CaveAir)
            {
                containers.Forget(position);
                continue;
            }

            yield return new ItemSourceOption(
                $"container {position.X} {position.Y} {position.Z} for {request.Items.Order().First()}",
                fetch(position, request));
        }
    }
}
