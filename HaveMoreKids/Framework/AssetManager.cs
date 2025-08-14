using System.Diagnostics.CodeAnalysis;
using Force.DeepCloner;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal static class AssetManager
{
    private const string NPC_CustomFields_KidId_Prefix = $"{ModEntry.ModId}/Kid.";
    internal const string Appearances_Prefix_Baby = "HMK_BABY";
    private const string Asset_ChildData = $"{ModEntry.ModId}/ChildData";
    private const string Asset_SharedKids = $"{ModEntry.ModId}/SharedKids";
    private const string Asset_Strings = $"{ModEntry.ModId}/Strings";
    private const string Asset_DefaultTextureName = $"{ModEntry.ModId}_NoPortrait";
    private const string Asset_NoPortrait = $"Portraits/{Asset_DefaultTextureName}";
    private const string Asset_PortraitPrefix = $"Portraits/{ModEntry.ModId}@";
    private const string Asset_SpritePrefix = $"Characters/{ModEntry.ModId}@";
    internal const string Asset_StringsUI = "Strings/UI";
    internal const string Asset_DataCharacters = "Data/Characters";
    internal const string Asset_DataNPCGiftTastes = "Data/NPCGiftTastes";
    internal const string Asset_CharactersDialogue = "Characters/Dialogue/";
    internal const string Asset_CharactersSchedule = "Characters/schedules/";
    internal static string[] ChildForwardedAssets = [Asset_CharactersDialogue, Asset_CharactersSchedule];
    internal const string Child_ModData_DisplayName = $"{ModEntry.ModId}/DisplayName";
    internal const string Child_ModData_NPCParent = $"{ModEntry.ModId}/NPCParent";
    internal const string Child_ModData_Birthday = $"{ModEntry.ModId}/Birthday";
    internal const string Child_ModData_AsNPC = $"{ModEntry.ModId}/AsNPC";
    internal const string ModData_NextKidId = $"{ModEntry.ModId}/NextKidId";

    private static Dictionary<string, CharacterData>? childData = null;

    internal static bool AppearanceIsValid(CharacterAppearanceData appearance)
    {
        return Game1.content.DoesAssetExist<Texture2D>(appearance.Sprite);
    }

    internal static bool AppearanceIsUnconditional(CharacterAppearanceData appearance)
    {
        ModEntry.LogOnce(
            $"{appearance.Id}: {appearance.Condition} {appearance.Indoors} {appearance.Outdoors} {appearance.IsIslandAttire}",
            LogLevel.Debug
        );
        return (string.IsNullOrEmpty(appearance.Condition) || GameStateQuery.IsImmutablyTrue(appearance.Condition))
            && appearance.Indoors
            && appearance.Outdoors
            && !appearance.IsIslandAttire;
    }

    internal static bool AppearanceIsBaby(CharacterAppearanceData appearance)
    {
        return appearance.Id?.StartsWith(Appearances_Prefix_Baby) ?? false;
    }

    internal static Dictionary<string, CharacterData> ChildData
    {
        get
        {
            if (childData == null)
            {
                HashSet<string> invalidKidEntries = [];
                childData = Game1.content.Load<Dictionary<string, CharacterData>>(Asset_ChildData);
                foreach ((string key, CharacterData value) in childData)
                {
                    value.Age = NpcAge.Child;
                    value.CanBeRomanced = false;
                    value.Calendar = CalendarBehavior.HiddenAlways;
                    value.SocialTab = SocialTabBehavior.HiddenAlways;
                    value.EndSlideShow = EndSlideShowBehavior.Hidden;
                    value.FlowerDanceCanDance = false;
                    value.PerfectionScore = false;

                    HashSet<CharacterAppearanceData> invalidAppearances = [];
                    byte isValidKidEntry = 0b00;
                    foreach (CharacterAppearanceData appearance in value.Appearance)
                    {
                        if (!AppearanceIsValid(appearance))
                        {
                            invalidAppearances.Add(appearance);
                            continue;
                        }
                        if (!Game1.content.DoesAssetExist<Texture2D>(appearance.Portrait))
                        {
                            appearance.Portrait = Asset_NoPortrait;
                        }
                        // check for an unconditional appearance entry
                        if (isValidKidEntry != 0b11 && AppearanceIsUnconditional(appearance))
                        {
                            if (AppearanceIsBaby(appearance))
                            {
                                isValidKidEntry |= 0b01;
                            }
                            else
                            {
                                isValidKidEntry |= 0b10;
                            }
                        }
                    }
                    if (invalidAppearances.Any())
                    {
                        ModEntry.Log(
                            $"Removed child appearances with invalid sprites from '{key}': {string.Join(',', invalidAppearances.Select(apr => apr.Id))}",
                            LogLevel.Warn
                        );
                        value.Appearance.RemoveAll(invalidAppearances.Contains);
                    }
                    if (isValidKidEntry != 0b11)
                        invalidKidEntries.Add(key);
                }
                if (invalidKidEntries.Any())
                {
                    ModEntry.Log(
                        $"Removed {invalidKidEntries.Count} invalid entries that lack an unconditional Appearance in {Asset_ChildData}: {string.Join(", ", invalidKidEntries)}",
                        LogLevel.Warn
                    );
                    childData.RemoveWhere(kv => invalidKidEntries.Contains(kv.Key));
                }
            }
            return childData;
        }
    }

    internal static Dictionary<string, bool> SharedKids =>
        Game1.content.Load<Dictionary<string, bool>>(Asset_SharedKids);

    internal static string LoadString(string key) => Game1.content.LoadString($"{Asset_Strings}:{key}");

    internal static string LoadString(string key, params object[] substitutions) =>
        Game1.content.LoadString($"{Asset_Strings}:{key}", substitutions);

    internal static bool TryLoadString(
        string key,
        [NotNullWhen(true)] out string? loaded,
        params object[] substitutions
    ) => (loaded = Game1.content.LoadStringReturnNullIfNotFound($"{Asset_Strings}:{key}", substitutions)) != null;

    internal static MarriageDialogueReference? LoadMarriageDialogueReference(string key)
    {
        if (Game1.content.LoadStringReturnNullIfNotFound($"{Asset_Strings}:{key}") is null)
        {
            return null;
        }
        return new MarriageDialogueReference(Asset_Strings, key, true);
    }

    private static IModHelper Helper = null!;

    internal static void Register(IModHelper helper)
    {
        Helper = helper;
        Helper.Events.Content.AssetRequested += OnAssetRequested;
        Helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        Helper.Events.Specialized.LoadStageChanged += OnLoadStageChanged;
    }

    private static readonly Dictionary<string, (string, string)> ChildToNPC = [];

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
                foreach (Child child in farmer.getChildren())
                {
                    if (child.modData.TryGetValue(Child_ModData_DisplayName, out string? displayName))
                    {
                        ModEntry.Log($"child.displayName: {child.displayName} -> {displayName}");
                        child.displayName = displayName;
                    }
                    else
                    {
                        ModEntry.Log($"child.displayName: {child.displayName}");
                    }
                    if (
                        ChildData.TryGetValue(child.Name, out CharacterData? childCharaData)
                        && !string.IsNullOrEmpty(childCharaData.CanSocialize)
                        && !GameStateQuery.IsImmutablyFalse(childCharaData.CanSocialize)
                    )
                    {
                        string childNPCId = FormChildNPCId(child.Name, farmer.UniqueMultiplayerID);
                        ChildToNPC[childNPCId] = new(child.Name, child.displayName);
                        child.modData[Child_ModData_AsNPC] = childNPCId;
                    }
                }
            }
            ChildNPCSetup();
        }
    }

    private static void ChildNPCSetup()
    {
        if (ChildToNPC.Any())
        {
            Helper.GameContent.InvalidateCache(Asset_DataCharacters);
            Helper.GameContent.InvalidateCache(Asset_DataNPCGiftTastes);
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_ChildData))
            e.LoadFrom(() => new Dictionary<string, CharacterData>(), AssetLoadPriority.Low);
        if (e.Name.IsEquivalentTo(Asset_SharedKids))
            e.LoadFrom(() => new Dictionary<string, bool>(), AssetLoadPriority.Low);
        if (e.Name.IsEquivalentTo(Asset_Strings))
            e.LoadFromModFile<Dictionary<string, string>>("i18n/default/strings.json", AssetLoadPriority.Exclusive);
        if (e.Name.IsEquivalentTo(Asset_NoPortrait))
            e.LoadFromModFile<Texture2D>("assets/no_portrait.png", AssetLoadPriority.Exclusive);

        if (e.Name.IsEquivalentTo(Asset_StringsUI) && e.Name.LocaleCode == Helper.Translation.Locale)
            e.Edit(Edit_StringsUI, AssetEditPriority.Late);

        if (e.Name.StartsWith(Asset_PortraitPrefix) || e.Name.StartsWith(Asset_SpritePrefix))
            e.LoadFrom(() => Game1.content.Load<Texture2D>(Asset_NoPortrait), AssetLoadPriority.Low);

        if (ChildToNPC.Any())
        {
            if (e.Name.IsEquivalentTo(Asset_DataCharacters))
                e.Edit(Edit_DataCharacters, AssetEditPriority.Early);
            if (e.NameWithoutLocale.IsEquivalentTo(Asset_DataNPCGiftTastes))
                e.Edit(Edit_DataNPCGiftTastes, AssetEditPriority.Late);
            foreach ((string childNPCId, (string childId, _)) in ChildToNPC)
            {
                foreach (string fwdAsset in ChildForwardedAssets)
                {
                    string fwdSrcAsset = string.Concat(fwdAsset, childId);
                    if (e.NameWithoutLocale.IsEquivalentTo(string.Concat(fwdAsset, childNPCId)))
                    {
                        e.LoadFrom(() => ForwardFrom_ChildIdAsset(fwdSrcAsset), AssetLoadPriority.Low);
                    }
                }
            }
        }
    }

    private static void Edit_StringsUI(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        foreach (
            string key in new string[]
            {
                "AskedToHaveBaby_Accepted_Male",
                "AskedToHaveBaby_Accepted_Female",
                "AskedToAdoptBaby_Accepted_Male",
                "AskedToAdoptBaby_Accepted_Female",
            }
        )
        {
            data[key] = data[key].Replace("14", ModEntry.Config.DaysPregnant.ToString());
        }
    }

    private static void Edit_DataCharacters(IAssetData asset)
    {
        IDictionary<string, CharacterData> data = asset.AsDictionary<string, CharacterData>().Data;
        foreach ((string childNPCId, (string childId, string childName)) in ChildToNPC)
        {
            if (ChildData.TryGetValue(childId, out CharacterData? childCharaData))
            {
                childCharaData = childCharaData.ShallowClone();
                childCharaData.DisplayName = childName;
                childCharaData.TextureName ??= Asset_DefaultTextureName;
                childCharaData.SpawnIfMissing = true;
                foreach (CharacterAppearanceData appearanceData in Enumerable.Reverse(childCharaData.Appearance))
                {
                    if (AppearanceIsBaby(appearanceData))
                    {
                        childCharaData.Appearance.Remove(appearanceData);
                    }
                }
                data[childNPCId] = childCharaData;
            }
        }
    }

    private static void Edit_DataNPCGiftTastes(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        foreach ((string childNPCId, (string childId, _)) in ChildToNPC)
        {
            if (data.TryGetValue(childId, out string? giftTastes))
            {
                data[childNPCId] = giftTastes;
            }
        }
    }

    private static object ForwardFrom_ChildIdAsset(string assetName)
    {
        if (Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName))
        {
            return Game1.content.Load<Dictionary<string, string>>(assetName).DeepClone();
        }
        return new Dictionary<string, string>();
    }

    private static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_ChildData)))
        {
            childData = null;
            Helper.GameContent.InvalidateCache(Asset_DataCharacters);
            ModEntry.Config.ResetMenu();
        }
        if (
            e.NamesWithoutLocale.Any(name =>
                name.IsEquivalentTo(Asset_DataCharacters) || name.IsEquivalentTo(Asset_SharedKids)
            )
        )
        {
            ModEntry.Config.ResetMenu();
        }
        foreach ((string childNPCId, (string childId, _)) in ChildToNPC)
        {
            foreach (string fwdAsset in ChildForwardedAssets)
            {
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(string.Concat(fwdAsset, childId))))
                {
                    ModEntry.Log($"{fwdAsset}{childId} -> {fwdAsset}{childNPCId}");
                    Helper.GameContent.InvalidateCache(string.Concat(fwdAsset, childNPCId));
                }
            }
        }
    }

    /// <summary>Get and validate kid id</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    internal static bool TryGetSpouseKidIds(
        CharacterData? data,
        [NotNullWhen(true)] out IList<string>? kidIds,
        [NotNullWhen(true)] out IDictionary<string, bool>? enabledByDefault
    )
    {
        kidIds = null;
        enabledByDefault = null;
        if (data?.CustomFields is Dictionary<string, string> customFields)
        {
            kidIds = [];
            enabledByDefault = new Dictionary<string, bool>();
            foreach (var kv in customFields)
            {
                if (kv.Key.StartsWith(NPC_CustomFields_KidId_Prefix))
                {
                    string kidId = kv.Key[NPC_CustomFields_KidId_Prefix.Length..];
                    if (ChildData.ContainsKey(kidId))
                    {
                        kidIds.Add(kidId);
                        enabledByDefault[kidId] = !bool.TryParse(kv.Value, out bool enabled) || enabled;
                    }
                }
            }
            return kidIds.Count > 0;
        }
        return false;
    }

    /// <summary>Choose a new kid Id. If originalName is given, only choose new one if the existing pick is invalid for the specific spouse</summary>
    /// <param name="spouse"></param>
    /// <param name="originalName"></param>
    /// <returns></returns>
    internal static string? PickKidId(NPC spouse, Child? child = null, bool? darkSkinned = null)
    {
        if (
            !TryGetAvailableKidIds(spouse, out string[]? availableKidIds)
            && !TryGetAvailableSharedKidIds(out availableKidIds)
        )
        {
            return null;
        }

        // Prioritize the kid id set by trigger action, if it is valid
        if (spouse.modData.TryGetValue(ModData_NextKidId, out string? nextKidId) && availableKidIds.Contains(nextKidId))
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

    private static bool TryGetAvailableKidIds(NPC spouse, [NotNullWhen(true)] out string[]? availableKidIds)
    {
        availableKidIds = null;
        if (!TryGetSpouseKidIds(spouse.GetData(), out IList<string>? kidIds, out _))
            return false;

        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        availableKidIds = kidIds
            .Where(id =>
                ChildData.ContainsKey(id)
                && !children.Contains(id)
                && ModEntry.Config.EnabledKids.GetValueOrDefault(new(spouse.Name, id))
            )
            .ToArray();
        return availableKidIds.Length > 0;
    }

    internal static bool TryGetAvailableSharedKidIds([NotNullWhen(true)] out string[]? sharedKidIds)
    {
        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        sharedKidIds = SharedKids
            .Keys.Where(id =>
                ChildData.ContainsKey(id)
                && !children.Contains(id)
                && ModEntry.Config.EnabledKids.GetValueOrDefault(new(ModConfig.SHARED_KEY, id))
            )
            .ToArray();
        ModEntry.Log($"Available sharedKidIds: {string.Join(",", SharedKids.Keys)}");
        return sharedKidIds.Length > 0;
    }

    internal static string PickMostLikelyKidId(
        string[] availableKidIds,
        bool? darkSkinned,
        Gender? gender,
        string? name
    )
    {
        // Prioritize "real name" if found
        List<string> moreLikelyMatch = [];
        foreach (var kidId in availableKidIds)
        {
            if (ChildData.TryGetValue(kidId, out CharacterData? data))
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
        return availableKidIds[daysPlayedRand.Next(availableKidIds.Length)];
    }

    internal static string? PickForSpecificKidId(NPC spouse, string name)
    {
        // Check for "real name" kid
        if (!TryGetAvailableKidIds(spouse, out string[]? availableKidIds))
        {
            return null;
        }
        foreach (var kidId in availableKidIds)
        {
            if (ChildData.TryGetValue(kidId, out CharacterData? data))
            {
                if (name != null && TokenParser.ParseText(data.DisplayName) == name)
                    return kidId;
            }
        }
        return null;
    }

    internal static Child ChooseAndApplyKidId(NPC spouse, Child newKid, bool newBorn = false)
    {
        string kidName = newKid.Name;
        if (kidName == null)
            return newKid;
        if (SharedKids.ContainsKey(kidName))
            return newKid;
        if (TryGetSpouseKidIds(spouse.GetData(), out IList<string>? kidIds, out _) && kidIds.Contains(newKid.Name))
            return newKid;

        if (PickKidId(spouse, newKid) is not string newKidId)
            return newKid;
        return ApplyKidId(spouse.Name, newKid, newBorn, kidName, newKidId);
    }

    internal static Child ApplyKidId(string? spouseName, Child newKid, bool newBorn, string kidName, string newKidId)
    {
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
        ModEntry.Log($"Assigned '{newKidId}' to child named '{kidName}'.");
        return newKid;
    }
}
