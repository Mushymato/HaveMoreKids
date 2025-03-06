using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids;

internal static class AssetManager
{
    private static string NPC_CustomFields_KidIds => $"{ModEntry.ModId}/KidIds";
    private static string Asset_ChildData => $"{ModEntry.ModId}/ChildData";
    private static Dictionary<string, CharacterData>? childData = null;
    internal static Dictionary<string, CharacterData> ChildData =>
        childData ??= Game1.content.Load<Dictionary<string, CharacterData>>(Asset_ChildData);

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_ChildData))
            e.LoadFrom(() => new Dictionary<string, CharacterData>(), AssetLoadPriority.Low);
    }

    private static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_ChildData)))
        {
            childData = null;
            ModEntry.Config.ResetMenu();
        }
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data/Characters")))
        {
            ModEntry.Config.ResetMenu();
        }
    }

    /// <summary>Get and validate kid id</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    internal static string[]? GetKidIds(CharacterData? data)
    {
        if (data?.CustomFields?.TryGetValue(NPC_CustomFields_KidIds, out string? kidIdsString) ?? false)
        {
            var kidIds = kidIdsString.Split(',').Where(ChildData.ContainsKey);
            data.CustomFields[NPC_CustomFields_KidIds] = string.Join(',', kidIds);
            return kidIds.ToArray();
        }
        return null;
    }

    /// <summary>Choose a new kid Id. If originalName is given, only choose new one if the existing pick is invalid for the specific spouse</summary>
    /// <param name="spouse"></param>
    /// <param name="originalName"></param>
    /// <returns></returns>
    internal static string? PickKidId(NPC spouse, string? originalName = null, bool newBorn = false)
    {
        if (GetKidIds(spouse.GetData()) is not string[] kidIds)
            return null;
        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        if (!newBorn && originalName != null && kidIds.Contains(originalName))
            return null;
        string[] availableKidIds = kidIds
            .Where(id =>
                !children.Contains(id) && !ModEntry.Config.DisabledKids.GetValueOrDefault(new(spouse.Name, id))
            )
            .ToArray();
        if (availableKidIds.Length == 0)
            return null;
        // Prioritize "real name" if found
        if (originalName != null)
        {
            foreach (var kidId in availableKidIds)
            {
                if (
                    ChildData.TryGetValue(kidId, out CharacterData? data)
                    && TokenParser.ParseText(data.DisplayName) == originalName
                )
                {
                    return kidId;
                }
            }
        }
        return availableKidIds[
            Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).Next(availableKidIds.Length)
        ];
    }
}
