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
                transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Transpiler)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Postfix))
                {
                    after = SpouseShim.FL_ModIds,
                    priority = Priority.Last,
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
                // 2 different spellings for some reason
                harmony.Patch(
                    original: AccessTools.DeclaredMethod(SpouseShim.FL_modType, "CanGetPregnant")
                        ?? AccessTools.DeclaredMethod(SpouseShim.FL_modType, "canGetPregnant"),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(FL_NPC_CanGetPregnant))
                );
            }
            catch (Exception err)
            {
                ModEntry.Log($"Failed to patch pregnancy(FL compat):\n{err}", LogLevel.Warn);
                throw;
            }
        }
    }

    private static void FL_NPC_CanGetPregnant(NPC spouse, ref bool __result)
    {
        if (spouse == null)
            return;
        ModEntry.LogOnce("FL_NPC_CanGetPregnant: make free love actually call NPC.canGetPregnant");
        __result = __result && spouse.canGetPregnant();
    }

    internal static bool UnderMaxChildrenCount(Farmer player, List<Child>? children = null)
    {
        if (ModEntry.Config.MaxChildren == -1)
            return true;
        children ??= player.getChildren();
        return children.Count(child => child.GetHMKAdoptedFromNPCId() is null) < ModEntry.Config.MaxChildren;
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

        if (!UnderMaxChildrenCount(player, children))
        {
            ModEntry.Log($"- max child count reached");
            return;
        }

        if (KidHandler.TryGetSpouseOrSharedKidIds(__instance, out string? pickedKey, out List<string>? availableKidIds))
        {
            bool? restrict = KidHandler.GetDarkSkinnedRestrict(player, __instance);
            if (KidHandler.FilterAvailableKidIds(pickedKey, availableKidIds, restrict) != null)
            {
                ModEntry.Log($"- success (restrict)! (custom kids: {pickedKey})");
                __result = true;
                return;
            }
            else if (
                restrict != null
                && pickedKey == __instance.Name
                && KidHandler.FilterAvailableKidIds(pickedKey, availableKidIds, null) != null
            )
            {
                ModEntry.Log($"- success (unrestrict)! (custom kids: {pickedKey})");
                __result = true;
                return;
            }
            else if (!ModEntry.Config.AlwaysAllowGenericChildren)
            {
                ModEntry.Log($"- no custom kids left ({restrict})!");
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

    /// <summary>Change pregnancy chance to configured number.</summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static bool Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance(
        ref CodeMatcher matcher,
        int expectedCount
    )
    {
        try
        {
            for (int i = 0; i < expectedCount; i++)
            {
                matcher
                    .MatchEndForward([
                        new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Random), nameof(Random.NextDouble))),
                        new(OpCodes.Ldc_R8, 0.05),
                    ])
                    .ThrowIfNotMatch("Did not find 'random.NextDouble() < 0.05'")
                    .Advance(1)
                    .InsertAndAdvance([
                        new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(ModifyPregnancyChance))),
                    ]);
            }
            ModEntry.LogDebug($"Replaced {expectedCount} 'random.NextDouble() < 0.05'");
            return true;
        }
        catch (Exception err)
        {
            ModEntry.Log(
                $"Error in Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance({expectedCount}):\n{err}",
                LogLevel.Warn
            );
            return false;
            ;
        }
    }

    private static IEnumerable<CodeInstruction> Utility_pickPersonalFarmEvent_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        CodeMatcher matcher = new(instructions, generator);
        if (Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance(ref matcher, 2))
            return matcher.Instructions();
        return instructions;
    }

    private static IEnumerable<CodeInstruction> FL_Utility_pickPersonalFarmEvent_Postfix_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        CodeMatcher matcher = new(instructions, generator);
        if (!Utility_pickPersonalFarmEvent_InsertModifyPregnancyChance(ref matcher, 1))
            return instructions;
        matcher.Start();

        try
        {
            // IL_016e: nop
            // IL_016f: ldsfld class [StardewModdingAPI]StardewModdingAPI.IMonitor FreeLove.ModEntry::SMonitor
            // IL_0174: ldstr "Utility_pickPersonalFarmEvent_Prefix children blocking pregnancy"
            // IL_0179: ldc.i4.0
            // IL_017a: callvirt instance void [StardewModdingAPI]StardewModdingAPI.IMonitor::Log(string, valuetype [StardewModdingAPI]StardewModdingAPI.LogLevel)
            // IL_017f: nop
            // IL_0180: br IL_02f3
            // matcher.Start();
            matcher
                .MatchStartForward([
                    new(OpCodes.Nop),
                    new(OpCodes.Ldsfld, AccessTools.Field(SpouseShim.FL_modType, "SMonitor")),
                    new(OpCodes.Ldstr, "Utility_pickPersonalFarmEvent_Prefix children blocking pregnancy"),
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Callvirt, AccessTools.Method(typeof(IMonitor), nameof(IMonitor.Log))),
                    new(OpCodes.Nop),
                    new(OpCodes.Br),
                ])
                .ThrowIfNotMatch("Failed to match 'children blocking pregnancy'")
                .InsertBranch(OpCodes.Br, matcher.Pos + 7);

            // IL_01b0: nop
            // IL_01b1: ldsfld class [StardewModdingAPI]StardewModdingAPI.IMonitor FreeLove.ModEntry::SMonitor
            // IL_01b6: ldstr "Utility_pickPersonalFarmEvent_Prefix house blocking pregnancy"
            // IL_01bb: ldc.i4.0
            // IL_01bc: callvirt instance void [StardewModdingAPI]StardewModdingAPI.IMonitor::Log(string, valuetype [StardewModdingAPI]StardewModdingAPI.LogLevel)
            // IL_01c1: nop
            // IL_01c2: br IL_02f3
            matcher
                .MatchStartForward([
                    new(OpCodes.Nop),
                    new(OpCodes.Ldsfld, AccessTools.Field(SpouseShim.FL_modType, "SMonitor")),
                    new(OpCodes.Ldstr, "Utility_pickPersonalFarmEvent_Prefix house blocking pregnancy"),
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Callvirt, AccessTools.Method(typeof(IMonitor), nameof(IMonitor.Log))),
                    new(OpCodes.Nop),
                    new(OpCodes.Br),
                ])
                .ThrowIfNotMatch("Failed to match 'house blocking pregnancy'")
                .InsertBranch(OpCodes.Br, matcher.Pos + 7);

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in FL_Utility_pickPersonalFarmEvent_Postfix_Transpiler:\n{err}", LogLevel.Warn);
            return instructions;
        }
    }

    private static readonly FieldInfo whichQuestionField = AccessTools.DeclaredField(
        typeof(QuestionEvent),
        "whichQuestion"
    );

    private static void Utility_pickPersonalFarmEvent_Postfix(ref FarmEvent __result)
    {
        if (!Game1.weddingToday && GameDelegates.SoloDaysUntilNewChild == 1)
        {
            HMKNewChildEvent hmkNewChildEvent = new();
            __result = hmkNewChildEvent;
            if (Game1.player.NextKidId() is string nextKidId)
            {
                hmkNewChildEvent.newKidId = nextKidId;
                ModEntry.LogDebug("pickPersonalFarmEvent: Force a solo HMKNewChildEvent");
            }
            hmkNewChildEvent.isSoloAdopt = true;
        }
        else if (__result is QuestionEvent && whichQuestionField.GetValue(__result) is int whichQ)
        {
            if (whichQ == QuestionEvent.pregnancyQuestion || whichQ == QuestionEvent.playerPregnancyQuestion)
            {
                ModEntry.LogDebug("pickPersonalFarmEvent: Replace QuestionEvent with HMKGetChildQuestionEvent");
                __result = new HMKGetChildQuestionEvent(whichQ);
            }
        }
        else if (__result is BirthingEvent)
        {
            // recheck crib availability before birth event
            if (!CribManager.HasAvailableCribs(Utility.getHomeOfFarmer(Game1.player)))
            {
                ModEntry.LogDebug(
                    "pickPersonalFarmEvent: Replace BirthingEvent with NULL due to insufficient cribs in the house"
                );
                __result = null!;
                return;
            }
            ModEntry.LogDebug("pickPersonalFarmEvent: Replace BirthingEvent with HMKNewChildEvent");
            __result = new HMKNewChildEvent();
        }
    }
}
