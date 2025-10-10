using HarmonyLib;
using StardewModdingAPI;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    private static readonly Harmony harmony = new(ModEntry.ModId);

    internal static void Apply()
    {
        try
        {
            Apply_Pregnancy();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch pregnancy:\n{err}", LogLevel.Error);
            throw;
        }
        try
        {
            Apply_Child();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch child:\n{err}", LogLevel.Error);
            throw;
        }
        try
        {
            Apply_Narrative();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch Narrative:\n{err}", LogLevel.Error);
        }
        try
        {
            Apply_Misc();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch Misc:\n{err}", LogLevel.Warn);
        }
        try
        {
            Apply_Portraiture();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch Portraiture:\n{err}", LogLevel.Warn);
        }
    }
}
