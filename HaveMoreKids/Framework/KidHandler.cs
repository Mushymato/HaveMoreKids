using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal sealed record KidEntry(
    string? KidNPCId,
    bool IsAdoptedFromNPC,
    long PlayerParent,
    string? OtherParent,
    Season BirthSeason,
    int BirthDay
)
{
    internal string DisplayName { get; set; } = "kid";
};

internal static class KidHandler
{
    private const string Appearances_Prefix_Baby = "HMK_BABY";
    private const string Child_ModData_Id = $"{ModEntry.ModId}/Id";
    private const string Child_ModData_DisplayName = $"{ModEntry.ModId}/DisplayName";
    private const string Child_ModData_NPCParent = $"{ModEntry.ModId}/NPCParent";
    private const string FL_ModData_OtherParent = "aedenthorn.FreeLove/OtherParent";
    internal const string Character_ModData_NextKidId = $"{ModEntry.ModId}/NextKidId";
    internal const string WhoseKids_Shared = $"{ModEntry.ModId}#SHARED";

    internal const string NPCChild_Prefix = $"{ModEntry.ModId}@AdoptedFromNPC@";
    internal const string ChildNPC_Suffix = $"@ChildToNPC@{ModEntry.ModId}";
    internal const string Parent_NPC_ADOPT = "NPC_ADOPT";
    internal const string Parent_SOLO_BIRTH = "SOLO_BIRTH";

    internal static bool AppearanceIsValid(this CharacterAppearanceData appearance)
    {
        return Game1.content.DoesAssetExist<Texture2D>(appearance.Sprite);
    }

    internal static bool AppearanceIsUnconditional(this CharacterAppearanceData appearance)
    {
        return (string.IsNullOrEmpty(appearance.Condition) || GameStateQuery.IsImmutablyTrue(appearance.Condition))
            && appearance.Indoors
            && appearance.Outdoors
            && !appearance.IsIslandAttire;
    }

    internal static bool AppearanceIsBaby(this CharacterAppearanceData appearance)
    {
        return appearance.Id?.StartsWith(Appearances_Prefix_Baby) ?? false;
    }

    internal static string? KidHMKId(this Child kid)
    {
        if (kid.modData.TryGetValue(Child_ModData_Id, out string kidId))
        {
            return kidId;
        }
        return null;
    }

    internal static string? KidNPCParent(this Child kid)
    {
        if (kid.modData.TryGetValue(Child_ModData_NPCParent, out string npcParent))
            return npcParent;
        return null;
    }

    internal static string? TryGetFLSpouseParent(Child kid)
    {
        if (kid.modData.TryGetValue(FL_ModData_OtherParent, out string npcParent))
        {
            return npcParent;
        }
        return null;
    }

    internal static string? KidDisplayName(this Child kid, bool allowNull = true)
    {
        if (kid.modData.TryGetValue(Child_ModData_DisplayName, out string kidDisplayName))
        {
            return kidDisplayName;
        }
        return allowNull ? null : (kid.displayName ?? kid.Name);
    }

    internal static void SetChildDisplayName(this Child kid, string newName)
    {
        if (kid.modData.ContainsKey(Child_ModData_DisplayName) && KidEntries.TryGetValue(kid.Name, out KidEntry? entry))
        {
            kid.modData[Child_ModData_DisplayName] = newName;
            entry.DisplayName = newName;
            if (NPCLookup.GetNonChildNPC(entry.KidNPCId) is NPC kidNPC)
            {
                kidNPC.displayName = newName;
            }
        }
        else
        {
            kid.Name = newName;
        }
        kid.displayName = newName;
        return;
    }

    internal static string? GetHMKAdoptedFromNPCId(this Character kid)
    {
        if (kid is Child && (kid.Name?.StartsWith(NPCChild_Prefix) ?? false))
        {
            return kid.Name.AsSpan()[NPCChild_Prefix.Length..].ToString();
        }
        return null;
    }

    internal static string? GetHMKChildNPCKidId(this NPC childNPC)
    {
        if (childNPC.Name?.EndsWith(ChildNPC_Suffix) ?? false)
        {
            return childNPC.Name.AsSpan()[..(childNPC.Name.Length - ChildNPC_Suffix.Length)].ToString();
        }
        return null;
    }

