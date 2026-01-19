using StardewValley.Characters;

namespace HaveMoreKids.Framework;

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
}
