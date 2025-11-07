using System.Runtime.CompilerServices;
using HaveMoreKids;
using HaveMoreKids.Framework;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Pathfinding;

internal record NPCKidCtx(Child Kid, NPC KidNPC, SchedulePathDescription? EndSPD)
{
    internal bool ShouldPathOutside => !Kid.IsInvisible && pathingState == PathingState.None;

    internal bool ShouldPathHome =>
        hasSeenEndSPD
        && pathingState == PathingState.PathedOutside
        && EndSPD?.targetLocationName == KidNPC.currentLocation.NameOrUniqueName
        && KidNPC.DirectionsToNewLocation == null;

    private enum PathingState
    {
        None = 0,
        PathedOutside = 1,
        PathedHome = 2,
    }

    private bool hasSeenEndSPD = false;

    public void CheckEndSPD()
    {
        if (EndSPD == null)
        {
            return;
        }
        hasSeenEndSPD = hasSeenEndSPD || EndSPD == KidNPC.DirectionsToNewLocation;
    }

    private PathingState pathingState = PathingState.None;

    internal static void SetKidInvisible(Child kid)
    {
        kid.Age = 4;
        kid.IsInvisible = true;
        kid.daysUntilNotInvisible = 1;
        kid.Halt();
        kid.controller = null;
    }

    internal static void SetKidVisible(Child kid)
    {
        kid.Age = 3;
        kid.IsInvisible = false;
        kid.daysUntilNotInvisible = 0;
    }

    internal static void SetNPCInvisible(NPC kidNPC)
    {
        kidNPC.IsInvisible = true;
        kidNPC.daysUntilNotInvisible = 1;
    }

    internal static void SetNPCVisible(NPC kidNPC)
    {
        kidNPC.IsInvisible = false;
        kidNPC.daysUntilNotInvisible = 0;
        kidNPC.forceOneTileWide.Value = true;
    }

    internal void SetKidToPathedOutside()
    {
        SetKidInvisible(Kid);
        SetNPCVisible(KidNPC);
        pathingState = PathingState.PathedOutside;
    }

    internal void SetKidToPathedHome()
    {
        SetKidVisible(Kid);
        SetNPCInvisible(KidNPC);
        pathingState = PathingState.PathedHome;
        KidHandler.RepositionKidInFarmhouse(Kid);
    }

    internal void RouteEnd_SetKidInvisibleAndNPCVisible(Character c, GameLocation l)
    {
        SetKidToPathedOutside();
        KidPathingManager.GoingToTheFarm.Remove(Kid.idOfParent.Value);
    }
}

internal static class KidPathingManager
{
    internal const string Child_ModData_RoamOnFarm = $"{ModEntry.ModId}/RoamOnFarm";
    internal const string Schedule_HMK_Home = "HMK_Home";

    internal static void Register()
    {
        // events
        ModEntry.help.Events.GameLoop.DayEnding += OnDayEnding;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
    }

    internal static readonly Dictionary<Child, NPCKidCtx> ManagedNPCKids = [];
    internal static readonly Dictionary<long, (Child, Point)> GoingToTheFarm = [];
    internal static readonly ConditionalWeakTable<FarmHouse, Tuple<Point, Point>?> FarmhouseDoors = [];
    internal static HashSet<string>? FarmAdjacent = null;

    private static Tuple<Point, Point>? GetFarmhouseDoor(FarmHouse farmhouse)
    {
        if (farmhouse.GetParentLocation() is not Farm farm)
        {
            return null;
        }
        Point doorExit = new(-1, -1);
        Point houseEntrance = new(-1, -1);
        foreach (Warp warp in farmhouse.warps)
        {
            if (warp.TargetName != farm.NameOrUniqueName)
            {
                continue;
            }
            doorExit = new(warp.X, warp.Y - 2);
            houseEntrance = new(warp.TargetX, warp.TargetY);
            return new(doorExit, houseEntrance);
        }
        return new(doorExit, houseEntrance);
    }

