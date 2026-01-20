using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;

namespace HaveMoreKids.Framework;

internal record DelegateWithSource<TArg, TRet>(IManifest Mod, Func<TArg, TRet> Delegate)
{
    internal TRet? Get(TArg arg) => Delegate(arg);
}

public sealed class HaveMoreKidsAPI : IHaveMoreKidsAPI
{
    /// <inheritdoc/>
    public void SetChildDisplayName(Child kid, string newName)
    {
        kid.SetChildDisplayName(newName);
    }

    /// <inheritdoc/>
    public IKidEntry? GetKidEntry(Child kid)
    {
        if (KidHandler.KidEntriesPopulated && KidHandler.KidEntries.TryGetValue(kid.Name, out KidEntry? entry))
        {
            return entry;
        }
        return null;
    }

    /// <inheritdoc/>
    public int GetDaysToNextChildGrowth(Child kid)
    {
        return kid.Age switch
        {
            0 => ModEntry.Config.TotalDaysBaby - kid.daysOld.Value,
            1 => ModEntry.Config.TotalDaysCrawer - kid.daysOld.Value,
            2 => ModEntry.Config.TotalDaysToddler - kid.daysOld.Value,
            3 => ModEntry.KidNPCEnabled ? ModEntry.Config.TotalDaysChild - kid.daysOld.Value : -1,
            _ => -1,
        };
    }

    /// <inheritdoc/>
    public string GetChildBirthdayString(Child kid)
    {
        if (GetKidEntry(kid) is KidEntry kidEntry)
        {
            int year = kid.daysOld.Value / (28 * 4) + 1;
            return new SDate(kidEntry.BirthDay, kidEntry.BirthSeason, year).ToLocaleString(withYear: true);
        }
        try
        {
            return SDate.Now().AddDays(-kid.daysOld.Value).ToLocaleString(withYear: true);
        }
        catch (ArithmeticException)
        {
            // The player probably changed the game date, so the birthday would be before the
            // game started. We'll just drop the year number from the output in that case.
            return new SDate(Game1.dayOfMonth, Game1.season, 100_000)
                .AddDays(-kid.daysOld.Value)
                .ToLocaleString(withYear: false);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<Child> GetAllChildrenOfFarmer(
        Farmer farmer,
        GetAllChildrenFilter childrenFilter = GetAllChildrenFilter.ALL
    )
    {
        return farmer.GetAllChildren(childrenFilter);
    }

    internal static DelegateWithSource<string, float?>? modPregnancyChanceDelegate;
    internal static DelegateWithSource<NPC, Dialogue?>? modNPCNewChildQuestionDelegate;
    internal static DelegateWithSource<long, string?>? modPlayerNewChildQuestionDelegate;

    /// <inheritdoc/>
    public void SetPregnancyChanceDelegate(IManifest mod, Func<string, float?> pregnancyChanceDelegate)
    {
        modPregnancyChanceDelegate = new(mod, pregnancyChanceDelegate);
    }

    /// <inheritdoc/>
    public void SetNPCNewChildQuestionDelegate(IManifest mod, Func<NPC, Dialogue?> newChildQuestionDelegateNPC)
    {
        modNPCNewChildQuestionDelegate = new(mod, newChildQuestionDelegateNPC);
    }

    /// <inheritdoc/>
    public void SetPlayerNewChildQuestionDelegate(IManifest mod, Func<long, string?> newChildQuestionDelegatePlayer)
    {
        modPlayerNewChildQuestionDelegate = new(mod, newChildQuestionDelegatePlayer);
    }
}
