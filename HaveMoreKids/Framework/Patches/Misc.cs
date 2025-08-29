using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    internal static void Apply_Misc(Harmony harmony)
    {
        // let child sleep on single beds
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.GetChildBed)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FarmHouse_GetChildBed_Postfix))
        );
        // make custom crib toss work
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.checkForAction)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Furniture_checkForAction_Postfix))
        );
        // lock the crib when it's got a baby inside
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.canBeRemoved)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Furniture_canBeRemoved_Postfix))
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
        // When child is in a crib do special drawing logic (sigh)
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Furniture_draw_Prefix)) { priority = Priority.First },
            postfix: new HarmonyMethod(typeof(Patches), nameof(Furniture_draw_Postfix)) { priority = Priority.Last }
        );
    }

    private static void Furniture_draw_Prefix(
        Furniture __instance,
        Netcode.NetVector2 ___drawPosition,
        SpriteBatch spriteBatch,
        float alpha,
        ref bool __state
    )
    {
        // basically a skip prefix :))))
        __state = __instance.isTemporarilyInvisible;
        if (CribManager.IsCrib(__instance))
        {
            __instance.isTemporarilyInvisible = true;
            ParsedItemData dataOrErrorItem = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
            Texture2D texture = dataOrErrorItem.GetTexture();
            Rectangle sourceRect = __instance.sourceRect.Value;
            spriteBatch.Draw(
                texture,
                Game1.GlobalToLocal(
                    Game1.viewport,
                    ___drawPosition.Value
                        + (
                            (__instance.shakeTimer > 0)
                                ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2))
                                : Vector2.Zero
                        )
                ),
                sourceRect,
                Color.White * alpha,
                0f,
                Vector2.Zero,
                4f,
                __instance.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                (__instance.boundingBox.Value.Top + 1) / 10000f
            );
            sourceRect.X += sourceRect.Width;
            sourceRect.Y += 2 * 16;
            spriteBatch.Draw(
                texture,
                Game1.GlobalToLocal(
                    Game1.viewport,
                    ___drawPosition.Value
                        + (
                            (__instance.shakeTimer > 0)
                                ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2))
                                : Vector2.Zero
                        )
                ),
                sourceRect,
                Color.White * alpha,
                0f,
                Vector2.Zero,
                4f,
                __instance.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                (__instance.boundingBox.Bottom - 1) / 10000f
            );
        }
    }

    private static void Furniture_draw_Postfix(Furniture __instance, ref bool __state)
    {
        __instance.isTemporarilyInvisible = __state;
    }

    private static void Furniture_canBeRemoved_Postfix(Furniture __instance, ref bool __result)
    {
        if (__result)
        {
            __result = !__instance.modData.ContainsKey(CribAssign.PlacedChild);
        }
    }

    private static void Furniture_checkForAction_Postfix(Furniture __instance, Farmer who, ref bool __result)
    {
        if (!__result)
        {
            __result = CribManager.DoCribAction(__instance, who);
        }
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