    internal static void AddManagedNPCKid(Child kid, NPC kidNPC, bool goOutside)
    {
        if (goOutside)
        {
            NPCKidCtx.SetNPCVisible(kidNPC);

            kidNPC.reloadData();
            kidNPC.reloadSprite(onlyAppearance: true);
            kidNPC.InvalidateMasterSchedule();
            SchedulePathDescription? endSPD = null;
            if (kidNPC.TryLoadSchedule())
            {
                SchedulePathDescription? prevSPD = null;
                foreach ((int timeOfDay, SchedulePathDescription spd) in kidNPC.Schedule.OrderBy(kv => kv.Key))
                {
                    if (prevSPD != null && spd.endOfRouteBehavior == Schedule_HMK_Home)
                    {
                        // Recompute this SchedulePathDescription
                        Point targetTile = spd.targetTile;
                        if (targetTile == Point.Zero)
                        {
                            targetTile = new((int)kidNPC.DefaultPosition.X / 64, (int)kidNPC.DefaultPosition.Y / 64);
                        }
                        endSPD = kidNPC.pathfindToNextScheduleLocation(
                            $"{kidNPC.ScheduleKey}!{Schedule_HMK_Home}",
                            prevSPD.targetLocationName,
                            prevSPD.targetTile.X,
                            prevSPD.targetTile.Y,
                            kidNPC.DefaultMap,
                            targetTile.X,
                            targetTile.Y,
                            spd.facingDirection,
                            null,
                            spd.endOfRouteMessage
                        );
                        endSPD.time = timeOfDay;
                        kidNPC.Schedule[timeOfDay] = endSPD;
                        break;
                    }
                    prevSPD = spd;
                }
            }
            kidNPC.performSpecialScheduleChanges();
            kidNPC.resetSeasonalDialogue();
            kidNPC.resetCurrentDialogue();
            kidNPC.Sprite.UpdateSourceRect();

            if (endSPD != null)
            {
                NPCKidCtx ctx = new(kid, kidNPC, endSPD);
                FarmAdjacent ??= FindLocationsAdjacentToFarm(Game1.getFarm());
                if (!FarmAdjacent.Contains(kidNPC.currentLocation.NameOrUniqueName))
                {
                    ctx.SetKidToPathedOutside();
                }
                ManagedNPCKids[kid] = ctx;
                ModEntry.Log($"Kid '{kid.displayName}' ({kid.Name}) will go outside as NPC today", LogLevel.Info);
            }
            else
            {
                NPCKidCtx.SetKidInvisible(kid);
                ModEntry.Log($"Kid '{kid.displayName}' ({kid.Name}) is outside as NPC today", LogLevel.Info);
            }
        }
        else
        {
            NPCKidCtx.SetKidVisible(kid);
            NPCKidCtx.SetNPCInvisible(kidNPC);
            ModEntry.Log($"Kid '{kid.displayName}' ({kid.Name}) is staying home today", LogLevel.Info);
        }
    }

    /// <summary>Get whether players can walk on a map tile.</summary>
    /// <param name="location">The location to check.</param>
    /// <param name="tile">The tile position.</param>
    /// <remarks>This is derived from <see cref="GameLocation.isTilePassable(Vector2)" />, but also checks tile properties in addition to tile index properties to match the actual game behavior.</remarks>
    /// <remarks>Originally written for DataLayers</remarks>
    private static bool IsTilePassable(GameLocation location, Point tile)
    {
        // passable if Buildings layer has 'Passable' property
        xTile.Tiles.Tile? buildingTile = location.map.RequireLayer("Buildings").Tiles[(int)tile.X, (int)tile.Y];
        if (buildingTile?.Properties.ContainsKey("Passable") is true)
            return true;

        // non-passable if Back layer has 'Passable' property
        xTile.Tiles.Tile? backTile = location.map.RequireLayer("Back").Tiles[(int)tile.X, (int)tile.Y];
        if (backTile?.Properties.ContainsKey("Passable") is true)
            return false;

        // else check tile indexes
        return location.isTilePassable(tile.ToVector2());
    }

    internal static bool IsTileStandable(GameLocation location, Point tile)
    {
        return IsTilePassable(location, tile)
            && !location.IsTileBlockedBy(tile.ToVector2(), ignorePassables: CollisionMask.All)
            && !location.isWaterTile(tile.X, tile.Y)
            && !location.warps.Any(warp => warp.X == tile.X && warp.Y == warp.Y);
    }

    internal static bool KidShouldRoamOnFarm(this Child kid)
    {
        if (ManagedNPCKids.ContainsKey(kid))
        {
            return false;
        }

        if (kid.modData.TryGetValue(Child_ModData_RoamOnFarm, out string value))
        {
            return value == "T";
        }

        bool result;
        if (kid.GetHMKKidDef() is KidDefinitionData kidDef && !string.IsNullOrEmpty(kidDef.RoamOnFarmCondition))
        {
            result = GameStateQuery.CheckConditions(kidDef.RoamOnFarmCondition, new());
        }
        else
        {
            // result = Random.Shared.NextBool();
            result = true;
        }

        kid.modData[Child_ModData_RoamOnFarm] = result ? "T" : "F";
        return result;
    }

