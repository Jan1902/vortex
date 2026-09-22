using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Crafting;

/// <summary>What an <see cref="UpdateRecipeBook"/> does to the recipes it lists.</summary>
public enum RecipeBookAction
{
    /// <summary>Replaces the recipe book, as at login.</summary>
    Init = 0,

    /// <summary>Unlocks recipes.</summary>
    Add = 1,

    /// <summary>Takes recipes away.</summary>
    Remove = 2
}

/// <summary>
/// Changes which recipes the player has unlocked.
/// </summary>
/// <remarks>
/// On <see cref="RecipeBookAction.Init"/> a second list follows, of recipes to
/// highlight as new. It is not needed and, being last, simply not read.
/// </remarks>
[AutoSerializedPacket(PacketIds.Play.ClientBound.Recipe)]
public record UpdateRecipeBook(
    RecipeBookAction Action,
    bool CraftingBookOpen,
    bool CraftingBookFiltering,
    bool SmeltingBookOpen,
    bool SmeltingBookFiltering,
    bool BlastFurnaceBookOpen,
    bool BlastFurnaceBookFiltering,
    bool SmokerBookOpen,
    bool SmokerBookFiltering,
    string[] Recipes) : PacketBase;

/// <summary>Asks the server to lay out a recipe from the recipe book.</summary>
[AutoSerializedPacket(PacketIds.Play.ServerBound.PlaceRecipe, packetDirection: PacketDirection.ServerBound)]
public record PlaceRecipe(byte WindowId, string Recipe, bool MakeAll) : PacketBase;