    internal static KidDefinitionData? GetHMKKidDef(this NPC kidCharacter)
    {
        string? kidKey;
        if (kidCharacter is Child kid)
        {
            // kid is adopted from NPC
            if (kid.GetHMKAdoptedFromNPCId() is string npcId)
            {
                kidKey = npcId;
            }
            // normal HMK kid
            else
            {
                kidKey = kidCharacter.Name;
            }
        }
        else
        {
            // NPC is kid
            if (kidCharacter.GetHMKChildNPCKidId() is string kidId)
            {
                kidKey = kidId;
            }
            // NPC is adopted to kid (should be validated elsewhere, unsafe here)
            else
            {
                kidKey = kidCharacter.Name;
            }
        }
        if (AssetManager.KidDefsByKidId.TryGetValue(kidKey, out KidDefinitionData? kidDef))
        {
            return kidDef;
        }
        return null;
    }

    internal static string? NextKidId(this Character character)
    {
        if (character.modData.TryGetValue(Character_ModData_NextKidId, out string nextKidId))
        {
            return nextKidId;
        }
        return null;
    }

    internal static string FormChildNPCId(string childName)
    {
        return string.Concat(childName, ChildNPC_Suffix);
    }

    internal static bool? GetDarkSkinnedRestrict(Farmer player, NPC spouse)
    {
        bool darkSkinnedPlayer = player.hasDarkSkin();
        bool darkSkinnedSpouse = spouse.hasDarkSkin();
        bool? darkSkinnedRestrict = null;
        if (darkSkinnedPlayer == darkSkinnedSpouse)
        {
            darkSkinnedRestrict = darkSkinnedPlayer;
        }
        return darkSkinnedRestrict;
    }

    internal static bool TrySetNextKid(Farmer player, NPC parent, string kidId)
    {
        if (
            !string.IsNullOrEmpty(kidId)
            && !kidId.EqualsIgnoreCase("Any")
            && TryGetAvailableSpouseOrSharedKidIds(
                parent,
                out List<string>? availableKidIds,
                GetDarkSkinnedRestrict(player, parent)
            )
            && availableKidIds.Contains(kidId)
        )
        {
            parent.modData[Character_ModData_NextKidId] = kidId;
            return true;
        }
        return false;
    }

    internal static void TrySetNextAdoptFromNPCKidId(Farmer player, string? kidId)
    {
        if (
            !string.IsNullOrEmpty(kidId)
            && !kidId.EqualsIgnoreCase("Any")
            && AssetManager.KidDefsByKidId.TryGetValue(kidId, out KidDefinitionData? kidDef)
            && (kidDef.AdoptedFromNPC != kidId || NPCLookup.GetNonChildNPC(kidId) is not null)
        )
        {
            ModEntry.Log($"Adopt '{kidId}' as Child");
            player.modData[Character_ModData_NextKidId] = kidId;
        }
    }

