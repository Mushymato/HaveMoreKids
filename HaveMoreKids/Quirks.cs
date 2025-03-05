using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;

namespace HaveMoreKids;

internal static class Quirks
{
    internal static void Register(IModHelper helper)
    {
        // events
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        // console commands
        helper.ConsoleCommands.Add("hmk-reload_sprites", "Reload sprites for all kids", ConsoleReloadSprites);
        helper.ConsoleCommands.Add(
            "hmk-set_ages",
            "Set kids age and daysOld, need to sleep for this to work properly.",
            ConsoleAgeKids
        );
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
}
