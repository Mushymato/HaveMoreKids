using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Triggers;

namespace HaveMoreKids.Framework.MapActions;

internal static class DarkShrine
{
    internal const string Action_EvilShrineLeft = "EvilShrineLeft";

    internal static void Register()
    {
        if (ModEntry.Config.PerKidDarkShrineOfSelfishness)
        {
            GameLocation.RegisterTileAction(Action_EvilShrineLeft, TilePerKidDarkShrine);
        }
        else
        {
            GameLocation.RegisterTileAction(Action_EvilShrineLeft, null);
        }
    }

    private static bool TilePerKidDarkShrine(GameLocation location, string[] arg2, Farmer who, Point point)
    {
        List<Child> children = who.getChildren();
        if (children.Count == 0)
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:WitchHut_EvilShrineLeftInactive"));
            return false;
        }
        else if (!who.Items.ContainsId(StardewValley.Object.prismaticShardQID))
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:WitchHut_NoOffering"));
            return false;
        }
        List<KeyValuePair<string, string>> responses = [];
        foreach (Child kid in children)
        {
            responses.Add(new($"DarkShrine_{kid.Name}", kid.displayName));
        }
        location.ShowPagedResponses(
            string.Concat(
                Game1.content.LoadString("Strings\\Locations:WitchHut_EvilShrineLeft"),
                "^",
                Game1.content.LoadString("Strings\\1_6_Strings:ChooseOne")
            ),
            responses,
            (obj) => OnResponse(location, who, point, children, obj)
        );
        return true;
    }

    private static void OnResponse(
        GameLocation location,
        Farmer who,
        Point point,
        List<Child> children,
        string response
    )
    {
        if (!response.StartsWith("DarkShrine_"))
        {
            return;
        }
        VisualEffects(location, point);
        string kidId = response.AsSpan()[11..].ToString();
        if (children.FirstOrDefault(child => child.Name == kidId) is Child child)
        {
            who.Items.ReduceId(StardewValley.Object.prismaticShardQID, 1);
            KidPathingManager.WarpKidToHouse(child, delay: false);
            FarmHouse homeOfFarmer = Utility.getHomeOfFarmer(who);
            homeOfFarmer.characters.RemoveWhere(character => character == child);
            TriggerActionManager.Raise(GameDelegates.Trigger_Doved);

            VisualEffects(location, point);
            string goodbye = Game1.content.LoadString("Strings\\Locations:WitchHut_Goodbye", child.getName());
            Game1.showGlobalMessage(goodbye);
            Game1.Multiplayer.globalChatInfoMessage("EvilShrine", who.Name);
        }
    }

    private static void VisualEffects(GameLocation location, Point point)
    {
        Game1.Multiplayer.broadcastSprites(
            Game1.currentLocation,
            new TemporaryAnimatedSprite(
                "LooseSprites\\Cursors",
                new Rectangle(536, 1945, 8, 8),
                new Vector2(156f, 388f),
                flipped: false,
                0f,
                Color.White
            )
            {
                interval = 50f,
                totalNumberOfLoops = 99999,
                animationLength = 7,
                layerDepth = 0.038500004f,
                scale = 4f,
            }
        );
        for (int num13 = 0; num13 < 20; num13++)
        {
            Game1.Multiplayer.broadcastSprites(
                Game1.currentLocation,
                new TemporaryAnimatedSprite(
                    "LooseSprites\\Cursors",
                    new Rectangle(372, 1956, 10, 10),
                    new Vector2(2f, 6f) * 64f + new Vector2(Game1.random.Next(-32, 64), Game1.random.Next(16)),
                    flipped: false,
                    0.002f,
                    Color.LightGray
                )
                {
                    alpha = 0.75f,
                    motion = new Vector2(1f, -0.5f),
                    acceleration = new Vector2(-0.002f, 0f),
                    interval = 99999f,
                    layerDepth = 0.0384f + Game1.random.Next(100) / 10000f,
                    scale = 3f,
                    scaleChange = 0.01f,
                    rotationChange = Game1.random.Next(-5, 6) * (float)Math.PI / 256f,
                    delayBeforeAnimationStart = num13 * 25,
                }
            );
        }
        Game1.currentLocation.playSound("fireball");
        Game1.Multiplayer.broadcastSprites(
            Game1.currentLocation,
            new TemporaryAnimatedSprite(
                "LooseSprites\\Cursors",
                new Rectangle(388, 1894, 24, 22),
                100f,
                6,
                9999,
                new Vector2(2f, 5f) * 64f,
                flicker: false,
                flipped: true,
                1f,
                0f,
                Color.White,
                4f,
                0f,
                0f,
                0f
            )
            {
                motion = new Vector2(4f, -2f),
            }
        );
    }
}
