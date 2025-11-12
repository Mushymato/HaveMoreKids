using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Objects;

namespace HaveMoreKids.Framework;

internal record CribAssign(Child Baby, Furniture? Furni)
{
    internal const string PlacedChild = $"{ModEntry.ModId}/CribPlacedChild";
    internal static Vector2 CribChildOffset = new(1f, 0f);

    internal bool IsInCrib(Vector2? pos = null)
    {
        if (Furni == null)
            return true;
        return Furni?.GetBoundingBox().Contains(pos ?? Baby.Position) ?? false;
    }

    internal void PlaceChild()
    {
        Baby.position.fieldChangeEvent -= OnPositionChange;

        if (Furni == null)
            return;

        switch (Baby.Age)
        {
            case 0:
                Baby.Position = (Furni.TileLocation + CribChildOffset) * Game1.tileSize + new Vector2(0, -24f);
                break;
            case 1:
                Baby.Position = (Furni.TileLocation + CribChildOffset) * Game1.tileSize + new Vector2(0, -12f);
                break;
            case 2:
                if (Game1.timeOfDay < 1800)
                {
                    return;
                }
                Baby.Position = (Furni.TileLocation + CribChildOffset) * Game1.tileSize + new Vector2(0, -24f);
                break;
            default:
                return;
        }

        Baby.position.fieldChangeEvent += OnPositionChange;
        Furni.modData[PlacedChild] = Baby.Name;

        return;
    }

    internal void UnplaceChild()
    {
        Furni?.modData.Remove(PlacedChild);
        Baby.position.fieldChangeEvent -= OnPositionChange;
    }

    private void OnPositionChange(NetPosition field, Vector2 oldValue, Vector2 newValue)
    {
        if (!IsInCrib(newValue) && !Baby.mutex.IsLocked())
        {
            DelayedAction.functionAfterDelay(UnplaceChild, 0);
        }
    }
}

internal static class CribManager
{
    internal const string CribTag = "hmk_crib";
    internal static ConditionalWeakTable<Child, CribAssign?> CribAssignments = [];

    internal static bool IsCrib(Furniture furni)
    {
        if (!furni.HasContextTag(CribTag))
            return false;
        Rectangle bounds = furni.defaultBoundingBox.Value;
        return bounds.Width == 3 * Game1.tileSize && bounds.Height == 2 * Game1.tileSize;
    }

    private static List<Furniture> GetAvailableCribs(FarmHouse farmHouse, HashSet<Furniture> assignedCribs) =>
        farmHouse.furniture.Where(furni => IsCrib(furni) && !assignedCribs.Contains(furni)).ToList();

    private static void GetAssignedCribs(
        FarmHouse farmHouse,
        out bool mapCribAvailable,
        out HashSet<Furniture> assignedCribs
    )
    {
        mapCribAvailable = farmHouse.cribStyle.Value > 0;
        assignedCribs = [];
        foreach ((Child bby, CribAssign? assign) in CribAssignments.Where(kv => kv.Key.currentLocation == farmHouse))
        {
            if (assign == null)
                continue;
            if (assign.Furni == null)
            {
                mapCribAvailable = false;
            }
            else
            {
                assignedCribs.Add(assign.Furni);
            }
        }
    }

    private static CribAssign? AssignNewCribToChild(Child child)
    {
        if ((child.currentLocation ?? Utility.getHomeOfFarmer(Game1.player)) is not FarmHouse farmHouse)
            return null;
        GetAssignedCribs(farmHouse, out bool mapCribAvailable, out HashSet<Furniture> assignedCribs);

        if (mapCribAvailable)
        {
            return new CribAssign(child, null);
        }

        List<Furniture> cribs = GetAvailableCribs(farmHouse, assignedCribs);
        if (!cribs.Any())
        {
            return null;
        }
        return new CribAssign(child, cribs.First());
    }

    internal static bool HasAvailableCribs(FarmHouse farmHouse)
    {
        GetAssignedCribs(farmHouse, out bool mapCribAvailable, out HashSet<Furniture> assignedCribs);
        return mapCribAvailable
            || farmHouse.furniture.Where(furni => IsCrib(furni) && !assignedCribs.Contains(furni)).Any();
    }

    internal static bool IsAssignedCrib(Furniture furni)
    {
        if (furni.Location is not FarmHouse farmHouse)
            return false;
        GetAssignedCribs(farmHouse, out _, out HashSet<Furniture> assignedCribs);
        return assignedCribs.Contains(furni);
    }

    internal static CribAssign? GetCribAssignment(this Child child) =>
        CribAssignments.GetValue(child, AssignNewCribToChild);

    internal static bool PutInACrib(Child bby)
    {
        if (bby.Age > 2)
            return false;
        CribAssign? cribAssign = GetCribAssignment(bby);
        if (cribAssign == null)
        {
            if (bby.currentLocation is not FarmHouse farmHouse)
                return false;
            // put child at random open tile
            Point randomTile;
            randomTile = KidPathingManager.GetRandomReachablePointInHouse(farmHouse, Random.Shared);
            bby.Position = randomTile.ToVector2() * Game1.tileSize;
            return false;
        }
        // position the child
        if (cribAssign.Furni == null)
            return true;

        DelayedAction.functionAfterDelay(cribAssign.PlaceChild, 0);
        return true;
    }

    internal static void RecheckCribAssignments()
    {
        HashSet<Child> shouldHaveCrib = [];
        foreach (Child child in KidHandler.AllKids())
        {
            if (child.Age < 2)
            {
                shouldHaveCrib.Add(child);
            }
        }
        foreach ((Child child, CribAssign? assign) in CribAssignments)
        {
            if (assign == null)
            {
                continue;
            }
            if (shouldHaveCrib.Contains(child))
            {
                assign.PlaceChild();
                shouldHaveCrib.Remove(child);
            }
            else
            {
                assign.UnplaceChild();
                CribAssignments.Remove(child);
            }
        }
        foreach (Child child in shouldHaveCrib)
        {
            PutInACrib(child);
        }
    }

    internal static bool DoCribAction(Furniture furni, Farmer who)
    {
        if (furni.Location is not GameLocation location || !IsCrib(furni))
            return false;
        foreach ((Child child, CribAssign? cribAssign) in CribAssignments)
        {
            if (child.currentLocation != location)
            {
                continue;
            }
            if (cribAssign?.Furni != furni)
            {
                continue;
            }
            switch (child.Age)
            {
                case 0:
                    Game1.drawObjectDialogue(
                        Game1.parseText(
                            Game1.content.LoadString(
                                "Strings\\Locations:FarmHouse_Crib_NewbornSleeping",
                                child.displayName
                            )
                        )
                    );
                    return true;
                case 1:
                    child.toss(who);
                    return true;
                case 2:
                    if (child.isInCrib())
                    {
                        return child.checkAction(who, furni.Location);
                    }
                    return true;
            }
        }
        if (who.getChildrenCount() >= ModEntry.Config.MaxChildren)
        {
            Game1.drawObjectDialogue(Game1.parseText(AssetManager.LoadString("Crib_Empty_EnoughKids")));
        }
        else
        {
            Game1.drawObjectDialogue(Game1.parseText(AssetManager.LoadString("Crib_Empty")));
        }
        return true;
    }
}
