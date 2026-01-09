using HaveMoreKids.Framework.ExtraFeatures;
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
        if (parts.Length < 2)
        {
            throw new InvalidDataException($"Malformed KidIdent: {SpouseKid}");
        }
        return new(parts[0], parts[1]);
    }

    public override string ToString()
    {
        return $"{Spouse}|{Kid}";
    }
}

internal enum NewChildGenderMode
{
    Alternating = 0,
    Random = 1,
    BoysOnly = 2,
    GirlsOnly = 3,
}

internal class ModConfigValues
{
    public int PregnancyChance { get; set; } = 20;
    public int DaysMarried { get; set; } = 7;
    public int DaysPregnant { get; set; } = 14;
    public int DaysBaby { get; set; } = 13;
    public int DaysCrawler { get; set; } = 27 - 13;
    public int DaysToddler { get; set; } = 55 - 27;
    public int DaysChild { get; set; } = 84 - 55;
    public int MaxChildren { get; set; } = 4;
    public bool AlwaysAllowGenericChildren { get; set; } = false;
    public NewChildGenderMode GenericChildrenGenderMode { get; set; } = NewChildGenderMode.Alternating;
    public bool ToddlerRoamOnFarm { get; set; } = true;
    public bool UseSingleBedAsChildBed { get; set; } = false;
    public bool PerKidDarkShrineOfSelfishness { get; set; } = true;
    public Dictionary<KidIdent, bool> EnabledKids { get; set; } = [];
}

internal sealed class ModConfig : ModConfigValues
{
    private Integration.IGenericModConfigMenuApi? GMCM;
    private IManifest Mod = null!;
    private Dictionary<string, IList<string>> EnabledKidsPages { get; set; } = [];
    internal bool UnregistedOnNonHost = false;

