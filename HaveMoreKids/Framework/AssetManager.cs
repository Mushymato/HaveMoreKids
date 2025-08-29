using System.Diagnostics.CodeAnalysis;
using Force.DeepCloner;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Shops;

namespace HaveMoreKids.Framework;

public sealed class WhoseKidData
{
    public string? Parent { get; set; } = null;
    public bool Shared { get; set; } = false;
    public bool DefaultEnabled { get; set; } = true;
    public string? Condition { get; set; } = null;
    public string? Twin { get; set; } = null;
    public string? TwinCondition { get; set; } = null;
    public string? TwinMessage { get; set; } = null;
}

internal static class AssetManager
{
    private const string Asset_ChildData = $"{ModEntry.ModId}/ChildData";
    private const string Asset_WhoseKids = $"{ModEntry.ModId}/WhoseKids";
    private const string Asset_Strings = $"{ModEntry.ModId}\\Strings";
    internal const string Asset_DefaultTextureName = $"{ModEntry.ModId}_NoPortrait";
    private const string Asset_NoPortrait = $"Portraits/{Asset_DefaultTextureName}";

    // private const string Asset_PortraitPrefix = "Portraits/";
    // private const string Asset_SpritePrefix = "Characters/";
    internal const string Asset_DataCharacters = "Data/Characters";
    internal const string Asset_DataNPCGiftTastes = "Data/NPCGiftTastes";
    private const string Furniture_DefaultCrib = $"{ModEntry.ModId}_Crib";
    internal const string Asset_CharactersDialogue = "Characters/Dialogue/";
    private const string Asset_CharactersSchedule = "Characters/schedules/";
    private static readonly string[] KidNPCForwardAssets = [Asset_CharactersDialogue, Asset_CharactersSchedule];

    private static Dictionary<string, CharacterData>? childData = null;

    internal static Dictionary<string, CharacterData> ChildData
    {
        get
        {
            if (childData == null)
            {
                List<(string, string)> invalidKidData = [];
                childData = Game1.content.Load<Dictionary<string, CharacterData>>(Asset_ChildData);
                foreach ((string key, CharacterData value) in childData)
                {
                    if (Game1.characterData.ContainsKey(key))
                    {
                        invalidKidData.Add(new(key, "ID collides with NPC"));
                        continue;
                    }

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
                        if (!appearance.AppearanceIsValid())
                        {
                            invalidAppearances.Add(appearance);
                            continue;
                        }
                        if (!Game1.content.DoesAssetExist<Texture2D>(appearance.Portrait))
                        {
                            appearance.Portrait = Asset_NoPortrait;
                        }
                        // check for an unconditional appearance entry
                        if (isValidKidEntry != 0b11 && appearance.AppearanceIsUnconditional())
                        {
                            if (appearance.AppearanceIsBaby())
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
                        invalidKidData.Add(new(key, "must have an unconditional Appearance"));
                }
                if (invalidKidData.Any())
                {
                    ModEntry.Log(
                        $"Removed {invalidKidData.Count} invalid entries from {Asset_ChildData}:",
                        LogLevel.Warn
                    );
                    foreach ((string key, string reason) in invalidKidData)
                    {
                        ModEntry.Log($"- {key}: {reason}");
                        childData.Remove(key);
                    }
                }
            }
            return childData;
        }
    }

    private static Dictionary<string, WhoseKidData>? whoseKidsRaw = null;
    internal static Dictionary<string, WhoseKidData> WhoseKidsRaw =>
        whoseKidsRaw ??= Game1.content.Load<Dictionary<string, WhoseKidData>>(Asset_WhoseKids);
    private static Dictionary<string, Dictionary<string, WhoseKidData>>? whoseKids = null;
    internal static Dictionary<string, Dictionary<string, WhoseKidData>> WhoseKids
    {
        get
        {
            if (whoseKids != null)
                return whoseKids;
            whoseKids = [];
            whoseKids[KidHandler.WhoseKids_Shared] = [];
            foreach ((string kidId, WhoseKidData whose) in WhoseKidsRaw)
            {
                if (!ChildData.ContainsKey(kidId))
                    continue;
                if (whose.Parent != null && Game1.characterData.ContainsKey(whose.Parent))
                {
                    if (!whoseKids.TryGetValue(whose.Parent, out Dictionary<string, WhoseKidData>? npcWhosekids))
                    {
                        npcWhosekids = [];
                        whoseKids[whose.Parent] = npcWhosekids;
                    }
                    npcWhosekids[kidId] = whose;
                }
                else if (whose.Shared)
                {
                    whoseKids[KidHandler.WhoseKids_Shared][kidId] = whose;
                }
            }
            return whoseKids;
        }
    }

    internal static string LoadString(string key) => Game1.content.LoadString($"{Asset_Strings}:{key}");

    internal static string LoadString(string key, params object[] substitutions) =>
        Game1.content.LoadString($"{Asset_Strings}:{key}", substitutions);

    internal static string LoadStringReturnNullIfNotFound(string key, params object[] substitutions) =>
        Game1.content.LoadStringReturnNullIfNotFound($"{Asset_Strings}:{key}", substitutions);

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

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        ModEntry.help.Events.Content.AssetReady += OnAssetReady;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        IAssetName name = e.NameWithoutLocale;
        if (name.IsEquivalentTo(Asset_ChildData))
            e.LoadFrom(() => new Dictionary<string, CharacterData>(), AssetLoadPriority.Low);
        if (name.IsEquivalentTo(Asset_WhoseKids))
            e.LoadFrom(() => new Dictionary<string, WhoseKidData>(), AssetLoadPriority.Low);
        if (name.IsEquivalentTo(Asset_NoPortrait))
            e.LoadFromModFile<Texture2D>("assets/no_portrait.png", AssetLoadPriority.Exclusive);
        if (e.Name.IsEquivalentTo("Data/Furniture"))
            e.Edit(Edit_DataFurniture, AssetEditPriority.Default);
        if (e.Name.IsEquivalentTo("Data/Shops"))
            e.Edit(Edit_DataShops, AssetEditPriority.Default);
        if (name.IsEquivalentTo(Asset_Strings))
        {
            string stringsAsset = Path.Combine("i18n", e.Name.LanguageCode.ToString() ?? "default", "strings.json");
            if (File.Exists(stringsAsset))
            {
                e.LoadFromModFile<Dictionary<string, string>>(stringsAsset, AssetLoadPriority.Exclusive);
            }
            else
            {
                e.LoadFromModFile<Dictionary<string, string>>("i18n/default/strings.json", AssetLoadPriority.Exclusive);
            }
        }

        if (e.Name.IsEquivalentTo("Strings/UI") && e.Name.LocaleCode == ModEntry.help.Translation.Locale)
            e.Edit(Edit_StringsUI, AssetEditPriority.Late);

        if (KidHandler.KidNPCToKid.Any())
        {
            if (name.IsEquivalentTo(Asset_DataCharacters))
                e.Edit(Edit_DataCharacters, AssetEditPriority.Early);
            if (name.IsEquivalentTo(Asset_DataNPCGiftTastes))
                e.Edit(Edit_DataNPCGiftTastes, AssetEditPriority.Late);
            foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
            {
                if (entry.KidNPCId == null)
                    continue;
                foreach (string fwdAsset in KidNPCForwardAssets)
                {
                    if (name.IsEquivalentTo(string.Concat(fwdAsset, entry.KidNPCId)))
                    {
                        e.LoadFrom(
                            () => ForwardFrom_ChildIdAsset(string.Concat(fwdAsset, kidId)),
                            AssetLoadPriority.Low
                        );
                    }
                }
            }
        }
    }

