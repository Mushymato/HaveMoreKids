using System.Diagnostics;
using System.Reflection;
using System.Text;
using HaveMoreKids.Framework;
using HaveMoreKids.Framework.ExtraFeatures;
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
    internal static HaveMoreKidsAPI? haveMoreKidsAPI = null;

    internal const string ModId = "mushymato.HaveMoreKids";
    internal static bool KidNPCEnabled => Config.DaysChild > 0;

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        help = helper;
        help.Events.GameLoop.GameLaunched += OnGameLaunched;
        help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        Config = helper.ReadConfig<ModConfig>();
        KidHandler.Register();
        KidPathingManager.Register();
        AssetManager.Register();
        MultiplayerSync.Register();
        ChildrenRegistry.Register();
        DarkShrine.Register();
        Patches.Apply();

        help.ConsoleCommands.Add("hmk-list_npcs", "List all NPC in the world", ConsoleListNPCs);
    }

    public override object? GetApi()
    {
        return haveMoreKidsAPI ??= new HaveMoreKidsAPI();
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
        SpouseShim.Register(Helper);
        IModInfo? modInfo = help.ModRegistry.Get("Candidus42.LittleNPCs");
        if (
            modInfo?.GetType().GetProperty("Mod")?.GetValue(modInfo)?.GetType().Assembly is Assembly littleNPC
            && littleNPC.GetType("LittleNPCs.Framework.Common") is Type littleNPCcommon
        )
        {
            KidHandler.Method_IsValidLittleNPCIndex = HarmonyLib.AccessTools.DeclaredMethod(
                littleNPCcommon,
                "IsValidLittleNPCIndex"
            );
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        Config.UnregistedOnNonHost = false;
        Config.ResetMenu();
        KidHandler.KidEntries.Clear();
        NPCLookup.Clear();
        CribManager.CribAssignments.Clear();
        KidPathingManager.ResetAllState();
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

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
