using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal static class AssetManager
{
    private const string NPC_CustomFields_KidId_Prefix = $"{ModEntry.ModId}/Kid.";
    private const string Asset_ChildData = $"{ModEntry.ModId}/ChildData";
    private const string Asset_SharedKids = $"{ModEntry.ModId}/SharedKids";
    private const string Asset_Strings = $"{ModEntry.ModId}\\Strings";
    internal const string Child_ModData_DisplayName = $"{ModEntry.ModId}/DisplayName";
    internal const string Child_ModData_NPCParent = $"{ModEntry.ModId}/NPCParent";
    internal const string Child_ModData_Birthday = $"{ModEntry.ModId}/Birthday";
    internal const string ModData_NextKidId = $"{ModEntry.ModId}/NextKidId";

    private static Dictionary<string, CharacterData>? childData = null;

    internal static Dictionary<string, CharacterData> ChildData
    {
        get
        {
            if (childData == null)
            {
                childData = Game1.content.Load<Dictionary<string, CharacterData>>(Asset_ChildData);
                foreach ((string key, CharacterData value) in childData)
                {
                    value.Age = NpcAge.Child;
                    value.CanBeRomanced = false;
                    foreach (CharacterAppearanceData appearance in value.Appearance)
                    {
                        if (string.IsNullOrEmpty(appearance.Portrait))
                        {
                            appearance.Portrait = appearance.Sprite;
                        }
                    }
                }
            }
            return childData;
        }
    }

    internal static string[] SharedKids => Game1.content.Load<string[]>(Asset_SharedKids);

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

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_ChildData))
            e.LoadFrom(() => new Dictionary<string, CharacterData>(), AssetLoadPriority.Low);
        if (e.Name.IsEquivalentTo(Asset_SharedKids))
            e.LoadFrom(() => new Dictionary<string, bool>(), AssetLoadPriority.Low);
        if (e.Name.IsEquivalentTo(Asset_Strings))
        {
            e.LoadFromModFile<Dictionary<string, string>>("i18n/default/strings.json", AssetLoadPriority.Exclusive);
            // make these strings gender neutral so I don't have to deal with them :)
        }
    }

    private static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_ChildData)))
        {
            childData = null;
            ModEntry.Config.ResetMenu();
        }
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data/Characters")))
        {
            ModEntry.Config.ResetMenu();
        }
    }

    /// <summary>Get and validate kid id</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    internal static bool TryGetKidIds(
        CharacterData? data,
        [NotNullWhen(true)] out IList<string>? kidIds,
        [NotNullWhen(true)] out IDictionary<string, bool>? disabledByDefault
    )
    {
        kidIds = null;
        disabledByDefault = null;
        if (data?.CustomFields is Dictionary<string, string> customFields)
        {
            kidIds = [];
            disabledByDefault = new Dictionary<string, bool>();
            foreach (var kv in customFields)
            {
                if (kv.Key.StartsWith(NPC_CustomFields_KidId_Prefix))
                {
                    string kidId = kv.Key[NPC_CustomFields_KidId_Prefix.Length..];
                    if (ChildData.ContainsKey(kidId))
                    {
                        kidIds.Add(kidId);
                        disabledByDefault[kidId] = bool.TryParse(kv.Value, out bool enabled) && !enabled;
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
        if (!TryGetAvailableKidIds(spouse, out string[]? availableKidIds))
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
        if (!TryGetKidIds(spouse.GetData(), out IList<string>? kidIds, out _))
            return false;

        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        availableKidIds = kidIds
            .Where(id =>
                ChildData.ContainsKey(id)
                && !children.Contains(id)
                && !ModEntry.Config.DisabledKids.GetValueOrDefault(new(spouse.Name, id))
            )
            .ToArray();
        return availableKidIds.Length > 0;
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
        if (
            TryGetKidIds(spouse.GetData(), out IList<string>? kidIds, out _)
            && newKid.Name != null
            && kidIds.Contains(newKid.Name)
        )
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
