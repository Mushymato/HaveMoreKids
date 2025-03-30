using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;

namespace HaveMoreKids;

public class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif
    private static IMonitor? mon;
    internal static ModConfig Config = null!;

    internal static string ModId { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        ModId = ModManifest.UniqueID;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        Config = helper.ReadConfig<ModConfig>();
        Patches.Apply();
        AssetManager.Register(helper);
        Quirks.Register(helper);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Config.Register(Helper, ModManifest);
        // Register a content patcher token for getting the display names of kids
        if (
            Helper.ModRegistry.GetApi<Integration.IContentPatcherAPI>("Pathoschild.ContentPatcher")
            is Integration.IContentPatcherAPI CP
        )
        {
            CP.RegisterToken(ModManifest, "ChildDisplayName", CPTokenChildDisplayNames);
        }
    }

    private static IEnumerable<string>? CPTokenChildDisplayNames() =>
        Context.IsWorldReady ? Game1.player.getChildren().Select(child => child.displayName) : null;

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
