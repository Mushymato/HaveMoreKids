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
using StardewValley.Buildings;
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
    private const string Character_ModData_NextKidId = $"{ModEntry.ModId}/NextKidId";
    private const string Stats_daysUntilBirth = $"{ModEntry.ModId}_daysUntilBirth";
    internal const string WhoseKids_Shared = $"{ModEntry.ModId}#SHARED";
    internal const string ChildNPC_Suffix = $"@{ModEntry.ModId}";

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

    internal static bool TrySetNextKid(NPC parent, string kidId)
    {
        if (
            TryGetAvailableSpouseOrSharedKidIds(parent, out List<string>? availableKidIds)
            && availableKidIds.Contains(kidId)
        )
        {
            parent.modData[Character_ModData_NextKidId] = kidId;
        }
        return false;
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
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
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
                        if (AssetManager.ChildData.ContainsKey(kidId))
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
            if (
                kid.Age > 2
                && AssetManager.ChildData.TryGetValue(kid.Name, out CharacterData? childCharaData)
                && !string.IsNullOrEmpty(childCharaData.CanSocialize)
                && !GameStateQuery.IsImmutablyFalse(childCharaData.CanSocialize)
            )
            {
                kidNPCId = FormChildNPCId(kid.Name);
            }
            KidEntries[kid.Name] = new(
                kidNPCId,
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
            if (Game1.player.getSpouse() is NPC spouse)
            {
                ChooseAndApplyKidId(spouse, kid);
            }
        }
    }

    /// <summary>Do some kid checks on day started</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        Game1.player.stats.Decrement(Stats_daysUntilBirth);
        if (!Context.IsMainPlayer)
        {
            return;
        }
        CribManager.RecheckCribAssignments();
        int totalDaysChild = ModEntry.Config.TotalDaysChild;
        // update all kids
        foreach ((Farmer farmer, Child kid) in AllFarmersAndKids())
        {
            kid.reloadSprite();
            // check if today is a Child day or a NPC day
            if (
                kid.GetData() is CharacterData childCharaData
                && KidEntries.TryGetValue(kid.Name, out KidEntry? entry)
                && entry.KidNPCId != null
                && Game1.getCharacterFromName(entry.KidNPCId) is NPC childAsNPC
            )
            {
                bool stayHome = true;
                if (
                    totalDaysChild >= 0
                    && kid.daysOld.Value >= totalDaysChild
                    && !string.IsNullOrEmpty(childCharaData.CanSocialize)
                )
                {
                    kid.Age = 4;
                    if (
                        GameStateQuery.CheckConditions(
                            childCharaData.CanSocialize,
                            new GameStateQueryContext(null, farmer, null, null, Game1.random)
                        )
                    )
                    {
                        kid.IsInvisible = true;
                        kid.daysUntilNotInvisible = 1;
                        stayHome = false;
                    }
                }

                if (stayHome)
                {
                    childAsNPC.IsInvisible = true;
                    childAsNPC.daysUntilNotInvisible = 1;
                    ModEntry.Log($"Child '{kid.displayName}' ({kid.Name}) will stay home today", LogLevel.Info);
                }
                else
                {
                    childAsNPC.GetData();
                    if (!Game1.player.friendshipData.TryGetValue(kid.Name, out Friendship childFriendship))
                    {
                        childFriendship = new Friendship(0);
                        Game1.player.friendshipData[kid.Name] = childFriendship;
                    }
                    // might be haunted in multiplayer?
                    Game1.player.friendshipData[entry.KidNPCId] = childFriendship;
                    childAsNPC.reloadSprite(onlyAppearance: true);
                    childAsNPC.InvalidateMasterSchedule();
                    childAsNPC.TryLoadSchedule();
                    childAsNPC.performSpecialScheduleChanges();
                    childAsNPC.resetSeasonalDialogue();
                    childAsNPC.resetCurrentDialogue();
                    childAsNPC.Sprite.UpdateSourceRect();
                    ModEntry.Log($"Child '{kid.displayName}' ({kid.Name}) will go outside today", LogLevel.Info);
                    // ModEntry.help.Events.Player.Warped += OnWarped;
                }
            }
        }
    }

    internal static bool IsTileStandable(GameLocation location, Point tile)
    {
        return location.hasTileAt(tile.X, tile.Y, "Back")
            && location.CanItemBePlacedHere(tile.ToVector2())
            && !location.isWaterTile(tile.X, tile.Y)
            && !location.warps.Any(warp => warp.X == tile.X && warp.Y == warp.Y);
    }

    private static readonly Dictionary<long, (Child, Point)> goingToTheFarm = [];

    /// <summary>
    /// Possibly skipping prefix for Child.tenMinuteUpdate
    /// </summary>
    /// <param name="kid"></param>
    /// <returns></returns>
    internal static bool TenMinuteUpdate(Child kid)
    {
        ModEntry.Log($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name}");
        // at 1900, banish them back to the house, and then allow vanilla logic to run
        if (Game1.timeOfDay >= 1900 && kid.currentLocation is not FarmHouse)
        {
            goingToTheFarm.Clear();
            ModEntry.Log($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} -> return to farmhouse");
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.GetPlayer(kid.idOfParent.Value));
            int childIndex = kid.GetChildIndex();
            Point childBedSpot = farmHouse.GetChildBedSpot(childIndex);
            DelayedAction.functionAfterDelay(
                () =>
                {
                    Game1.warpCharacter(kid, farmHouse, childBedSpot.ToVector2());
                    kid.controller = null;
                },
                0
            );
            return true;
        }

        // before 1100, roll and see if the child could go outside
        if (
            Game1.timeOfDay < 1100
            && kid.currentLocation is FarmHouse farmhouse
            && farmhouse.GetParentLocation() is Farm farm
        )
        {
            // only 1 kid allowed to path to farm, they can't already be pathing to somewhere, and there's some rng
            if (goingToTheFarm.ContainsKey(kid.idOfParent.Value) || kid.controller != null || Random.Shared.NextBool())
            {
                return false;
            }

            ModEntry.Log($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} -> go outside");
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

            for (int i = 0; i < 30; i++)
            {
                Point trial =
                    new(houseEntry.X + Random.Shared.Next(-4, 4), houseEntry.Y + 2 + Random.Shared.Next(0, 8));
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
                        goingToTheFarm[kid.idOfParent.Value] = new(kid, trial);
                        return false;
                    }
                }
            }
        }

        // while the kid is on the farm, do some path find controller things
        FarmPathFinding(kid);
        return false;
    }

    private static void WarpKidToFarm(Character c, GameLocation l)
    {
        if (
            c is not Child kid
            || l.GetParentLocation() is not Farm farm
            || !goingToTheFarm.TryGetValue(kid.idOfParent.Value, out (Child, Point) info)
        )
            return;
        if (kid != info.Item1)
            return;

        kid.Halt();
        kid.controller = null;
        Game1.warpCharacter(kid, farm, info.Item2.ToVector2());
        kid.toddlerReachedDestination(kid, l);
        goingToTheFarm.Remove(kid.idOfParent.Value);
        ModEntry.Log($"WarpKidToFarm({Game1.timeOfDay}): {kid.Name}");
    }

    private static void FarmPathFinding(Child kid)
    {
        // already in a path find
        if (kid.controller != null || kid.currentLocation is not Farm farm)
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

        ModEntry.Log($"FarmPathFinding({kid.Name}): {kid.TilePoint} -> {targetTile.Value}");
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

    /// <summary>Unset HMK related data on saving</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        foreach (Child kid in AllKids())
        {
            if (kid.modData.TryGetValue(Child_ModData_DisplayName, out string? displayName))
            {
#if DEBUG
                ModEntry.Log($"Unset before saving: '{displayName}' ({kid.Name})");
#endif
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
#if DEBUG
                ModEntry.Log($"Restore after saving: '{kid.Name}' ({kidId})");
#endif

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

    internal static void HaveAKid(
        NPC? spouse,
        string? newKidId,
        bool isDarkSkinned,
        string babyName,
        out WhoseKidData? whoseKidForTwin,
        bool isTwin
    )
    {
        // create and add kid
        Child newKid;
        if (spouse != null)
        {
            if (PickForSpecificKidId(spouse, babyName) is string specificKidName)
            {
                newKidId = specificKidName;
            }
            spouse.modData.Remove(Character_ModData_NextKidId);
        }

        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);
        babyName = AntiNameCollision(babyName);
        whoseKidForTwin = null;
        if (newKidId != null && AssetManager.ChildData.TryGetValue(newKidId, out CharacterData? childData))
        {
            newKid = ApplyKidId(
                spouse?.Name ?? "SOLO_BIRTH",
                new(newKidId, childData.Gender == Gender.Male, childData.IsDarkSkinned, Game1.player),
                true,
                babyName,
                newKidId
            );
            if (
                !isTwin
                && AssetManager.WhoseKidsRaw.TryGetValue(newKidId, out WhoseKidData? whoseKid)
                && whoseKid.Twin != null
                && GameStateQuery.CheckConditions(whoseKid.TwinCondition, farmHouse, Game1.player)
            )
            {
                whoseKidForTwin = whoseKid;
            }
        }
        else
        {
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

        newKid.Age = 0;
        farmHouse.characters.Add(newKid);
        newKid.currentLocation = farmHouse;

        Game1.morningQueue.Enqueue(() =>
        {
            string text2 =
                Game1.getCharacterFromName(Game1.player.spouse)?.GetTokenizedDisplayName() ?? Game1.player.spouse;
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
        if (AssetManager.WhoseKids.TryGetValue(spouseId, out Dictionary<string, WhoseKidData>? whoseKidsInfo))
        {
            kidIds = [];
            foreach ((string key, WhoseKidData data) in whoseKidsInfo)
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
        [NotNullWhen(true)] out Dictionary<string, WhoseKidData>? whoseKidsInfo
    )
    {
        kidIds = null;
        if (AssetManager.WhoseKids.TryGetValue(spouseId, out whoseKidsInfo))
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
        string kidName = kid.Name;
        if (kidName == null || PickKidId(spouse, kid) is not string newKidId)
            return kid;
        return ApplyKidId(spouse.Name, kid, false, kidName, newKidId);
    }

    internal static Child ApplyKidId(string? spouseName, Child newKid, bool newBorn, string kidName, string newKidId)
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
        if (newKid.GetData() is not CharacterData data)
        {
            ModEntry.Log($"Failed to get data for child ID '{newKidId}', '{kidName}' may be broken.", LogLevel.Error);
            return newKid;
        }
        newKid.Gender = data.Gender;
        newKid.darkSkinned.Value = data.IsDarkSkinned;
        newKid.reloadSprite(onlyAppearance: true);
        ModEntry.Log($"Assigned '{newKidId}' to child named '{kidName}'.", LogLevel.Info);
        return newKid;
    }
}
