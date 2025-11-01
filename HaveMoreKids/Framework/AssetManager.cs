using System.Diagnostics.CodeAnalysis;
using Force.DeepCloner;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Shops;

namespace HaveMoreKids.Framework;

public sealed class KidDefinitionData
{
    public string? Parent { get; set; } = null;
    public bool Shared { get; set; } = false;
    public bool DefaultEnabled { get; set; } = true;
    public string? Condition { get; set; } = null;
    public string? Twin { get; set; } = null;
    public string? TwinCondition { get; set; } = null;
    public string? TwinMessage { get; set; } = null;
    public string? AdoptedFromNPC { get; set; } = null;
    public string? BirthOrAdoptMessage { get; set; } = null;
    public string? CanAdoptFromAdoptionRegistry { get; set; } = null;
}

internal static class AssetManager
{
    private const string Asset_ChildData = $"{ModEntry.ModId}/ChildData";
    private const string Asset_KidDefinitions = $"{ModEntry.ModId}/Kids";
    private const string Asset_Strings = $"{ModEntry.ModId}\\Strings";
    internal const string Asset_DefaultTextureName = $"{ModEntry.ModId}_NoPortrait";
    private const string Asset_NoPortrait = $"Portraits/{Asset_DefaultTextureName}";
    internal const string Asset_DataCharacters = "Data/Characters";
    internal const string Asset_DataNPCGiftTastes = "Data/NPCGiftTastes";
    private const string Furniture_DefaultCrib = $"{ModEntry.ModId}_Crib";
    internal const string Asset_CharactersDialogue = "Characters/Dialogue/";
    internal const string Asset_CharactersSchedule = "Characters/schedules/";

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
                        invalidKidData.Add(new(key, "ID collides with NPC (Data/Characters)"));
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

