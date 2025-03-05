using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;

namespace HaveMoreKids;

internal static class Quirks
{
    internal static string Child_ModData_KidId => $"{ModEntry.ModId}/KidId";

    internal static void Register(IModHelper helper)
    {
        // events
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        // console commands
        helper.ConsoleCommands.Add(
            "hmk-unset_kids",
            "Unset the internal names for unique kids, use this if you want to uninstall this mod completely.",
            ConsoleUnsetKids
        );
        helper.ConsoleCommands.Add(
            "hmk-set_ages",
            "Set kids age and daysOld, need to sleep for this to work properly.",
            ConsoleAgeKids
        );
    }

    /// <summary>Apply unique kids to any existing children</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    ///
    private static void ConsoleUnsetKids(string arg1, string[] arg2)
    {
        foreach (Child kid in Game1.player.getChildren())
        {
            if (kid.modData?.TryGetValue(Patches.Child_ModData_DisplayName, out string? displayName) ?? false)
            {
                ModEntry.Log($"Unset '{displayName}' ({kid.Name})", LogLevel.Info);
                kid.modData.Remove(Patches.Child_ModData_DisplayName);
                kid.Name = displayName;
                kid.reloadSprite(onlyAppearance: true);
            }
        }
    }

    /// <summary>Apply unique kids to any existing children</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        foreach (Child kid in Game1.player.getChildren())
            if (Game1.player.getSpouse() is NPC spouse && AssetManager.PickKidId(spouse, kid.Name) is string kidId)
            {
                kid.Name = kidId;
                if (kid.GetData() is not CharacterData data)
                {
                    ModEntry.Log(
                        $"Failed to get data for child ID '{kidId}', '{kid.displayName}' may be broken.",
                        LogLevel.Error
                    );
                    continue;
                }
                kid.Gender = data.Gender;
                ModEntry.Log($"Assigned '{kidId}' to child named '{kid.displayName}'.");
            }
    }

    /// <summary>Do reload sprite on day started, to properly deal with newborns (y r they like this)</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        foreach (Child kid in Game1.player.getChildren())
            kid.reloadSprite();
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
            if (kid.Age != age)
            {
                switch (age)
                {
                    case 3:
                        kid.daysOld.Value = ModEntry.Config.DaysToddler;
                        break;
                    case 2:
                        kid.daysOld.Value = ModEntry.Config.DaysCrawler;
                        break;
                    case 1:
                        kid.daysOld.Value = ModEntry.Config.DaysBaby;
                        break;
                }
            }
        }
    }
}
