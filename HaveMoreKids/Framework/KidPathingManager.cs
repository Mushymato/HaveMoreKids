using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
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

internal record NPCKidCtx(Child Kid, NPC KidNPC, int GoOutsideTime, SchedulePathDescription? EndSPD)
{
    internal bool ShouldPathOutside =>
        Game1.timeOfDay >= GoOutsideTime && !Kid.IsInvisible && pathingState == PathingState.None;

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
        KidPathingManager.RepositionKidInFarmhouse(Kid);
        KidHandler.RefreshDialogues(Kid, null);
    }

    internal void RouteEnd_SetKidInvisibleAndNPCVisible(Character c, GameLocation l)
    {
        SetKidToPathedOutside();
        KidPathingManager.GoingToTheFarm.Remove(Kid.idOfParent.Value);
    }
}

internal record GoingToFarmCtx(Child Kid, Point ExitPoint, int QueueTime);

internal record FarmTopologyInfo(
    Point DoorExit,
    Point HouseEntrance,
    List<Point> TileReachableInside,
    List<Point> TileReachableOutside
);

internal static class KidPathingManager
{
    internal const string Child_ModData_RoamOnFarm = $"{ModEntry.ModId}/RoamOnFarm";
    internal const string Schedule_HMK_Home = "HMK_Home";
    private const int LatestOutTheDoorTime = 1200;

    internal static void Register()
    {
        // events
        ModEntry.help.Events.GameLoop.DayEnding += OnDayEnding;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;

#if DEBUG_PATHING
        ModEntry.help.Events.Display.RenderedWorld += DebugPathingNonsense;
#endif
    }

    internal static readonly Dictionary<Child, NPCKidCtx> ManagedNPCKids = [];
    internal static readonly Dictionary<long, GoingToFarmCtx> GoingToTheFarm = [];
    private static readonly ConditionalWeakTable<FarmHouse, FarmTopologyInfo?> FarmTopolgyCache = [];

    internal static FarmTopologyInfo? GetFarmTopology(FarmHouse farmHouse) =>
        FarmTopolgyCache.GetValue(farmHouse, CreateFarmTopology);

    internal static HashSet<string>? FarmAdjacent = null;

    private static IEnumerable<Point> SurroundingTiles(Point nextPoint, int maxX, int maxY)
    {
        if (nextPoint.X > 0)
            yield return new(nextPoint.X - 1, nextPoint.Y);
        if (nextPoint.Y > 0)
            yield return new(nextPoint.X, nextPoint.Y - 1);
        if (nextPoint.X < maxX - 1)
            yield return new(nextPoint.X + 1, nextPoint.Y);
        if (nextPoint.Y < maxY - 1)
            yield return new(nextPoint.X, nextPoint.Y + 1);
    }

    internal static bool IsTileStandable(GameLocation location, Point tile, CollisionMask collisionMask)
    {
        return IsTilePassable(location, tile)
            && !location.warps.Any(warp => warp.X == tile.X && warp.Y == tile.Y)
            && !location.IsTileBlockedBy(
                tile.ToVector2(),
                collisionMask: collisionMask,
                ignorePassables: CollisionMask.All
            )
            && (location is not DecoratableLocation decoLoc || !decoLoc.isTileOnWall(tile.X, tile.Y));
    }

