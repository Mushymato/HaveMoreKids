using StardewValley;
using StardewValley.Characters;

namespace HaveMoreKids;

public interface IKidEntry
{
    /// <summary>The internal name of the NPC version of this kid, or null if they don't have one</summary>
    public string? KidNPCId { get; }
    /// <summary>Whether this kid is adopted from an NPC</summary>
    public bool IsAdoptedFromNPC { get; }
    /// <summary>The unique multiplayer id of the player parent (i.e. the player who owns the farmhouse this kid lives in)</summary>
    public long PlayerParent { get; }
    /// <summary>The other parent of the kid (either NPC or the other player's unique multiplayer id)</summary>
    public string? OtherParent { get; }
    /// <summary>Birth season (may be different than what <see cref="Child.daysOld"> indicates)</summary>
    public Season BirthSeason { get; }
    /// <summary>Birth day (may be different than what <see cref="Child.daysOld"> indicates)</summary>
    public int BirthDay { get; }
    /// <summary>Kid display name</summary>
    public string DisplayName { get; }

    /// <summary>Fetch the kid NPC instance</summary>
    public NPC? GetKidNPC();
}

public interface IHaveMoreKidsAPI
{
    /// <summary>
    /// Set the display name for a child, works for generic or custom children but not adopted from NPC kids.
    /// Please use this instead of directly setting <see cref="Child.Name"/> and <see cref="Child.displayName"/>.
    /// </summary>
    /// <param name="kid"></param>
    /// <param name="newName"></param>
    public void SetChildDisplayName(Child kid, string newName);

    /// <summary>
    /// Return HMK's kid entry info list, which will be populated once a save is loaded.
    /// Generic children will not have an entry.
    /// </summary>
    /// <returns>Iterator pair of (kidId, entry)</returns>
    public IEnumerable<(string, IKidEntry)> GetKidEntries();

    /// <summary>
    /// Return all children of a farmer, including adopted from NPC kids.
    /// Use this instead of <see cref="Farmer.getChildren"/> if you need access to adopted from NPC children.
    /// </summary>
    /// <returns>Iterator of Child</returns>
    /// <exception cref="KeyNotFoundException"/> Player home location is not ready
    /// <exception cref="InvalidCastException"/> Player home location is not a <see cref="StardewValley.Locations.FarmHouse"/>
    public IEnumerable<Child> GetAllChildOfFarmer(Farmer farmer);
}
