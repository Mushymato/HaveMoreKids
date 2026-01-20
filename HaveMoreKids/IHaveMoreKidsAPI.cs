using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace HaveMoreKids;

public interface IKidEntry
{
    /// <summary>The internal name of the Child</summary>
    public string? KidId { get; }

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

    /// <summary>Fetch the Child instance</summary>
    public Child? GetChild();

    /// <summary>Fetch the kid NPC instance</summary>
    public NPC? GetKidNPC();
}

public interface IHaveMoreKidsAPI
{
    #region Kid Info

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
    ///
    /// The result is not sorted in any particular way, but you can sort them by age (<see cref="Child.daysOld"/>) like this:
    /// <code>
    /// HMKApi.GetAllChildOfFarmer(Game1.player).Sort((kidA, kidB) => kidB.daysOld.Value.CompareTo(kidA.daysOld.Value));
    /// </code>
    /// </summary>
    /// <returns>Iterator of Child</returns>
    /// <exception cref="KeyNotFoundException"/> Player home location is not ready
    /// <exception cref="InvalidCastException"/> Player home location is not a <see cref="StardewValley.Locations.FarmHouse"/>
    public IEnumerable<Child> GetAllChildOfFarmer(Farmer farmer, bool includeAdoptedFromNPC = true);

    #endregion

    #region Pregnancy Questions

    /// <summary>
    /// Adds a delegate to control pregnancy chance, instead of using HMK's config.
    /// This will be invoked by the method inserted via transpiler after relevant 'random.NextDouble() &lt; 0.05'.
    /// The string? argument indicates which callsite is the one invoking the delegate:
    /// <list type="bullet">
    /// <item>
    ///     <term>"Utility_pickPersonalFarmEvent_Transpiler:0"</term>
    ///     <description>Applies to the vanilla spouse, or the primary spouse for Free Love mods.</description>
    /// </item>
    /// <item>
    ///     <term>"Utility_pickPersonalFarmEvent_Transpiler:1"</term>
    ///     <description>Applies to player couple.</description>
    /// </item>
    /// <item>
    ///     <term>"FL_Utility_pickPersonalFarmEvent_Postfix_Transpiler:0"</term>
    ///     <description>Applies to any additional Free Love spouses.</description>
    /// </item>
    /// </list>
    /// Returning null here means HMK uses it's logic for this.
    /// Only one delegate can be set at a time, the last mod to do so wins.
    /// </summary>
    /// <param name="mod"></param>
    /// <param name="pregnancyChanceDelegate"></param>
    public void SetPregnancyChanceDelegate(IManifest mod, Func<string, float?> pregnancyChanceDelegate);

    /// <summary>
    /// Adds a delegate to control pregnancy dialogue for NPC, instead of using HMK's keys.
    /// The argument given is the NPC asking the question.
    /// This delegate will not be called for adopt from NPC.
    /// Returning null here means HMK uses it's logic for this.
    /// Only one delegate can be set at a time, the last mod to do so wins.
    /// </summary>
    /// <param name="mod"></param>
    /// <param name="pregnancyChanceDelegate"></param>
    public void SetNPCNewChildQuestionDelegate(IManifest mod, Func<NPC, Dialogue?> newChildQuestionDelegate);

    /// <summary>
    /// Adds a delegate to control pregnancy dialogue for Player, instead of using HMK's logic.
    /// The argument given is the unique player id of the farmer asking the question.
    /// Returning null here means HMK uses it's logic for this.
    /// Only one delegate can be set at a time, the last mod to do so wins.
    /// </summary>
    /// <param name="mod"></param>
    /// <param name="pregnancyChanceDelegate"></param>
    public void SetPlayerNewChildQuestionDelegate(IManifest mod, Func<long, string?> newPlayerChildQuestionDelegate);

    #endregion
}
