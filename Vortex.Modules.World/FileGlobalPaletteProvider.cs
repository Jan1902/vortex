using Newtonsoft.Json.Linq;
using Vortex.Shared;

namespace Vortex.Modules.World;

internal class FileGlobalPaletteProvider : IGlobalPaletteProvider
{
    private readonly Dictionary<int, BlockState> _idToState = [];

    public FileGlobalPaletteProvider() => Load();

    private void Load()
    {
        var blocksJson = ResourceHelper.ReadResource("blocks.json");
        var json = JObject.Parse(blocksJson);

        foreach (var block in json.Properties())
        {
            var states = ((JObject)block.Value)["states"];
            if (states is null)
                continue;

            foreach (var state in states)
            {
                var id = (int?)state["id"];
                if (id is null)
                    continue;

                var blockState = new BlockState(id.Value, block.Name);
                _idToState[id.Value] = blockState;
            }
        }
    }

    public BlockState GetStateFromId(int id)
        => _idToState[id];
}