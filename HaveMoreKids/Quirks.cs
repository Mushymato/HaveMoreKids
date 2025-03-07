using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace HaveMoreKids;

internal static class Quirks
{
    internal static string GSQ_ChildAge => $"{ModEntry.ModId}_CHILD_AGE";
    internal static string Action_SetChildBirth => $"{ModEntry.ModId}_SetChildBirth";

    internal static void Register(IModHelper helper)
    {
        // events
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        // delegates
        GameStateQuery.Register(GSQ_ChildAge, CHILD_AGE);
        TriggerActionManager.RegisterAction(Action_SetChildBirth, SetChildBirth);
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

    private static bool CHILD_AGE(string[] query, GameStateQueryContext context)
    {
        if (
            !ArgUtility.TryGet(query, 1, out string kidId, out string error)
            || !ArgUtility.TryGetInt(query, 2, out int age, out error)
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        return context.Player.getChildren().FirstOrDefault(child => child.Name == kidId)?.Age == age;
    }

    private static bool SetChildBirth(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGetInt(args, 1, out int daysUntilBirth, out error, name: "int daysUntilBirth")
            || !ArgUtility.TryGetOptional(args, 2, out string? kidId, out error, name: "string? kidId")
            || !ArgUtility.TryGetOptional(args, 3, out string? spouseName, out error, name: "string? spouseName")
        )
        {
            return false;
        }

        NPC spouse;
        if (string.IsNullOrEmpty(spouseName))
        {
            if ((spouse = Game1.player.getSpouse()) == null)
            {
                error = "Player does not have a spouse";
                return false;
            }
        }
        else
        {
            if ((spouse = Game1.getCharacterFromName(spouseName)) == null)
            {
                error = $"{spouseName} is not an NPC";
                return false;
            }
            if (spouse.getSpouse() != Game1.player)
            {
                error = $"{spouse.Name} is not the player's spouse";
                return false;
            }
        }

        WorldDate worldDate = new(Game1.Date);
        worldDate.TotalDays += daysUntilBirth;
        Game1.player.GetSpouseFriendship().NextBirthingDate = worldDate;

        if (!string.IsNullOrEmpty(kidId) && AssetManager.ChildData.ContainsKey(kidId))
        {
            spouse.modData[AssetManager.NPC_ModData_NextKidId] = kidId;
        }

        return true;
    }

    /// <summary>Apply unique kids to any existing children</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    ///
    private static void ConsoleUnsetKids(string arg1, string[] arg2)
    {
        foreach (Child kid in Game1.player.getChildren())
        {
            if (kid.modData?.TryGetValue(AssetManager.Child_ModData_DisplayName, out string? displayName) ?? false)
            {
                ModEntry.Log($"Unset '{displayName}' ({kid.Name})", LogLevel.Info);
                kid.modData.Remove(AssetManager.Child_ModData_DisplayName);
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
            if (Game1.player.getSpouse() is NPC spouse)
            {
                AssetManager.ChooseAndApplyKidId(spouse, kid, false);
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