    private static Dictionary<string, KidDefinitionData>? kidDefsByKidId = null;
    internal static Dictionary<string, KidDefinitionData> KidDefsByKidId =>
        kidDefsByKidId ??= Game1.content.Load<Dictionary<string, KidDefinitionData>>(Asset_KidDefinitions);
    private static Dictionary<string, Dictionary<string, KidDefinitionData>>? kidDefsByParentId = null;
    internal static Dictionary<string, Dictionary<string, KidDefinitionData>> KidDefsByParentId
    {
        get
        {
            if (kidDefsByParentId != null)
                return kidDefsByParentId;
            kidDefsByParentId = [];
            kidDefsByParentId[KidHandler.WhoseKids_Shared] = [];
            foreach ((string kidId, KidDefinitionData whose) in KidDefsByKidId)
            {
                if (whose.AdoptedFromNPC != null)
                    continue;
                if (!ChildData.ContainsKey(kidId))
                    continue;
                if (!string.IsNullOrEmpty(whose.Parent))
                {
                    if (
                        !kidDefsByParentId.TryGetValue(
                            whose.Parent,
                            out Dictionary<string, KidDefinitionData>? npcWhosekids
                        )
                    )
                    {
                        npcWhosekids = [];
                        kidDefsByParentId[whose.Parent] = npcWhosekids;
                    }
                    npcWhosekids[kidId] = whose;
                }
                else if (whose.Shared)
                {
                    kidDefsByParentId[KidHandler.WhoseKids_Shared][kidId] = whose;
                }
                // else, special traction only kid
            }
            return kidDefsByParentId;
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

    internal static MarriageDialogueReference? TryGetMarriageDialogueReference(string assetName, string key)
    {
        if (Game1.content.LoadStringReturnNullIfNotFound($"{assetName}:{key}") is null)
        {
            return null;
        }
        return new MarriageDialogueReference(assetName, key, true);
    }

    internal static bool TryGetDialogueForChildCount(
        NPC spouse,
        string keyPrefix,
        string babyName,
        int childrenCount,
        [NotNullWhen(true)] out Dialogue? dialogue,
        [NotNullWhen(true)] out MarriageDialogueReference? marriageDialogueReference
    )
    {
        marriageDialogueReference = null;
        for (int i = childrenCount; i > 0; i--)
        {
            string dialogueKey = string.Concat(keyPrefix, "_", i);
            if ((dialogue = spouse.tryToGetMarriageSpecificDialogue(dialogueKey)) is not null)
            {
                marriageDialogueReference = new MarriageDialogueReference(
                    "MarriageDialogue",
                    dialogueKey,
                    false,
                    babyName
                );
                return true;
            }
        }
        if ((dialogue = spouse.tryToGetMarriageSpecificDialogue(keyPrefix)) is not null)
        {
            marriageDialogueReference = new MarriageDialogueReference("MarriageDialogue", keyPrefix, false, babyName);
            return true;
        }
        return false;
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
        if (name.IsEquivalentTo(Asset_KidDefinitions))
            e.LoadFrom(() => new Dictionary<string, KidDefinitionData>(), AssetLoadPriority.Low);
        if (name.IsEquivalentTo(Asset_NoPortrait))
            e.LoadFromModFile<Texture2D>("assets/no_portrait.png", AssetLoadPriority.Exclusive);
        if (e.Name.IsEquivalentTo("Data/Furniture"))
            e.Edit(Edit_DataFurniture, AssetEditPriority.Default);
        if (e.Name.IsEquivalentTo("Data/Shops"))
            e.Edit(Edit_DataShops, AssetEditPriority.Default);
        if (e.Name.IsEquivalentTo("Maps/Hospital"))
            e.Edit(Edit_MapsHospital, AssetEditPriority.Late);
        if (name.IsEquivalentTo(Asset_Strings))
        {
            string stringsAsset = Path.Combine("i18n", e.Name.LanguageCode.ToString() ?? "default", "strings.json");
            if (File.Exists(Path.Combine(ModEntry.help.DirectoryPath, stringsAsset)))
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
                e.Edit(Edit_DataCharacters, AssetEditPriority.Late);
            if (name.IsEquivalentTo(Asset_DataNPCGiftTastes))
                e.Edit(Edit_DataNPCGiftTastes, AssetEditPriority.Late);
        }
    }

    private static void Edit_MapsHospital(IAssetData asset)
    {
        xTile.Map map = asset.AsMap().Data;
        if (map.GetLayer("Front2") is xTile.Layers.Layer front2)
        {
            front2.Tiles[3, 15] = new xTile.Tiles.StaticTile(
                front2,
                map.GetTileSheet("1"),
                xTile.Tiles.BlendMode.Alpha,
                675
            );
        }
        if (map.GetLayer("Buildings") is xTile.Layers.Layer buildings)
        {
            buildings.Tiles[3, 16].Properties["Action"] = AdoptionRegistry.Action_ShowAdoption;
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
            if (
                entry.IsAdoptedFromNPC
                || entry.KidNPCId == null
                || !ChildData.TryGetValue(kidId, out CharacterData? childCharaData)
            )
            {
                continue;
            }

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

    private static void Edit_DataNPCGiftTastes(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
        {
            if (entry.IsAdoptedFromNPC || entry.KidNPCId == null)
                continue;

            if (entry.IsAdoptedFromNPC && data.TryGetValue(entry.KidNPCId, out string? giftTastes))
            {
                data[kidId] = giftTastes;
            }
            else if (data.TryGetValue(kidId, out giftTastes))
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
            ModEntry.help.GameContent.InvalidateCache(Asset_KidDefinitions);
            ModEntry.Config.ResetMenu();
        }
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_KidDefinitions)))
        {
            kidDefsByKidId = null;
            kidDefsByParentId = null;
            ModEntry.Config.ResetMenu();
        }
    }

    private static void OnAssetReady(object? sender, AssetReadyEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(Asset_ChildData))
        {
            DelayedAction.functionAfterDelay(() => KidHandler.KidEntries_Populate(), 0);
        }
        if (e.NameWithoutLocale.IsEquivalentTo(Asset_DataCharacters))
        {
            DelayedAction.functionAfterDelay(ModEntry.Config.ResetMenu, 0);
        }
    }
}
