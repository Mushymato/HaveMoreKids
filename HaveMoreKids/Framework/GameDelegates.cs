using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace HaveMoreKids.Framework;

internal static class GameDelegates
{
    internal const string GSQ_CHILD_AGE = $"{ModEntry.ModId}_CHILD_AGE";
    internal const string GSQ_HAS_CHILD = $"{ModEntry.ModId}_HAS_CHILD";
    internal const string Action_SetChildBirth = $"{ModEntry.ModId}_SetChildBirth";
    internal const string Action_SetChildAge = $"{ModEntry.ModId}_SetChildAge";
    internal const string Stats_daysUntilBirth = $"{ModEntry.ModId}_daysUntilBirth";

    internal static void Register(IManifest mod)
    {
        // delegates
        GameStateQuery.Register(GSQ_CHILD_AGE, CHILD_AGE);
        GameStateQuery.Register(GSQ_HAS_CHILD, HAS_CHILD);
        TriggerActionManager.RegisterAction(Action_SetChildBirth, SetChildBirth);
        TriggerActionManager.RegisterAction(Action_SetChildAge, SetChildAge);
        // Register content patcher tokens for getting the display names of kids
        if (
            ModEntry.help.ModRegistry.GetApi<Integration.IContentPatcherAPI>("Pathoschild.ContentPatcher")
            is Integration.IContentPatcherAPI CP
        )
        {
            CP.RegisterToken(mod, "ChildDisplayName", CPTokenChildDisplayNames);
        }
    }

    private static IEnumerable<string>? CPTokenChildDisplayNames() =>
        Context.IsWorldReady ? Game1.player.getChildren().Select(child => child.displayName) : null;

    private static Child? FindChild(Farmer player, string kidId)
    {
        List<Child> children = player.getChildren();
        if (kidId[0] == '#' && int.TryParse(kidId.AsSpan(1), out int index) && index < children.Count)
            return children[index];
        return children.FirstOrDefault(child => child.Name == kidId);
    }

    private static bool HAS_CHILD(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out string kidId, out string error))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        return FindChild(context.Player, kidId) != null;
    }

    private static bool CHILD_AGE(string[] query, GameStateQueryContext context)
    {
        if (
            !ArgUtility.TryGet(query, 1, out string kidId, out string error, name: "int kidId")
            || !ArgUtility.TryGetInt(query, 2, out int ageMin, out error, name: "int ageMin")
            || !ArgUtility.TryGetOptionalInt(
                query,
                3,
                out int ageMax,
                out error,
                defaultValue: ageMin,
                name: "int ageMax"
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        int age = FindChild(context.Player, kidId)?.Age ?? 0;
        return age >= ageMin && age <= ageMax;
    }

    private static bool SetChildAge(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGet(args, 1, out string kidId, out error)
            || !ArgUtility.TryGetInt(args, 2, out int age, out error)
        )
        {
            return false;
        }
        if (age < 0 || age > 4)
        {
            error = $"Age must be between 0 and 4, got '{age}'";
            return false;
        }
        if (FindChild(Game1.player, kidId) is Child kid)
        {
            int totalDaysChild;
            switch (age)
            {
                case 4:
                    if ((totalDaysChild = ModEntry.Config.TotalDaysChild) > -1)
                    {
                        kid.daysOld.Value = totalDaysChild;
                        return true;
                    }
                    goto case 3;
                case 3:
                    kid.daysOld.Value = ModEntry.Config.TotalDaysToddler;
                    return true;
                case 2:
                    kid.daysOld.Value = ModEntry.Config.TotalDaysCrawer;
                    return true;
                case 1:
                    kid.daysOld.Value = ModEntry.Config.TotalDaysBaby;
                    return true;
                case 0:
                    kid.daysOld.Value = 0;
                    kid.Age = 0;
                    return true;
            }
        }
        return false;
    }

    private static bool SetChildBirth(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGetInt(args, 1, out int daysUntilBirth, out error, name: "int daysUntilBirth")
            || !ArgUtility.TryGetOptional(args, 2, out string? kidId, out error, name: "string? kidId")
            || !ArgUtility.TryGetOptional(args, 3, out string? spouseName, out error, name: "string? spouseName")
            || !ArgUtility.TryGetOptional(args, 4, out string? message, out error, name: "string? message")
        )
        {
            return false;
        }

        if (!Game1.player.getChildren().All(child => child.Age > 2))
        {
            error = "Crib is currently occupied, all children must be age 3/toddler before you can have more kids";
            return false;
        }

        if (daysUntilBirth < 0)
        {
            error = "daysUntilBirth cannot be negative.";
            return false;
        }

        if (Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID) is long spouseId)
        {
            // player couple path
            Friendship friendship = Game1.player.team.GetFriendship(Game1.player.UniqueMultiplayerID, spouseId);
            WorldDate worldDate = new(Game1.Date);
            worldDate.TotalDays += daysUntilBirth;
            friendship.NextBirthingDate = worldDate;
        }
        else if (spouseName.EqualsIgnoreCase("Player"))
        {
            // solo adopt path
            // this is +1 because stats get unset at 0
            Game1.player.stats.Set(Stats_daysUntilBirth, daysUntilBirth + 1);
        }
        else
        {
            // npc spouse path
            NPC? spouse;
            if (string.IsNullOrEmpty(spouseName) || spouseName.EqualsIgnoreCase("Any"))
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

            if (!string.IsNullOrEmpty(kidId) && kidId.EqualsIgnoreCase("Any"))
            {
                KidHandler.TrySetNextKid(spouse, kidId);
            }
        }

        if (message != null)
        {
            string? parsedMessage = null;
            if (Game1.content.IsValidTranslationKey(message))
                parsedMessage = Game1.content.LoadString(message);
            else
                parsedMessage = TokenParser.ParseText(message);
            if (parsedMessage != null)
            {
                Game1.addHUDMessage(new HUDMessage(parsedMessage) { noIcon = true });
            }
        }

        return true;
    }
}