    internal static IEnumerable<Child> AllKids()
    {
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Child kid in farmer.getChildren())
            {
                yield return kid;
            }
        }
    }

    internal static IEnumerable<(Farmer, Child)> AllFarmersAndKids()
    {
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Child kid in farmer.getChildren())
            {
                yield return new(farmer, kid);
            }
        }
    }

    internal static void Register()
    {
        // events
        ModEntry.help.Events.Specialized.LoadStageChanged += OnLoadStageChanged;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.GameLoop.Saving += OnSaving;
        ModEntry.help.Events.GameLoop.Saved += OnSaved;
    }

    internal static bool KidEntriesPopulated { get; private set; } = false;
    internal static Dictionary<string, KidEntry> KidEntries { get; private set; } = [];
    internal static Dictionary<string, string> KidNPCToKid { get; private set; } = [];

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        KidNPCToKid.Clear();
        KidEntries.Clear();
        KidEntriesPopulated = false;
        ModEntry.help.GameContent.InvalidateCache(AssetManager.Asset_DataCharacters);
    }

    private static void OnLoadStageChanged(object? sender, LoadStageChangedEventArgs e)
    {
        if (e.NewStage == StardewModdingAPI.Enums.LoadStage.SaveLoadedLocations && Context.IsMainPlayer)
        {
            try
            {
                foreach (Child kid in AllKids())
                {
                    if (kid.KidHMKId() is string kidId)
                    {
                        if (kidId.StartsWith(NPCChild_Prefix) || AssetManager.ChildData.ContainsKey(kidId))
                        {
                            ModEntry.Log($"Restore on load: '{kid.Name}' ({kidId})");
                            kid.modData[Child_ModData_DisplayName] = kid.Name;
                            kid.Name = kidId;
                        }
                        else
                        {
                            ModEntry.Log($"Missing custom kid: '{kid.Name}' ({kidId})", LogLevel.Warn);
                            kid.Name = kid.KidDisplayName(allowNull: false);
                        }
                        // reset displayName
                        kid.displayName = null;
                    }
                }
                KidEntries_Populate();
            }
            catch (Exception err)
            {
                ModEntry.Log($"Error in LoadStageChanged:\n{err}", LogLevel.Error);
            }
        }
    }

    internal static void KidEntries_Populate([CallerMemberName] string? caller = null)
    {
        if (!Context.IsMainPlayer)
            return;

        ModEntry.Log($"Populating kid entries for '{caller}'...");
        KidEntries.Clear();

        foreach (Child kid in AllKids())
        {
            if (!Utility.TryParseEnum(kid.Birthday_Season, out Season season))
            {
                SDate birthday = SDate.Now();
                int daysOld = kid.daysOld.Value;
                while (daysOld > birthday.DaysSinceStart)
                {
                    // 10 years
                    birthday.AddDays(28 * 40);
                }
                birthday = birthday.AddDays(-daysOld);
                season = birthday.Season;
                kid.Birthday_Season = Utility.getSeasonKey(birthday.Season);
                kid.Birthday_Day = birthday.Day;
            }

            string? kidNPCId;
            bool adoptedFromNPC;

            if (
                kid.GetHMKAdoptedFromNPCId() is string npcId
                && AssetManager.KidDefsByKidId.TryGetValue(npcId, out KidDefinitionData? kidDef)
                && kidDef.AdoptedFromNPC == npcId
                && Game1.characterData.ContainsKey(kidDef.AdoptedFromNPC)
            )
            {
                kidNPCId = kidDef.AdoptedFromNPC;
                adoptedFromNPC = true;
            }
            else
            {
                kidNPCId = FormChildNPCId(kid.Name);
                adoptedFromNPC = false;
                if (
                    !ModEntry.KidNPCEnabled
                    || kid.Age <= 2
                    || kid.GetHMKKidDef() is not KidDefinitionData kidDef2
                    || GameStateQuery.IsImmutablyFalse(kidDef2.IsNPCTodayCondition)
                )
                {
                    kidNPCId = null;
                    if (NPCLookup.GetNonChildNPC(kidNPCId) is NPC kidAsNPC)
                    {
                        kidAsNPC.currentLocation?.characters.Remove(kidAsNPC);
                    }
                }
                else
                {
                    kid.Age = 4;
                }
            }

            string? npcParentId = kid.KidNPCParent();
            if (NPCLookup.GetNPCParent(npcParentId) is NPC parent)
            {
                // FL parent name display support
                kid.modData[FL_ModData_OtherParent] = parent.displayName;
            }

            KidEntries[kid.Name] = new(
                kidNPCId,
                adoptedFromNPC,
                kid.idOfParent.Value,
                npcParentId,
                season,
                kid.Birthday_Day
            )
            {
                DisplayName = kid.displayName,
            };
        }
        KidNPCSetup();
        MultiplayerSync.SendKidEntries(null);
    }

    internal static void KidEntries_FromHost(Dictionary<string, KidEntry> newChildToNPC)
    {
        KidEntries = newChildToNPC;
        KidNPCSetup();
    }

    private static void KidNPCSetup()
    {
        KidEntriesPopulated = true;
        KidNPCToKid.Clear();
        ModEntry.Log($"Got {KidEntries.Count} kids.");
        foreach ((string kidId, KidEntry entry) in KidEntries)
        {
            ModEntry.Log($"- {kidId}: {entry}");
            if (entry.KidNPCId != null)
                KidNPCToKid[entry.KidNPCId] = kidId;
        }
        if (!KidNPCToKid.Any())
            return;
        ModEntry.help.GameContent.InvalidateCache(AssetManager.Asset_DataCharacters);
        ModEntry.help.GameContent.InvalidateCache(AssetManager.Asset_DataNPCGiftTastes);
        if (!Context.IsMainPlayer)
        {
            foreach (KidEntry entry in KidEntries.Values)
            {
                if (NPCLookup.GetNonChildNPC(entry.KidNPCId) is NPC kidNPC)
                {
                    kidNPC.reloadSprite(onlyAppearance: true);
                    kidNPC.Sprite.UpdateSourceRect();
                }
            }
        }
    }

    // $"{ModEntry.ModId}@{childName}@{uniqueMultiplayerId}"
    private static readonly Regex childNPCPattern = new(@"^mushymato\.HaveMoreKids@([^\s]+)@\d+$");

    /// <summary>Check some one time kid stuff on save loaded</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        HashSet<string> kidNPCs = KidEntries
            .Select(kv => kv.Value.KidNPCId)
            .Where(kidNPCId => kidNPCId is not null)
            .ToHashSet()!;

        Utility.ForEachLocation(location =>
        {
            // Remove any invalid kids
            location.characters.RemoveWhere(
                (npc) =>
                {
                    if (npc?.Name is not string npcId)
                        return false;
                    if (childNPCPattern.Match(npcId) is not Match match || !match.Success)
                        return false;
                    if (kidNPCs.Contains(npc.Name))
                        return false;
                    ModEntry.Log(
                        $"Removed invalid kid NPC '{npc.Name}' from '{location.NameOrUniqueName}'",
                        LogLevel.Warn
                    );
                    return true;
                }
            );
            // Clear the mod data on cribs
            foreach (Furniture furniture in location.furniture)
            {
                furniture?.modData.Remove(CribAssign.PlacedChild);
            }
            return true;
        });
        // Apply unique kids to any existing children
        foreach (Child kid in Game1.player.getChildren())
        {
            if (kid.GetHMKAdoptedFromNPCId() is not null)
                continue;
            if ((kid.KidNPCParent() ?? TryGetFLSpouseParent(kid)) is string npcParent)
            {
                if (npcParent == Parent_NPC_ADOPT || npcParent == Parent_SOLO_BIRTH)
                {
                    continue;
                }
                if (SpouseShim.GetSpouses(Game1.player).FirstOrDefault(npc => npc.Name == npcParent) is NPC spouse)
                {
                    ChooseAndApplyKidId(spouse, kid);
                }
            }
            else if (SpouseShim.GetSpouses(Game1.player).FirstOrDefault() is NPC firstSpouse)
            {
                ChooseAndApplyKidId(firstSpouse, kid);
            }
        }
    }

    /// <summary>Do some kid checks on day started</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (GameDelegates.SoloDaysUntilNewChild > 1)
        {
            Game1.player.stats.Decrement(GameDelegates.Stats_daysUntilNewChild);
        }
        if (!Context.IsMainPlayer)
        {
            return;
        }
        KidEntries_Populate();
        CribManager.RecheckCribAssignments();
        // update all kids
        foreach ((Farmer farmer, Child kid) in AllFarmersAndKids())
        {
            kid.reloadSprite();
            kid.displayName = null;
            if (kid.Age <= 2)
                continue;

            // Ensure kid is in a tile that can reach the door
            KidPathingManager.RepositionKidInFarmhouse(kid);

            GameStateQueryContext gsqCtx = new(null, farmer, null, null, Game1.random);
            // check if today is a Child day or a NPC day
            if (
                ModEntry.KidNPCEnabled
                && KidEntries.TryGetValue(kid.Name, out KidEntry? entry)
                && NPCLookup.GetNonChildNPC(entry.KidNPCId) is NPC kidAsNPC
            )
            {
                string key;
                string? goOutsideGSQ = null;
                bool goOutside = false;

                if (kid.GetHMKKidDef() is KidDefinitionData kidDef)
                {
                    goOutsideGSQ = kidDef.IsNPCTodayCondition;
                }

                ModEntry.Log($"{kid.Name} {kid.daysOld.Value} {ModEntry.Config.TotalDaysChild} {goOutsideGSQ}");

                if (entry.IsAdoptedFromNPC)
                {
                    key = kid.GetHMKAdoptedFromNPCId() ?? kid.Name;

                    kid.daysOld.Value = ModEntry.Config.TotalDaysChild;
                    if (Game1.characterData.TryGetValue(key, out CharacterData? data))
                    {
                        kid.displayName = TokenParser.ParseText(data.DisplayName);
                        goOutside =
                            !string.IsNullOrEmpty(goOutsideGSQ) && GameStateQuery.CheckConditions(goOutsideGSQ, gsqCtx);
                    }
                }
                else
                {
                    key = entry.KidNPCId!;
                    if (kid.daysOld.Value >= ModEntry.Config.TotalDaysChild && !string.IsNullOrEmpty(goOutsideGSQ))
                    {
                        goOutside = GameStateQuery.CheckConditions(goOutsideGSQ, gsqCtx);
                    }
                }

                if (!Game1.player.friendshipData.TryGetValue(key, out Friendship kidFriendship))
                {
                    kidFriendship = new Friendship(0);
                }
                kidFriendship.ProposalRejected = false;
                kidFriendship.RoommateMarriage = false;
                kidFriendship.WeddingDate = null;
                kidFriendship.NextBirthingDate = null;
                kidFriendship.Status = FriendshipStatus.Friendly;
                kidFriendship.Proposer = 0L;
                Game1.player.friendshipData[kid.Name] = kidFriendship;
                Game1.player.friendshipData[entry.KidNPCId] = kidFriendship;
                KidPathingManager.AddManagedNPCKid(kid, kidAsNPC, goOutside);
            }

            ResetDialogues(kid);
        }
        ModEntry.LogDebug("Done day started setup");
        KidPathingManager.PathKidNPCToDoor(Game1.timeOfDay);
    }

    internal static void ResetDialogues(Child kid)
    {
        ModEntry.Log($"ResetDialogues for '{kid.Name}'");
        // make sure dialogue gets reloaded
        kid.resetSeasonalDialogue();
        kid.resetCurrentDialogue();
        kid.updatedDialogueYet = false;
        kid.nonVillagerNPCTimesTalked = 0;
        return;
    }

    /// <summary>Unset HMK related data on saving</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaving(object? sender, SavingEventArgs e)
    {
        NPCLookup.Clear();
        if (!Context.IsMainPlayer)
            return;
        foreach (Child kid in AllKids())
        {
            kid.modData.Remove(KidPathingManager.Child_ModData_RoamOnFarm);
            if (kid.modData.TryGetValue(Child_ModData_DisplayName, out string? displayName))
            {
                ModEntry.LogDebug($"Unset before saving: '{displayName}' ({kid.Name})");
                kid.modData.Remove(Child_ModData_DisplayName);
                kid.modData[Child_ModData_Id] = kid.Name;
                kid.Name = displayName;
            }
        }
    }

    /// <summary>Unset HMK related data on saving</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaved(object? sender, SavedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        foreach (Child kid in AllKids())
        {
            if (kid.modData.TryGetValue(Child_ModData_Id, out string kidId))
            {
                ModEntry.LogDebug($"Restore after saving: '{kid.Name}' ({kidId})");
                kid.modData[Child_ModData_DisplayName] = kid.Name;
                kid.Name = kidId;
            }
        }
    }

    public static string AntiNameCollision(string name)
    {
        HashSet<string> npcIds = Utility.getAllCharacters().Select(npc => npc.Name).ToHashSet();
        while (npcIds.Contains(name))
        {
            name += " ";
        }
        return name;
    }

    internal static Child HaveAKid(
        NPC? spouse,
        string? newKidId,
        bool isDarkSkinned,
        string babyName,
        out KidDefinitionData? whoseKidForTwin,
        bool isTwin
    )
    {
        // create and add kid
        Child? newKid = null;
        if (spouse != null)
        {
            if (!spouse.modData.ContainsKey(Character_ModData_NextKidId))
            {
                // try to pick a kid of matching name
                if (PickForSpecificKidId(spouse, babyName) is string specificKidName)
                {
                    newKidId = specificKidName;
                }
            }
            spouse.modData.Remove(Character_ModData_NextKidId);
        }

        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

        whoseKidForTwin = null;
        string spouseNameForKid = spouse?.Name ?? Parent_SOLO_BIRTH;
        bool startAsToddler = false;
        if (!string.IsNullOrEmpty(newKidId))
        {
            CharacterData? childData = null;
            if (AssetManager.KidDefsByKidId.TryGetValue(newKidId, out KidDefinitionData? kidDef))
            {
                if (kidDef.AdoptedFromNPC != null && Game1.characterData.TryGetValue(newKidId, out childData))
                {
                    spouseNameForKid = Parent_NPC_ADOPT;
                    babyName = string.Concat(NPCChild_Prefix, newKidId);
                    newKidId = babyName;
                    startAsToddler = true;
                }
                else if (kidDef.BirthOrAdoptAsToddler && AssetManager.ChildData.TryGetValue(newKidId, out childData))
                {
                    babyName = childData.DisplayName;
                    startAsToddler = true;
                }
                else
                {
                    babyName = AntiNameCollision(babyName);
                }

                if (
                    !isTwin
                    && kidDef?.Twin != null
                    && kidDef.Twin != newKidId
                    && GameStateQuery.CheckConditions(kidDef.TwinCondition, farmHouse, Game1.player)
                )
                {
                    whoseKidForTwin = kidDef;
                }
            }
            if (childData == null)
            {
                AssetManager.ChildData.TryGetValue(newKidId, out childData);
            }

            if (childData != null)
            {
                newKid = ApplyKidId(
                    spouseNameForKid,
                    new(newKidId, childData.Gender == Gender.Male, childData.IsDarkSkinned, Game1.player),
                    true,
                    babyName,
                    newKidId,
                    childData
                );
            }
        }

        if (newKid == null)
        {
            babyName = AntiNameCollision(babyName);
            bool isMale = false;
            switch (ModEntry.Config.GenericChildrenGenderMode)
            {
                case NewChildGenderMode.Alternating:
                    {
                        List<Child> children = Game1.player.getChildren();
                        isMale =
                            children.Count == 0
                                ? Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).NextBool()
                                : children.Last().Gender == Gender.Female;
                    }
                    break;
                case NewChildGenderMode.Random:
                    isMale = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).NextBool();
                    break;
                case NewChildGenderMode.BoysOnly:
                    isMale = true;
                    break;
                case NewChildGenderMode.GirlsOnly:
                    isMale = false;
                    break;
            }
            newKid = new(babyName, isMale, isDarkSkinned, Game1.player);
        }

        if (startAsToddler)
        {
            newKid.daysOld.Value = ModEntry.Config.TotalDaysChild;
            newKid.Age = 3;
        }
        else
        {
            newKid.Age = 0;
        }
        farmHouse.characters.Add(newKid);
        newKid.currentLocation = farmHouse;

        if (GameDelegates.SoloDaysUntilNewChild == 1)
        {
            GameDelegates.CountDown_SoloDaysUntilNewChild(1);
            Game1.player.modData.Remove(Character_ModData_NextKidId);
        }

        if (spouse != null)
        {
            Game1.morningQueue.Enqueue(() =>
            {
                string text2 = spouse.displayName;
                Game1.Multiplayer.globalChatInfoMessage(
                    "Baby",
                    Lexicon.capitalize(Game1.player.Name),
                    text2,
                    Lexicon.getTokenizedGenderedChildTerm(newKid.Gender == Gender.Male),
                    Lexicon.getTokenizedPronoun(newKid.Gender == Gender.Male),
                    newKid.displayName
                );
            });
        }

        return newKid;
    }

    /// <summary>Choose a new kid Id. If originalName is given, only choose new one if the existing pick is invalid for the specific spouse</summary>
    /// <param name="spouse"></param>
    /// <param name="originalName"></param>
    /// <returns></returns>
    internal static string? PickKidId(NPC spouse, Child? child = null, bool? darkSkinned = null)
    {
        if (!TryGetSpouseOrSharedKidIds(spouse, out string? pickedKey, out List<string>? availableKidIds))
        {
            return null;
        }

        // already a HMK child, try to apply the changes
        if (child != null && availableKidIds.Contains(child.Name))
        {
            return null;
        }

        if ((availableKidIds = FilterAvailableKidIds(pickedKey, availableKidIds, darkSkinned)) == null)
        {
            return null;
        }

        // Prioritize the kid id set by trigger action, if it is valid
        if (spouse.NextKidId() is string nextKidId && availableKidIds.Contains(nextKidId))
        {
            return nextKidId;
        }

        string? name = null;
        Gender? gender = null;
        if (child != null)
        {
            name = child.Name;
            darkSkinned = child.darkSkinned.Value;
            gender = child.Gender;
        }
        return PickMostLikelyKidId(availableKidIds, darkSkinned, gender, name);
    }

    /// <summary>Get and validate kid id</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    internal static bool TryGetKidIds(string spouseId, [NotNullWhen(true)] out List<string>? kidIds)
    {
        kidIds = null;
        if (
            AssetManager.KidDefsByParentId.TryGetValue(
                spouseId,
                out Dictionary<string, KidDefinitionData>? whoseKidsInfo
            )
        )
        {
            kidIds = [];
            foreach ((string key, KidDefinitionData data) in whoseKidsInfo)
            {
                if (GameStateQuery.CheckConditions(data.Condition))
                {
                    kidIds.Add(key);
                }
            }
            return kidIds.Any();
        }
        return false;
    }

    /// <summary>Get and validate kid id</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    internal static bool TryGetKidIds(
        string spouseId,
        [NotNullWhen(true)] out List<string>? kidIds,
        [NotNullWhen(true)] out Dictionary<string, KidDefinitionData>? whoseKidsInfo
    )
    {
        kidIds = null;
        if (AssetManager.KidDefsByParentId.TryGetValue(spouseId, out whoseKidsInfo))
        {
            kidIds = whoseKidsInfo.Keys.ToList();
            return whoseKidsInfo.Values.Any(value => value.DefaultEnabled);
        }
        return false;
    }

    internal static List<string>? FilterAvailableKidIds(string key, List<string> kidIds, bool? darkSkinnedRestrict)
    {
        if (!kidIds.Any())
            return null;
        HashSet<string?> children = Game1.player.getChildren().Select(child => child.KidHMKId()).ToHashSet();
        List<string> filteredKidIds = kidIds
            .Where(id =>
                !children.Contains(id)
                && ModEntry.Config.EnabledKids.GetValueOrDefault(new(key, id))
                && AssetManager.ChildData.TryGetValue(id, out CharacterData? kidData)
                && (darkSkinnedRestrict == null || kidData.IsDarkSkinned == darkSkinnedRestrict)
            )
            .ToList();
        if (!filteredKidIds.Any())
            return null;
        return filteredKidIds;
    }

    internal static bool TryGetSpouseKidIds(NPC spouse, [NotNullWhen(true)] out List<string>? availableKidIds)
    {
        availableKidIds = null;
        if (spouse?.Name == null)
            return false;
        return TryGetKidIds(spouse.Name, out availableKidIds);
    }

    internal static bool TryGetSharedKidIds([NotNullWhen(true)] out List<string>? availableKidIds)
    {
        return TryGetKidIds(WhoseKids_Shared, out availableKidIds);
    }

    internal static bool TryGetAvailableSharedKidIds(
        [NotNullWhen(true)] out List<string>? availableKidIds,
        bool? darkSkinnedRestrict
    )
    {
        return TryGetKidIds(WhoseKids_Shared, out availableKidIds)
            && (availableKidIds = FilterAvailableKidIds(WhoseKids_Shared, availableKidIds, darkSkinnedRestrict))
                != null;
    }

    internal static bool TryGetSpouseOrSharedKidIds(
        NPC spouse,
        [NotNullWhen(true)] out string? pickedKey,
        [NotNullWhen(true)] out List<string>? availableKidIds
    )
    {
        if (TryGetSpouseKidIds(spouse, out availableKidIds))
        {
            pickedKey = spouse.Name;
            return true;
        }
        else if (TryGetSharedKidIds(out availableKidIds))
        {
            pickedKey = WhoseKids_Shared;
            return true;
        }
        pickedKey = null;
        return false;
    }

    internal static bool TryGetAvailableSpouseOrSharedKidIds(
        NPC spouse,
        [NotNullWhen(true)] out List<string>? availableKidIds,
        bool? darkSkinnedRestrict
    )
    {
        if (TryGetSpouseOrSharedKidIds(spouse, out _, out availableKidIds))
        {
            return (availableKidIds = FilterAvailableKidIds(spouse.Name, availableKidIds, darkSkinnedRestrict)) != null;
        }
        return false;
    }

    internal static string? PickMostLikelyKidId(
        List<string> availableKidIds,
        bool? darkSkinned,
        Gender? gender,
        string? name
    )
    {
        if (!availableKidIds.Any())
            return null;
        // Prioritize matching gender/darkskinned
        List<string> moreLikelyMatch = [];
        foreach (var kidId in availableKidIds)
        {
            if (AssetManager.ChildData.TryGetValue(kidId, out CharacterData? data))
            {
                if (name != null && TokenParser.ParseText(data.DisplayName) == name)
                    return kidId;
                if (
                    (gender == null || gender == data.Gender)
                    && (darkSkinned == null || darkSkinned == data.IsDarkSkinned)
                )
                {
                    moreLikelyMatch.Add(kidId);
                }
            }
        }
        Random daysPlayedRand = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
        if (moreLikelyMatch.Count > 0)
            return moreLikelyMatch[daysPlayedRand.Next(moreLikelyMatch.Count)];
        return availableKidIds[daysPlayedRand.Next(availableKidIds.Count)];
    }

    internal static string? PickForSpecificKidId(NPC spouse, string name)
    {
        // Check for "real name" kid
        if (!TryGetAvailableSpouseOrSharedKidIds(spouse, out List<string>? availableKidIds, null))
        {
            return null;
        }
        // Prioritize the kid id set by trigger action, if it is valid
        if (spouse.NextKidId() is string nextKidId && availableKidIds.Contains(nextKidId))
        {
            return nextKidId;
        }
        // Check display names
        foreach (var kidId in availableKidIds)
        {
            if (AssetManager.ChildData.TryGetValue(kidId, out CharacterData? data))
            {
                if (name != null && TokenParser.ParseText(data.DisplayName) == name)
                    return kidId;
            }
        }
        return null;
    }

    internal static Child ChooseAndApplyKidId(NPC spouse, Child kid)
    {
        if (kid.modData.TryGetValue(Child_ModData_Id, out string? kidId) && KidEntries.ContainsKey(kidId))
        {
            return kid;
        }
        string kidName = kid.Name;
        if (kidName == null || PickKidId(spouse, kid) is not string newKidId)
            return kid;
        return ApplyKidId(spouse.Name, kid, false, kidName, newKidId);
    }

    internal static Child ApplyKidId(
        string? spouseName,
        Child newKid,
        bool newBorn,
        string kidName,
        string newKidId,
        CharacterData? characterData = null
    )
    {
        newKid.Name = newKidId;
        newKid.displayName = kidName;
        newKid.modData[Child_ModData_Id] = newKidId;
        newKid.modData[Child_ModData_DisplayName] = kidName;
        newKid.modData[Child_ModData_NPCParent] = spouseName;
        if (newBorn)
        {
            // newKid.modData[Child_ModData_Birthday] = $"{Game1.season}|{Game1.dayOfMonth}";
            newKid.Birthday_Season = Utility.getSeasonKey(Game1.season);
            newKid.Birthday_Day = Game1.dayOfMonth;
        }
        if ((characterData ??= newKid.GetData()) is null)
        {
            ModEntry.Log($"Failed to get data for child ID '{newKidId}', '{kidName}' may be broken.", LogLevel.Error);
            return newKid;
        }
        newKid.Gender = characterData.Gender;
        newKid.darkSkinned.Value = characterData.IsDarkSkinned;
        newKid.reloadSprite(onlyAppearance: true);
        ModEntry.Log($"Assigned '{newKidId}' to child named '{kidName}'.", LogLevel.Info);
        return newKid;
    }
}
