using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
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
using StardewValley.Pathfinding;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal sealed record KidEntry(
    string? KidNPCId,
    bool IsAdoptedFromNPC,
    string DisplayName,
    long PlayerParent,
    string? OtherParent,
    Season BirthSeason,
    int BirthDay
);

internal static class KidHandler
{
    private const string Appearances_Prefix_Baby = "HMK_BABY";
    private const string Child_ModData_Id = $"{ModEntry.ModId}/Id";
    private const string Child_ModData_DisplayName = $"{ModEntry.ModId}/DisplayName";
    private const string Child_ModData_NPCParent = $"{ModEntry.ModId}/NPCParent";
    private const string FL_ModData_OtherParent = "aedenthorn.FreeLove/OtherParent";
    private const string Child_CustomField_GoOutsideCondition = $"{ModEntry.ModId}/GoOutsideCondition";
    internal const string Character_ModData_NextKidId = $"{ModEntry.ModId}/NextKidId";
    private const string Character_CustomField_IsNPCToday = $"{ModEntry.ModId}/IsNPCToday";
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

    internal static string? KidDisplayName(this Child kid, bool allowNull = true)
    {
        if (kid.modData.TryGetValue(Child_ModData_DisplayName, out string kidDisplayName))
        {
            return kidDisplayName;
        }
        return allowNull ? null : (kid.displayName ?? kid.Name);
    }

    internal static string? KidHMKFromNPCId(this Character kid)
    {
        if (kid.Name?.StartsWith(NPCChild_Prefix) ?? false)
        {
            return kid.Name.AsSpan()[NPCChild_Prefix.Length..].ToString();
        }
        return null;
    }

    internal static bool TrySetNextKid(NPC parent, string kidId)
    {
        if (
            !string.IsNullOrEmpty(kidId)
            && !kidId.EqualsIgnoreCase("Any")
            && TryGetAvailableSpouseOrSharedKidIds(parent, out List<string>? availableKidIds)
            && availableKidIds.Contains(kidId)
        )
        {
            parent.modData[Character_ModData_NextKidId] = kidId;
        }
        return false;
    }

