using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;

namespace HaveMoreKids;

internal static class Quirks
{
    internal static string NPC_CustomFields_KidIds => $"{ModEntry.ModId}/KidIds";
    internal static string MailFlag_Applied => $"{ModEntry.ModId}_Applied";
    internal static string Child_ModData_DisplayName => $"{ModEntry.ModId}/DisplayName";

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.ConsoleCommands.Add("hmk-reload_sprites", "Reload sprites for all kids", ConsoleReloadSprites);
        helper.ConsoleCommands.Add(
            "hmk-set_ages",
            "Set kids age and daysOld, need to sleep for this to work properly.",
            ConsoleAgeKids
        );
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (Game1.player.mailReceived.Contains(MailFlag_Applied))
            return;
        foreach (Child kid in Game1.player.getChildren())
            if (Game1.player.getSpouse() is NPC spouse && PickKidId(spouse) is string kidId)
                kid.Name = kidId;
        Game1.player.mailReceived.Add(MailFlag_Applied);
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
        if (arg2.Length < 1)
            return;
        int age = int.Parse(arg2[0]);
        foreach (Child kid in Game1.player.getChildren())
        {
            switch (age)
            {
                case 3:
                    kid.daysOld.Value = 55;
                    break;
                case 2:
                    kid.daysOld.Value = 27;
                    break;
                case 1:
                    kid.daysOld.Value = 13;
                    break;
            }
            kid.Age = age;
            kid.dayUpdate(Game1.dayOfMonth);
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

    internal static string[]? GetKidIds(NPC spouse)
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

    internal static string? PickKidId(NPC spouse)
    {
        if (GetKidIds(spouse) is not string[] kidIds)
            return null;
        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        string[] availableKidIds = kidIds.Where(id => !children.Contains(id)).ToArray();
        return availableKidIds[
            Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).Next(availableKidIds.Length)
        ];
    }
}
