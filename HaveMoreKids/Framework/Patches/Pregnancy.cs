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
    internal static void Apply_Pregnancy()
    {
        try
        {
            // Change pregnancy chance
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.pickPersonalFarmEvent)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Prefix)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Transpiler)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Postfix))
                {
                    after = SpouseShim.FL_ModIds,
                }
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

    internal static void Apply_PregnancyFL()
    {
        if (SpouseShim.FL_modType != null)
        {
            try
            {
                harmony.Patch(
                    original: AccessTools.DeclaredMethod(
                        SpouseShim.FL_modType,
                        "Utility_pickPersonalFarmEvent_Postfix"
                    ),
                    transpiler: new HarmonyMethod(
                        typeof(Patches),
                        nameof(FL_Utility_pickPersonalFarmEvent_Postfix_Transpiler)
                    )
                );
            }
            catch (Exception err)
            {
                ModEntry.Log($"Failed to patch pregnancy(FL compat):\n{err}", LogLevel.Warn);
                throw;
            }
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
        if (player == null || !SpouseShim.TryGetNPCFriendship(player, __instance, out Friendship? spouseFriendship))
        {
            ModEntry.Log("- not married");
            return;
        }

        if (player.divorceTonight.Value || spouseFriendship.DaysMarried < ModEntry.Config.DaysMarried)
        {
            ModEntry.Log("- not married long enough");
            return;
        }

        // valid home
        if (!GameDelegates.HomeIsValidForPregnancy(Utility.getHomeOfFarmer(player), out string error))
        {
            ModEntry.Log($"- {error}");
            return;
        }

        // friendly not pregnant spouse
        if (spouseFriendship.DaysUntilBirthing >= 0)
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

    private static bool Utility_pickPersonalFarmEvent_Prefix(ref FarmEvent __result, ref string? __state)
    {
        __state = null;
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
    private static IEnumerable<CodeInstruction> Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator,
        int expectedCount
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            for (int i = 0; i < expectedCount; i++)
            {
                matcher
                    .MatchEndForward(
                        [
                            new(
                                OpCodes.Callvirt,
                                AccessTools.DeclaredMethod(typeof(Random), nameof(Random.NextDouble))
                            ),
                            new(OpCodes.Ldc_R8, 0.05),
                        ]
                    )
                    .ThrowIfNotMatch("Did not find 'random.NextDouble() < 0.05'")
                    .Advance(1)
                    .InsertAndAdvance(
                        [new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(ModifyPregnancyChance)))]
                    );
            }
            ModEntry.LogDebug($"Replaced {expectedCount} 'random.NextDouble() < 0.05'");
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Error in Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance({expectedCount}):\n{err}",
                LogLevel.Warn
            );
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> Utility_pickPersonalFarmEvent_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance(instructions, generator, 2);
    }

    private static IEnumerable<CodeInstruction> FL_Utility_pickPersonalFarmEvent_Postfix_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance(instructions, generator, 1);
    }

    private static readonly FieldInfo whichQuestionField = AccessTools.DeclaredField(
        typeof(QuestionEvent),
        "whichQuestion"
    );

    private static void Utility_pickPersonalFarmEvent_Postfix(ref FarmEvent __result, ref string? __state)
    {
        ModEntry.LogDebug($"Utility_pickPersonalFarmEvent_Postfix {__result}");
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
