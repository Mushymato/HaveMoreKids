using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;

namespace HaveMoreKids.Framework;

internal record CribAssign(Furniture? Furniture)
{
    internal static readonly Vector2 ChildOffset = new Vector2(1f, 1f) * Game1.tileSize;

    internal void RepositionChild(Child child)
    {
        if (Furniture == null)
            return;
        switch (child.Age)
        {
            case 0:
                child.Position = Furniture.TileLocation * Game1.tileSize + ChildOffset + new Vector2(0, -24f);
                break;
            case 1:
                child.Position = Furniture.TileLocation * Game1.tileSize + ChildOffset + new Vector2(0, -12f);
                break;
            case 2:
                if (Game1.timeOfDay >= 1800)
                {
                    child.Position = Furniture.TileLocation * Game1.tileSize + ChildOffset + new Vector2(0, -24f);
                }
                break;
        }
    }
}

internal static class CribManager
{
    internal const string CribTag = "hmk_crib";
    internal static ConditionalWeakTable<Child, CribAssign?> CribAssignment = [];

    private static List<Furniture> GetAvailableCribs(FarmHouse farmHouse, HashSet<Furniture> assignedCribs) =>
        farmHouse.furniture.Where(furni => furni.HasContextTag(CribTag) && !assignedCribs.Contains(furni)).ToList();

    private static void GetAssignedCribs(
        FarmHouse farmHouse,
        out bool mapCribAvailable,
        out HashSet<Furniture> assignedCribs
    )
    {
        mapCribAvailable = farmHouse.cribStyle.Value > 0;
        assignedCribs = [];
        foreach ((Child bby, CribAssign? assign) in CribAssignment.Where(kv => kv.Key.currentLocation == farmHouse))
        {
            if (assign == null)
                continue;
            if (assign.Furniture == null)
            {
                mapCribAvailable = false;
            }
            else
            {
                assignedCribs.Add(assign.Furniture);
            }
        }
    }

    private static CribAssign? GetCribForChild(Child child)
    {
        if ((child.currentLocation ?? Utility.getHomeOfFarmer(Game1.player)) is not FarmHouse farmHouse)
            return null;
        GetAssignedCribs(farmHouse, out bool mapCribAvailable, out HashSet<Furniture> assignedCribs);

        if (mapCribAvailable)
        {
            return new CribAssign(null);
        }

        List<Furniture> cribs = GetAvailableCribs(farmHouse, assignedCribs);
        if (!cribs.Any())
        {
            return null;
        }
        return new CribAssign(cribs.First());
    }

    internal static bool HasAvailableCribs(FarmHouse farmHouse)
    {
        GetAssignedCribs(farmHouse, out bool mapCribAvailable, out HashSet<Furniture> assignedCribs);
        return mapCribAvailable
            || farmHouse.furniture.Where(furni => furni.HasContextTag(CribTag) && !assignedCribs.Contains(furni)).Any();
    }

    internal static CribAssign? GetCribAssignment(this Child child) => CribAssignment.GetValue(child, GetCribForChild);

    internal static bool PutInACrib(Child bby)
    {
        if (bby.Age > 2)
            return false;
        CribAssign? cribAssign = CribManager.GetCribAssignment(bby);
        if (cribAssign == null)
        {
            if (bby.currentLocation is not FarmHouse farmHouse)
                return false;
            // put child at random open tile
            Vector2 randomTile = farmHouse.getRandomTile(Random.Shared);
            for (int i = 0; i < 32; i++)
            {
                if (farmHouse.isTileLocationOpen(randomTile))
                {
                    break;
                }
                randomTile = farmHouse.getRandomTile(Random.Shared);
            }
            bby.Position = randomTile * Game1.tileSize;
            return false;
        }
        // position the child
        cribAssign.RepositionChild(bby);
        return true;
    }
}
