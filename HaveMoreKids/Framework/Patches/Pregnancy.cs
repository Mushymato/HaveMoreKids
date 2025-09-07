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
                finalizer: new HarmonyMethod(typeof(Patches), nameof(NPC_canGetPregnant_Finalizer))
            );
            // Allow pregnancy past the second child for players
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.playersCanGetPregnantHere)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_playersCanGetPregnantHere_Transpiler))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch pregnancy:\n{err}", LogLevel.Error);
            throw;
        }
    }

    private static int ModifyBaseKidCount(int count) => ModEntry.Config.BaseMaxChildren;

    /// <summary>
    /// Change can get pregnant checking
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static bool NPC_canGetPregnant_Finalizer(NPC __instance, ref bool __result)
    {
        ModEntry.Log($"Checking NPC.canGetPregnant finalizer for '{__instance.Name}'");
        __result = false;
        if (__instance is Horse || __instance.IsInvisible)
        {
            ModEntry.Log("- is horse or invisible");
            return false;
        }
        // roommate adoption
        if (
            __instance.isRoommate()
            && __instance.GetData()?.SpouseAdopts is string spouseAdopts
            && !GameStateQuery.CheckConditions(spouseAdopts)
        )
        {
            ModEntry.Log("- is roommate and not adopt");
            return false;
        }

        // valid marriage
        Farmer player = __instance.getSpouse();
        if (player == null || player.divorceTonight.Value || player.GetDaysMarried() < ModEntry.Config.DaysMarried)
        {
            ModEntry.Log("- not married hard enough");
            return false;
        }

        // valid home
        if (!GameDelegates.PlayerHasValidHome(player, out string error))
        {
            ModEntry.Log($"- {error}");
            return false;
        }

        // friendly not pregnant spouse
        Friendship spouseFriendship = player.GetSpouseFriendship();
        if (spouseFriendship.DaysUntilBirthing > 0)
        {
            ModEntry.Log("- already pregnant");
            return false;
        }
        if (player.getFriendshipHeartLevelForNPC(__instance.Name) < 10)
        {
            ModEntry.Log("- a loveless marriage");
            return false;
        }

        __instance.DefaultMap = player.homeLocation.Value;
        List<Child> children = player.getChildren();

        if (KidHandler.TryGetSpouseOrSharedKidIds(__instance, out string? pickedKey, out List<string>? availableKidIds))
        {
            if (KidHandler.FilterAvailableKidIds(pickedKey, ref availableKidIds))
            {
                ModEntry.Log($"- success! (custom kids: {pickedKey})");
                __result = true;
            }
            else
            {
                ModEntry.Log($"- no custom kids left!");
            }
        }
        else
        {
            ModEntry.Log("- success! (generic kids)");
            __result = children.Count < ModEntry.Config.BaseMaxChildren;
        }
        return false;
    }

    /// <summary>
    /// Check all kids are of right arge
    /// </summary>
    /// <param name="children"></param>
    /// <returns></returns>
    private static bool ShouldHaveKidsPlayer(List<Child> children)
    {
        return children.All(child => child.Age > 2);
    }

    private static IEnumerable<CodeInstruction> Utility_playersCanGetPregnantHere_Transpiler(
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
                        new(
                            OpCodes.Callvirt,
                            AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.getChildrenCount))
                        ),
                        new(OpCodes.Ldc_I4_2),
                        new(OpCodes.Bge_S),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'farmHouse.getChildrenCount() < 2'")
                .InsertAndAdvance(
                    [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyBaseKidCount)))]
                );

            matcher
                .MatchEndForward(
                    [
                        new(inst => inst.IsLdloc()),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<Child>), nameof(List<Child>.Count))
                        ),
                        new(OpCodes.Ldc_I4_2),
                        new(OpCodes.Bge_S),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'children.Count < 2'");
            CodeInstruction ldlocChildren = matcher.InstructionAt(-3);
            ldlocChildren = new(ldlocChildren.opcode, ldlocChildren.operand);
            matcher.InsertAndAdvance(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyBaseKidCount)))]
            );
            matcher
                .MatchEndForward(
                    [
                        new(inst => inst.IsLdloc()),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<Child>), nameof(List<Child>.Count))
                        ),
                        new(OpCodes.Brfalse_S),
                        new(inst => inst.IsLdloc()),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'children.Count != 0'")
                .Insert(
                    [
                        ldlocChildren,
                        new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ShouldHaveKidsPlayer))),
                        new(OpCodes.Ret),
                    ]
                );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Utility_playersCanGetPregnantHere_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
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
        if (Game1.player.stats.Get(GameDelegates.Stats_daysUntilBirth) == 1)
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
            ModEntry.Log($"whichQ: {whichQ}");
            if (whichQ == QuestionEvent.pregnancyQuestion || whichQ == QuestionEvent.playerPregnancyQuestion)
            {
                __result = new HMKGetChildQuestionEvent(whichQ);
            }
        }
        else if (__result is BirthingEvent)
        {
            __result = new HMKNewChildEvent();
        }
        else if (__result is PlayerCoupleBirthingEvent)
        {
            __result = new HMKPlayerCoupleNewChildEvent();
        }
    }
}
