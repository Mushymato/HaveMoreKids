using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    internal static void Apply_Portraiture()
    {
        // Portraiture Compat (sigh)
        var modInfo = ModEntry.help.ModRegistry.Get("Platonymous.Portraiture");
        if (modInfo?.GetType().GetProperty("Mod")?.GetValue(modInfo) is IMod mod)
        {
            var assembly = mod.GetType().Assembly;
            if (
                assembly.GetType("Portraiture.TextureLoader") is Type portraitureTxLoaderType
                && AccessTools.DeclaredMethod(portraitureTxLoaderType, "getPortrait")
                    is MethodInfo portraitureGetPortrait
            )
            {
                ModEntry.Log($"Patching Portraiture: {portraitureTxLoaderType}::{portraitureGetPortrait}");
                harmony.Patch(
                    portraitureGetPortrait,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(PortraitureTextureLoader_getPortrait_Prefix)),
                    finalizer: new HarmonyMethod(
                        typeof(Patches),
                        nameof(PortraitureTextureLoader_getPortrait_Finalizer)
                    )
                );
            }
        }
    }

    private static void PortraitureTextureLoader_getPortrait_Prefix(NPC npc, ref string? __state)
    {
        if (npc.Name != null && KidHandler.KidNPCToKid.TryGetValue(npc.Name, out string? kidId))
        {
            ModEntry.LogOnce($"PortraitureTextureLoader_getPortrait_Prefix: {npc.Name} -> {kidId}");
            __state = npc.Name;
            npc.Name = kidId;
        }
    }

    private static void PortraitureTextureLoader_getPortrait_Finalizer(NPC npc, ref string? __state)
    {
        if (__state != null)
        {
            npc.Name = __state;
        }
    }
}
