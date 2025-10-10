using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace HaveMoreKids.Framework;

public sealed class CPTokenKidNPC
{
    private static Dictionary<string, string>? KidIds = null;

    public bool IsMutable() => true;

    public bool IsDeterministicForInput() => false;

    public bool AllowsInput() => true;

    public bool RequiresInput() => false;

    public bool CanHaveMultipleValues(string input) => input == null;

    public bool TryValidateInput(string? input, [NotNullWhen(false)] out string? error)
    {
        error = null!;
        if (input != null && (KidIds == null || !KidIds.ContainsKey(input)))
        {
            error = $"Kid '{input}' not found";
            return false;
        }
        return true;
    }

    public bool UpdateContext()
    {
        if (!Context.IsWorldReady)
            return false;
        bool changed = KidIds == null || KidIds.Count != KidHandler.KidEntries.Count;
        Dictionary<string, string> newKidIds = [];
        foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
        {
            if (entry.KidNPCId != null)
            {
                newKidIds[kidId] = entry.KidNPCId;
            }
            if (
                !changed
                && (!(KidIds?.TryGetValue(kidId, out string? prevKidNPCId) ?? false) || prevKidNPCId != entry.KidNPCId)
            )
            {
                changed = true;
            }
        }
        KidIds = newKidIds;
        return changed;
    }

    public bool IsReady() => Context.IsWorldReady;

    public IEnumerable<string> GetValues(string? input)
    {
        if (input != null && KidIds!.TryGetValue(input, out string? kidNPCid))
        {
            yield return kidNPCid;
            yield break;
        }
        else
        {
            foreach (string kidId in KidIds!.Values)
            {
                yield return kidId;
            }
        }
    }
}

internal static class GameDelegates
{
    internal const string GSQ_CHILD_AGE = $"{ModEntry.ModId}_CHILD_AGE";
    internal const string GSQ_HAS_CHILD = $"{ModEntry.ModId}_HAS_CHILD";
    internal const string Action_SetNewChildEvent = $"{ModEntry.ModId}_SetNewChildEvent";
    internal const string Action_SetChildAge = $"{ModEntry.ModId}_SetChildAge";
    internal const string Stats_daysUntilNewChild = $"{ModEntry.ModId}_daysUntilNewChild";
    internal const string TS_Endearment = $"{ModEntry.ModId}_Endearment";
    internal const string CPT_KidDisplayName = "KidDisplayName";
    internal const string CPT_KidNPCId = "KidNPCId";

    internal static void Register(IManifest mod)
    {
        // GSQ
        GameStateQuery.Register(GSQ_CHILD_AGE, CHILD_AGE);
        GameStateQuery.Register(GSQ_HAS_CHILD, HAS_CHILD);
        // TAction
        TriggerActionManager.RegisterAction(Action_SetNewChildEvent, SetNewChildEvent);
        TriggerActionManager.RegisterAction(Action_SetChildAge, SetChildAge);
        // Tokenizable String
        TokenParser.RegisterParser(TS_Endearment, TSEndearment);
        // CP Tokens
        if (
            ModEntry.help.ModRegistry.GetApi<Integration.IContentPatcherAPI>("Pathoschild.ContentPatcher")
            is Integration.IContentPatcherAPI CP
        )
        {
            CP.RegisterToken(mod, CPT_KidDisplayName, CPTokenChildDisplayNames);
            CP.RegisterToken(mod, CPT_KidNPCId, new CPTokenKidNPC());
        }
    }

