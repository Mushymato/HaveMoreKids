using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal static class KidHandler
{
    private const string Appearances_Prefix_Baby = "HMK_BABY";
    private const string Child_ModData_Id = $"{ModEntry.ModId}/Id";
    private const string Child_ModData_DisplayName = $"{ModEntry.ModId}/DisplayName";
    private const string Child_ModData_NPCParent = $"{ModEntry.ModId}/NPCParent";
    private const string Child_ModData_Birthday = $"{ModEntry.ModId}/Birthday";
    private const string Child_ModData_AsNPC = $"{ModEntry.ModId}/AsNPC";
    private const string Character_ModData_NextKidId = $"{ModEntry.ModId}/NextKidId";
    private const string Stats_daysUntilBirth = $"{ModEntry.ModId}_daysUntilBirth";
    internal const string WhoseKids_Shared = $"{ModEntry.ModId}#SHARED";

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

    internal static void Register()
    {
        // events
        ModEntry.help.Events.Specialized.LoadStageChanged += OnLoadStageChanged;
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.GameLoop.Saving += OnSaving;
        ModEntry.help.Events.GameLoop.Saved += OnSaved;
    }

    internal static Dictionary<string, (string, string)> ChildToNPC { get; private set; } = [];

    private static string FormChildNPCId(string childName, long uniqueMultiplayerId)
    {
        return $"{ModEntry.ModId}@{childName}@{uniqueMultiplayerId}";
    }

    private static void OnLoadStageChanged(object? sender, LoadStageChangedEventArgs e)
    {
        if (e.NewStage == StardewModdingAPI.Enums.LoadStage.SaveLoadedLocations && Context.IsMainPlayer)
        {
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                foreach (Child kid in farmer.getChildren())
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
            }
            ChildToNPC_Check();
        }
    }

    internal static void ChildToNPC_Check()
    {
        if (!Context.IsMainPlayer)
            return;

        ModEntry.Log("Check if any child needs NPC version");
        ChildToNPC.Clear();
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Child kid in farmer.getChildren())
            {
                if (
                    AssetManager.ChildData.TryGetValue(kid.Name, out CharacterData? childCharaData)
                    && !string.IsNullOrEmpty(childCharaData.CanSocialize)
                    && !GameStateQuery.IsImmutablyFalse(childCharaData.CanSocialize)
                )
                {
                    string childNPCId = FormChildNPCId(kid.Name, farmer.UniqueMultiplayerID);
                    ChildToNPC[childNPCId] = new(kid.Name, kid.displayName);
                    kid.modData[Child_ModData_AsNPC] = childNPCId;
                }
            }
        }
        ChildToNPC_Setup();
        MultiplayerSync.SendChildToNPC(null);
    }

    internal static void ChildToNPC_FromHost(Dictionary<string, (string, string)> newChildToNPC)
    {
        ChildToNPC = newChildToNPC;
        ChildToNPC_Setup();
    }

    private static void ChildToNPC_Setup()
    {
        if (!ChildToNPC.Any())
        {
            return;
        }
        foreach (var kv in ChildToNPC)
        {
            ModEntry.Log($"- {kv.Key}: {kv.Value}");
        }
        ModEntry.help.GameContent.InvalidateCache(AssetManager.Asset_DataCharacters);
        ModEntry.help.GameContent.InvalidateCache(AssetManager.Asset_DataNPCGiftTastes);
        if (!Context.IsMainPlayer)
        {
            foreach (string childAsNPCId in ChildToNPC.Keys)
            {
                if (Game1.getCharacterFromName(childAsNPCId) is NPC childAsNPC)
                {
                    childAsNPC.reloadSprite(onlyAppearance: true);
                    childAsNPC.Sprite.UpdateSourceRect();
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
        // Remove any invalid kids
        Utility.ForEachLocation(location =>
        {
            location.characters.RemoveWhere(
                (npc) =>
                {
                    if (npc?.Name is not string npcId)
                        return false;
                    if (childNPCPattern.Match(npcId) is not Match match || !match.Success)
                        return false;
                    if (ChildToNPC.ContainsKey(npc.Name))
                        return false;
                    ModEntry.Log(
                        $"Removed invalid kid NPC '{npc.Name}' from '{location.NameOrUniqueName}'",
                        LogLevel.Warn
                    );
                    return true;
                }
            );
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
        int totalDaysChild = ModEntry.Config.TotalDaysChild;
        // update all kids
        foreach (Child kid in Game1.player.getChildren())
        {
            // check if today is a Child day or a NPC day
            if (
                kid.GetData() is CharacterData childCharaData
                && kid.modData.TryGetValue(Child_ModData_AsNPC, out string childAsNPCId)
                && Game1.getCharacterFromName(childAsNPCId) is NPC childAsNPC
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
                    if (GameStateQuery.CheckConditions(childCharaData.CanSocialize))
                    {
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
                    Game1.player.friendshipData[childAsNPCId] = childFriendship;
                    childAsNPC.reloadSprite(onlyAppearance: true);
                    childAsNPC.InvalidateMasterSchedule();
                    childAsNPC.TryLoadSchedule();
                    childAsNPC.performSpecialScheduleChanges();
                    childAsNPC.resetSeasonalDialogue();
                    childAsNPC.resetCurrentDialogue();
                    childAsNPC.Sprite.UpdateSourceRect();
                    ModEntry.Log($"Child '{kid.displayName}' ({kid.Name}) will go outside today", LogLevel.Info);
                    ModEntry.help.Events.Player.Warped += OnWarped;
                }
            }
            else
            {
                kid.reloadSprite();
            }
        }
        Game1.player.stats.Decrement(Stats_daysUntilBirth);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.NewLocation is not FarmHouse)
        {
            foreach (Child kid in Game1.player.getChildren())
            {
                if (kid.daysUntilNotInvisible > 0)
                {
                    kid.IsInvisible = true;
                }
            }
            ModEntry.help.Events.Player.Warped -= OnWarped;
        }
    }

    /// <summary>Unset HMK related data on saving</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Child kid in farmer.getChildren())
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
    }

    /// <summary>Unset HMK related data on saving</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnSaved(object? sender, SavedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Child kid in farmer.getChildren())
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
            spouse.modData.Remove(Character_ModData_NextKidId);
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
            kidIds = whoseKidsInfo.Keys.ToList();
            return true;
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
        // Prioritize "real name" if found
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
        newKid.modData[Child_ModData_Id] = newKidId;
        newKid.modData[Child_ModData_DisplayName] = kidName;
        newKid.modData[Child_ModData_NPCParent] = spouseName;
        if (newBorn)
        {
            newKid.modData[Child_ModData_Birthday] = $"{Game1.season}|{Game1.dayOfMonth}";
        }
        else
        {
            SDate birthday = SDate.Now();
            int daysOld = newKid.daysOld.Value;
            while (daysOld > birthday.DaysSinceStart)
            {
                birthday.AddDays(28 * 40);
            }
            birthday = birthday.AddDays(-daysOld);
            newKid.modData[Child_ModData_Birthday] = $"{birthday.Season}|{birthday.Day}";
        }
        newKid.Name = newKidId;
        newKid.displayName = kidName;
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
