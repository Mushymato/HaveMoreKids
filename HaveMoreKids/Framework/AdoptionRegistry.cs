using Microsoft.Xna.Framework;
using StardewValley;

namespace HaveMoreKids.Framework;

internal static class AdoptionRegistry
{
    internal const string Action_ShowAdoption = $"{ModEntry.ModId}_ShowAdoption";

    internal static void Register()
    {
        GameLocation.RegisterTileAction(Action_ShowAdoption, TileShowAdoption);
    }

    private static bool TileShowAdoption(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        // List<KeyValuePair<string, string>> responses = [AssetManager.LoadString("Adoption_Prompt")];
        // location.ShowPagedResponses(AssetManager.LoadString("Adoption_Prompt"), responses, OnResponse);
        return false;
    }

    private static void OnResponse(string obj)
    {
        throw new NotImplementedException();
    }
}
