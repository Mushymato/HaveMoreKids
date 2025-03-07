using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids;

internal static class AssetManager
{
    private static string NPC_CustomFields_KidId_Prefix => $"{ModEntry.ModId}/Kid.";
    private static string Asset_ChildData => $"{ModEntry.ModId}/ChildData";
    internal static string Child_ModData_DisplayName => $"{ModEntry.ModId}/DisplayName";
    internal static string Child_ModData_NPCParent => $"{ModEntry.ModId}/NPCParent";
    internal static string NPC_ModData_NextKidId => $"{ModEntry.ModId}/NextKidId";

    private static Dictionary<string, CharacterData>? childData = null;
    private static ITranslationHelper translation = null!;

    internal static Dictionary<string, CharacterData> ChildData
    {
        get
        {
            if (childData == null)
            {
                childData = Game1.content.Load<Dictionary<string, CharacterData>>(Asset_ChildData);
                foreach (var kv in childData)
                {
                    kv.Value.Age = NpcAge.Child;
                    kv.Value.CanBeRomanced = false;
                }
            }
            return childData;
        }
    }

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        translation = helper.Translation;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_ChildData))
            e.LoadFrom(() => new Dictionary<string, CharacterData>(), AssetLoadPriority.Low);
        if (e.NameWithoutLocale.IsEquivalentTo("Strings/Events") && e.Name.LocaleCode == translation.Locale)
        {
            e.Edit(
                (asset) =>
                {
                    if (
                        translation
                            .GetInAllLocales("StringEvents.BabyNamingTitle")
                            .TryGetValue(translation.Locale, out var babyNaming)
                    )
                    {
                        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
                        data["BabyNamingTitle_Male"] = babyNaming;
                        data["BabyNamingTitle_Female"] = babyNaming;
                    }
                },
                AssetEditPriority.Early
            );
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
    internal static string? PickKidId(NPC spouse, Child? child = null, bool newBorn = false)
    {
        if (!TryGetKidIds(spouse.GetData(), out IList<string>? kidIds, out _))
            return null;
        if (!newBorn && child?.Name != null && kidIds.Contains(child.Name))
            return null;
        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        string[] availableKidIds = kidIds
            .Where(id =>
                ChildData.ContainsKey(id)
                && !children.Contains(id)
                && !ModEntry.Config.DisabledKids.GetValueOrDefault(new(spouse.Name, id))
            )
            .ToArray();
        if (availableKidIds.Length == 0)
            return null;
        // Prioritize the kid id set by trigger action, if it is valid
        if (
            spouse.modData.TryGetValue(NPC_ModData_NextKidId, out string? nextKidId)
            && availableKidIds.Contains(nextKidId)
        )
        {
            spouse.modData.Remove(NPC_ModData_NextKidId);
            return nextKidId;
        }
        // Prioritize "real name" if found
        List<string> matchingSkinTone = [];
        if (child != null)
        {
            foreach (var kidId in availableKidIds)
            {
                if (ChildData.TryGetValue(kidId, out CharacterData? data))
                {
                    if (TokenParser.ParseText(data.DisplayName) == child.Name)
                        return kidId;
                    if (data.IsDarkSkinned == child.darkSkinned.Value)
                        matchingSkinTone.Add(kidId);
                }
            }
        }
        Random daysPlayedRand = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
        if (matchingSkinTone.Count > 0)
            return matchingSkinTone[daysPlayedRand.Next(availableKidIds.Length)];
        return availableKidIds[daysPlayedRand.Next(availableKidIds.Length)];
    }

    internal static Child ChooseAndApplyKidId(NPC spouse, Child newKid, bool newBorn = false)
    {
        string kidName = newKid.Name;
        if (PickKidId(spouse, newKid, newBorn) is not string newKidId)
            return newKid;
        newKid.modData[Child_ModData_DisplayName] = kidName;
        newKid.modData[Child_ModData_NPCParent] = spouse.Name;
        newKid.Name = newKidId;
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
