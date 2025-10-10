using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HaveMoreKids.Framework.NightEvents;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Locations;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    internal static void Apply_Pregnancy(Harmony harmony)
    {
        try
        {
            // Change pregnancy chance
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.pickPersonalFarmEvent)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Prefix)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Transpiler)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Postfix))
            );
            // Allow pregnancy past the second child
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.canGetPregnant)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(NPC_canGetPregnant_Postfix))
            );
            // Allow pregnancy past the second child for players
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.playersCanGetPregnantHere)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Utility_playersCanGetPregnantHere_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch pregnancy:\n{err}", LogLevel.Error);
            throw;
        }
    }

    /// <summary>
    /// Change can get pregnant checking
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static void NPC_canGetPregnant_Postfix(NPC __instance, ref bool __result)
    {
        ModEntry.Log($"Checking NPC.canGetPregnant postfix for '{__instance.Name}'");
        __result = false;
        if (__instance is Horse || __instance.IsInvisible)
        {
            ModEntry.Log("- is horse or invisible");
            return;
        }
        // roommate adoption
        if (
            __instance.isRoommate()
            && __instance.GetData()?.SpouseAdopts is string spouseAdopts
            && !GameStateQuery.CheckConditions(spouseAdopts)
        )
        {
            ModEntry.Log("- is roommate and not adopt");
            return;
        }

        // valid marriage
        Farmer player = __instance.getSpouse();
        if (player == null || player.divorceTonight.Value || player.GetDaysMarried() < ModEntry.Config.DaysMarried)
        {
            ModEntry.Log("- not married hard enough");
            return;
        }

        // valid home
        if (!GameDelegates.HomeIsValidForPregnancy(Utility.getHomeOfFarmer(player), out string error))
        {
            ModEntry.Log($"- {error}");
            return;
        }

        // friendly not pregnant spouse
        Friendship spouseFriendship = player.GetSpouseFriendship();
        if (spouseFriendship.DaysUntilBirthing > 0)
        {
            ModEntry.Log("- already pregnant");
            return;
        }
        if (player.getFriendshipHeartLevelForNPC(__instance.Name) < 10)
        {
            ModEntry.Log("- a loveless marriage");
            return;
        }

        __instance.DefaultMap = player.homeLocation.Value;
        List<Child> children = player.getChildren();

        if (ModEntry.Config.MaxChildren != -1 && children.Count >= ModEntry.Config.MaxChildren)
        {
            ModEntry.Log($"- max child count reached");
            return;
        }

        if (KidHandler.TryGetSpouseOrSharedKidIds(__instance, out string? pickedKey, out List<string>? availableKidIds))
        {
            if (KidHandler.FilterAvailableKidIds(pickedKey, ref availableKidIds))
            {
                ModEntry.Log($"- success! (custom kids: {pickedKey})");
                __result = true;
                return;
            }
            else if (!ModEntry.Config.AlwaysAllowGenericChildren)
            {
                ModEntry.Log($"- no custom kids left!");
                return;
            }
        }

        ModEntry.Log("- success! (generic kids)");
        __result = true;
    }

    private static void Utility_playersCanGetPregnantHere_Postfix(FarmHouse farmHouse, ref bool __result)
    {
        if (__result)
            return;
        if (GameDelegates.HomeIsValidForPregnancy(farmHouse, out _))
        {
            __result = true;
        }
    }

    private static float ModifyPregnancyChance(float originalValue)
    {
        ModEntry.LogOnce($"Modify pregnancy chance: {originalValue} -> {ModEntry.Config.PregnancyChance / 100f}");
        return ModEntry.Config.PregnancyChance / 100f;
    }

    private static bool Utility_pickPersonalFarmEvent_Prefix(ref FarmEvent __result)
    {
        if (Game1.weddingToday)
        {
            return true;
        }
        if (Game1.player.stats.Get(GameDelegates.Stats_daysUntilNewChild) == 1)
        {
            HMKNewChildEvent hmkNewChildEvent = new();
            __result = hmkNewChildEvent;
            if (Game1.player.modData.TryGetValue(KidHandler.Character_ModData_NextKidId, out string? nextKidId))
            {
                hmkNewChildEvent.newKidId = nextKidId;
                hmkNewChildEvent.isAdoptedFromNPC = true;
                Game1.player.modData.Remove(KidHandler.Character_ModData_NextKidId);
            }
            return false;
        }
        return true;
    }

    /// <summary>Change pregnancy chance to configured number.</summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> Utility_pickPersonalFarmEvent_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            matcher
                .MatchEndForward(
                    [
                        new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Random), nameof(Random.NextDouble))),
                        new(OpCodes.Ldc_R8, 0.05),
                    ]
                )
                .ThrowIfNotMatch("Did not find first 'random.NextDouble() < 0.05'")
                .Advance(1)
                .Insert([new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(ModifyPregnancyChance)))])
                .MatchEndForward(
                    [
                        new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Random), nameof(Random.NextDouble))),
                        new(OpCodes.Ldc_R8, 0.05),
                    ]
                )
                .ThrowIfNotMatch("Did not find second 'random.NextDouble() < 0.05'")
                .Advance(1)
                .Insert([new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(ModifyPregnancyChance)))]);
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Utility_pickPersonalFarmEvent_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static readonly FieldInfo whichQuestionField = AccessTools.DeclaredField(
        typeof(QuestionEvent),
        "whichQuestion"
    );

    private static void Utility_pickPersonalFarmEvent_Postfix(ref FarmEvent __result)
    {
        ModEntry.Log($"Utility_pickPersonalFarmEvent_Postfix {__result}");
        if (__result is QuestionEvent && whichQuestionField.GetValue(__result) is int whichQ)
        {
            if (whichQ == QuestionEvent.pregnancyQuestion || whichQ == QuestionEvent.playerPregnancyQuestion)
            {
                __result = new HMKGetChildQuestionEvent(whichQ);
            }
        }
        else if (__result is BirthingEvent)
        {
            __result = new HMKNewChildEvent();
        }
    }
}