    internal static void TrySetNextAdoptFromNPCKidId(Farmer player, string kidId)
    {
        if (
            !string.IsNullOrEmpty(kidId)
            && !kidId.EqualsIgnoreCase("Any")
            && AssetManager.KidDefsByKidId.TryGetValue(kidId, out KidDefinitionData? kidDef)
            && kidDef.AdoptedFromNPC == kidId
        )
        {
            ModEntry.Log($"Adopt {kidId} as Child");
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
        ModEntry.help.Events.GameLoop.DayEnding += OnDayEnding;
        ModEntry.help.Events.GameLoop.Saving += OnSaving;
        ModEntry.help.Events.GameLoop.Saved += OnSaved;
    }

    internal static Dictionary<string, KidEntry> KidEntries { get; private set; } = [];
    internal static Dictionary<string, string> KidNPCToKid { get; private set; } = [];

    internal static string FormChildNPCId(string childName)
    {
        return string.Concat(childName, ChildNPC_Suffix);
    }

    internal static bool IsHMKChildNPC(this NPC childNPC)
    {
        return childNPC.Name?.EndsWith(ChildNPC_Suffix) ?? false;
    }

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        KidNPCToKid.Clear();
        KidEntries.Clear();
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

            string? kidNPCId = null;
            bool adoptedFromNPC = false;

            if (
                kid.KidHMKFromNPCId() is string npcId
                && AssetManager.KidDefsByKidId.TryGetValue(npcId, out KidDefinitionData? kidDef)
                && kidDef.AdoptedFromNPC == npcId
                && Game1.characterData.ContainsKey(kidDef.AdoptedFromNPC)
            )
            {
                kidNPCId = kidDef.AdoptedFromNPC;
                adoptedFromNPC = true;
            }

            if (
                kid.Age > 2
                && ModEntry.Config.DaysChild > -1
                && AssetManager.ChildData.TryGetValue(kid.Name, out CharacterData? childCharaData)
                && childCharaData.CanSocialize != null
                && !GameStateQuery.IsImmutablyFalse(childCharaData.CanSocialize)
            )
            {
                kidNPCId = FormChildNPCId(kid.Name);
                adoptedFromNPC = false;
            }

            KidEntries[kid.Name] = new(
                kidNPCId,
                adoptedFromNPC,
                kid.displayName,
                kid.idOfParent.Value,
                kid.KidNPCParent(),
                season,
                kid.Birthday_Day
            );
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
                if (entry.KidNPCId != null && Game1.getCharacterFromName(entry.KidNPCId) is NPC kidNPC)
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
            if (kid.KidHMKFromNPCId() is not null)
                continue;
            foreach (NPC spouse in SpouseShim.GetSpouses(Game1.player))
            {
                ChooseAndApplyKidId(spouse, kid);
            }
        }
    }

    private static NPC? GetNonChildNPCByName(string kidNPCId)
    {
        NPC? match = null;
        Utility.ForEachCharacter(
            npc =>
            {
                if (npc.Name == kidNPCId && npc is not Child && npc.IsVillager)
                {
                    if (npc.currentLocation?.IsActiveLocation() ?? false)
                    {
                        match = npc;
                        return false;
                    }
                }
                return true;
            },
            false
        );
        return match;
    }

    /// <summary>Do some kid checks on day started</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        Game1.player.stats.Decrement(GameDelegates.Stats_daysUntilNewChild);
        if (!Context.IsMainPlayer)
        {
            return;
        }
        CribManager.RecheckCribAssignments();
        // update all kids
        foreach ((Farmer farmer, Child kid) in AllFarmersAndKids())
        {
            kid.reloadSprite();
            kid.displayName = null;
            if (kid.Age <= 2)
                continue;
            // make sure dialogue gets reloaded
            kid.resetSeasonalDialogue();
            kid.resetCurrentDialogue();

            GameStateQueryContext gsqCtx = new(null, farmer, null, null, Game1.random);
            // check if today is a Child day or a NPC day
            if (
                KidEntries.TryGetValue(kid.Name, out KidEntry? entry)
                && entry.KidNPCId != null
                && GetNonChildNPCByName(entry.KidNPCId) is NPC childAsNPC
            )
            {
                string key = entry.KidNPCId;
                bool stayHome = true;
                if (entry.IsAdoptedFromNPC)
                {
                    // For some reason, the NPC flavored child don't get dayUpdate so their positions are messed up
                    // Redo this work ourselves
                    kid.speed = 4;
                    if (kid.currentLocation is FarmHouse farmHouse)
                    {
                        Random r = Utility.CreateDaySaveRandom(farmHouse.OwnerId * 2);
                        Point randomOpenPointInHouse2 = farmHouse.getRandomOpenPointInHouse(r, 1, 200);
                        if (!randomOpenPointInHouse2.Equals(Point.Zero))
                        {
                            kid.setTilePosition(randomOpenPointInHouse2);
                        }
                        else
                        {
                            randomOpenPointInHouse2 = farmHouse.GetChildBedSpot(kid.GetChildIndex());
                            if (!randomOpenPointInHouse2.Equals(Point.Zero))
                            {
                                kid.setTilePosition(randomOpenPointInHouse2);
                            }
                        }
                        kid.Sprite.CurrentAnimation = null;
                    }

                    key = kid.KidHMKFromNPCId() ?? kid.Name;
                    ModEntry.Log($"{kid.Name} -> {key} ({kid.daysOld.Value})");

                    kid.daysOld.Value = ModEntry.Config.TotalDaysChild;
                    if (Game1.characterData.TryGetValue(key, out CharacterData? data))
                    {
                        kid.displayName = TokenParser.ParseText(data.DisplayName);
                        if (
                            data.CustomFields?.TryGetValue(Character_CustomField_IsNPCToday, out string? npcTodayGSQ)
                            ?? false
                        )
                        {
                            stayHome = !GameStateQuery.CheckConditions(npcTodayGSQ, gsqCtx);
                        }
                        else
                        {
                            stayHome = true;
                        }
                    }
                }
                else if (kid.GetData() is CharacterData childCharaData)
                {
                    key = entry.KidNPCId;
                    if (
                        ModEntry.Config.DaysChild > -1
                        && kid.daysOld.Value >= ModEntry.Config.TotalDaysChild
                        && !string.IsNullOrEmpty(childCharaData.CanSocialize)
                    )
                    {
                        if (GameStateQuery.CheckConditions(childCharaData.CanSocialize, gsqCtx))
                        {
                            stayHome = false;
                        }
                    }
                }

                if (!Game1.player.friendshipData.TryGetValue(key, out Friendship childFriendship))
                {
                    childFriendship = new Friendship(0);
                }
                childFriendship.ProposalRejected = false;
                childFriendship.RoommateMarriage = false;
                childFriendship.WeddingDate = null;
                childFriendship.NextBirthingDate = null;
                childFriendship.Status = FriendshipStatus.Friendly;
                childFriendship.Proposer = 0L;
                Game1.player.friendshipData[kid.Name] = childFriendship;
                Game1.player.friendshipData[entry.KidNPCId] = childFriendship;

                if (stayHome)
                {
                    kid.Age = 3;
                    kid.IsInvisible = false;
                    kid.daysUntilNotInvisible = 0;
                    childAsNPC.IsInvisible = true;
                    childAsNPC.daysUntilNotInvisible = 1;
                    ModEntry.Log($"Child '{kid.displayName}' ({kid.Name}) will stay home today", LogLevel.Info);
                }
                else
                {
                    kid.Age = 4;
                    kid.IsInvisible = true;
                    kid.daysUntilNotInvisible = 1;
                    childAsNPC.IsInvisible = false;
                    childAsNPC.daysUntilNotInvisible = 0;

                    childAsNPC.GetData();
                    childAsNPC.reloadSprite(onlyAppearance: true);
                    childAsNPC.InvalidateMasterSchedule();
                    childAsNPC.TryLoadSchedule();
                    childAsNPC.performSpecialScheduleChanges();
                    childAsNPC.resetSeasonalDialogue();
                    childAsNPC.resetCurrentDialogue();
                    childAsNPC.Sprite.UpdateSourceRect();
                    ModEntry.Log($"Child '{kid.displayName}' ({kid.Name}) will go to town today", LogLevel.Info);
                    // ModEntry.help.Events.Player.Warped += OnWarped;
                }
            }
        }
    }

    /// <summary>Unset HMK related data on saving</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        foreach (Child kid in AllKids())
        {
            kid.modData.Remove(Child_CustomField_GoOutsideCondition);
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
        bool isTwin,
        bool isAdoptedFromNPC
    )
    {
        // create and add kid
        Child? newKid = null;
        if (!isAdoptedFromNPC && spouse != null)
        {
            if (PickForSpecificKidId(spouse, babyName) is string specificKidName)
            {
                newKidId = specificKidName;
            }
            spouse.modData.Remove(Character_ModData_NextKidId);
        }

        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

        whoseKidForTwin = null;
        string spouseNameForKid = spouse?.Name ?? Parent_SOLO_BIRTH;
        if (newKidId != null)
        {
            CharacterData? childData = null;
            if (AssetManager.KidDefsByKidId.TryGetValue(newKidId, out KidDefinitionData? kidDef))
            {
                if (kidDef.AdoptedFromNPC != null && Game1.characterData.TryGetValue(newKidId, out childData))
                {
                    spouseNameForKid = Parent_NPC_ADOPT;
                    babyName = string.Concat(NPCChild_Prefix, newKidId);
                    newKidId = babyName;
                }
                else
                {
                    babyName = AntiNameCollision(babyName);
                }

                if (
                    !isTwin
                    && kidDef?.Twin != null
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
            bool isMale;
            if (Game1.player.getNumberOfChildren() == 0)
            {
                isMale = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).NextBool();
            }
            else
            {
                isMale = Game1.player.getChildren().Last().Gender == Gender.Female;
            }
            newKid = new(babyName, isMale, isDarkSkinned, Game1.player);
        }

        if (isAdoptedFromNPC)
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

        if (spouse != null)
        {
            Game1.morningQueue.Enqueue(() =>
            {
                string text2 = spouse.GetTokenizedDisplayName();
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

        if (!FilterAvailableKidIds(pickedKey, ref availableKidIds))
        {
            return null;
        }

        // Prioritize the kid id set by trigger action, if it is valid
        if (
            spouse.modData.TryGetValue(Character_ModData_NextKidId, out string? nextKidId)
            && availableKidIds.Contains(nextKidId)
        )
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
            return whoseKidsInfo.Values.Where(value => value.DefaultEnabled).Any();
        }
        return false;
    }

    internal static bool FilterAvailableKidIds(string key, ref List<string> kidIds)
    {
        if (kidIds == null)
            return false;
        HashSet<string?> children = Game1.player.getChildren().Select(child => child.KidHMKId()).ToHashSet();
        kidIds = kidIds
            .Where(id =>
                !children.Contains(id)
                && AssetManager.ChildData.ContainsKey(id)
                && ModEntry.Config.EnabledKids.GetValueOrDefault(new(key, id))
            )
            .ToList();
        return kidIds.Count > 0;
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

    internal static bool TryGetAvailableSharedKidIds([NotNullWhen(true)] out List<string>? availableKidIds)
    {
        return TryGetKidIds(WhoseKids_Shared, out availableKidIds)
            && FilterAvailableKidIds(WhoseKids_Shared, ref availableKidIds);
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
        [NotNullWhen(true)] out List<string>? availableKidIds
    )
    {
        return (
                TryGetSpouseKidIds(spouse, out availableKidIds)
                && FilterAvailableKidIds(spouse.Name, ref availableKidIds)
            )
            || (
                TryGetSharedKidIds(out availableKidIds) && FilterAvailableKidIds(WhoseKids_Shared, ref availableKidIds)
            );
    }

    internal static string PickMostLikelyKidId(
        List<string> availableKidIds,
        bool? darkSkinned,
        Gender? gender,
        string? name
    )
    {
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
        if (!TryGetAvailableSpouseOrSharedKidIds(spouse, out List<string>? availableKidIds))
        {
            return null;
        }
        // Prioritize the kid id set by trigger action, if it is valid
        if (
            spouse.modData.TryGetValue(Character_ModData_NextKidId, out string? nextKidId)
            && availableKidIds.Contains(nextKidId)
        )
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

    #region kid pathing

    /// <summary>Get whether players can walk on a map tile.</summary>
    /// <param name="location">The location to check.</param>
    /// <param name="tile">The tile position.</param>
    /// <remarks>This is derived from <see cref="GameLocation.isTilePassable(Vector2)" />, but also checks tile properties in addition to tile index properties to match the actual game behavior.</remarks>
    private static bool IsTilePassable(GameLocation location, Point tile)
    {
        // passable if Buildings layer has 'Passable' property
        xTile.Tiles.Tile? buildingTile = location.map.RequireLayer("Buildings").Tiles[(int)tile.X, (int)tile.Y];
        if (buildingTile?.Properties.ContainsKey("Passable") is true)
            return true;

        // non-passable if Back layer has 'Passable' property
        xTile.Tiles.Tile? backTile = location.map.RequireLayer("Back").Tiles[(int)tile.X, (int)tile.Y];
        if (backTile?.Properties.ContainsKey("Passable") is true)
            return false;

        // else check tile indexes
        return location.isTilePassable(tile.ToVector2());
    }

    internal static bool IsTileStandable(GameLocation location, Point tile)
    {
        return IsTilePassable(location, tile)
            && !location.IsTileBlockedBy(tile.ToVector2(), ignorePassables: CollisionMask.All)
            && !location.isWaterTile(tile.X, tile.Y)
            && !location.warps.Any(warp => warp.X == tile.X && warp.Y == warp.Y);
    }

    internal static readonly Dictionary<long, (Child, Point)> GoingToTheFarm = [];

    internal static bool KidShouldGoOutside(this Child kid)
    {
        if (kid.modData.TryGetValue(Child_CustomField_GoOutsideCondition, out string value))
        {
            return value == "T";
        }

        bool result;
        if (
            kid.GetData()?.CustomFields?.TryGetValue(Child_CustomField_GoOutsideCondition, out string? condition)
            ?? false
        )
        {
            result = GameStateQuery.CheckConditions(condition, new());
        }
        else
        {
            // result = Random.Shared.NextBool();
            result = true;
        }

        kid.modData[Child_CustomField_GoOutsideCondition] = result ? "T" : "F";
        return result;
    }

    /// <summary>
    /// Possibly skipping prefix for Child.tenMinuteUpdate
    /// </summary>
    /// <param name="kid"></param>
    /// <returns></returns>
    internal static bool TenMinuteUpdate(Child kid)
    {
        // at 1900, banish them back to the house, and then allow vanilla logic to run
        if (Game1.timeOfDay >= 1850 && kid.currentLocation is not FarmHouse)
        {
            GoingToTheFarm.Clear();
            ModEntry.LogDebug($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} -> return to farmhouse");
            WarpKidToHouse(kid);
            return false;
        }

        // before 1000, roll and see if the child could go outside
        if (
            ModEntry.Config.ToddlerRoamOnFarm
            && Game1.timeOfDay <= 1000
            && kid.currentLocation is FarmHouse farmhouse
            && farmhouse.GetParentLocation() is Farm farm
        )
        {
            return SendKidToOutside(kid, farmhouse, farm);
        }

        // while the kid is on the farm, do some path find controller things
        FarmPathFinding(kid);
        return false;
    }

    private static bool SendKidToOutside(Child kid, FarmHouse farmhouse, Farm farm)
    {
        // check if kid should go out
        if (!kid.KidShouldGoOutside())
        {
            return false;
        }

        // if kid ought to be outside already, skip directly to warp
        if (GoingToTheFarm.TryGetValue(kid.idOfParent.Value, out (Child, Point) goingInfo))
        {
            if (goingInfo.Item1 == kid)
            {
                DelayedAction.functionAfterDelay(() => WarpKidToFarm(kid, farm), 0);
                return false;
            }
            else
            {
                return Random.Shared.NextBool();
            }
        }
        // only 1 kid allowed to path to farm at once and they should not be moving
        if (kid.isMoving() || kid.mutex.IsLocked())
        {
            return false;
        }

        ModEntry.LogDebug($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} ({kid.TilePoint}) -> go outside");
        bool foundWarp = false;
        Point doorExit = new(-1, -1);
        Point houseEntry = new(-1, -1);
        foreach (Warp warp in farmhouse.warps)
        {
            if (warp.TargetName != farm.NameOrUniqueName)
            {
                continue;
            }
            doorExit = new(warp.X, warp.Y - 2);
            houseEntry = new(warp.TargetX, warp.TargetY);
            foundWarp = true;
        }

        if (!foundWarp)
        {
            return true;
        }

        for (int i = 0; i < 1000; i++)
        {
            Point trial = new(houseEntry.X + Random.Shared.Next(-8, 8), houseEntry.Y + 2 + Random.Shared.Next(0, 16));
            if (IsTileStandable(farm, trial))
            {
                kid.controller = new PathFindController(kid, farmhouse, doorExit, -1, WarpKidToFarm);
                if (
                    kid.controller.pathToEndPoint == null
                    || !kid.controller.pathToEndPoint.Any()
                    || !kid.currentLocation.isTileOnMap(kid.controller.pathToEndPoint.Last())
                )
                {
                    kid.controller = null;
                    return true;
                }
                else
                {
                    GoingToTheFarm[kid.idOfParent.Value] = new(kid, trial);
                    return false;
                }
            }
        }

        return true;
    }

    private static void WarpKidToHouse(Child kid, bool delay = true)
    {
        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.GetPlayer(kid.idOfParent.Value));
        if (delay)
        {
            DelayedAction.functionAfterDelay(
                () =>
                {
                    Game1.warpCharacter(
                        kid,
                        farmHouse,
                        farmHouse.getRandomOpenPointInHouse(Random.Shared, 2).ToVector2()
                    );
                    kid.controller = null;
                },
                0
            );
        }
        else
        {
            Game1.warpCharacter(kid, farmHouse, farmHouse.getRandomOpenPointInHouse(Random.Shared, 2).ToVector2());
            kid.controller = null;
        }
    }

    private static void WarpKidToFarm(Character c, GameLocation l)
    {
        if (c is not Child kid || !GoingToTheFarm.TryGetValue(kid.idOfParent.Value, out (Child, Point) info))
            return;
        if (kid != info.Item1 || kid.currentLocation?.GetParentLocation() is not Farm farm)
        {
            GoingToTheFarm.Remove(kid.idOfParent.Value);
            return;
        }

        kid.Halt();
        kid.controller = null;
        GoingToTheFarm.Remove(kid.idOfParent.Value);
        Game1.warpCharacter(kid, farm, info.Item2.ToVector2());
        kid.toddlerReachedDestination(kid, farm);
    }

    private static void FarmPathFinding(Child kid)
    {
        // already in a path find
        if (kid.controller != null || kid.mutex.IsLocked() || kid.currentLocation is not Farm farm)
        {
            return;
        }

        // 50% chance
        if (Random.Shared.NextBool())
        {
            return;
        }

        kid.IsWalkingInSquare = false;
        kid.Halt();

        Point? targetTile = null;

        if (!targetTile.HasValue || !IsTileStandable(farm, targetTile.Value))
        {
            for (int i = 0; i < 30; i++)
            {
                Point trial = kid.TilePoint + new Point(Random.Shared.Next(-10, 10), Random.Shared.Next(-10, 10));
                if (IsTileStandable(farm, trial))
                {
                    targetTile = trial;
                    break;
                }
            }
            if (!targetTile.HasValue)
            {
                return;
            }
        }

        ModEntry.LogDebug($"FarmPathFinding({kid.Name}): {kid.TilePoint} -> {targetTile.Value}");
        kid.controller = new PathFindController(kid, farm, targetTile.Value, -1, kid.toddlerReachedDestination);
        if (
            kid.controller.pathToEndPoint == null
            || !kid.controller.pathToEndPoint.Any()
            || !farm.isTileOnMap(kid.controller.pathToEndPoint.Last())
        )
        {
            kid.controller = null;
        }
    }

    private static void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        GoingToTheFarm.Clear();
        ReturnKidsToHouse(AllKids());
    }

    internal static void ReturnKidsToHouse(IEnumerable<Child> kidsList)
    {
        List<Child> needWarp = [];
        foreach (Child kid in kidsList)
        {
            if (kid.currentLocation is not FarmHouse)
            {
                needWarp.Add(kid);
            }
        }
        foreach (Child kid in needWarp)
        {
            WarpKidToHouse(kid, false);
        }
    }
    #endregion
}