    private static bool TSEndearment(string[] query, out string replacement, Random random, Farmer player)
    {
        if (
            !ArgUtility.TryGet(query, 1, out string kidId, out string error, allowBlank: false, name: "string kidId")
            || !ArgUtility.TryGetOptionalBool(query, 2, out bool capitalize, out error, name: "bool capitalize")
        )
        {
            return TokenParser.LogTokenError(query, error, out replacement);
        }
        string? endearment;
        if (
            (
                endearment = Game1.content.LoadStringReturnNullIfNotFound(
                    string.Concat(
                        AssetManager.Asset_CharactersDialogue,
                        kidId,
                        ":Endearment_",
                        player.Gender.ToString()
                    )
                )
            )
            is not null
        )
        {
            replacement = endearment;
        }
        else if (
            (
                endearment = Game1.content.LoadStringReturnNullIfNotFound(
                    string.Concat(AssetManager.Asset_CharactersDialogue, kidId, ":Endearment")
                )
            )
            is not null
        )
        {
            replacement = endearment;
        }
        if (
            (
                endearment = Game1.content.LoadStringReturnNullIfNotFound(
                    string.Concat(
                        AssetManager.Asset_CharactersDialogue,
                        kidId,
                        ":Endearment_",
                        player.Gender.ToString()
                    )
                )
            )
                is null
            && (
                endearment = Game1.content.LoadStringReturnNullIfNotFound(
                    string.Concat(AssetManager.Asset_CharactersDialogue, kidId, ":Endearment")
                )
            )
                is null
        )
        {
            endearment = player.Gender switch
            {
                Gender.Male => AssetManager.LoadStringReturnNullIfNotFound("Endearment_Male")
                    ?? Game1.content.LoadString("Strings/Characters:Relative_Dad"),
                Gender.Female => AssetManager.LoadStringReturnNullIfNotFound("Endearment_Female")
                    ?? Game1.content.LoadString("Strings/Characters:Relative_Mom"),
                _ => AssetManager.LoadStringReturnNullIfNotFound("Endearment_Neutral") ?? player.displayName,
            };
        }
        if (endearment is null)
        {
            replacement = null!;
            return false;
        }
        replacement = endearment;
        if (capitalize)
        {
            replacement = Lexicon.capitalize(replacement);
        }
        return true;
    }

    private static IEnumerable<string>? CPTokenChildDisplayNames()
    {
        if (!Context.IsWorldReady)
            return null;
        return Game1.player.getChildren().Select(value => value.displayName);
    }

    private static Child? FindChild(Farmer player, string kidId, bool allowIdx = true)
    {
        List<Child> children = player.getChildren();
        if (allowIdx && kidId[0] == '#' && int.TryParse(kidId.AsSpan(1), out int index) && index < children.Count)
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
            switch (age)
            {
                case 4:
                    if (ModEntry.Config.DaysChild > -1)
                    {
                        kid.daysOld.Value = ModEntry.Config.TotalDaysChild;
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

    internal static bool HomeIsValidForPregnancy(FarmHouse homeOfFarmer, out string error)
    {
        error = null!;
        if (homeOfFarmer.upgradeLevel < 2)
        {
            error = "housing market in shambles";
            return false;
        }
        if (!CribManager.HasAvailableCribs(homeOfFarmer))
        {
            error = "no crib no baby";
            return false;
        }
        return true;
    }

    private static bool SetNewChildEvent(string[] args, TriggerActionContext context, out string error)
    {
        if (
            !ArgUtility.TryGetInt(args, 1, out int daysUntilNewChild, out error, name: "int daysUntilNewChild")
            || !ArgUtility.TryGetOptional(args, 2, out string? kidId, out error, name: "string? kidId")
            || !ArgUtility.TryGetOptional(args, 3, out string? spouseName, out error, name: "string? spouseName")
            || !ArgUtility.TryGetOptional(args, 4, out string? message, out error, name: "string? message")
        )
        {
            return false;
        }

        if (daysUntilNewChild < 0)
        {
            error = "daysUntilNewChild cannot be negative.";
            return false;
        }

        if (!HomeIsValidForPregnancy(Utility.getHomeOfFarmer(Game1.player), out error))
        {
            return false;
        }

        if (spouseName.EqualsIgnoreCase("Player"))
        {
            // solo adopt path
            // this is +1 because stats get unset at 0
            Game1.player.stats.Set(Stats_daysUntilNewChild, daysUntilNewChild + 1);
            KidHandler.TrySetNextAdoptFromNPCKidId(Game1.player, kidId);
        }
        else if (Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID) is long spouseId)
        {
            // player couple path
            Friendship friendship = Game1.player.team.GetFriendship(Game1.player.UniqueMultiplayerID, spouseId);
            WorldDate worldDate = new(Game1.Date);
            worldDate.TotalDays += daysUntilNewChild;
            friendship.NextBirthingDate = worldDate;
        }
        else
        {
            // npc spouse path
            NPC? spouse;
            if (string.IsNullOrEmpty(spouseName) || spouseName.EqualsIgnoreCase("Any"))
            {
                IEnumerable<NPC> spouses = SpouseShim.GetSpouses(Game1.player);
                if (!spouses.Any())
                {
                    error = "Player does not have a spouse";
                    return false;
                }
                spouse = Game1.random.ChooseFrom(spouses.ToList());
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

            SpouseShim.SetNPCNewChildDate(Game1.player, spouse, daysUntilNewChild);
            KidHandler.TrySetNextKid(spouse, kidId);
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
