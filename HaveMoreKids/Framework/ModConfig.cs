using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal sealed record KidIdent(string Spouse, string Kid)
{
    public static implicit operator KidIdent(string SpouseKid)
    {
        string[] parts = SpouseKid.Split('|');
        return new(parts[0], parts[1]);
    }

    public override string ToString()
    {
        return $"{Spouse}|{Kid}";
    }
}

internal sealed class ModConfig
{
    public const string SHARED_KEY = "#SHARED";
    private Integration.IGenericModConfigMenuApi? GMCM;
    private IModHelper Helper = null!;
    private IManifest Mod = null!;

    public int PregnancyChance { get; set; } = 20;
    public int DaysPregnant { get; set; } = 14;
    public int DaysBaby { get; set; } = 13;
    public int DaysCrawler { get; set; } = 27 - 13;
    public int DaysToddler { get; set; } = 55 - 27;
    public int DaysChild { get; set; } = 84 - 55;
    public int BaseMaxChildren { get; set; } = 4;
    public bool UseSingleBedAsChildBed { get; set; } = false;
    public Dictionary<KidIdent, bool> EnabledKids { get; set; } = [];
    private Dictionary<string, IList<string>> EnabledKidsPages { get; set; } = [];

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        PregnancyChance = 20;
        DaysPregnant = 14;
        DaysBaby = 13;
        DaysCrawler = 27 - 13;
        DaysToddler = 55 - 27;
        DaysChild = 84 - 55;
        BaseMaxChildren = 4;
        UseSingleBedAsChildBed = false;
        EnabledKids.Clear();
        CheckEnabledByDefault();
    }

    private void CheckEnabledByDefault()
    {
        foreach ((string key, CharacterData charaData) in DataLoader.Characters(Game1.content))
        {
            if (
                !AssetManager.TryGetSpouseKidIds(
                    charaData,
                    out IList<string>? kidIds,
                    out IDictionary<string, bool>? enabledByDefault
                )
            )
                continue;
            foreach (string kidId in kidIds)
            {
                KidIdent kidKey = new(key, kidId);
                EnabledKids[kidKey] = enabledByDefault[kidId];
            }
            if (kidIds.Any())
                EnabledKidsPages[key] = kidIds;
        }
        foreach ((string kidId, bool enabled) in AssetManager.SharedKids)
        {
            KidIdent kidKey = new("#SHARED", kidId);
            EnabledKids[kidKey] = enabled;
        }
        if (AssetManager.SharedKids.Any())
            EnabledKidsPages[SHARED_KEY] = AssetManager.SharedKids.Keys.ToList();
    }

    /// <summary>Add mod config to GMCM if available</summary>
    /// <param name="helper"></param>
    /// <param name="mod"></param>
    public void Register(IModHelper helper, IManifest mod)
    {
        GMCM ??= helper.ModRegistry.GetApi<Integration.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        Helper = helper;
        Mod = mod;
        CheckEnabledByDefault();
        if (GMCM == null)
        {
            helper.WriteConfig(this);
            return;
        }
        SetupMenu();
    }

    public void SetupMenu()
    {
        if (GMCM == null)
            return;
        GMCM.Register(
            mod: Mod,
            reset: () =>
            {
                Reset();
                Helper.WriteConfig(this);
            },
            save: () =>
            {
                Helper.WriteConfig(this);
            },
            titleScreenOnly: false
        );

        GMCM.AddPageLink(Mod, "Pregnancy", I18n.Config_Page_Pregnancy_Name, I18n.Config_Page_Pregnancy_Description);
        GMCM.AddPage(Mod, "Pregnancy", I18n.Config_Page_Pregnancy_Name);
        GMCM.AddNumberOption(
            Mod,
            () => PregnancyChance,
            (value) => PregnancyChance = value,
            I18n.Config_PregnancyChance_Name,
            I18n.Config_PregnancyChance_Description,
            min: 0,
            max: 100
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysPregnant,
            (value) =>
            {
                DaysPregnant = value;
                Helper.GameContent.InvalidateCache("Strings/UI");
            },
            I18n.Config_DaysPregnant_Name,
            I18n.Config_DaysPregnant_Description,
            min: 1,
            max: 28
        );
        GMCM.AddNumberOption(
            Mod,
            () => BaseMaxChildren,
            (value) => BaseMaxChildren = value,
            I18n.Config_BaseMaxChildren_Name,
            I18n.Config_BaseMaxChildren_Description,
            min: 1,
            max: 8
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysBaby,
            (value) => DaysBaby = value,
            I18n.Config_DaysBaby_Name,
            I18n.Config_DaysBaby_Description,
            min: 0,
            max: 28
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysCrawler,
            (value) => DaysCrawler = value,
            I18n.Config_DaysCrawler_Name,
            I18n.Config_DaysCrawler_Description,
            min: 0,
            max: 28
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysToddler,
            (value) => DaysToddler = value,
            I18n.Config_DaysToddler_Name,
            I18n.Config_DaysToddler_Description,
            min: 0,
            max: 56
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysChild,
            (value) => DaysChild = value,
            I18n.Config_DaysChild_Name,
            I18n.Config_DaysChild_Description,
            min: -1,
            max: 56
        );
        GMCM.AddBoolOption(
            Mod,
            () => UseSingleBedAsChildBed,
            (value) => UseSingleBedAsChildBed = value,
            I18n.Config_UseSingleBedAsChildBed_Name,
            I18n.Config_UseSingleBedAsChildBed_Description
        );
        GMCM.AddPage(Mod, "");

        if (EnabledKidsPages.Any())
        {
            GMCM.AddParagraph(Mod, I18n.Config_Page_SpecificKids_Description);
            var characterDatas = DataLoader.Characters(Game1.content);
            foreach ((string key, IList<string> kidIds) in EnabledKidsPages)
            {
                if (key == SHARED_KEY)
                {
                    SetupSpouseKidsPage(key, I18n.Config_Page_SharedKids_Name, kidIds);
                }
                else if (characterDatas.TryGetValue(key, out CharacterData? charaData))
                {
                    SetupSpouseKidsPage(
                        key,
                        () => I18n.Config_Page_Spousekids_Name(TokenParser.ParseText(charaData.DisplayName)),
                        kidIds
                    );
                }
            }
        }
        else
        {
            GMCM.AddParagraph(Mod, I18n.Config_Page_Nokids_Description);
        }
    }

    private void SetupSpouseKidsPage(string key, Func<string> labelFunc, IList<string> kidIds)
    {
        GMCM!.AddPageLink(Mod, key, labelFunc);
        GMCM.AddPage(Mod, key, labelFunc);
        foreach (string kidId in kidIds)
        {
            KidIdent kidKey = new(key, kidId);
            if (AssetManager.ChildData.TryGetValue(kidId, out CharacterData? data))
            {
                if (
                    data.Appearance?.FirstOrDefault(apr => !apr.Id.StartsWith(AssetManager.Appearances_Prefix_Baby))
                    is CharacterAppearanceData appearanceData
                )
                {
                    GMCM.AddComplexOption(
                        Mod,
                        name: () => "",
                        draw: new KidPreview(appearanceData, data.Size).Draw,
                        height: () => 0
                    );
                }
                GMCM.AddBoolOption(
                    Mod,
                    () => EnabledKids[kidKey],
                    (value) => EnabledKids[kidKey] = !value,
                    () => TokenParser.ParseText(data.DisplayName) ?? kidId
                );
            }
        }
        GMCM.AddPage(Mod, "");
    }

    private sealed record KidPreview(CharacterAppearanceData Apr, Point Size)
    {
        private readonly Texture2D? spriteTx = Game1.content.DoesAssetExist<Texture2D>(Apr.Sprite)
            ? Game1.content.Load<Texture2D>(Apr.Sprite)
            : null;

        public void Draw(SpriteBatch b, Vector2 origin)
        {
            if (spriteTx != null)
            {
                b.Draw(
                    spriteTx,
                    new(
                        (int)origin.X - Size.X * 4 - Game1.tileSize,
                        (int)origin.Y - Size.Y * 2,
                        Size.X * 4,
                        Size.Y * 4
                    ),
                    new(0, 0, Size.X, Size.Y),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    1f
                );
            }
        }
    }

    public void ResetMenu()
    {
        if (GMCM == null)
            return;
        GMCM.Unregister(Mod);
        CheckEnabledByDefault();
        SetupMenu();
    }
}