    internal static List<Point> TileStandableBFS(
        GameLocation location,
        Point startingTile,
        int maxDepth,
        int maxCount = -1,
        CollisionMask collisionMask = ~(CollisionMask.Characters | CollisionMask.Farmers)
    )
    {
        int maxX = location.Map.DisplayWidth / 64;
        int maxY = location.Map.DisplayHeight / 64;
        Dictionary<Point, bool> tileStandableState = [];
        tileStandableState[startingTile] = IsTileStandable(location, startingTile, collisionMask);
        Queue<(Point, int)> tileQueue = [];
        tileQueue.Enqueue(new(startingTile, 0));
        int standableCnt = 1;
        while (tileQueue.Count > 0)
        {
            (Point, int) next = tileQueue.Dequeue();
            Point nextPoint = next.Item1;
            int depth = next.Item2 + 1;
            if (depth > maxDepth)
            {
                break;
            }
            foreach (Point neighbour in SurroundingTiles(nextPoint, maxX, maxY))
            {
                if (!tileStandableState.ContainsKey(neighbour))
                {
                    bool standable = IsTileStandable(location, neighbour, collisionMask);
                    tileStandableState[neighbour] = standable;
                    if (standable)
                    {
                        standableCnt++;
                        tileQueue.Enqueue(new(neighbour, depth));
                        if (maxCount > -1 && standableCnt >= maxCount)
                        {
                            goto return_lbl;
                        }
                    }
                }
            }
        }
        return_lbl:
        return tileStandableState.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

#if DEBUG_PATHING
    private static void DebugPathingNonsense(object? sender, RenderedWorldEventArgs e)
    {
        FarmHouse farmTopoRef;
        if (Game1.currentLocation is FarmHouse farmHouse)
        {
            farmTopoRef = farmHouse;
        }
        else if (Game1.currentLocation is Farm)
        {
            farmTopoRef = Utility.getHomeOfFarmer(Game1.player);
        }
        else
        {
            return;
        }

        if (GetFarmTopology(farmTopoRef) is not FarmTopologyInfo farmTopologyInfo)
        {
            return;
        }

        foreach (
            Point pnt in Game1.currentLocation is FarmHouse
                ? farmTopologyInfo.TileReachableInside
                : farmTopologyInfo.TileReachableOutside
        )
        {
            Vector2 drawPos = Game1.GlobalToLocal(pnt.ToVector2() * 64);
            Utility.DrawSquare(
                e.SpriteBatch,
                new((int)drawPos.X, (int)drawPos.Y, 64, 64),
                0,
                backgroundColor: Color.Blue * 0.3f
            );
        }

        foreach (Character chara in Game1.currentLocation.characters)
        {
            if (chara is Child kid && !kid.IsInvisible)
            {
                if (kid.controller != null)
                {
                    foreach (Point pnt in kid.controller.pathToEndPoint)
                    {
                        Vector2 drawPos = Game1.GlobalToLocal(pnt.ToVector2() * 64);
                        Utility.DrawSquare(
                            e.SpriteBatch,
                            new((int)drawPos.X, (int)drawPos.Y, 64, 64),
                            0,
                            backgroundColor: Color.Yellow * 0.3f
                        );
                    }
                }
                Rectangle boundingBox = kid.GetBoundingBox();
                Vector2 drawPos2 = Game1.GlobalToLocal(new(boundingBox.X, boundingBox.Y));
                Utility.DrawSquare(
                    e.SpriteBatch,
                    new((int)drawPos2.X, (int)drawPos2.Y, boundingBox.Width, boundingBox.Height),
                    0,
                    backgroundColor: Color.Red * 0.3f
                );
            }
        }
    }
#endif

    private static FarmTopologyInfo? CreateFarmTopology(FarmHouse farmHouse)
    {
        if (
            farmHouse == null
            || farmHouse.Map == null
            || farmHouse.GetParentLocation() is not Farm farm
            || farm.Map == null
        )
        {
            return null;
        }
        Point doorExit = new(-1, -1);
        Point houseEntrance = new(-1, -1);
        foreach (Warp warp in farmHouse.warps)
        {
            if (warp.TargetName != farm.NameOrUniqueName)
            {
                continue;
            }
            doorExit = new(warp.X, warp.Y - 2);
            houseEntrance = new(warp.TargetX, warp.TargetY);
            break;
        }
        ModEntry.LogDebug($"FarmHouse {doorExit} -> Farm {houseEntrance}");
        return new(
            doorExit,
            houseEntrance,
            TileStandableBFS(farmHouse, doorExit, 64),
            TileStandableBFS(farm, houseEntrance, 64)
        );
    }

    internal static Point GetRandomReachablePointInHouse(FarmHouse farmHouse, Random random)
    {
        Point randomTile;
        if (GetFarmTopology(farmHouse) is FarmTopologyInfo farmTopologyInfo)
        {
            randomTile = random.ChooseFrom(farmTopologyInfo.TileReachableInside);
        }
        else
        {
            randomTile = farmHouse.getRandomOpenPointInHouse(random);
        }
        return randomTile;
    }

    internal static void AddManagedNPCKid(Child kid, NPC kidNPC, bool goOutside)
    {
        if (goOutside)
        {
            kidNPC.reloadData();
            kidNPC.reloadSprite(onlyAppearance: true);
            kidNPC.InvalidateMasterSchedule();
            SchedulePathDescription? endSPD = null;
            int goOutsideTime = 0600;
            if (kidNPC.TryLoadSchedule())
            {
                SchedulePathDescription? firstSPD = null;
                SchedulePathDescription? prevSPD = null;
                foreach ((int timeOfDay, SchedulePathDescription spd) in kidNPC.Schedule.OrderBy(kv => kv.Key))
                {
                    firstSPD ??= spd;
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
                if (firstSPD != null)
                {
                    goOutsideTime = Utility.ConvertMinutesToTime(Utility.ConvertTimeToMinutes(firstSPD.time) - 30);
                }
            }
            kidNPC.performSpecialScheduleChanges();
            kidNPC.resetSeasonalDialogue();
            kidNPC.resetCurrentDialogue();
            kidNPC.Sprite.UpdateSourceRect();

            FarmAdjacent ??= FindLocationsAdjacentToFarm(Game1.getFarm());
            bool isFarmAdjacent = FarmAdjacent.Contains(kidNPC.currentLocation.NameOrUniqueName);

            if ((endSPD != null || isFarmAdjacent) && goOutsideTime < LatestOutTheDoorTime)
            {
                NPCKidCtx ctx = new(kid, kidNPC, goOutsideTime, endSPD);
                if (isFarmAdjacent)
                {
                    NPCKidCtx.SetKidVisible(kid);
                    NPCKidCtx.SetNPCInvisible(kidNPC);
                    if (endSPD != null)
                    {
                        ModEntry.Log(
                            $"Kid '{kid.displayName}' ({kid.Name}) will leave as NPC today after {goOutsideTime} and will return home around {endSPD.time}",
                            LogLevel.Info
                        );
                    }
                    else
                    {
                        ModEntry.Log(
                            $"Kid '{kid.displayName}' ({kid.Name}) will leave as NPC today after {goOutsideTime}",
                            LogLevel.Info
                        );
                    }
                }
                else
                {
                    ctx.SetKidToPathedOutside();
                    if (endSPD != null)
                    {
                        ModEntry.Log(
                            $"Kid '{kid.displayName}' ({kid.Name}) is outside as NPC today and will return home around {endSPD.time}",
                            LogLevel.Info
                        );
                    }
                }
                ManagedNPCKids[kid] = ctx;
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

    internal static bool KidShouldRoamOnFarm(this Child kid)
    {
        if (!ModEntry.Config.ToddlerRoamOnFarm || Game1.timeOfDay >= LatestOutTheDoorTime)
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
            result = !kid.currentLocation.IsRainingHere();
        }
        kid.modData[Child_ModData_RoamOnFarm] = result ? "T" : "F";

        if (result)
        {
            ModEntry.Log($"Kid '{kid.displayName}' ({kid.Name}) will play outside on the farm today");
        }
        else
        {
            ModEntry.Log($"Kid '{kid.displayName}' ({kid.Name}) will stay indoors today");
        }
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
            ModEntry.LogDebug($"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} -> return to ");
            WarpKidToHouse(kid);
            return true;
        }

        // kid is on the farm
        if (kid.currentLocation is Farm)
        {
            return KidPathFinding(kid);
        }

        // kid not in a proper farmhouse, return early
        if (kid.currentLocation is not FarmHouse farmHouse || farmHouse.GetParentLocation() is not Farm farm)
        {
            return false;
        }

        if (ManagedNPCKids.TryGetValue(kid, out NPCKidCtx? ctx))
        {
            // if kid is supposed to go outside as NPC, don't do anything until night
            return !ctx.ShouldPathOutside;
        }

        bool roamOnFarm = kid.KidShouldRoamOnFarm() && Random.Shared.NextBool();
        if (roamOnFarm)
        {
            return SendKidToOutside(kid, farmHouse, farm);
        }
        else
        {
            return KidPathFinding(kid);
        }
    }

    private static bool SendKidToOutside(Child kid, FarmHouse farmHouse, Farm farm)
    {
        if (SendEnrouteKidOutside(kid, farm))
        {
            return false;
        }

        // only 1 kid allowed to path to farm at once and they should not be moving
        if (kid.isMoving() || kid.mutex.IsLocked())
        {
            return false;
        }

        if (GetFarmTopology(farmHouse) is not FarmTopologyInfo farmTopologyInfo)
        {
            return false;
        }

        List<Point> nearbyPoints = farmTopologyInfo
            .TileReachableOutside.Where(point =>
                Math.Abs(point.X - farmTopologyInfo.HouseEntrance.X) <= 6
                && point.Y > farmTopologyInfo.HouseEntrance.Y
                && point.Y - farmTopologyInfo.HouseEntrance.Y <= 12
            )
            .ToList();

        Point outsidePoint =
            nearbyPoints.Count > 0 ? Random.Shared.ChooseFrom(nearbyPoints) : farmTopologyInfo.HouseEntrance;

        ModEntry.LogDebug(
            $"TenMinuteUpdate({Game1.timeOfDay}): {kid.Name} ({kid.TilePoint}) -> go outside ({outsidePoint})"
        );

        kid.controller = new PathFindController(kid, farmHouse, farmTopologyInfo.DoorExit, -1, WarpKidToFarm);
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
            GoingToTheFarm[kid.idOfParent.Value] = new(kid, outsidePoint, Game1.timeOfDay);
            return false;
        }
    }

    private static bool SendEnrouteKidOutside(Child kid, Farm farm)
    {
        if (!GoingToTheFarm.TryGetValue(kid.idOfParent.Value, out GoingToFarmCtx? goingInfo))
        {
            return false;
        }
        // force one kid at a time
        if (goingInfo.QueueTime == Game1.timeOfDay || goingInfo.Kid != kid)
        {
            return true;
        }
        // if kid ought to be outside already, skip directly to warp
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
            return false;
        }
        DelayedAction.functionAfterDelay(() => kid.controller.endBehaviorFunction?.Invoke(kid, farm), 0);
        return false;
    }

    private static HashSet<string> FindLocationsAdjacentToFarm(Farm farm)
    {
        HashSet<string> result = [];
        foreach (Warp warp in farm.warps)
        {
            result.Add(warp.TargetName);
        }
        // hardcoding: ensure that BusStop/Forest/Backwoods are part of the set, to avoid weirdness with weird farms
        result.Add("BusStop");
        result.Add("Forest");
        result.Add("Backwoods");
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

        if (timeOfDay == 1840)
        {
            // reset farm topology cache 10m before roaming toddlers go home
            FarmTopolgyCache.Clear();
        }

        List<NPCKidCtx> kidsThatNeedOut = [];
        foreach (NPCKidCtx ctx in ManagedNPCKids.Values)
        {
            ctx.CheckEndSPD();
            if (ctx.ShouldPathOutside)
            {
                if (timeOfDay >= LatestOutTheDoorTime)
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
        foreach (GoingToFarmCtx goingEntry in GoingToTheFarm.Values)
        {
            SendEnrouteKidOutside(goingEntry.Kid, playerFarm);
        }

        NPCKidCtx ctxNeedOut = kidsThatNeedOut[0];
        if (ctxNeedOut.Kid.currentLocation is not FarmHouse farmHouse || farmHouse.GetParentLocation() is not Farm farm)
        {
            ctxNeedOut.SetKidToPathedOutside();
            return;
        }
        if (GetFarmTopology(farmHouse) is not FarmTopologyInfo farmTopologyInfo)
        {
            ctxNeedOut.SetKidToPathedOutside();
            return;
        }

        ModEntry.Log($"Sending kid '{ctxNeedOut.Kid.Name} ({ctxNeedOut.KidNPC.Name})' out the farmhouse door");
        ctxNeedOut.Kid.controller = new PathFindController(
            ctxNeedOut.Kid,
            farmHouse,
            farmTopologyInfo.DoorExit,
            -1,
            ctxNeedOut.RouteEnd_SetKidInvisibleAndNPCVisible
        );
        GoingToTheFarm[ctxNeedOut.Kid.idOfParent.Value] = new(ctxNeedOut.Kid, Point.Zero, Game1.timeOfDay);
    }

    internal static void RepositionKidInFarmhouse(Child kid)
    {
        kid.speed = 4;
        if (kid.currentLocation is FarmHouse farmHouse)
        {
            int kidIdx = kid.GetChildIndex();
            Point randomTile = GetRandomReachablePointInHouse(
                farmHouse,
                Utility.CreateDaySaveRandom(farmHouse.OwnerId * 2, kidIdx)
            );
            if (!randomTile.Equals(Point.Zero))
            {
                kid.setTilePosition(randomTile);
            }
            else
            {
                randomTile = farmHouse.GetChildBedSpot(kidIdx);
                if (!randomTile.Equals(Point.Zero))
                {
                    kid.setTilePosition(randomTile);
                }
            }
            kid.Sprite.CurrentAnimation = null;
        }
    }

    internal static void WarpKidToHouse(Child kid, bool delay = true)
    {
        FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.GetPlayer(kid.idOfParent.Value));
        Vector2 targetTile = GetRandomReachablePointInHouse(farmHouse, Random.Shared).ToVector2();
        if (delay)
        {
            DelayedAction.functionAfterDelay(
                () =>
                {
                    Game1.warpCharacter(kid, farmHouse, targetTile);
                    kid.controller = null;
                },
                0
            );
        }
        else
        {
            Game1.warpCharacter(kid, farmHouse, targetTile);
            kid.controller = null;
        }
    }

    private static void WarpKidToFarm(Character c, GameLocation l)
    {
        if (c is not Child kid || !GoingToTheFarm.TryGetValue(kid.idOfParent.Value, out GoingToFarmCtx? goingEntry))
            return;
        if (kid != goingEntry.Kid || kid.currentLocation?.GetParentLocation() is not Farm farm)
        {
            GoingToTheFarm.Remove(kid.idOfParent.Value);
            return;
        }

        kid.Halt();
        kid.controller = null;
        GoingToTheFarm.Remove(kid.idOfParent.Value);
        Game1.warpCharacter(kid, farm, goingEntry.ExitPoint.ToVector2());
        kid.toddlerReachedDestination(kid, farm);
        KidHandler.RefreshDialogues(kid, null);
    }

    private static bool KidPathFinding(Child kid)
    {
        // already in a path find
        if (kid.controller != null || kid.mutex.IsLocked())
        {
            return false;
        }

#if DEBUG_PATHING
#else
        // 25% chance
        if (Random.Shared.NextBool() && Random.Shared.NextBool())
        {
            return false;
        }
#endif

        kid.IsWalkingInSquare = false;
        kid.Halt();

        List<Point> tileReachable;
        if (kid.currentLocation is Farm)
        {
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.GetPlayer(kid.idOfParent.Value));
            if (GetFarmTopology(farmHouse) is FarmTopologyInfo farmTopologyInfo)
            {
                tileReachable = farmTopologyInfo.TileReachableOutside;
            }
            else
            {
                return true;
            }
        }
        else if (kid.currentLocation is FarmHouse farmHouse && farmHouse.OwnerId == kid.idOfParent.Value)
        {
            if (GetFarmTopology(farmHouse) is FarmTopologyInfo farmTopologyInfo)
            {
                tileReachable = farmTopologyInfo.TileReachableInside;
            }
            else
            {
                return true;
            }
        }
        else
        {
            return true;
        }

        if (tileReachable.Count == 0)
            return false;

        Point currentTile = kid.TilePoint;
        Point targetTile = Random.Shared.ChooseFrom(
            tileReachable
                .Where(tile => Math.Abs(tile.X - currentTile.X) <= 8 && Math.Abs(tile.Y - currentTile.Y) <= 8)
                .ToList()
        );

        ModEntry.LogDebug(
            $"KidPathFinding({kid.Name}, {kid.currentLocation.NameOrUniqueName}): {kid.TilePoint} -> {targetTile}"
        );
        kid.controller = new PathFindController(
            kid,
            kid.currentLocation,
            targetTile,
            -1,
            kid.toddlerReachedDestination
        );
        if (
            kid.controller.pathToEndPoint == null
            || !kid.controller.pathToEndPoint.Any()
            || !kid.currentLocation.isTileOnMap(kid.controller.pathToEndPoint.Last())
        )
        {
            kid.controller = null;
        }
        return false;
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
        FarmTopolgyCache.Clear();
        ManagedNPCKids.Clear();
        GoingToTheFarm.Clear();
    }
}
