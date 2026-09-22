using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Crafting;

internal class CraftingPacketHandler(CraftingManager crafting) : IPacketHandler<UpdateRecipeBook>
{
    public Task HandleAsync(UpdateRecipeBook packet)
    {
        crafting.UpdateRecipeBook(packet.Action, packet.Recipes);

        return Task.CompletedTask;
    }
}