    private static void Edit_DataFurniture(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        data[Furniture_DefaultCrib] =
            $"{Furniture_DefaultCrib}/decor/3 4/3 2/1/5000/-1/[LocalizedText {Asset_Strings}:Crib_Name]/185/Maps\\farmhouse_tiles/true/hmk_crib";
    }

    private static void Edit_DataShops(IAssetData asset)
    {
        IDictionary<string, ShopData> data = asset.AsDictionary<string, ShopData>().Data;
        if (data.TryGetValue("Carpenter", out ShopData? carpenterShop))
        {
            carpenterShop.Items.Add(
                new() { Id = $"{Furniture_DefaultCrib}_Default", ItemId = $"(F){Furniture_DefaultCrib}" }
            );
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
        foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
        {
            if (entry.KidNPCId != null && ChildData.TryGetValue(kidId, out CharacterData? childCharaData))
            {
                childCharaData = childCharaData.ShallowClone();
                childCharaData.DisplayName = entry.DisplayName;
                childCharaData.TextureName ??= Asset_DefaultTextureName;
                childCharaData.SpawnIfMissing = true;
                childCharaData.BirthSeason = entry.BirthSeason;
                childCharaData.BirthDay = entry.BirthDay;
                foreach (CharacterAppearanceData appearanceData in Enumerable.Reverse(childCharaData.Appearance))
                {
                    if (KidHandler.AppearanceIsBaby(appearanceData))
                    {
                        childCharaData.Appearance.Remove(appearanceData);
                    }
                }
                data[entry.KidNPCId] = childCharaData;
            }
        }
    }

    private static void Edit_DataNPCGiftTastes(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
        {
            if (entry.KidNPCId != null && data.TryGetValue(kidId, out string? giftTastes))
            {
                data[entry.KidNPCId] = giftTastes;
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
            ModEntry.help.GameContent.InvalidateCache(Asset_WhoseKids);
            ModEntry.Config.ResetMenu();
        }
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_WhoseKids)))
        {
            whoseKidsRaw = null;
            whoseKids = null;
            ModEntry.Config.ResetMenu();
        }
        foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
        {
            foreach (string fwdAsset in KidNPCForwardAssets)
            {
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(string.Concat(fwdAsset, kidId))))
                {
                    ModEntry.Log($"Propagate {fwdAsset}{kidId} -> {fwdAsset}{entry.KidNPCId}");
                    ModEntry.help.GameContent.InvalidateCache(string.Concat(fwdAsset, entry.KidNPCId));
                }
            }
        }
    }

    private static void OnAssetReady(object? sender, AssetReadyEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(Asset_ChildData))
        {
            DelayedAction.functionAfterDelay(() => KidHandler.KidEntries_Populate(), 0);
        }
    }
}
