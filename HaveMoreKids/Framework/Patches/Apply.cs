using HarmonyLib;
using StardewModdingAPI;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    internal static void Apply()
    {
        Harmony harmony = new(ModEntry.ModId);
        try
        {
            Apply_Pregnancy(harmony);
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch pregnancy:\n{err}", LogLevel.Error);
            throw;
        }
        try
        {
            Apply_Child(harmony);
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch child:\n{err}", LogLevel.Error);
            throw;
        }
        try
        {
            Apply_Portraiture(harmony);
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch Portraiture:\n{err}", LogLevel.Warn);
        }
        try
        {
            Apply_Misc(harmony);
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch Portraiture:\n{err}", LogLevel.Warn);
        }
    }
}
