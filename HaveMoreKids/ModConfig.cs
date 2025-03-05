using StardewModdingAPI;

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

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        PregnancyChance = 20;
        DaysPregnant = 14;
        DaysBaby = 13;
        DaysCrawler = 27 - 13;
        DaysToddler = 55 - 27;
        BaseMaxChildren = 4;
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
    }

    public void ResetMenu()
    {
        if (GMCM == null)
            return;
        GMCM.Unregister(Mod);
        SetupMenu();
    }
}
