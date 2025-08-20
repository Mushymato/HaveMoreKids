using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    internal static void Apply_Misc(Harmony harmony)
    {
        // Let child sleep on single beds
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.GetChildBed)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FarmHouse_GetChildBed_Postfix))
        );
        // show child birthday on the calendar
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Billboard), nameof(Billboard.GetBirthdays)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Billboard_GetBirthdays_Postfix))
        );
        // make calendar respect appearances >:(
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Billboard), nameof(Billboard.GetEventsForDay)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Billboard_GetEventsForDay_Prefix)),
            finalizer: new HarmonyMethod(typeof(Patches), nameof(Billboard_GetEventsForDay_Finalizer))
        );
        // show hearts and gifts this week for Age 3
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(SocialPage), nameof(SocialPage.drawNPCSlot)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(SocialPage_drawNPCSlot_Prefix)),
            finalizer: new HarmonyMethod(typeof(Patches), nameof(SocialPage_drawNPCSlot_Finalizer))
        );
    }

    private static bool In_Billboard_GetEventsForDay = true;

    private static void Billboard_GetEventsForDay_Prefix()
    {
        In_Billboard_GetEventsForDay = true;
    }

    private static void Billboard_GetEventsForDay_Finalizer()
    {
        In_Billboard_GetEventsForDay = false;
    }

    private static readonly FieldInfo IsChildField = AccessTools.DeclaredField(
        typeof(SocialPage.SocialEntry),
        nameof(SocialPage.SocialEntry.IsChild)
    );

    private static void SocialPage_drawNPCSlot_Prefix(SocialPage __instance, int i)
    {
        SocialPage.SocialEntry socialEntry = __instance.GetSocialEntry(i);
        if (socialEntry.IsChild && socialEntry.Character is Child child && child.CanReceiveGifts())
        {
            IsChildField.SetValue(socialEntry, false);
        }
    }

    private static void SocialPage_drawNPCSlot_Finalizer(SocialPage __instance, int i)
    {
        SocialPage.SocialEntry socialEntry = __instance.GetSocialEntry(i);
        if (socialEntry.Character is Child)
        {
            IsChildField.SetValue(socialEntry, true);
        }
    }

    private static void Billboard_GetBirthdays_Postfix(ref Dictionary<int, List<NPC>> __result)
    {
        foreach (Child kid in KidHandler.AllKids())
        {
            ModEntry.Log($"{Game1.currentSeason}: {kid.Birthday_Season}/{kid.Birthday_Day}");
            if (kid.Birthday_Season != Game1.currentSeason)
                continue;
            if (!__result.TryGetValue(kid.Birthday_Day, out List<NPC>? npcs))
            {
                npcs = [];
                __result[kid.Birthday_Day] = npcs;
            }
            npcs.Add(kid);
        }
    }

    private static void FarmHouse_GetChildBed_Postfix(FarmHouse __instance, ref BedFurniture __result)
    {
        if (ModEntry.Config.UseSingleBedAsChildBed)
            __result ??= __instance.GetBed(BedFurniture.BedType.Single);
    }
}
