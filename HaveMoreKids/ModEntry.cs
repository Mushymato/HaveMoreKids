using System.Text;
using HaveMoreKids.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace HaveMoreKids;

public class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif
    private static IMonitor? mon;
    internal static IModHelper help = null!;
    internal static ModConfig Config = null!;

    internal const string ModId = "mushymato.HaveMoreKids";

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        help = helper;
        help.Events.GameLoop.GameLaunched += OnGameLaunched;
        help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        Config = helper.ReadConfig<ModConfig>();
        KidHandler.Register();
        AssetManager.Register();
        MultiplayerSync.Register();
        Patches.Apply();

        help.ConsoleCommands.Add("hmk-list_npcs", "List all NPC in the world", ConsoleListNPCs);
    }

    internal static void ConsoleListNPCs(string arg1, string[] arg2)
    {
        if (!Context.IsWorldReady)
            return;
        StringBuilder sb = new();
        Utility.ForEachCharacter(chara =>
        {
            Log($"{chara.Name} :: {chara.GetType()}, {Game1.player.friendshipData.ContainsKey(chara.Name)}");
            return true;
        });
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Config.Register(ModManifest);
        GameDelegates.Register(ModManifest);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        Config.UnregistedOnNonHost = false;
        Config.ResetMenu();
        KidHandler.GoingToTheFarm.Clear();
    }

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }
}