    internal sbyte TotalDaysBaby => (sbyte)DaysBaby;
    internal sbyte TotalDaysCrawer => (sbyte)(DaysBaby + DaysCrawler);
    internal sbyte TotalDaysToddler => (sbyte)(DaysBaby + DaysCrawler + DaysToddler);
    internal sbyte TotalDaysChild => (sbyte)(DaysBaby + DaysCrawler + DaysToddler + (DaysChild == -1 ? 28 : DaysChild));

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        PregnancyChance = 20;
        DaysMarried = 7;
        DaysPregnant = 14;
        DaysBaby = 13;
        DaysCrawler = 27 - 13;
        DaysToddler = 55 - 27;
        DaysChild = 84 - 55;
        MaxChildren = 4;
        AlwaysAllowGenericChildren = false;
        GenericChildrenGenderMode = NewChildGenderMode.Alternating;
        ToddlerRoamOnFarm = true;
        UseSingleBedAsChildBed = false;
        PerKidDarkShrineOfSelfishness = false;
        EnabledKids.Clear();
        CheckDefaultEnabled();
    }

    public void SyncAndUnregister(ModConfigValues other)
    {
        UnregistedOnNonHost = true;

        DaysPregnant = other.DaysPregnant;
        DaysBaby = other.DaysBaby;
        DaysCrawler = other.DaysCrawler;
        DaysToddler = other.DaysToddler;
        DaysChild = other.DaysChild;
        ToddlerRoamOnFarm = other.ToddlerRoamOnFarm;
        UseSingleBedAsChildBed = other.UseSingleBedAsChildBed;

        ResetMenu();
    }

    private void CheckDefaultEnabled()
    {
        EnabledKidsPages.Clear();
        foreach (
            (string spouseId, Dictionary<string, KidDefinitionData>? whoseKidsInfo) in AssetManager.KidDefsByParentId
        )
        {
            if (!whoseKidsInfo.Any())
                continue;
            foreach (string kidId in whoseKidsInfo.Keys)
            {
                KidIdent kidKey = new(spouseId, kidId);
                if (!EnabledKids.ContainsKey(kidKey))
                    EnabledKids[kidKey] = whoseKidsInfo[kidId].DefaultEnabled;
            }
            EnabledKidsPages[spouseId] = whoseKidsInfo.Keys.ToList();
        }
    }

    /// <summary>Add mod config to GMCM if available</summary>
    /// <param name="helper"></param>
    /// <param name="mod"></param>
    public void Register(IManifest mod)
    {
        GMCM ??= ModEntry.help.ModRegistry.GetApi<Integration.IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu"
        );
        Mod = mod;
        CheckDefaultEnabled();
        if (GMCM == null)
        {
            ModEntry.help.WriteConfig(this);
            return;
        }
        SetupMenu();
    }

    private string NegativeOneIsUnlimited(int arg)
    {
        if (arg == -1)
            return I18n.Config_Value_Unlimited();
        return arg.ToString();
    }

    private string ZeroIsDisabled(int arg)
    {
        if (arg == 0)
            return I18n.Config_Value_Disabled();
        return arg.ToString();
    }

    public void SetupMenuFarmhand()
    {
        if (!RegisterGMCM())
        {
            return;
        }

        GMCM!.AddPageLink(Mod, "General", I18n.Config_Page_General_Name, I18n.Config_Page_General_Description);
        GMCM.AddPage(Mod, "General", I18n.Config_Page_General_Name);
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
            () => DaysMarried,
            (value) => DaysMarried = value,
            I18n.Config_DaysMarried_Name,
            I18n.Config_DaysMarried_Description,
            min: 1,
            max: 14
        );

        GMCM.AddNumberOption(
            Mod,
            () => MaxChildren,
            (value) => MaxChildren = value,
            I18n.Config_MaxChildren_Name,
            I18n.Config_MaxChildren_Description,
            min: -1,
            max: 16,
            formatValue: NegativeOneIsUnlimited
        );
        GMCM.AddBoolOption(
            Mod,
            () => AlwaysAllowGenericChildren,
            (value) => AlwaysAllowGenericChildren = value,
            I18n.Config_AlwaysAllowGenericChildren_Name,
            I18n.Config_AlwaysAllowGenericChildren_Description
        );
        GMCM.AddTextOption(
            Mod,
            () => GenericChildrenGenderMode.ToString(),
            (value) => GenericChildrenGenderMode = Enum.Parse<NewChildGenderMode>(value),
            I18n.Config_GenericChildrenGenderMode_Name,
            I18n.Config_GenericChildrenGenderMode_Description,
            allowedValues: Enum.GetValues<NewChildGenderMode>().Select(value => value.ToString()).ToArray(),
            formatAllowedValue: (value) => I18n.GetByKey($"config.GenericChildrenGenderMode.value.{value}")
        );
        GMCM.AddBoolOption(
            Mod,
            () => PerKidDarkShrineOfSelfishness,
            (value) =>
            {
                PerKidDarkShrineOfSelfishness = value;
                DarkShrine.Register();
            },
            I18n.Config_PerKidDarkShrineOfSelfishness_Name,
            I18n.Config_PerKidDarkShrineOfSelfishness_Description
        );

        SetupEnabledKids();
    }

    public void SetupMenu()
    {
        if (!RegisterGMCM())
        {
            return;
        }

        GMCM!.AddPageLink(Mod, "General", I18n.Config_Page_General_Name, I18n.Config_Page_General_Description);
        GMCM.AddPage(Mod, "General", I18n.Config_Page_General_Name);
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
            () => DaysMarried,
            (value) => DaysMarried = value,
            I18n.Config_DaysMarried_Name,
            I18n.Config_DaysMarried_Description,
            min: 1,
            max: 14
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

        if (ModEntry.hasLittleNPC)
        {
            GMCM.AddParagraph(Mod, I18n.Config_DaysChild_Disabled);
        }
        else
        {
            GMCM.AddNumberOption(
                Mod,
                () => DaysChild,
                (value) => DaysChild = value,
                I18n.Config_DaysChild_Name,
                I18n.Config_DaysChild_Description,
                min: 0,
                max: 56,
                formatValue: ZeroIsDisabled
            );
        }

        GMCM.AddNumberOption(
            Mod,
            () => MaxChildren,
            (value) => MaxChildren = value,
            I18n.Config_MaxChildren_Name,
            I18n.Config_MaxChildren_Description,
            min: -1,
            max: 16,
            formatValue: NegativeOneIsUnlimited
        );
        GMCM.AddBoolOption(
            Mod,
            () => AlwaysAllowGenericChildren,
            (value) => AlwaysAllowGenericChildren = value,
            I18n.Config_AlwaysAllowGenericChildren_Name,
            I18n.Config_AlwaysAllowGenericChildren_Description
        );
        GMCM.AddTextOption(
            Mod,
            () => GenericChildrenGenderMode.ToString(),
            (value) => GenericChildrenGenderMode = Enum.Parse<NewChildGenderMode>(value),
            I18n.Config_GenericChildrenGenderMode_Name,
            I18n.Config_GenericChildrenGenderMode_Description,
            allowedValues: Enum.GetValues<NewChildGenderMode>().Select(value => value.ToString()).ToArray(),
            formatAllowedValue: (value) => I18n.GetByKey($"config.GenericChildrenGenderMode.value.{value}")
        );

        GMCM.AddBoolOption(
            Mod,
            () => ToddlerRoamOnFarm,
            (value) => ToddlerRoamOnFarm = value,
            I18n.Config_ToddlerRoamOnFarm_Name,
            I18n.Config_ToddlerRoamOnFarm_Description
        );
        GMCM.AddBoolOption(
            Mod,
            () => UseSingleBedAsChildBed,
            (value) => UseSingleBedAsChildBed = value,
            I18n.Config_UseSingleBedAsChildBed_Name,
            I18n.Config_UseSingleBedAsChildBed_Description
        );
        GMCM.AddBoolOption(
            Mod,
            () => PerKidDarkShrineOfSelfishness,
            (value) =>
            {
                PerKidDarkShrineOfSelfishness = value;
                DarkShrine.Register();
            },
            I18n.Config_PerKidDarkShrineOfSelfishness_Name,
            I18n.Config_PerKidDarkShrineOfSelfishness_Description
        );
        SetupEnabledKids();
    }

    private void SetupEnabledKids()
    {
        GMCM!.AddPage(Mod, "");
        if (EnabledKidsPages.Any())
        {
            GMCM.AddParagraph(Mod, I18n.Config_Page_CustomKids_Description);
            foreach ((string key, IList<string> kidIds) in EnabledKidsPages)
            {
                if (key == KidHandler.WhoseKids_Shared)
                {
                    SetupSpouseKidsPage(key, I18n.Config_Page_SharedKids_Name, kidIds);
                }
                else
                {
                    string displayName = key;
                    SetupSpouseKidsPage(
                        key,
                        () =>
                        {
                            if (Game1.characterData.TryGetValue(key, out CharacterData? charaData))
                            {
                                TokenParser.ParseText(charaData.DisplayName);
                            }
                            return I18n.Config_Page_Spousekids_Name(displayName);
                        },
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

    private bool RegisterGMCM()
    {
        if (GMCM == null)
            return false;
        GMCM.Register(
            mod: Mod,
            reset: () =>
            {
                Reset();
                ModEntry.help.WriteConfig(this);
                MultiplayerSync.SendModConfig(null);
            },
            save: () =>
            {
                ModEntry.help.WriteConfig(this);
                MultiplayerSync.SendModConfig(null);
            },
            titleScreenOnly: false
        );
        return true;
    }

    private void SetupSpouseKidsPage(string key, Func<string> labelFunc, IList<string> kidIds)
    {
        GMCM!.AddPageLink(Mod, key, labelFunc);
        GMCM.AddPage(Mod, key, labelFunc);
        foreach (string kidId in kidIds)
        {
            KidIdent kidKey = new(key, kidId);
            bool hasDialogue = false;
            bool isKidNPC = false;
            if (AssetManager.KidDefsByKidId.TryGetValue(kidId, out KidDefinitionData? kidDef))
            {
                hasDialogue = Game1.content.DoesAssetExist<Dictionary<string, string>>(
                    $"{AssetManager.Asset_CharactersDialogue}/{kidDef.KidDialogueSheetName ?? kidId}"
                );
                isKidNPC =
                    !string.IsNullOrEmpty(kidDef.IsNPCTodayCondition)
                    && !GameStateQuery.IsImmutablyFalse(kidDef.IsNPCTodayCondition);
            }
            if (AssetManager.ChildData.TryGetValue(kidId, out CharacterData? data))
            {
                if (
                    data.Appearance?.FirstOrDefault(apr => apr.AppearanceIsUnconditional() && !apr.AppearanceIsBaby())
                    is CharacterAppearanceData toddlerApr
                )
                {
                    CharacterAppearanceData? babyApr = data.Appearance?.FirstOrDefault(apr => apr.AppearanceIsBaby());
                    GMCM.AddComplexOption(
                        Mod,
                        name: () => "",
                        draw: new KidPreview(toddlerApr, data.Size, babyApr).Draw,
                        height: () => 0
                    );
                }
                string name = TokenParser.ParseText(data.DisplayName) ?? kidId;
                GMCM.AddBoolOption(
                    Mod,
                    () => EnabledKids[kidKey],
                    (value) => EnabledKids[kidKey] = value,
                    () => name,
                    () =>
                        I18n.Config_Page_Kids_Description(
                            displayName: name,
                            hasDialogue
                                ? Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_Yes")
                                : Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_No"),
                            isKidNPC
                                ? Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_Yes")
                                : Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_No")
                        )
                );
            }
        }
        GMCM.AddPage(Mod, "");
    }

    private sealed record KidPreview(CharacterAppearanceData Apr, Point Size, CharacterAppearanceData? BabyApr)
    {
        private readonly Texture2D spriteTx = Game1.content.Load<Texture2D>(Apr.Sprite);

        private readonly Texture2D? babySpriteTx =
            (BabyApr != null && Game1.content.DoesAssetExist<Texture2D>(BabyApr.Sprite))
                ? Game1.content.Load<Texture2D>(BabyApr.Sprite)
                : null;

        public void Draw(SpriteBatch b, Vector2 origin)
        {
            Rectangle drawRect = new((int)origin.X + 64, (int)origin.Y - Size.Y * 2, Size.X * 4, Size.Y * 4);
            if (babySpriteTx != null)
            {
                Rectangle drawRectBaby = new(drawRect.X + Size.X * 4 + 4, (int)origin.Y - 12, 22 * 4, 16 * 4);
                b.Draw(
                    babySpriteTx,
                    drawRectBaby,
                    new(0, 160, 22, 16),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    1f
                );
            }
            if (spriteTx != null)
            {
                b.Draw(
                    spriteTx,
                    drawRect,
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
        CheckDefaultEnabled();
        if (UnregistedOnNonHost)
        {
            SetupMenuFarmhand();
        }
        else
        {
            SetupMenu();
        }
    }
}
