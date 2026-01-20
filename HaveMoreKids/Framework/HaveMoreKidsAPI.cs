using StardewModdingAPI;
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
    public IEnumerable<(string, IKidEntry)> GetKidEntries()
    {
        foreach ((string kidId, KidEntry entry) in KidHandler.KidEntries)
        {
            yield return (kidId, entry);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<Child> GetAllChildOfFarmer(Farmer farmer, bool includeAdoptedFromNPC = true)
    {
        return farmer.GetAllChildren(includeAdoptedFromNPC);
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
