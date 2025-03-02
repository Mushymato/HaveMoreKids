using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;

namespace HaveMoreKids;

internal static class Quirks
{
    internal static string NPC_CustomFields_KidIds => $"{ModEntry.ModId}/KidIds";

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.ConsoleCommands.Add("hmk-reload_sprites", "Reload sprites for all kids", ConsoleReloadSprites);
        helper.ConsoleCommands.Add("hmk-age_up", "Age all kids to 3", ConsoleAgeKids);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        foreach (Child kid in Game1.player.getChildren())
        {
            kid.reloadSprite();
        }
    }

    private static void ConsoleAgeKids(string arg1, string[] arg2)
    {
        if (!Context.IsWorldReady)
            return;
        foreach (Child kid in Game1.player.getChildren())
        {
            if (kid.Age < 3)
                kid.daysOld.Value = 55;
        }
    }

    private static void ConsoleReloadSprites(string arg1, string[] arg2)
    {
        if (!Context.IsWorldReady)
            return;
        foreach (Child kid in Game1.player.getChildren())
        {
            ModEntry.Log($"Child({kid.Name}).reloadSprite");
            kid.reloadSprite();
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Characters"))
        {
            e.Edit(FixHaveMoreKidsCharacters, AssetEditPriority.Late + 100);
        }
    }

    private static void FixHaveMoreKidsCharacters(IAssetData asset)
    {
        var data = asset.AsDictionary<string, CharacterData>().Data;
        foreach (var kv in data)
        {
            if (kv.Key.StartsWith(ModEntry.ModId))
            {
                kv.Value.UnlockConditions = "FALSE";
                kv.Value.SpawnIfMissing = false;
            }
        }
    }

    internal static string[]? GetKidIds(this NPC spouse)
    {
        CharacterData? data = spouse.GetData();
        if (data?.CustomFields?.TryGetValue(NPC_CustomFields_KidIds, out string? kidIdsString) ?? false)
        {
            var res = kidIdsString
                .Split(',')
                .Where(kidId => Game1.characterData.ContainsKey($"{ModEntry.ModId}_{kidId}"))
                .ToArray();

            string fixedRes = string.Join(',', res);
            data.CustomFields[NPC_CustomFields_KidIds] = fixedRes;
            ModEntry.LogOnce($"{spouse.Name}: {fixedRes}");
            return res;
        }
        return null;
    }
}