    /// <summary>
    /// Possibly skipping prefix for Child.tenMinuteUpdate
    /// </summary>
    /// <param name="kid"></param>
    /// <returns></returns>
    internal static bool TenMinuteUpdate(Child kid)
    {
        // at 1900, banish them back to the house, and then allow vanilla logic to run
        if (Game1.timeOfDay >= 1850 && kid.currentLocation is not FarmHouse)
        {
            GoingToTheFarm.Clear();
            ModEntry.LogDebug($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} -> return to farmhouse");
            WarpKidToHouse(kid);
            return false;
        }

        // kid not in a proper farmhouse, return early
        if (kid.currentLocation is not FarmHouse farmhouse || farmhouse.GetParentLocation() is not Farm farm)
        {
            return false;
        }

        // before 1000, roll and see if the child could go outside
        if (ModEntry.Config.ToddlerRoamOnFarm && Game1.timeOfDay <= 1000)
        {
            return SendKidToOutside(kid, farmhouse, farm);
        }

        // while the kid is on the farm, do some path find controller things
        FarmPathFinding(kid);
        return false;
    }

    private static bool SendKidToOutside(Child kid, FarmHouse farmhouse, Farm farm)
    {
        // check if kid should go out
        if (!kid.KidShouldRoamOnFarm())
        {
            return false;
        }

        if (SendEnrouteKidOutside(kid, farm))
        {
            return false;
        }

        // only 1 kid allowed to path to farm at once and they should not be moving
        if (kid.isMoving() || kid.mutex.IsLocked())
        {
            return false;
        }

        if (FarmhouseDoors.GetValue(farmhouse, GetFarmhouseDoor) is not (Point doorExit, Point houseEntry))
        {
            return true;
        }

        ModEntry.LogDebug($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} ({kid.TilePoint}) -> go outside");

        for (int i = 0; i < 1000; i++)
        {
            Point trial = new(houseEntry.X + Random.Shared.Next(-8, 8), houseEntry.Y + 2 + Random.Shared.Next(0, 16));
            if (IsTileStandable(farm, trial))
            {
                kid.controller = new PathFindController(kid, farmhouse, doorExit, -1, WarpKidToFarm);
                if (
                    kid.controller.pathToEndPoint == null
                    || !kid.controller.pathToEndPoint.Any()
                    || !kid.currentLocation.isTileOnMap(kid.controller.pathToEndPoint.Last())
                )
                {
                    kid.controller = null;
                    return true;
                }
                else
                {
                    GoingToTheFarm[kid.idOfParent.Value] = new(kid, trial);
                    return false;
                }
            }
        }

        return true;
    }

    private static bool SendEnrouteKidOutside(Child kid, Farm farm)
    {
        // if kid ought to be outside already, skip directly to warp
        if (GoingToTheFarm.TryGetValue(kid.idOfParent.Value, out (Child, Point) goingInfo))
        {
            if (goingInfo.Item1 == kid)
            {
                if (kid.controller == null)
                {
                    // lost controller, try to determine which method we wanted
                    if (ManagedNPCKids.TryGetValue(kid, out NPCKidCtx? ctx))
                    {
                        DelayedAction.functionAfterDelay(() => ctx.RouteEnd_SetKidInvisibleAndNPCVisible(kid, farm), 0);
                    }
                    else
                    {
                        DelayedAction.functionAfterDelay(() => WarpKidToFarm(kid, farm), 0);
                    }
                    return true;
                }
                DelayedAction.functionAfterDelay(() => kid.controller.endBehaviorFunction?.Invoke(kid, farm), 0);
                return true;
            }
        }
        return false;
    }

