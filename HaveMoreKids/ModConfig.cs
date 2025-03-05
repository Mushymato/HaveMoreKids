using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids;

internal sealed class ModConfig
{
    private Integration.IGenericModConfigMenuApi? GMCM;
    private IModHelper Helper = null!;
    private IManifest Mod = null!;

    public int PregnancyChance { get; set; } = 20;
    public int DaysPregnant { get; set; } = 14;
    public int DaysBaby { get; set; } = 13;
    public int DaysCrawler { get; set; } = 27 - 13;
    public int DaysToddler { get; set; } = 55 - 27;
    public int BaseMaxChildren { get; set; } = 4;
    public Dictionary<string, bool> DisabledKids { get; set; } = [];

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        PregnancyChance = 20;
        DaysPregnant = 14;
        DaysBaby = 13;
        DaysCrawler = 27 - 13;
        DaysToddler = 55 - 27;
        BaseMaxChildren = 4;
        DisabledKids.Clear();
    }

    /// <summary>Add mod config to GMCM if available</summary>
    /// <param name="helper"></param>
    /// <param name="mod"></param>
    public void Register(IModHelper helper, IManifest mod)
    {
        GMCM ??= helper.ModRegistry.GetApi<Integration.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        Helper = helper;
        Mod = mod;
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

        GMCM.AddSectionTitle(Mod, I18n.Config_Header_Pregnancy);
        GMCM.AddNumberOption(
            Mod,
            () => PregnancyChance,
            (value) => PregnancyChance = value,
            I18n.Config_PregnancyChance_Name,
            I18n.Config_PregnancyChance_Description,
            min: 5,
            max: 100
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysPregnant,
            (value) => DaysPregnant = value,
            I18n.Config_DaysPregnant_Name,
            I18n.Config_DaysPregnant_Description,
            min: 1,
            max: 28
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysBaby,
            (value) => DaysBaby = value,
            I18n.Config_DaysBaby_Name,
            I18n.Config_DaysBaby_Description,
            min: 1,
            max: 28
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysCrawler,
            (value) => DaysCrawler = value,
            I18n.Config_DaysCrawler_Name,
            I18n.Config_DaysCrawler_Description,
            min: 1,
            max: 28
        );
        GMCM.AddNumberOption(
            Mod,
            () => DaysToddler,
            (value) => DaysToddler = value,
            I18n.Config_DaysToddler_Name,
            I18n.Config_DaysToddler_Description,
            min: 1,
            max: 56
        );

        List<ValueTuple<string, CharacterData, string[]>> needPageSetup = [];
        foreach (var kv in DataLoader.Characters(Game1.content))
        {
            if (AssetManager.GetKidIds(kv.Value) is not string[] kidIds)
                continue;
            needPageSetup.Add(new(kv.Key, kv.Value, kidIds));
        }
        if (needPageSetup.Any())
        {
            GMCM.AddSectionTitle(Mod, I18n.Config_Header_Kids);
            GMCM.AddParagraph(Mod, I18n.Config_Header_ChooseKids);
            foreach (var tpl in needPageSetup)
            {
                SetupSpouseKidsPage(tpl.Item1, tpl.Item2, tpl.Item3);
            }
        }
    }

    private void SetupSpouseKidsPage(string key, CharacterData chara, string[] kidIds)
    {
        GMCM!.AddPageLink(Mod, key, () => TokenParser.ParseText(chara.DisplayName));
        GMCM.AddPage(Mod, key, () => TokenParser.ParseText(chara.DisplayName));
        foreach (string kidId in kidIds)
        {
            if (AssetManager.ChildData.TryGetValue(kidId, out CharacterData? data))
            {
                if (
                    data.Appearance?.FirstOrDefault(apr => apr.Id.StartsWith(Patches.Appearances_Prefix_Toddler))
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
                    () => !DisabledKids.GetValueOrDefault(kidId),
                    (value) => DisabledKids[kidId] = !value,
                    () => TokenParser.ParseText(data.DisplayName) ?? kidId
                );
            }
        }
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
        SetupMenu();
    }
}
