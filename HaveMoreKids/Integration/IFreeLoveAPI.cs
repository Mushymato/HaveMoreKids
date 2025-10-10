using StardewValley;

namespace HaveMoreKids.Integration;

public interface IFreeLoveAPI
{
    public Dictionary<string, NPC> GetSpouses(Farmer farmer, bool all = true);
}