    private static HashSet<string> FindLocationsAdjacentToFarm(Farm farm)
    {
        HashSet<string> result = [];
        foreach (Warp warp in farm.warps)
        {
            result.Add(warp.TargetName);
        }
        ModEntry.LogOnce($"Farm adjacent locations: {string.Join(',', result)}");
        return result;
    }

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        PathKidNPCToDoor(e.NewTime);
    }

    internal static void PathKidNPCToDoor(int timeOfDay)
    {
        if (!Context.IsMainPlayer)
            return;

        List<NPCKidCtx> kidsThatNeedOut = [];
        foreach (NPCKidCtx ctx in ManagedNPCKids.Values)
        {
            ctx.CheckEndSPD();
            if (ctx.ShouldPathOutside)
            {
                if (timeOfDay >= 1000 || ctx.Kid.currentLocation.farmers.Count == 0)
                {
                    ctx.SetKidToPathedOutside();
                }
                else
                {
                    kidsThatNeedOut.Add(ctx);
                }
            }
            else if (ctx.ShouldPathHome)
            {
                ctx.SetKidToPathedHome();
            }
        }

        if (kidsThatNeedOut.Count == 0)
            return;

        Farm playerFarm = Game1.getFarm();
        foreach ((Child kid, _) in GoingToTheFarm.Values)
        {
            SendEnrouteKidOutside(kid, playerFarm);
        }

        NPCKidCtx ctxNeedOut = kidsThatNeedOut[0];
        if (ctxNeedOut.Kid.currentLocation is not FarmHouse farmhouse || farmhouse.GetParentLocation() is not Farm farm)
        {
            ctxNeedOut.SetKidToPathedOutside();
            return;
        }
        if (FarmhouseDoors.GetValue(farmhouse, GetFarmhouseDoor) is not (Point doorExit, Point))
        {
            ctxNeedOut.SetKidToPathedOutside();
            return;
        }

        ModEntry.Log($"Sending kid '{ctxNeedOut.Kid} ({ctxNeedOut.KidNPC.Name})' out the farmhouse door");
        ctxNeedOut.Kid.controller = new PathFindController(
            ctxNeedOut.Kid,
            farmhouse,
            doorExit,
            -1,
            ctxNeedOut.RouteEnd_SetKidInvisibleAndNPCVisible
        );
        GoingToTheFarm[ctxNeedOut.Kid.idOfParent.Value] = new(ctxNeedOut.Kid, Point.Zero);
    }

    private static void WarpKidToHouse(Child kid, bool delay = true)
    {
        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.GetPlayer(kid.idOfParent.Value));
        if (delay)
        {
            DelayedAction.functionAfterDelay(
                () =>
                {
                    Game1.warpCharacter(
                        kid,
                        farmHouse,
                        farmHouse.getRandomOpenPointInHouse(Random.Shared, 2).ToVector2()
                    );
                    kid.controller = null;
                },
                0
            );
        }
        else
        {
            Game1.warpCharacter(kid, farmHouse, farmHouse.getRandomOpenPointInHouse(Random.Shared, 2).ToVector2());
            kid.controller = null;
        }
    }

    private static void WarpKidToFarm(Character c, GameLocation l)
    {
        if (c is not Child kid || !GoingToTheFarm.TryGetValue(kid.idOfParent.Value, out (Child, Point) info))
            return;
        if (kid != info.Item1 || kid.currentLocation?.GetParentLocation() is not Farm farm)
        {
            GoingToTheFarm.Remove(kid.idOfParent.Value);
            return;
        }

        kid.Halt();
        kid.controller = null;
        GoingToTheFarm.Remove(kid.idOfParent.Value);
        Game1.warpCharacter(kid, farm, info.Item2.ToVector2());
        kid.toddlerReachedDestination(kid, farm);
    }

    private static void FarmPathFinding(Child kid)
    {
        // already in a path find
        if (kid.controller != null || kid.mutex.IsLocked() || kid.currentLocation is not Farm farm)
        {
            return;
        }

        // 50% chance
        if (Random.Shared.NextBool())
        {
            return;
        }

        kid.IsWalkingInSquare = false;
        kid.Halt();

        Point? targetTile = null;

        if (!targetTile.HasValue || !IsTileStandable(farm, targetTile.Value))
        {
            for (int i = 0; i < 30; i++)
            {
                Point trial = kid.TilePoint + new Point(Random.Shared.Next(-10, 10), Random.Shared.Next(-10, 10));
                if (IsTileStandable(farm, trial))
                {
                    targetTile = trial;
                    break;
                }
            }
            if (!targetTile.HasValue)
            {
                return;
            }
        }

        ModEntry.LogDebug($"FarmPathFinding({kid.Name}): {kid.TilePoint} -> {targetTile.Value}");
        kid.controller = new PathFindController(kid, farm, targetTile.Value, -1, kid.toddlerReachedDestination);
        if (
            kid.controller.pathToEndPoint == null
            || !kid.controller.pathToEndPoint.Any()
            || !farm.isTileOnMap(kid.controller.pathToEndPoint.Last())
        )
        {
            kid.controller = null;
        }
    }

    private static void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        ResetAllState();
        ReturnKidsToHouse(KidHandler.AllKids());
    }

    internal static void ReturnKidsToHouse(IEnumerable<Child> kidsList)
    {
        List<Child> needWarp = [];
        foreach (Child kid in kidsList)
        {
            if (kid.currentLocation is not FarmHouse)
            {
                needWarp.Add(kid);
            }
        }
        foreach (Child kid in needWarp)
        {
            WarpKidToHouse(kid, false);
        }
    }

    internal static void ResetAllState()
    {
        FarmAdjacent = null;
        FarmhouseDoors.Clear();
        ManagedNPCKids.Clear();
        GoingToTheFarm.Clear();
    }
}
